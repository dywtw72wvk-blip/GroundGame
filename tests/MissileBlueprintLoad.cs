using System;
using System.IO;
using System.Reflection;
using System.Xml.Serialization;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using VRage.Game;
using VRage.ObjectBuilders;

// Uses Keen's shipped, precompiled XML serializers; no game process is started.
class MissileBlueprintLoad
{
    static object Member(object obj,string name)
    {
        Check(obj!=null,"Null object while reading "+name);
        var f=obj.GetType().GetField(name);
        if(f!=null) return f.GetValue(obj);
        var p=obj.GetType().GetProperty(name);
        Check(p!=null,"Missing member "+obj.GetType().FullName+"."+name);
        return p.GetValue(obj,null);
    }
    sealed class Remapper : RealProxy
    {
        public Remapper(Type type):base(type) { }
        public override IMessage Invoke(IMessage message)
        {
            var call=(IMethodCallMessage)message;
            object result=null;
            if(call.MethodName=="RemapEntityId") result=(long)call.Args[0]+1000000L;
            else if(call.MethodName=="RemapGroupId") result=call.Args[1];
            return new ReturnMessage(result,null,0,call.LogicalCallContext,call);
        }
    }
    static void CheckToolbar(MyObjectBuilder_CubeGrid grid)
    {
        var ids=new HashSet<long>(); var named=new Dictionary<string,object>();
        foreach(var b in grid.CubeBlocks)
        {
            Check(b.EntityId!=0 && ids.Add(b.EntityId),"Missing or duplicate block entity ID");
            var f=b.GetType().GetField("CustomName");
            if(f!=null) named[(string)f.GetValue(b)]=b;
        }
        var remote=named["[GG-MISSILE] Remote"];
        var computer=named["[GG-MISSILE] Computer"];
        object bar=Member(remote,"Toolbar");
        var expected=new Dictionary<int,string>{{0,"CHECK"},{1,"SCAN"},{2,"TEST"},{3,"LAUNCH"},{4,"ABORT"},{7,"STATUS"},{8,"ABORT"}};
        int count=0;
        foreach(var slot in (IEnumerable)Member(bar,"Slots"))
        {
            count++; int index=Convert.ToInt32(Member(slot,"Index"));
            var data=Member(slot,"Data");
            long targetId=(long)Member(data,"BlockEntityId");
            Check(ids.Contains(targetId),"Toolbar link points outside missile");
            if(expected.ContainsKey(index))
            {
                Check(targetId==(long)Member(computer,"EntityId") && (string)Member(data,"_Action")=="Run","Command is not linked to Computer Run");
                Check((string)Member(data,"CustomIconTitle")==expected[index],"Command label is missing");
                var parameters=(IList)Member(data,"Parameters");
                Check(parameters.Count==1 && (string)Member(parameters[0],"Value")==expected[index],"Wrong toolbar run argument");
            }
            else
            {
                string camera=index==5 ? "[GG-MISSILE] Camera nose" : "[GG-MISSILE] Camera Down";
                Check((string)Member(data,"_Action")=="View" && targetId==(long)Member(named[camera],"EntityId"),"Camera toolbar link is incorrect");
            }
        }
        Check(count==9,"Expected nine keyboard toolbar buttons");
        count=0;
        foreach(var slot in (IEnumerable)Member(bar,"SlotsGamepad"))
        { count++; Check(ids.Contains((long)Member(Member(slot,"Data"),"BlockEntityId")),"Gamepad references unknown entity"); }
        Check(count==8,"Expected two gamepad banks");
    }
    static void Check(bool condition,string message)
    { if(!condition)throw new Exception(message); }
    static void Main()
    {
        var flags=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        var keen=typeof(MyObjectBuilderSerializer).Assembly.GetType("VRage.ObjectBuilders.Private.MyObjectBuilderSerializerKeen");
        foreach(string name in new[]{"VRage.Game","Sandbox.Common","SpaceEngineers.ObjectBuilders","Sandbox.Game","SpaceEngineers.Game"})
        {
            var a=Assembly.Load(name);
            MyObjectBuilderType.RegisterFromAssembly(a,true);
            VRage.MyXmlSerializerManager.RegisterFromAssembly(a);
            keen.GetMethod("RegisterFromAssembly",flags).Invoke(null,new object[]{a});
        }
        keen.GetMethod("LoadSerializers",flags).Invoke(null,null);
        foreach(string name in new[]{"VRage.Game","SpaceEngineers.ObjectBuilders"})
            foreach(var t in Assembly.Load(name).GetTypes())
                if(typeof(MyObjectBuilder_Base).IsAssignableFrom(t) && !t.IsAbstract)
                    VRage.MyXmlSerializerManager.RegisterSerializer(t);
        var sa=Assembly.Load("VRage.Game.XmlSerializers");
        var contract=(XmlSerializerImplementation)Activator.CreateInstance(sa.GetType("Microsoft.Xml.Serialization.GeneratedAssembly.XmlSerializerContract"));
        var xs=contract.GetSerializer(typeof(MyObjectBuilder_Definitions));
        string root=Environment.GetEnvironmentVariable("GROUNDGAME_ROOT");
        MyObjectBuilder_Definitions defs;
        using(var reader=File.OpenRead(Path.Combine(root,@"blueprints\[GG] Topdown Missile\bp.sbc")))
            defs=(MyObjectBuilder_Definitions)xs.Deserialize(reader);
        Check(defs.ShipBlueprints.Length==1,"Expected one blueprint");
        var grid=defs.ShipBlueprints[0].CubeGrids[0];
        Check(!grid.IsStatic,"Missile must be dynamic");
        int warheads=0,tanks=0,computers=0,radars=0;
        foreach(var b in grid.CubeBlocks)
        {
            var type=b.GetType();
            if(b.SubtypeName=="SmallBlockBatteryBlock")
                Check(b.Min.X==-1 && b.Min.Y==13 && b.Min.Z==-1,"Battery must remain inline inside the tank cross-section");
            if(type.Name=="MyObjectBuilder_Warhead")
            {
                warheads++;
                Check(!(bool)type.GetField("IsArmed").GetValue(b),"Blueprint warhead armed");
                Check(!(bool)type.GetField("IsCountingDown").GetValue(b),"Countdown enabled");
            }
            if(type.Name=="MyObjectBuilder_OxygenTank")
            {
                tanks++;
                Check((float)type.GetField("FilledRatio").GetValue(b)>0.99,"Tank fill did not deserialize");
                Check(!(bool)type.GetField("IsStockpiling").GetValue(b),"Tank in stockpile mode");
            }
            if(type.Name=="MyObjectBuilder_MyProgrammableBlock")
            {
                computers++;
                Check((string)type.GetField("Program").GetValue(b)==File.ReadAllText(Path.Combine(root,@"dist\TopdownMissile.cs")),"Embedded PB differs from release");
            }
            if(type.Name=="MyObjectBuilder_LargeGatlingTurret")
            {
                radars++;
                var f=type.GetField("Flags").GetValue(b);
                Check((bool)f.GetType().GetField("TargetEnemies").GetValue(f),"Radar enemy targeting disabled");
                Check(!(bool)f.GetType().GetField("TargetFriends").GetValue(f),"Radar friendly targeting enabled");
                Check(!(bool)f.GetType().GetField("TargetNeutrals").GetValue(f),"Radar neutral targeting enabled");
            }
        }
        Check(warheads==4 && tanks==2 && computers==1 && radars==1,"Incorrect missile systems");
        CheckToolbar(grid);
        var remap=grid.GetType().GetMethod("Remap");
        remap.Invoke(grid,new object[]{new Remapper(remap.GetParameters()[0].ParameterType).GetTransparentProxy()});
        CheckToolbar(grid);
        Console.WriteLine("Remote toolbar links passed before and after native blueprint entity remapping.");
        Console.WriteLine("Native blueprint load passed: "+grid.CubeBlocks.Count+" blocks; script, fuel, radar flags and disarmed payload verified.");
    }
}
