# GroundGame Topdown missile — prototype 0.4

A vanilla Space Engineers missile with camera raycast acquisition, vertical boost, overhead approach and downward attack. Requires natural gravity and in-game scripts enabled. No mods or DLC required. The battery remains inline. The targeting turret has been removed; cameras provide all target reports.

## Fixed-position pivot search

After boost, the missile holds a point about 200 m above launch and turns slowly around the gravity axis at **3 degrees/second**. Its nose and main lift engine stay vertical. It does not fly a spiral or tip sideways to sweep. Existing cameras also sweep their ray angles while the body turns; no rotor hardware is needed.

The main engine carries the weight. The controller reserves gravity support first, then altitude correction, and only then assigns the remaining per-axis thrust to lateral drift correction. Each budget uses current effective thrust and measured ship mass; lateral engines are not assumed as powerful as the main engine. Commands leave 10% thrust headroom. Vertical braking remains independent of large sideways correction requests.

The pivot pauses if the missile leans more than 5 degrees, is more than 3 m from the scan point, or moves faster than 1.5 m/s vertically/horizontally. It restores upright attitude and settles before resuming. If gravity support cannot fit the current per-axis thrust budgets with headroom, it cancels search and reports the fault. Insufficient thrust cannot be fixed by software; abort/dampeners do not guarantee altitude recovery on a damaged or overloaded ship.

Computer output shows lift-to-weight ratio, weakest lateral acceleration, and whether the pivot is settling, restoring upright attitude or turning. If a target is lost away from the initial scan point, the missile holds its current horizontal location and climbs back to at least the original search-height plane before resuming the pivot.

On an existing 0.3 missile, abort first and reload **GroundGame Topdown** into Computer through Browse Scripts, then Check code and Remember & Exit. Geometry and button wiring are unchanged in 0.4. Fresh F10 copies embed the new script.

## Install or update

Extract `dist/GroundGame_Topdown_Prototype.zip` into `%APPDATA%\SpaceEngineers`, or run `powershell -File tools/install_missile.ps1` from this checkout. The installer backs up existing copies and invalidates the old binary blueprint cache.

- F10 → Local: **[GG] Topdown Missile**.
- Browse Scripts → Local: **GroundGame Topdown**.
- The blueprint embeds `dist/TopdownMissile.cs`. Do not paste the compiler wrapper in `src/missile/Program.cs`.

**Refresh F10 and paste a new copy.** Updating the library cannot add buttons or change geometry on a missile already in a world.

## Exterior launch buttons

Two button panels sit on the lower left side, near the lower fuel tank:

- **START 10s / LAUNCH IN 10s** begins a **live** ten-second launch sequence after checking power, fuel, orientation, cameras and thrust. A failed check stays put and reports the fault on Computer.
- **ABORT / CANCEL** stops the timer and cancels launch. It also aborts active flight if you can reach it.

The real timer in the nose deck counts ten seconds, beeps, and runs the launch action. The exterior lamp flashes amber. Warheads stay disarmed throughout countdown; the controller applies no launch thrust. Computer shows `Countdown T-10s` down to zero. Pressing START again does not restart an active sequence. A late timer action after ABORT is ignored.

The exterior button launches live without requiring a preselected target. For a disarmed trial use remote **3 TEST 10s**, not the exterior live button.

## Prepare and launch

1. Paste in a creative test world, clear of terrain and other grids. The main hydrogen engine goes underneath; the nose camera faces upward. The remote's Forward axis must be within about 18 degrees of vertical. Detach from static merged grids first; the script does not operate merge blocks.
2. Take ownership. Fill both tanks with Stockpile off and charge the battery. Creative paste contains fuel/charge; survival projection and welding do not supply those resources. No turret ammunition is needed.
3. Use an expendable enemy-owned small or large grid for testing. Friendly, neutral and unowned grids are rejected. Merely naming a grid "enemy" does not qualify it.
4. Select **[GG-MISSILE] Remote → Control**, then toolbar page 1 (`Ctrl+1` by default). Press **1 CHECK**. Select **Computer → Detailed Info** for `READY` or the fault. Its built-in screen also shows output.
5. Press **2 SCAN** for passive camera search without launching. Wait for camera charge. An eligible hit creates an `ENEMY: name` report and puts the name in the remote's title.
6. Use **3 TEST 10s**, **4 LAUNCH 10s**, or the exterior live START button. Stand clear before the countdown expires. Flight follows **Countdown → Boost → Search → Overhead → Dive**.
7. **5/9 ABORT** cancels countdown/flight, disarms, stops timers, releases overrides and enables dampeners. It does not land or return the missile. Maintain antenna access to use the remote toolbar after launch.

**TEST still flies toward the target and can collide with it.** It keeps warheads disarmed; it is not a return-to-base mode.

## Camera acquisition

