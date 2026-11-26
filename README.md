# GroundGame

Space Engineers mining blueprints and the PAM Vertical Ore extension.

- [PAM Vertical Ore 1.1](dist/PAM_Vertical_Ore.cs): the paste-ready release recovered from the previous cloud chat.
- [Browse Scripts archive](dist/PAM_Vertical_Ore_Local.zip): extract into `%APPDATA%\SpaceEngineers\IngameScripts\local`, then choose **Edit → Browse Scripts → Local → PAM Vertical Ore**.
- [`[Dot] Drone [PAM+]` blueprint](blueprints/%5BDot%5D%20Drone%20%5BPAM%2B%5D/): the original blueprint and thumbnail copied from Steam's local cloud cache.
- [Recovery notes and current findings](docs/RECOVERY.md).

The blueprint preserves the ship as saved, including its earlier embedded PAM script. To use the latest release, stop PAM, replace the core programmable block's code with `dist/PAM_Vertical_Ore.cs`, select **Check code**, then **Remember & Exit**.

Open **Setup mining job**, set **Vertical mode** to **Auto**, and choose **Target ore**. Auto's first hole collects normally to the configured depth so later holes can use measured ore depths to plan terrain clearing. For a known deposit, **Surveyed** mode lets you set **Ore top** in advance.

The saved core programmable block has empty Custom Data, so vertical mining defaults to **Off**. The latest release identifies itself as **Vertical Ore 1.1** in Detailed Info and reports requested drill clearing modes and confirmation failures. Live operation on this ship has not been verified.

Original [PAM by Keks](https://steamcommunity.com/sharedfiles/filedetails/?id=1553126390). The recovered extension and its setup guide are also available on the [cloud download page](https://chatgpt.com/space/page_f2805f9328fc819185d42710da4a3b59).
