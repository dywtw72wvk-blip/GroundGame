# Cloud chat recovery — November 26, 2026

The previous cloud chat built a PAM extension for vertical ore mining, then stopped while trying to add `[Dot] Drone [PAM+]` to GroundGame. The cloud workspace could not access the user's Steam Cloud blueprint. The Windows checkout now contains that blueprint and the latest released script.

## Recovered files

| File | Source | SHA-256 |
| --- | --- | --- |
| `blueprints/[Dot] Drone [PAM+]/bp.sbc` | Steam's local cloud cache, `Blueprints/cloud/[Dot] Drone [PAM+]` | `eea8683312ea346d14ac4ceb4bb39717ee7fe2118b7a2a7fa2f4758f5f1c4afe` |
| `dist/PAM_Vertical_Ore.cs` | Latest source attachment on the [PAM Vertical Ore download page](https://chatgpt.com/space/page_f2805f9328fc819185d42710da4a3b59) | `ee4fd04636348a79fac1d70c426b994c53618f3119ea0691d3ba09ae21bf338c` |

The original `bp.sbc` is GZip-compressed XML. Its bytes and companion `thumb.png` are preserved. The script is also byte-identical to the released Page attachment. The ZIP in `dist/` is rebuilt locally from this exact script and contains `PAM Vertical Ore/Script.cs`.

## Saved ship findings

- One small grid named `[Dot] Drone [PAM+]`, with 445 blocks and six drills.
- The core programmable block, `[PB] Core [HAMD]`, contains an earlier PAM v1.3.1 script with the Vertical Ore extension. The blueprint has not been updated to the recovered release.
- The core programmable block's Custom Data is empty. Vertical mining mode therefore defaults to Off, with Any as the default target. The saved blueprint does not establish which settings were active later in a running world.
- All six drills share the same bore direction, matching the remote control's downward axis. PAM derives its bore axis from the reference drill, so the drill rotations do not create a direction ambiguity. Alignment with natural gravity still needs a live check.
- The recovered release adds ore selection in the menu and `Vertical Ore 1.1` diagnostics. It reads each drill's terrain-clearing mode back, retries a missed change, and holds the forward pass if an active bore drill cannot confirm its mode or working state.

## Verification and remaining work

Local recovery checks confirmed both hashes, valid blueprint XML, the grid name, and the script's diagnostic version marker. The script occupies 99,032 UTF-16 units with Windows line endings, below the programmable block editor's 100,000-unit limit. The ZIP was checked for integrity and exact source contents.

The cloud download page records compilation against Space Engineers assemblies, an MDK2 programmable-block API analyzer pass, and simulations of pilot/production holes, drill-mode failures, menu persistence, and classic-save behavior. Those checks were not rerun during this import; their harnesses were not recovered into this checkout.

Next, load the recovered release on the miner, configure Auto or Surveyed mode, and inspect Detailed Info while drilling. Auto intentionally collects on its first pilot hole. The blueprint alone cannot verify gravity alignment, actual drill operation, or flight behavior in a live world.
