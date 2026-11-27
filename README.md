# GroundGame

## [Dot] Drone [PAM+] · vertical mining

The drone's main remote runs **PAM Vertical Ore 1.1** on `[PB] Core [HAMD]`. The blueprint starts in **Auto** with **Any** ore selected. Its six drills point down when the remote is level. The external PAM LCD shows the menu, and the core's built-in screen and terminal **Detailed Info** show drill diagnostics.

[Download the wired drone for the local blueprint library](dist/Dot_Drone_PAM_Vertical.zip?raw=1) · [Blueprint source and thumbnail](blueprints/%5BDot%5D%20Drone%20%5BPAM%2B%5D/)

Extract the folder inside the ZIP into `%APPDATA%\SpaceEngineers\Blueprints\local\`. Open **F10 → Blueprints**, choose the local **[Dot] Drone [PAM+]**, and paste it in a world with in-game scripts enabled. The script and controls are already aboard.

### First flight

1. Take control of **Remote Control [HAMD] [PAM]**. The blueprint carries the original ship's saved dock and path. For a newly pasted ship, dock at your connector and press **toolbar page 2, key 2** (`PATHHOME`). Wait until the PAM LCD shows **Home: Was set!**.
2. Undock and fly the route you want PAM to use. Hover over the first hole and use **page 1** keys **2 / 3 / 4** for **Up / Down / Apply**. Select **Stop path recording** and press **Apply**.
3. Open **Setup mining job**. Choose the job width, height, maximum **Depth**, and **Target ore**. Press **page 2, key 3** (`ALIGN`) and wait for alignment in natural gravity. Press **page 2, key 4** (`START`) to start a fresh job at that position.
4. In **Auto**, the first hole collects to the configured depth. If it finds the selected ore deep enough, a nearby hole can clear the overburden, collect through the seam, then advance after sustained waste. On the core screen, **Clear request ON | ON 6/6** means all six drills report terrain clearing. If you already know the seam depth, choose **Surveyed** and set **Ore top** before starting.

### Remote controls

Keyboard page 1 keeps the cameras, PAM menu, ejectors, batteries, and connector. Page 2 key 1 keeps the original AI sensor toggle. On gamepad, use the main remote's ship toolbar. Its original menu buttons are on bank 1, with **Left** for Up, **Right** for Down, and **Down** for Apply.

| Keyboard page 2 | Gamepad | PB run argument | Action |
| --- | --- | --- | --- |
| 2 | Bank 4 ↑ | `PATHHOME` | Record a home connector and flight path |
| 3 | Bank 4 ← | `ALIGN` | Align the drone to planetary gravity |
| 4 | Bank 4 → | `START` | Start a fresh job here |
| 5 | Bank 4 ↓ | `STOP` | Stop PAM and pause the job |
| 6 | Bank 5 ↑ | `CONT` | Resume the saved job |
| 7 | Bank 5 ← | `PROBE NEXT` | Make the next new hole collect to full depth |
| 8 | Bank 5 → | `NEXT` | Skip to the next hole |
| 9 | Bank 5 ↓ | `HOMEPOS` | Return to the recorded home |

Both stone-only sorters carry `[PAM] [PAM-Stone]` so PAM can control ejection while sampling. The ore detector carries `[PAM-Ore]` for worlds using the optional Ore Detector Raycast mod. Auto works with vanilla drills.

### Updating a drone already in your world

To keep your existing ship, stop PAM. Load the release into **[PB] Core [HAMD]** with **Edit → Browse Scripts → Local → PAM Vertical Ore**, then **Check code → Remember & Exit**. Set **Vertical mode** to **Auto** in the PAM job menu and choose **Target ore**. In the main remote's toolbar configuration, assign the core block's **Run** action with the exact arguments in the table. For matching sorter behavior, append `[PAM] [PAM-Stone]` to the two Stone-only sorters and enable **Toggle sorters** in PAM.

[Browse Scripts archive](dist/PAM_Vertical_Ore_Local.zip?raw=1) · [Paste-ready script](dist/PAM_Vertical_Ore.cs) · [Original PAM by Keks](https://steamcommunity.com/sharedfiles/filedetails/?id=1553126390)

To install the Browse Scripts archive, extract it into `%APPDATA%\SpaceEngineers\IngameScripts\local\`. The original import details are in the [recovery notes](docs/RECOVERY.md).
