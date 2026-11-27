# GroundGame Topdown missile — prototype 0.2

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
4. In the terminal select **[GG-MISSILE] Remote** and press **Control**. The remote's name subsequently shows the flight phase and target. Select keyboard toolbar **page 1**. Press **1 — CHECK**. Open the terminal, select **[GG-MISSILE] Computer**, and read its **Detailed Info** for `READY` or the exact fault. Its built-in screen also shows this readout. The remote's Forward axis must point upward within about 18 degrees. A merged static grid must be detached before CHECK/LAUNCH; the script does not operate merge blocks.
5. Press **2 — SCAN** to watch for enemies without launching. The remote name shows `SCAN | target name` on acquisition or `SCAN | NO LOCK` without a fresh contact. Computer Detailed Info shows **ENEMY: name | distance** or **NO ENEMY LOCK**, plus the turret's contact relationship. SCAN never launches or arms the missile. If no enemy is visible before launch, LAUNCH can still take off and search on its own.
6. Press **3 — TEST** for a payload-disarmed trial, or **4 — LAUNCH** for the attack. Launch starts the sequence automatically: **Boost → Search → Overhead → Dive**. No manual crosshair lock or GPS is required. **TEST still flies toward the target and can collide with it.** It is not a return-to-base mode.
7. Press **5 — ABORT** (or **9 — ABORT**) to disarm, stop countdowns, release thrust/gyro overrides and enable dampeners. This does not land or return the missile. Maintain an antenna connection and reconnect to the remote to use its toolbar after launch; the blueprint includes a 5 km radio antenna. The terminal can also run `ABORT` on Computer.

## Remote toolbar

All buttons are prewired, labeled and saved in the blueprint. Select keyboard toolbar page 1 (default: **Ctrl+1**) before using these keys.

| Page 1 key | Label | Action |
| --- | --- | --- |
| 1 | CHECK | Check launch readiness; report faults on Computer. |
| 2 | SCAN | Passive enemy acquisition and contact display; no launch. |
| 3 | TEST | Launch and fly the attack with warheads disarmed. |
| 4 | LAUNCH | Launch the live warhead attack. |
| 5 | ABORT | Disarm and brake with dampeners. |
| 6 | NOSE | View the nose camera. |
| 7 | DOWN | View the downward camera. |
| 8 | STATUS | Refresh Computer Detailed Info without changing flight state. |
| 9 | ABORT | Duplicate abort button. |

Gamepad bank 1: **Up CHECK · Left SCAN · Right TEST · Down ABORT**. Bank 2: **Up LAUNCH · Left nose camera · Right down camera · Down ABORT**. The same legend is saved in the remote's Custom Data and Computer's output. Camera views are for observation; looking at a ship through them does not select it as the attack target.

## Getting a target to acquire

- Use a powered, visible small or large grid owned by an enemy faction, such as an exposed hostile pirate grid. An unowned test grid or another ship you own will be rejected. Set ownership/faction relations for a creative test; merely painting or naming a grid "enemy" does not make it hostile.
- Put the enemy about **300–500 m horizontally from the launch point**, near ground level, with unobstructed space around and above it. This keeps it within the turret's 600 m slant range after the 200 m climb. The missile may need to move laterally to bring a target out of the hull/turret's blind area; a ground target directly underneath is a poor first acquisition test.
- Press SCAN and check the displayed relationship and enemy name. `Friends`, `Neutral`, or `NoOwnership` cannot become an attack lock. If there is no turret contact, check range, power, ammunition, faction relations and line of sight. Do not manually take control of the radar turret during autonomous acquisition.
- LAUNCH clears the prelaunch contact and reacquires automatically. It selects an eligible visible enemy; this version has no operator selection among multiple enemies. Test with one intended enemy grid first. It keeps that entity while reports stay fresh, moves above it, then attacks downward.
- `Search / NO LOCK` means no fresh eligible target. `Overhead / target name` means an enemy is being tracked for the top-down approach. `Dive / target name` means the terminal attack has begun. The Computer readout shows track age, range, phase and the launch button legend.

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

The 68-block small grid contains two hydrogen tanks, nine hydrogen thrusters in six directions, a battery, gyro, remote control, programmable block, gatling targeting turret, six raycast cameras, radio antenna and four warheads. The battery sits inline between the upper tank and the nose deck, entirely inside the tanks' 5 × 5 cell cross-section. The nose is one meter taller, with no rear battery pod. Extended conveyor risers supply the upward-facing retro thrusters. The payload is saved disarmed with no active countdown. The script only controls blocks on its own grid whose names include `[GG-MISSILE]`.

After installing this update, refresh F10 and paste a **new** copy. Updating the library does not move the battery or change toolbars on a missile already pasted into a world.

## Development and evidence

On Windows with Space Engineers installed:

```powershell
python tools/build_missile.py --game 'Z:\SteamLibrary\steamapps\common\SpaceEngineers'
python tools/check_missile.py --game 'Z:\SteamLibrary\steamapps\common\SpaceEngineers'
```

The build checks installed block subtypes, occupied-cell overlap, hull-cell connectivity and the intended conveyor-riser cells, then compiles the controller against the installed game assemblies. The check command runs 28 controller scenarios using the actual PB body behind interface proxies, and loads the blueprint with Keen's shipped XML serializers. Native loading verifies the embedded release source, inline battery position, tank fill, enemy-only radar flags, disarmed payload, toolbar labels and command parameters. Toolbar entity links are checked again after Keen's native blueprint remapping, which simulates how those links survive pasting a copy.

**Live flight has not been tested.** The checks do not simulate flight physics, mount strength, hydrogen flow, turret line of sight, fuel endurance or blast/plume effects. Treat this as a first flight-test prototype. Start with CHECK and a TEST run in a creative world before building a survival fleet.

API references: [turret target reports](https://keensoftwarehouse.github.io/SpaceEngineersModAPI/api/Sandbox.ModAPI.Ingame.IMyLargeTurretBase.html), [camera raycasts](https://keensoftwarehouse.github.io/SpaceEngineersModAPI/api/Sandbox.ModAPI.Ingame.IMyCameraBlock.html), [warhead control](https://keensoftwarehouse.github.io/SpaceEngineersModAPI/api/Sandbox.ModAPI.Ingame.IMyWarhead.html).
