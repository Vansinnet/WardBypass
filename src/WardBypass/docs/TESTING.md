# Ward Bypass: Runtime Acceptance

Status: **Runtime-pending**. General method, environments and log locations: `docs/testing.md`.

## Smallest Required Test

1. Deploy with `scripts/Deploy-Mod.ps1 -Mod WardBypass` and start the game. Confirm `Ward Bypass 0.1.0 loaded. Bypass on; F11 toggles it. Locator off; F10 toggles it.` in `BepInEx/LogOutput.log` and no Harmony errors.
2. On a dedicated server, stand inside an enabled ward placed by another player (not permitted). With the bypass on: open a door and a chest, place and remove a piece with the Hammer, flatten ground with the Hoe and dig with a pickaxe. All should work.
3. Press F11 ("Ward bypass off"). The same actions should be refused with the vanilla "private zone" message and the ward flash.
4. Press F10 ("Ward locator on: N wards nearby"). Every player ward within 100 m has a marker with owner and distance, also behind a wall; turning away pins it to the screen edge with an arrow pointing the right way; nearby enabled wards show their ground ring. Press F10 again: the markers vanish, the rings within half a second.

## Full Matrix

- Dedicated server (vanilla, no mod) with the mod on one client only; the ward owner online and offline.
- Portals, item stands, armor stands, signs, beehives, fermenters, map tables, incinerators, traps inside a foreign ward.
- Location wards (Dvergr outpost): still protected with the bypass on.
- A creature with a terrain attack (for example a troll or Gjall) fighting inside a foreign ward while your client simulates it: no terrain change.
- `ActiveOnStart = false`, rebinding the toggle key, key ignored in chat, console and menus.
- Single player and listen host: own wards unchanged.
- Locator: own (green), foreign enabled (amber) and disabled (grey-blue) wards; a ward buried in terrain or under a floor; standing inside a foreign ward pulses its marker; a ward straight behind the camera appears at the bottom edge; Ctrl+F3, menu, map and inventory hide the markers; 1080p and 1440p/4K (marker and text scale); render scale below 100 % (the pin dot stays on the stone while turning the camera); `Range`, `MaxMarkers`, `ShowRadius` and `RadiusRange` edits; no frame-time drop with 25 wards in range; walking away until wards unload removes their markers without errors.

## Observation Log

| Date | Role | Valheim / BepInEx | Plugin SHA-256 | Result | Notes |
|---|---|---|---|---|---|
| 2026-10-08 | Client (mod) on dedicated server | 1.0.17 / 5.4.23.5 | not recorded | Pass (user report) | Smallest required test reported working by the user; individual steps and the full matrix not itemized. |
| 2026-10-16 | Client (mod) on dedicated server | 1.0.20 / 5.4.23.5 | `92DB2835958489DABF26D2CCD3EA7B4E832462279C52E7C5DB6DA6DE0D8372E7` (pin markers) | Pass (user report) | Ward locator (step 4) reported working by the user. An earlier build (`372ABFB0...`) showed markers drifting while turning the camera; fixed by viewport projection and the stone-centre pin anchor. `LogOutput.log`: plugin loaded, no errors or exceptions. Full matrix not itemized. |
| 2026-10-16 | Client (role not recorded) | 1.0.20 / 5.4.23.5 | `92DB2835958489DABF26D2CCD3EA7B4E832462279C52E7C5DB6DA6DE0D8372E7` (0.1.0 release) | Pass (user report) | User reported that all three mods work fine in game. The installed build matches the release DLL. At the user's explicit request the GitHub release v0.1.0 was changed from prerelease to a normal release marked latest, although the full matrix above is not itemized; `runtimeStatus` stays `Runtime-pending`. |
