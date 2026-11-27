const string Tag = "[GG-MISSILE]";
const double SearchHeight = 200, SearchRadius = 650, SearchSpeed = 35;
const double AttackHeight = 180, DiveSpeed = 80, TrackTimeout = 2;
enum Phase { Idle, Boost, Search, Overhead, Dive, Abort }
Phase phase = Phase.Idle;
IMyRemoteControl remote;
readonly List<IMyThrust> thrust = new List<IMyThrust>();
readonly List<IMyGyro> gyros = new List<IMyGyro>();
readonly List<IMyWarhead> warheads = new List<IMyWarhead>();
readonly List<IMyLargeTurretBase> turrets = new List<IMyLargeTurretBase>();
readonly List<IMyCameraBlock> cameras = new List<IMyCameraBlock>();
readonly List<IMyGasTank> tanks = new List<IMyGasTank>();
readonly List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();
Vector3D launch, up, east, north, target, targetVelocity;
BoundingBoxD targetBox;
long targetId;
double elapsed, age = 999, phaseTime, scanClock, searchAngle, searchRadius = 60;
int scanIndex;
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
    GridTerminalSystem.GetBlocksOfType(turrets, b => Own(b));
    GridTerminalSystem.GetBlocksOfType(cameras, b => Own(b));
    GridTerminalSystem.GetBlocksOfType(tanks, b => Own(b));
    GridTerminalSystem.GetBlocksOfType(batteries, b => Own(b));
    foreach (var c in cameras) c.EnableRaycast = true;
}

bool Own(IMyTerminalBlock b)
{ return b.CubeGrid == Me.CubeGrid && b.CustomName.Contains(Tag); }

public void Main(string argument, UpdateType source)
{
    string cmd = (argument ?? "").Trim().ToUpperInvariant();
    if (cmd == "ABORT" || cmd == "STOP") { Abort("Operator abort"); }
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
                acquisition = "No radar contact: enemy-owned grid within 600m and clear view required.";
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
        if (Active()) Tick(dt);
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
    if (thrust.Count < 6 || gyros.Count == 0 || turrets.Count == 0 || warheads.Count == 0)
        return "Need tagged thrust in six axes, gyro, turret and warhead.";
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
    bool radar = false;
    foreach (var t in turrets) if (t.IsWorking) radar = true;
    return radar ? "READY" : "Targeting turret is offline.";
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
    east = Vector3D.CalculatePerpendicularVector(up);
    north = Vector3D.Cross(up, east);
    targetId = 0; age = 999; elapsed = phaseTime = scanClock = searchAngle = 0;
    searchRadius = 60;
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

    if (phase == Phase.Boost)
    {
        desiredVelocity = up * Math.Min(40, Math.Max(6, (SearchHeight - height) * 0.5));
        if (height > SearchHeight - 12) SetPhase(Phase.Search, "Searching expanding circles");
    }
    else if ((phase == Phase.Overhead || phase == Phase.Dive) && age > TrackTimeout)
    {
        Disarm(); targetId = 0;
        SetPhase(Phase.Search, "Target lost: warheads disarmed, resuming search");
    }

    if (phase == Phase.Search)
    {
        if (targetId != 0 && age < TrackTimeout) SetPhase(Phase.Overhead, "Enemy acquired: climbing above target");
        else
        {
            searchAngle += dt * SearchSpeed / Math.Max(60, searchRadius);
            searchRadius = Math.Min(SearchRadius, 60 + searchAngle * 15);
            Vector3D offset = east * Math.Cos(searchAngle) + north * Math.Sin(searchAngle);
            Vector3D waypoint = launch + offset * searchRadius + up * SearchHeight;
            desiredVelocity = GuidanceVelocity(waypoint - pos, SearchSpeed, up, 20);
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
    Vector3D desiredAcceleration = Limit((desiredVelocity - velocity) * 1.5, 20);
    Vector3D force = (desiredAcceleration - remote.GetNaturalGravity()) * remote.CalculateShipMass().PhysicalMass;
    ApplyForce(force);
    Aim(nose);
}

void Acquire()
{
    acquisition = "No turret contact: needs enemy grid <=600m and line of sight.";
    foreach (var turret in turrets)
    {
        if (!turret.IsWorking) continue;
        var hit = turret.GetTargetedEntity();
        if (!hit.IsEmpty()) acquisition = hit.Relationship + " " + hit.Type + ": " + hit.Name;
        Consider(hit);
    }
    if (scanClock < 0.2) return;
    scanClock = 0;
    foreach (var camera in cameras)
    {
        if (!camera.IsWorking) continue;
        if (targetId != 0 && age < TrackTimeout)
        {
            Vector3D point = target + targetVelocity * age;
            Vector3D delta = point - camera.GetPosition();
            if (delta.LengthSquared() > 1 && Vector3D.Dot(Unit(delta, up), camera.WorldMatrix.Forward) > 0.72 && camera.CanScan(point))
            { Consider(camera.Raycast(point)); break; }
        }
        else if (camera.CanScan(500))
        {
            // Sparse supplemental search; the turret is the primary detector.
            float pitch = (scanIndex % 3 - 1) * 20;
            float yaw = ((scanIndex / 3) % 3 - 1) * 20;
            Consider(camera.Raycast(500, pitch, yaw)); scanIndex++;
            break;
        }
    }
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

void Aim(Vector3D nose)
{
    Vector3D current = remote.WorldMatrix.Forward;
    Vector3D axis = Vector3D.Cross(current, nose);
    double sine = axis.Length();
    double cosine = MathHelper.Clamp(Vector3D.Dot(current, nose), -1, 1);
    // Cross products vanish at 180 degrees: explicitly choose a flip axis.
    if (sine < 0.001 && cosine < 0) axis = remote.WorldMatrix.Right;
    else if (sine > 0.001) axis /= sine;
    Vector3D omega = axis * Math.Min(1.5, Math.Atan2(sine, cosine) * 2);
    omega -= remote.GetShipVelocities().AngularVelocity * 0.4;
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
    string contact = targetId == 0 || remote == null ? "NO ENEMY LOCK" : "ENEMY: " + targetName + " | " + Vector3D.Distance(remote.GetPosition(), target).ToString("0") + "m";
    string text = "GroundGame Topdown 0.2\n" + phase + (watchContacts ? " | PASSIVE SCAN" : "") + (dryRun ? " | TEST: DISARMED" : "") +
        "\n" + note + "\nFlight " + elapsed.ToString("0") + "s | track age " + age.ToString("0.0") +
        "s\n" + contact + "\nRadar limit 600m | " + acquisition +
        "\nThrusters " + thrust.Count + " | gyros " + gyros.Count +
        "\nRemote page 1: 1 CHECK  2 SCAN\n3 TEST  4 LAUNCH  5 ABORT\n6 Nose  7 Down  8 STATUS  9 ABORT";
    Echo(text);
    if (remote != null)
    {
        string shortName = targetName.Replace('\n',' ').Replace('\r',' ');
        if (shortName.Length > 24) shortName = shortName.Substring(0,24);
        string remoteTitle = Tag + " Remote | " + (watchContacts ? "SCAN" : phase.ToString()) +
            (targetId == 0 ? " | NO LOCK" : " | " + shortName);
        if (remote.CustomName != remoteTitle) remote.CustomName = remoteTitle;
    }
    var surface = Me.GetSurface(0);
    surface.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
    surface.FontSize = 0.9f;
    surface.WriteText(text);
}
