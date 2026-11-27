using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage.Game;
using VRage.Game.ModAPI.Ingame;
using VRageMath;

// Runs the actual PB body with interface proxies; it does not simulate physics.
public class Proxy : RealProxy
{
    public readonly Dictionary<string,object> Values = new Dictionary<string,object>();
    public Func<string,object[],object> Method;
    public Proxy(Type type) : base(type) { }
    public override IMessage Invoke(IMessage message)
    {
        var call = (IMethodCallMessage)message;
        try
        {
            object result = null;
            string name = call.MethodName;
            if (name.StartsWith("set_")) Values[name.Substring(4)] = call.Args[0];
            else if (name.StartsWith("get_")) Values.TryGetValue(name.Substring(4),out result);
            else if (Method != null) result = Method(name,call.Args);
            Type rt = ((MethodInfo)call.MethodBase).ReturnType;
            if (result == null && rt.IsValueType && rt != typeof(void)) result = Activator.CreateInstance(rt);
            return new ReturnMessage(result,null,0,call.LogicalCallContext,call);
        }
        catch(Exception ex) { return new ReturnMessage(ex,call); }
    }
    public T As<T>() { return (T)GetTransparentProxy(); }
}
public class TestRuntime
{
    public UpdateFrequency UpdateFrequency;
    public TimeSpan TimeSinceLastRun = TimeSpan.FromSeconds(1.0/6);
}
public class FakeGridProgram
{
    public IMyGridTerminalSystem GridTerminalSystem = Harness.Grid;
    public IMyProgrammableBlock Me = Harness.Me;
    public TestRuntime Runtime = new TestRuntime();
    public string Storage = Harness.Saved;
    public void Echo(string s) { }
}
public class Harness
{
    public static IMyGridTerminalSystem Grid;
    public static IMyProgrammableBlock Me;
    public static string Saved = "";
    static List<object> blocks;
    static Proxy remote, gyro, warhead, turret;
    static List<Proxy> engines;
    static Vector3D position, velocity;
    static MyDetectedEntityInfo contact;
    static int detonations, checks;
    static Program program;
    static IMyCubeGrid cube;
    static MatrixD remoteMatrix;
    static void Assert(bool ok,string name)
    { checks++; if(!ok) throw new Exception("FAIL: "+name); Console.WriteLine("PASS "+name); }
    static Proxy Block<T>(string name)
    {
        var p = new Proxy(typeof(T));
        p.Values["CustomName"] = "[GG-MISSILE] "+name;
        p.Values["CubeGrid"] = cube;
        p.Values["IsWorking"] = true;
        p.Values["Enabled"] = true;
        p.Values["WorldMatrix"] = MatrixD.Identity;
        blocks.Add(p.As<T>());
        return p;
    }
    static MyDetectedEntityInfo Enemy(MyRelationsBetweenPlayerAndBlock relationship)
    {
        return new MyDetectedEntityInfo(55,"Test grid",MyDetectedEntityType.LargeGrid,null,
            MatrixD.Identity,Vector3.Zero,relationship,
            new BoundingBoxD(new Vector3D(95,-5,-5),new Vector3D(105,5,5)),1);
    }
    static void Setup()
    {
        blocks = new List<object>(); engines = new List<Proxy>();
        contact = new MyDetectedEntityInfo(); position = velocity = Vector3D.Zero; detonations = 0;
        var cp = new Proxy(typeof(IMyCubeGrid)); cp.Values["EntityId"] = 1L;
        cube = cp.As<IMyCubeGrid>();
        var screen = new Proxy(typeof(IMyTextSurface));
        var me = Block<IMyProgrammableBlock>("Computer");
        me.Method = (n,a) => n == "GetSurface" ? screen.As<IMyTextSurface>() : null;
        Me = me.As<IMyProgrammableBlock>();
        remote = Block<IMyRemoteControl>("Remote");
        remoteMatrix = MatrixD.CreateWorld(Vector3D.Zero,Vector3D.Up,Vector3D.Backward);
        remote.Values["WorldMatrix"] = remoteMatrix;
        remote.Method = (n,a) => {
            if(n == "GetNaturalGravity") return new Vector3D(0,-9.81,0);
            if(n == "GetPosition") return position;
            if(n == "CalculateShipMass") return new MyShipMass(10000,10000,10000);
            if(n == "GetShipVelocities") return new MyShipVelocities(velocity,Vector3D.Zero);
            return null;
        };
        foreach(Vector3D direction in new[]{Vector3D.Up,Vector3D.Down,Vector3D.Left,Vector3D.Right,Vector3D.Forward,Vector3D.Backward})
        {
            var engine = Block<IMyThrust>("Engine");
            engine.Values["MaxEffectiveThrust"] = 480000f;
            engine.Values["WorldMatrix"] = MatrixD.CreateWorld(Vector3D.Zero,-direction,Vector3D.CalculatePerpendicularVector(direction));
            engines.Add(engine);
        }
        gyro = Block<IMyGyro>("Gyro");
        warhead = Block<IMyWarhead>("Warhead");
        warhead.Method = (n,a) => { if(n=="Detonate")detonations++;return null; };
        turret = Block<IMyLargeTurretBase>("Radar");
        turret.Method = (n,a) => n=="GetTargetedEntity" ? (object)contact : null;
        var gs = new Proxy(typeof(IMyGridTerminalSystem));
        gs.Method = (n,a) => {
            if(n=="GetBlocksOfType")
            {
                var list = (IList)a[0]; list.Clear();
                Type type = list.GetType().GetGenericArguments()[0];
                var filter = a.Length>1 ? a[1] as Delegate : null;
                foreach(object b in blocks) if(type.IsInstanceOfType(b) && (filter==null || (bool)filter.DynamicInvoke(b))) list.Add(b);
            }
            return null;
        };
        Grid = gs.As<IMyGridTerminalSystem>();
        program = new Program();
    }
    static object Field(string name) { return typeof(Program).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(program); }
    static string Phase() { return Field("phase").ToString(); }
    static bool Armed() { return (bool)warhead.Values["IsArmed"]; }
    static void Step(int count)
    { for(int i=0;i<count;i++)program.Main("",UpdateType.Update10); }
    static void Command(string cmd) { program.Main(cmd,UpdateType.Terminal); }
    static void ToDive(bool test)
    {
        Command(test ? "TEST" : "LAUNCH");
        position = new Vector3D(0,200,0); Step(1);
        contact = Enemy(MyRelationsBetweenPlayerAndBlock.Enemies); Step(1);
        position = new Vector3D(100,180,0); Step(1);
    }
    public static void Main()
    {
        Setup(); Assert(!Armed(),"startup disarms warheads");
        Command("CHECK"); Assert((string)Field("note")=="READY","six-axis preflight");
        Command("LAUNCH"); Assert(Phase()=="Boost","launch enters vertical boost");
        Step(1); Assert(!Armed(),"boost remains disarmed");
        position = new Vector3D(0,200,0); Step(1); Assert(Phase()=="Search","climb transitions into search");
        contact = Enemy(MyRelationsBetweenPlayerAndBlock.Friends); Step(1);
        Assert(Phase()=="Search" && (long)Field("targetId")==0,"friendly grid rejected");
        contact = Enemy(MyRelationsBetweenPlayerAndBlock.Enemies); Step(1);
        Assert(Phase()=="Overhead","enemy triggers overhead approach");
        position = new Vector3D(100,180,0); Step(1);
        Assert(Phase()=="Dive","dive requires overhead geometry");
        Assert(!Armed(),"attitude flip remains disarmed");
        Assert(Math.Abs((float)gyro.Values["Pitch"])+Math.Abs((float)gyro.Values["Yaw"])+Math.Abs((float)gyro.Values["Roll"])>0.1,"180-degree flip produces gyro command");
        remote.Values["WorldMatrix"] = MatrixD.CreateWorld(Vector3D.Zero,Vector3D.Down,Vector3D.Forward); Step(1);
        Assert(Armed(),"fresh downward terminal track permits arming");
        contact = new MyDetectedEntityInfo(); Step(5);
        Assert(!Armed(),"stale terminal track disarms before timeout");
        Step(10); Assert(Phase()=="Search","lost track returns to search");
        Command("ABORT"); Assert(Phase()=="Abort" && !Armed(),"abort disarms");
        Assert((bool)remote.Values["DampenersOverride"],"abort enables dampeners");
        Assert(!(bool)gyro.Values["GyroOverride"],"abort releases gyro");
        Assert(engines.TrueForAll(e=>(float)e.Values["ThrustOverridePercentage"]==0),"abort releases all thrust overrides");
        Setup(); ToDive(true);
        remote.Values["WorldMatrix"] = MatrixD.CreateWorld(Vector3D.Zero,Vector3D.Down,Vector3D.Forward);
        position = new Vector3D(100,8,0); Step(1);
        Assert(Phase()=="Abort" && detonations==0 && !Armed(),"TEST reaches intercept without detonation");
        Setup(); ToDive(false);
        remote.Values["WorldMatrix"] = MatrixD.CreateWorld(Vector3D.Zero,Vector3D.Down,Vector3D.Forward);
        position = new Vector3D(100,8,0); Step(1);
        Assert(detonations==1,"fresh aligned terminal intercept detonates");
        Setup(); ToDive(false); contact=Enemy(MyRelationsBetweenPlayerAndBlock.Friends); Step(1);
        Assert(Phase()=="Search" && !Armed(),"relationship change cancels attack");
        Saved="flight"; Setup(); Saved="";
        Assert(Phase()=="Idle" && !Armed(),"reload cancels active flight");
        Console.WriteLine(checks+" controller checks passed; no physics simulation performed.");
    }
}