SCAN sweeps all six cameras fairly with center-out patterns up to approximately ±40 degrees. Search rays request up to **750 m**, reduced if the world sets a shorter camera limit. A camera fires only with enough accumulated scan range. No turret or radar is involved.

Raycasts are thin lines, not a radar volume. A small ship between rays can be missed even inside 750 m. Search takes time, requires line of sight, and cannot guarantee discovery of every nearby grid. Terrain and the missile can block rays. Use a broad exposed enemy target about 300–500 m away for the first trial.

For a deliberate centerline ray:

1. Press **6 AIM** to view the horizontal Forward side camera, or view another camera through the terminal.
2. Align the ship so the enemy lies on that camera block's forward axis. Camera look-around/panning does not steer the scripted ray.
3. Press **8 LOCK**. It uses the camera currently being viewed, or Forward camera if none is active. A successful hit shows the enemy name. A miss, friendly contact or charging camera is reported without launching.

Once acquired, cameras refresh the predicted entity position using measured velocity and the surface-hit offset. Fresh reports can carry into boost. Two seconds without a matching report disarms and returns flight to search. A different enemy may be selected after a lost track; test with one intended enemy first.

Computer reports ray count, relationship, track age, range, phase and countdown. Increasing rays with `NO ENEMY LOCK` means no eligible fresh hit. A stationary ray count may mean charging, offline cameras, or inability to aim at the predicted point.

## Remote toolbar

| Page 1 | Label | Action |
| --- | --- | --- |
| 1 | CHECK | Preflight and faults. |
| 2 | SCAN | Passive camera sweep; no launch. |
| 3 | TEST 10s | Countdown to a disarmed attack flight. |
| 4 | LAUNCH 10s | Countdown to a live attack. |
| 5 | ABORT | Cancel and disarm. |
| 6 | AIM | View horizontal Forward camera. |
| 7 | DOWN | View downward camera. |
| 8 | LOCK | Camera centerline ray. |
| 9 | ABORT | Duplicate abort. |

Gamepad bank 1: **Up CHECK · Left SCAN · Right TEST 10s · Down ABORT**. Bank 2: **Up LAUNCH 10s · Left AIM · Right DOWN · Down ABORT**. Bank 3: **Up LOCK · Left STATUS · Right SCAN · Down ABORT**.

Direct PB commands `LAUNCH` and `TEST` remain immediate for compatibility. Saved exterior/remote buttons use `COUNTDOWN` or `COUNTDOWN TEST`. The timer runs guarded `IGNITE`. Do not wire an immediate launch onto the exterior panel.

## Flight and verification

Boost climbs about 200 m above launch. Search holds a fixed point and pivots, bounded by a 150-second flight limit and fuel. Overhead approaches about 180 m above the target, then dives at up to 80 m/s. Targets more than 60 m above the missile are excluded. Outside boost/dive, reported terrain clearance below 35 m aborts. No obstacle map or guaranteed clear route exists.

Arming requires an enemy report younger than 0.6 seconds, terminal range below 200 m, more than 100 m separation from launch, and an aligned downward nose. An 8 m bounding-box proximity check may detonate; armed-warhead collision can occur first.

The 67-block small grid has two tanks, nine hydrogen thrusters, an inline battery, gyro, remote, PB, six cameras, antenna, four warheads, a timer, two exterior panels and a warning lamp. The payload is saved disarmed and the timer idle.

```powershell
python tools/build_missile.py --game 'Z:\SteamLibrary\steamapps\common\SpaceEngineers'
python tools/check_missile.py --game 'Z:\SteamLibrary\steamapps\common\SpaceEngineers'
```

Compilation uses installed game assemblies. The 56 controller checks cover camera acquisition, countdown/abort, upright pivot, asymmetric thrust limits, lift priority, tilt pause and insufficient-lift rejection. Native XML loading and entity remapping verify remote, timer and exterior-panel links. Occupied-cell overlap, connectivity and intended gas-riser cells are also checked.

A 125-second numerical translation model used a 12,000 kg test mass, 480 kN main lift, 393.6 kN reverse thrust and 98.4 kN lateral axes. It began 6 m low, descending at 3 m/s, with horizontal drift; it also applied sustained lateral disturbance and a temporary downward gust. Peak height error was 6.307 m, final height error rounded to 0.000 m, and final lateral offset was 0.481 m. This model uses the actual controller output but assumes upright attitude, prescribed yaw and instantaneous thrust. It does not simulate Space Engineers' gyros, collisions, thruster response or full flight physics.

**Live flight remains untested.** These checks do not establish physical flight, fuel endurance, mount strength, conveyor flow, raycast coverage or blast/plume effects. Start with a creative TEST flight.

References: [Keen camera API](https://keensoftwarehouse.github.io/SpaceEngineersModAPI/api/Sandbox.ModAPI.Ingame.IMyCameraBlock.html), [warhead API](https://keensoftwarehouse.github.io/SpaceEngineersModAPI/api/Sandbox.ModAPI.Ingame.IMyWarhead.html).
