using System;
using System.IO;
using System.Reflection;
using System.Xml.Serialization;
using VRage.Game;
using VRage.ObjectBuilders;

// Uses Keen's shipped, precompiled XML serializers; no game process is started.
class MissileBlueprintLoad
{
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
        Console.WriteLine("Native blueprint load passed: "+grid.CubeBlocks.Count+" blocks; script, fuel, radar flags and disarmed payload verified.");
    }
}
