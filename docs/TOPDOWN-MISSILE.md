# GroundGame Topdown missile — prototype 0.1

A disposable vanilla Space Engineers missile with a vertical boost, independent enemy search, overhead approach and guided downward attack. This is powered guidance throughout, with a ballistic-style launch. It requires natural gravity and in-game scripts enabled. No mods or DLC are required.

## Install

Extract `dist/GroundGame_Topdown_Prototype.zip` into `%APPDATA%\SpaceEngineers`. This adds:

- `Blueprints\local\[GG] Topdown Missile\bp.sbc` to the local F10 blueprint list.
- `IngameScripts\local\GroundGame Topdown\Script.cs` to Browse Scripts → Local.

The blueprint already embeds the script. If replacing it, use `dist/TopdownMissile.cs`; `src/missile/Program.cs` includes an external compiler wrapper and should not be pasted whole into the PB editor.

## Prepare and launch

1. Paste `[GG] Topdown Missile` in a creative test world. Point the turret end upward, with the large hydrogen engine underneath. Put it clear of terrain and other grids. It is a dynamic small grid, not a static launch tower.
2. Take ownership of the missile and ensure the intended target grid is owned by an enemy faction. The controller rejects friendly, neutral and unowned contacts. Use an expendable enemy grid for the first trial.
3. Check hydrogen, battery charge, and the radar turret's ammunition. Creative paste stores full tanks, a charged battery and 20 ammunition magazines. Projecting and welding it in survival does not supply those resources: fill both tanks, disable Stockpile, charge the battery and manually load the turret. Keep survival construction supported until it has power and lift.
4. Select `[GG-MISSILE] Computer`, check its code and run `CHECK`. Its display should say `READY`. The remote's Forward axis must point upward within about 18 degrees. A merged static grid must be detached before CHECK/LAUNCH; the script does not operate merge blocks.
5. Run `TEST` for a payload-disarmed trial, or `LAUNCH` for the attack. **TEST still flies toward the target and can collide with it.** It verifies the flight sequence without arming or detonating the warheads; it is not a return-to-base mode.
6. `ABORT` or `STOP` disarms the payload, stops any countdown, releases thrust/gyro overrides and enables dampeners. It does not land or return the missile. Use a working antenna connection and the terminal to issue it after launch; the blueprint includes a 5 km radio antenna.

Commands are case-insensitive. The script runs autonomously after launch; it does not require GPS or a target message from a carrier. A script reload cancels the attack rather than resuming armed guidance.

## Flight sequence

| Phase | Behavior |
| --- | --- |
| Boost | Climbs vertically to about 200 m above the launch plane. |
| Search | Flies expanding circles at that altitude, using the turret and sparse camera rays to find an enemy grid. |
| Overhead | Tracks the selected entity, climbs to about 180 m above its center, and moves within 25 m horizontally. |
| Dive | Turns nose-down, waits for alignment, then commands up to 80 m/s toward a velocity-led intercept. |
| Abort | Disarms and hands braking to vanilla dampeners. |

The radar is a small-grid gatling turret. Its installed vanilla definition limits it to **600 m**. It cannot reveal every hostile on the planet or see through terrain. Cameras supplement acquisition and refresh the selected contact. The controller attacks only small or large enemy grids, and locks one entity until it is lost. It does not target characters.

Targets more than 60 m above the missile are excluded; this version is intended for ground targets and lower-flying grids. Search circles expand toward a configured 650 m radius, but the 150-second flight limit and available hydrogen bound how far a particular launch explores. Hills can block detection. Outside the terminal dive, reported terrain clearance below 35 m aborts flight; there is no obstacle-avoidance map or guaranteed clear route.

After two seconds without a matching enemy report the missile disarms and resumes searching. Arming also requires a track younger than 0.6 seconds, terminal range below 200 m, more than 100 m separation from launch, and a downward/aligned nose. Detonation uses an 8 m proximity check against the freshly tracked target's bounding box. Actual collision with an armed warhead can occur first.

## Blueprint

The 60-block small grid contains two hydrogen tanks, nine hydrogen thrusters in six directions, a battery, gyro, remote control, programmable block, gatling targeting turret, six raycast cameras, radio antenna and four warheads. Conveyor risers supply the upward-facing retro thrusters. The payload is saved disarmed with no active countdown. The script only controls blocks on its own grid whose names include `[GG-MISSILE]`.

## Development and evidence

On Windows with Space Engineers installed:

```powershell
python tools/build_missile.py --game 'Z:\SteamLibrary\steamapps\common\SpaceEngineers'
python tools/check_missile.py --game 'Z:\SteamLibrary\steamapps\common\SpaceEngineers'
```

The build checks installed block subtypes, occupied-cell overlap, hull-cell connectivity and the intended conveyor-riser cells, then compiles the controller against the installed game assemblies. The check command runs 21 controller scenarios using the actual PB body behind interface proxies, and loads the blueprint with Keen's shipped XML serializers. Native loading also verifies the embedded release source, tank fill, enemy-only radar flags and disarmed payload.

**Live flight has not been tested.** The checks do not simulate flight physics, mount strength, hydrogen flow, turret line of sight, fuel endurance or blast/plume effects. Treat this as a first flight-test prototype. Start with CHECK and a TEST run in a creative world before building a survival fleet.

API references: [turret target reports](https://keensoftwarehouse.github.io/SpaceEngineersModAPI/api/Sandbox.ModAPI.Ingame.IMyLargeTurretBase.html), [camera raycasts](https://keensoftwarehouse.github.io/SpaceEngineersModAPI/api/Sandbox.ModAPI.Ingame.IMyCameraBlock.html), [warhead control](https://keensoftwarehouse.github.io/SpaceEngineersModAPI/api/Sandbox.ModAPI.Ingame.IMyWarhead.html).
