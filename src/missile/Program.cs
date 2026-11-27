// GroundGame Topdown 0.4 - Space Engineers programmable block script.
// Paste the BODY between the markers, not this file's using/class wrapper.
using System;
using System.Collections.Generic;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage.Game;
using VRage.Game.ModAPI.Ingame;
using VRageMath;

public sealed class Program : MyGridProgram
{
// BEGIN PB BODY
const string Tag = "[GG-MISSILE]";
const double SearchHeight = 200, ScanTurnRate = Math.PI / 60;
const double AttackHeight = 180, DiveSpeed = 80, TrackTimeout = 2;
const double CameraRange = 750, LaunchDelay = 10;
enum Phase { Idle, Countdown, Boost, Search, Overhead, Dive, Abort }
Phase phase = Phase.Idle;
IMyRemoteControl remote;
readonly List<IMyThrust> thrust = new List<IMyThrust>();
readonly List<IMyGyro> gyros = new List<IMyGyro>();
readonly List<IMyWarhead> warheads = new List<IMyWarhead>();
readonly List<IMyCameraBlock> cameras = new List<IMyCameraBlock>();
readonly List<IMyTimerBlock> timers = new List<IMyTimerBlock>();
readonly List<IMyLightingBlock> lights = new List<IMyLightingBlock>();
readonly List<int> cameraSteps = new List<int>();
readonly List<IMyGasTank> tanks = new List<IMyGasTank>();
readonly List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();
Vector3D launch, up, scanAnchor, target, targetVelocity;
BoundingBoxD targetBox;
long targetId;
double elapsed, age = 999, phaseTime, scanClock;
string hoverInfo = "";
int cameraCursor, rayCount;
double countdownLeft;
Vector3D targetOffset;
string note = "Run CHECK, then LAUNCH. ABORT releases control.";
string targetName = "none", acquisition = "Press SCAN to check visible contacts without launching.";
bool dryRun;
bool watchContacts;

public Program()
{
    FindBlocks();
    SafeStop(); // A reload never silently resumes an armed attack.
    if (!string.IsNullOrEmpty(Storage)) note = "Reloaded: attack cancelled. Run CHECK.";
    Storage = "";
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
}

public void Save() { Storage = phase == Phase.Idle || phase == Phase.Abort ? "" : "flight"; }

void FindBlocks()
{
    var remotes = new List<IMyRemoteControl>();
    GridTerminalSystem.GetBlocksOfType(remotes, b => Own(b));
    remote = remotes.Count == 1 ? remotes[0] : null;
    GridTerminalSystem.GetBlocksOfType(thrust, b => Own(b));
    GridTerminalSystem.GetBlocksOfType(gyros, b => Own(b));
    GridTerminalSystem.GetBlocksOfType(warheads, b => Own(b));
    GridTerminalSystem.GetBlocksOfType(cameras, b => Own(b));
    GridTerminalSystem.GetBlocksOfType(timers, b => Own(b));
    GridTerminalSystem.GetBlocksOfType(lights, b => Own(b));
    GridTerminalSystem.GetBlocksOfType(tanks, b => Own(b));
    GridTerminalSystem.GetBlocksOfType(batteries, b => Own(b));
    foreach (var c in cameras) c.EnableRaycast = true;
    while (cameraSteps.Count < cameras.Count) cameraSteps.Add(0);
    var panels = new List<IMyButtonPanel>();
    GridTerminalSystem.GetBlocksOfType(panels, b => Own(b));
    foreach (var panel in panels) panel.SetCustomButtonName(0, panel.CustomName.Contains("START") ? "LAUNCH IN 10s" : "ABORT / CANCEL");
}

bool Own(IMyTerminalBlock b)
{ return b.CubeGrid == Me.CubeGrid && b.CustomName.Contains(Tag); }

public void Main(string argument, UpdateType source)
{
    string cmd = (argument ?? "").Trim().ToUpperInvariant();
    if (cmd == "ABORT" || cmd == "STOP") { Abort("Operator abort"); }
    else if (cmd == "COUNTDOWN" || cmd == "COUNTDOWN TEST")
    {
        if (!Active()) BeginCountdown(cmd == "COUNTDOWN TEST");
        else note = "Sequence already active; ABORT cancels.";
    }
    else if (cmd == "IGNITE")
    {
        if (phase == Phase.Countdown && countdownLeft <= 0.5) Start(dryRun);
        else note = "Ignored IGNITE: no completed launch countdown.";
    }
    else if (cmd == "LOCK")
    {
        if (!Active()) LockCamera();
        else note = "Acquisition runs automatically during flight/countdown.";
    }
    else if (cmd == "CHECK")
    {
        if (Active()) note = "Abort before CHECK.";
        else { FindBlocks(); SafeStop(); note = Preflight(); }
    }
    else if (cmd == "SCAN")
    {
        if (Active()) note = "Acquisition runs automatically during flight.";
        else
        {
            FindBlocks(); SafeStop(); targetId = 0; age = 999;
            if (remote == null) note = "SCAN needs exactly one tagged remote.";
            else
            {
                watchContacts = true;
                up = Unit(-remote.GetNaturalGravity(), remote.WorldMatrix.Forward);
                acquisition = "Camera sweep: enemy grid <=750m; rays need charge and line of sight.";
                scanClock = 1;
                Acquire();
                note = "Passive SCAN: watching contacts without launch. LAUNCH searches automatically.";
            }
        }
    }
    else if (cmd == "LAUNCH" || cmd == "TEST")
    {
        if (!Active()) Start(cmd == "TEST");
        else note = "Already in flight.";
    }
    // Terminal/toolbar calls do not advance simulation time.
    if ((source & (UpdateType.Update1 | UpdateType.Update10 | UpdateType.Update100)) != 0)
    {
        double dt = Math.Max(0.001, Math.Min(0.5, Runtime.TimeSinceLastRun.TotalSeconds));
        if (phase == Phase.Countdown) TickCountdown(dt);
        else if (Active()) Tick(dt);
        else if (watchContacts && remote != null && remote.IsWorking)
        {
            age += dt; scanClock += dt;
            Acquire();
            if (age > TrackTimeout) targetId = 0;
        }
    }
    Report();
}

bool Active() { return phase != Phase.Idle && phase != Phase.Abort; }

string Preflight()
{
    if (remote == null) return "Need exactly one tagged remote on this grid.";
    if (Me.CubeGrid.IsStatic) return "Detach from the static launch grid first.";
    if (!remote.IsWorking) return "Remote has no working power.";
    if (thrust.Count < 6 || gyros.Count == 0 || cameras.Count == 0 || warheads.Count == 0)
        return "Need tagged thrust in six axes, gyro, camera and warhead.";
    if (remote.GetNaturalGravity().Length() < 0.5) return "Planetary gravity required.";
    foreach (var b in batteries) if (!b.IsWorking || b.CurrentStoredPower < 0.02)
        return "Charge the batteries first.";
    foreach (var t in tanks) if (t.FilledRatio < 0.1 || t.Stockpile)
        return "Fill tanks above 10% and disable Stockpile.";
    Vector3D u = -Vector3D.Normalize(remote.GetNaturalGravity());
    if (Vector3D.Dot(remote.WorldMatrix.Forward, u) < 0.95)
        return "Place the missile nose upward before launch.";
    foreach (var g in gyros) if (!g.IsWorking) return "A gyro is offline.";
    double mass = remote.CalculateShipMass().PhysicalMass;
    if (Capacity(u) < mass * (remote.GetNaturalGravity().Length() + 3))
        return "Insufficient working upward thrust for launch.";
    // Verify braking authority in every body axis before attempting the flip.
    Vector3D[] axes = { remote.WorldMatrix.Forward, remote.WorldMatrix.Backward,
        remote.WorldMatrix.Left, remote.WorldMatrix.Right, remote.WorldMatrix.Up, remote.WorldMatrix.Down };
    foreach (var axis in axes)
        if (Capacity(axis) < mass * 3) return "Insufficient thrust in a body axis.";
    foreach (var c in cameras) if (c.IsWorking) return "READY";
    return "No working raycast camera.";
}

void BeginCountdown(bool test)
{
    FindBlocks(); SafeStop();
    string check = Preflight();
    if (check != "READY") { note = check; return; }
    if (timers.Count != 1 || !timers[0].IsWorking) { note = "Need one working tagged launch timer."; return; }
    dryRun = test; countdownLeft = LaunchDelay;
    up = -Vector3D.Normalize(remote.GetNaturalGravity());
    timers[0].TriggerDelay = (float)LaunchDelay;
    SetPhase(Phase.Countdown, "Launch countdown started. ABORT cancels.");
    timers[0].StartCountdown();
}

void TickCountdown(double dt)
{
    countdownLeft -= dt; age += dt; scanClock += dt;
    Disarm();
    if (remote == null || !remote.IsWorking || timers.Count != 1 || !timers[0].IsWorking)
    { Abort("Countdown hardware offline"); return; }
    Acquire();
    if (countdownLeft < -2) Abort("Timer expired without launch action; check timer toolbar");
}

double Capacity(Vector3D direction)
{
    double force = 0;
    foreach (var t in thrust)
        if (t.IsWorking) force += t.MaxEffectiveThrust * Math.Max(0, Vector3D.Dot(t.WorldMatrix.Backward, direction));
    return force;
}

void Start(bool test)
{
    FindBlocks(); SafeStop();
    string check = Preflight();
    if (check != "READY") { note = check; return; }
    dryRun = test;
    watchContacts = false;
    launch = remote.GetPosition();
    up = -Vector3D.Normalize(remote.GetNaturalGravity());
    scanAnchor = launch + up * SearchHeight;
    if (age > TrackTimeout) { targetId = 0; age = 999; }
    elapsed = phaseTime = scanClock = 0;
    foreach (var b in batteries) b.ChargeMode = ChargeMode.Auto;
    var connectors = new List<IMyShipConnector>();
    GridTerminalSystem.GetBlocksOfType(connectors, b => Own(b));
    foreach (var c in connectors) c.Disconnect();
    var gears = new List<IMyLandingGear>();
    GridTerminalSystem.GetBlocksOfType(gears, b => Own(b));
    foreach (var g in gears) { g.AutoLock = false; g.Unlock(); }
    remote.SetAutoPilotEnabled(false);
    remote.DampenersOverride = false;
    SetPhase(Phase.Boost, "Vertical boost");
}

void SetPhase(Phase p, string message) { phase = p; phaseTime = 0; note = message; }

void Tick(double dt)
{
    elapsed += dt; age += dt; phaseTime += dt; scanClock += dt;
    if (remote == null || !remote.IsWorking) { Abort("Lost remote/power"); return; }
    if (elapsed > 150) { Abort("Search/flight time limit reached"); return; }
    foreach (var tank in tanks) if (tank.FilledRatio < 0.03) { Abort("Fuel reserve reached"); return; }
    if (remote.GetNaturalGravity().Length() < 0.5) { Abort("Left planetary flight envelope"); return; }
    Acquire();
    Vector3D pos = remote.GetPosition();
    Vector3D velocity = remote.GetShipVelocities().LinearVelocity;
    double height = Vector3D.Dot(pos - launch, up);
    Vector3D desiredVelocity = Vector3D.Zero;
    Vector3D nose = up;
    double pivotRate = 0;

    if (phase == Phase.Boost)
    {
        desiredVelocity = up * Math.Min(40, Math.Max(6, (SearchHeight - height) * 0.5));
        if (height > SearchHeight - 12) SetPhase(Phase.Search, "Holding scan point; upright pivot search");
    }
    else if ((phase == Phase.Overhead || phase == Phase.Dive) && age > TrackTimeout)
    {
        Disarm(); targetId = 0;
        scanAnchor = pos + up * Math.Max(0, SearchHeight - height);
        SetPhase(Phase.Search, "Target lost: warheads disarmed, resuming search");
    }

    if (phase == Phase.Search)
    {
        if (targetId != 0 && age < TrackTimeout) SetPhase(Phase.Overhead, "Enemy acquired: climbing above target");
        else
        {
            Vector3D error = scanAnchor - pos;
            desiredVelocity = GuidanceVelocity(error, 8, up, 5);
            bool upright = Vector3D.Dot(remote.WorldMatrix.Forward, up) > Math.Cos(5 * Math.PI / 180);
            bool settled = Reject(error,up).Length() < 3 && Math.Abs(Vector3D.Dot(error,up)) < 3 &&
                Reject(velocity,up).Length() < 1.5 && Math.Abs(Vector3D.Dot(velocity,up)) < 1.5;
            if (upright && settled) pivotRate = ScanTurnRate;
            note = !upright ? "Restoring upright attitude; pivot paused" : settled ? "Holding scan point; pivoting 3 deg/s" : "Settling at scan point; pivot paused";
        }
    }
    if (phase == Phase.Overhead || phase == Phase.Dive)
    {
        Vector3D predicted = target + targetVelocity * Math.Min(age, TrackTimeout);
        if (phase == Phase.Overhead)
        {
            Vector3D aim = predicted + up * AttackHeight;
            desiredVelocity = GuidanceVelocity(aim - pos, 40, up, 25) + targetVelocity;
            Vector3D horizontal = Reject(predicted - pos, up);
            double above = Vector3D.Dot(pos - predicted, up);
            if (horizontal.Length() < 25 && above >= AttackHeight - 15 &&
                Reject(velocity - targetVelocity, up).Length() < 12)
                SetPhase(Phase.Dive, "Overhead: terminal dive");
        }
        if (phase == Phase.Dive)
        {
            Vector3D delta = predicted - pos;
            double travel = Math.Min(3, delta.Length() / DiveSpeed);
            Vector3D intercept = predicted + targetVelocity * travel;
            nose = Unit(intercept - pos, -up);
            desiredVelocity = nose * DiveSpeed;
            // Do not accelerate through the attitude flip. Hold while turning.
            if (Vector3D.Dot(remote.WorldMatrix.Forward, nose) < 0.85) desiredVelocity = Vector3D.Zero;
            double range = delta.Length();
            bool permit = !dryRun && age < 0.6 && range < 200 &&
                Vector3D.Distance(pos, launch) > 100 && Vector3D.Dot(nose, up) < -0.5 &&
                Vector3D.Dot(remote.WorldMatrix.Forward, nose) > 0.85;
            foreach (var w in warheads) w.IsArmed = permit;
            BoundingBoxD box = new BoundingBoxD(targetBox.Min + targetVelocity * age,
                targetBox.Max + targetVelocity * age);
            double surfaceDistance = Vector3D.Distance(pos, Vector3D.Clamp(pos, box.Min, box.Max));
            if (surfaceDistance < 8 && age < 0.6)
            {
                if (dryRun) { Abort("TEST intercept reached; warheads stayed disarmed"); return; }
                if (permit) { foreach (var w in warheads) w.Detonate(); return; }
            }
        }
    }
    // Terrain is not an avoidance map. Abort descent outside terminal attack.
    double ground;
    if (phase != Phase.Boost && phase != Phase.Dive &&
        remote.TryGetPlanetElevation(MyPlanetElevation.Surface, out ground) && ground < 35)
    { Abort("Terrain clearance below 35m"); return; }
    desiredVelocity = Limit(desiredVelocity, 90);
    Vector3D correction = (desiredVelocity - velocity) * 1.5;
    Vector3D desiredAcceleration = phase == Phase.Search ?
        up * MathHelper.Clamp(Vector3D.Dot(correction,up),-10,10) + Limit(Reject(correction,up),20) : Limit(correction,20);
    double mass = remote.CalculateShipMass().PhysicalMass;
    Vector3D gravity = remote.GetNaturalGravity();
    Vector3D force = (desiredAcceleration - gravity) * mass;
    if (phase == Phase.Search)
    {
        // Reserve lift first. Lateral corrections can use only the remaining
        // per-axis thrust, including gravity's projection when slightly tilted.
        Vector3D weightSupport = -gravity * mass;
        if (!CanSupport(weightSupport)) { Abort("Cannot support weight with 10% thrust margin; scan cancelled"); return; }
        Vector3D vertical = up * Vector3D.Dot(desiredAcceleration,up) * mass;
        force = weightSupport + vertical * ForceScale(weightSupport,vertical);
        Vector3D lateral = Reject(desiredAcceleration,up) * mass;
        if (Vector3D.Dot(remote.WorldMatrix.Forward,up) < Math.Cos(5 * Math.PI / 180)) lateral = Vector3D.Zero;
        force += lateral * ForceScale(force,lateral);
        double side = Math.Min(Math.Min(Capacity(remote.WorldMatrix.Left),Capacity(remote.WorldMatrix.Right)),
            Math.Min(Capacity(remote.WorldMatrix.Up),Capacity(remote.WorldMatrix.Down))) / mass;
        hoverInfo = "Lift/weight " + (Capacity(up) / Math.Max(1,mass*gravity.Length())).ToString("0.00") +
            " | side max " + side.ToString("0.0") + "m/s2 | lift priority";
    }
    else hoverInfo = "";
    ApplyForce(force);
    Aim(nose,pivotRate);
}

bool CanSupport(Vector3D support)
{
    foreach (var t in thrust)
    {
        Vector3D d = t.WorldMatrix.Backward;
        if (Vector3D.Dot(support,d) > Capacity(d)*0.9 + 1) return false;
    }
    return true;
}

double ForceScale(Vector3D reserved, Vector3D requested)
{
    double scale = 1;
    foreach (var t in thrust)
    {
        Vector3D d = t.WorldMatrix.Backward;
        double delta = Vector3D.Dot(requested,d);
        if (delta > 0.001) scale = Math.Min(scale, (Capacity(d)*0.9 - Vector3D.Dot(reserved,d))/delta);
    }
    return Math.Max(0,Math.Min(1,scale));
}

void Acquire()
{
    if (scanClock < 0.15 || cameras.Count == 0) return;
    scanClock = 0;
    int attempts = cameras.Count, fired = 0;
    acquisition = "Camera sweep: charging or no ray hit.";
    while (attempts-- > 0 && fired < 3)
    {
        int index = cameraCursor++ % cameras.Count;
        var camera = cameras[index];
        if (!camera.IsWorking) continue;
        MyDetectedEntityInfo hit;
        if (targetId != 0 && age < 6)
        {
            Vector3D point = target + targetOffset + targetVelocity * age;
            if (!camera.CanScan(point)) continue;
            hit = camera.Raycast(point);
        }
        else
        {
            double range = camera.RaycastDistanceLimit < 0 ? CameraRange : Math.Min(CameraRange, camera.RaycastDistanceLimit);
            if (range < 1 || !camera.CanScan(range)) continue;
            float pitch, yaw;
            SweepAngles(cameraSteps[index]++, out pitch, out yaw);
            float cone = Math.Max(0, Math.Min(40, camera.RaycastConeLimit - 1));
            hit = camera.Raycast(range, pitch * cone / 40, yaw * cone / 40);
        }
        fired++; rayCount++;
        if (!hit.IsEmpty()) acquisition = "Camera: " + hit.Relationship + " " + hit.Type + " " + hit.Name;
        Consider(hit);
        if (age == 0) break;
    }
}

static void SweepAngles(int step, out float pitch, out float yaw)
{
    int n = step % 289, x = 0, y = 0;
    if (n > 0)
    {
        int ring = (int)Math.Ceiling((Math.Sqrt(n + 1) - 1) / 2);
        int side = 2 * ring, offset = (2 * ring + 1) * (2 * ring + 1) - 1 - n;
        if (offset < side) { x = ring - offset; y = -ring; }
        else if (offset < 2 * side) { x = -ring; y = -ring + offset - side; }
        else if (offset < 3 * side) { x = -ring + offset - 2 * side; y = ring; }
        else { x = ring; y = ring - offset + 3 * side; }
    }
    float shift = (step / 289) % 2 == 0 ? 0 : 2.5f;
    pitch = Math.Max(-40, Math.Min(40, y * 5 + shift));
    yaw = Math.Max(-40, Math.Min(40, x * 5 + shift));
}

void LockCamera()
{
    FindBlocks(); SafeStop();
    if (remote == null || cameras.Count == 0) { note = "LOCK requires a remote and camera."; return; }
    IMyCameraBlock chosen = null;
    foreach (var c in cameras) if (c.IsActive) { chosen = c; break; }
    if (chosen == null) foreach (var c in cameras) if (c.CustomName.Contains("Camera Forward")) { chosen = c; break; }
    if (chosen == null) chosen = cameras[0];
    double range = chosen.RaycastDistanceLimit < 0 ? CameraRange : Math.Min(CameraRange, chosen.RaycastDistanceLimit);
    if (!chosen.IsWorking || !chosen.CanScan(range)) { note = "LOCK camera charging/offline. Try again shortly."; return; }
    up = Unit(-remote.GetNaturalGravity(), remote.WorldMatrix.Forward);
    targetId = 0; age = 999; watchContacts = true;
    var hit = chosen.Raycast(range, 0, 0); rayCount++;
    acquisition = hit.IsEmpty() ? "LOCK ray missed; sweep continues." : "Camera: " + hit.Relationship + " " + hit.Name;
    Consider(hit);
    note = targetId == 0 ? "No eligible enemy on camera centerline." : "Camera locked " + targetName;
}

void Consider(MyDetectedEntityInfo hit)
{
    if (hit.IsEmpty() || hit.EntityId == Me.CubeGrid.EntityId) return;
    if (hit.EntityId == targetId && hit.Relationship != MyRelationsBetweenPlayerAndBlock.Enemies)
    { targetId = 0; age = 999; Disarm(); return; }
    if (hit.Relationship != MyRelationsBetweenPlayerAndBlock.Enemies ||
        (hit.Type != MyDetectedEntityType.SmallGrid && hit.Type != MyDetectedEntityType.LargeGrid)) return;
    if (targetId != 0 && age < TrackTimeout && hit.EntityId != targetId) return;
    if (Vector3D.Dot(hit.Position - remote.GetPosition(), up) > 60) return;
    targetId = hit.EntityId;
    targetName = hit.Name;
    targetOffset = (hit.HitPosition ?? hit.Position) - hit.Position;
    target = hit.Position; targetVelocity = hit.Velocity; targetBox = hit.BoundingBox; age = 0;
}

void ApplyForce(Vector3D force)
{
    // All blueprint thrusters use orthogonal axes. Share demand within an axis.
    foreach (var t in thrust)
    {
        if (!t.IsWorking) { t.ThrustOverridePercentage = 0; continue; }
        Vector3D direction = t.WorldMatrix.Backward;
        double available = Capacity(direction);
        double demand = Math.Max(0, Vector3D.Dot(force, direction));
        t.ThrustOverridePercentage = (float)Math.Min(1, demand / Math.Max(1, available));
    }
}

void Aim(Vector3D nose, double pivotRate)
{
    Vector3D current = remote.WorldMatrix.Forward;
    Vector3D axis = Vector3D.Cross(current, nose);
    double sine = axis.Length();
    double cosine = MathHelper.Clamp(Vector3D.Dot(current, nose), -1, 1);
    // Cross products vanish at 180 degrees: explicitly choose a flip axis.
    if (sine < 0.001 && cosine < 0) axis = remote.WorldMatrix.Right;
    else if (sine > 0.001) axis /= sine;
    Vector3D omega = axis * Math.Min(1.5, Math.Atan2(sine, cosine) * 2);
    omega += up * pivotRate;
    omega -= (remote.GetShipVelocities().AngularVelocity - up * pivotRate) * 0.4;
    foreach (var gyro in gyros)
    {
        Vector3D local = Vector3D.TransformNormal(omega, MatrixD.Transpose(gyro.WorldMatrix));
        gyro.Pitch = (float)-local.X; gyro.Yaw = (float)-local.Y; gyro.Roll = (float)-local.Z;
        gyro.GyroOverride = true;
    }
}

void Disarm() { foreach (var w in warheads) { w.StopCountdown(); w.IsArmed = false; } }
void SafeStop()
{
    foreach (var timer in timers) timer.StopCountdown();
    foreach (var t in thrust) t.ThrustOverridePercentage = 0;
    foreach (var g in gyros) { g.GyroOverride = false; g.Pitch = g.Yaw = g.Roll = 0; }
    Disarm();
    if (remote != null) { remote.SetAutoPilotEnabled(false); remote.DampenersOverride = true; }
}
void Abort(string reason) { phase = Phase.Abort; watchContacts = false; note = reason; SafeStop(); Storage = ""; }
static Vector3D Reject(Vector3D v, Vector3D normal) { return v - normal * Vector3D.Dot(v, normal); }
static Vector3D Unit(Vector3D v, Vector3D fallback) { return v.LengthSquared() < 0.000001 ? fallback : Vector3D.Normalize(v); }
static Vector3D Limit(Vector3D v, double max) { return v.LengthSquared() <= max * max ? v : Unit(v, Vector3D.Zero) * max; }
static Vector3D GuidanceVelocity(Vector3D error, double speed, Vector3D vertical, double verticalSpeed)
{
    return Limit(Reject(error, vertical) * 0.5, speed) + vertical * MathHelper.Clamp(Vector3D.Dot(error, vertical) * 0.5, -verticalSpeed, verticalSpeed);
}

void Report()
{
    bool fresh = targetId != 0 && age <= TrackTimeout;
    string contact = !fresh || remote == null ? "NO ENEMY LOCK" : "ENEMY: " + targetName + " | " + Vector3D.Distance(remote.GetPosition(), target).ToString("0") + "m";
    string text = "GroundGame Topdown 0.4\n" + phase + (phase == Phase.Countdown ? " T-" + Math.Ceiling(Math.Max(0,countdownLeft)) + "s" : "") + (watchContacts ? " | PASSIVE SCAN" : "") + (dryRun ? " | TEST: DISARMED" : "") +
        "\n" + note + "\nFlight " + elapsed.ToString("0") + "s | track age " + age.ToString("0.0") +
        "s\n" + contact + "\nCamera rays " + rayCount + " | 750m sweep | " + acquisition +
        "\nThrusters " + thrust.Count + " | gyros " + gyros.Count +
        (hoverInfo.Length == 0 ? "" : "\n" + hoverInfo) +
        "\nRemote page 1: 1 CHECK  2 SCAN\n3 TEST 10s  4 LAUNCH 10s  5 ABORT\n6 AIM  7 Down  8 LOCK  9 ABORT";
    Echo(text);
    if (remote != null)
    {
        string shortName = targetName.Replace('\n',' ').Replace('\r',' ');
        if (shortName.Length > 24) shortName = shortName.Substring(0,24);
        string remoteTitle = Tag + " Remote | " + (watchContacts ? "SCAN" : phase.ToString()) +
            (!fresh ? " | NO LOCK" : " | " + shortName);
        if (remote.CustomName != remoteTitle) remote.CustomName = remoteTitle;
    }
    foreach (var light in lights)
    {
        light.Enabled = true;
        light.Color = phase == Phase.Countdown ? new Color(255,170,0) : (Active() ? Color.Red : (fresh ? Color.Green : Color.Blue));
        light.BlinkIntervalSeconds = phase == Phase.Countdown ? 1 : 0;
        light.BlinkLength = 50;
    }
    var surface = Me.GetSurface(0);
    surface.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
    surface.FontSize = 0.9f;
    surface.WriteText(text);
}
// END PB BODY
}
