# Ward Bypass

Version **0.1.0** for Valheim **1.0.20** with BepInEx 5.

Lets you build, remove, open, use and dig inside other players' wards. Client-only: only the player who installs it is affected; servers and other players need nothing. F11 toggles it; F10 shows a ward locator that marks nearby wards through walls.

**Status: Runtime-pending.** The bypass and the ward locator have been tried on a dedicated server, but the full test matrix (single player, listen host, every ward-protected object, location wards, creature terrain attacks) has not been observed yet.

**Download:** [GitHub releases](https://github.com/Vansinnet/WardBypass/releases). Take the newest release (marked *Pre-release* while the mod is Runtime-pending), download `WardBypass-<version>.zip` under *Assets* and extract it into `BepInEx/plugins/WardBypass/`.

## What It Does

Valheim decides on your own computer whether a ward lets you act at a spot. While the bypass is on, every ward built by a player counts as if you were on its permitted list:

- build, repair and remove pieces with the Hammer, and use the Hoe and Cultivator;
- open doors and chests, use portals, item stands, armor stands, signs, beehives, fermenters, map tables, incinerators and traps;
- dig terrain with a pickaxe.

Unchanged:

- Wards that belong to world locations rather than players (for example Dvergr outposts) still apply.
- Creatures simulated by your computer cannot reshape terrain inside wards.
- You cannot switch someone else's ward on or off or change its permitted list; that check runs on the computer that owns the ward.
- Hitting a structure inside a ward still makes the ward flash for everyone nearby, as in vanilla.
- The server and other players see your actions as ordinary play.

## Ward Locator

Press **F10** to find wards around you, including ones hidden behind walls, under floors or buried in terrain. Every player-built ward within 100 m gets a marker drawn on top of the world:

- a pin: a bright dot on the exact centre of the ward stone, with the ward's icon in a glowing ring just above it and the owner's name and distance below;
- **amber** for an enabled ward of another player, **green** for your own, **grey-blue** for a disabled ward;
- a marker that slowly pulses when you stand inside that ward's protected area, so the ward that blocks you is easy to pick out;
- wards outside the view are pinned to the edge of the screen with an arrow pointing towards them and their distance;
- the vanilla ring on the ground that shows the protected area of each enabled ward within 50 m.

Nearer markers are larger and brighter. The markers hide while the HUD is hidden (Ctrl+F3) and while the menu, map or inventory is open. The locator only reads what your game already knows: it changes no ward and sends nothing. Your game only loads the world around you, so wards beyond the loaded area (roughly 100-150 m) appear once you come closer. Wards of world locations (Dvergr outposts) are not shown.

## Installation

Install side: **client**. Put `WardBypass.dll` in `BepInEx/plugins/WardBypass/`, or install the package with a mod manager. The server and other players need nothing. On a dedicated server the mod does nothing.

## Use

Press **F11** to turn the bypass on or off and **F10** to show or hide the ward locator. A message in the top left corner shows the new state. The keys are ignored while chat, the console, a text field, the menu, the inventory or the map is open.

## Configuration

`BepInEx/config/se.junge.valheim.wardbypass.cfg`:

- `[General] Enabled = true` turns the mod on or off. Restart after editing the file.
- `[General] ActiveOnStart = true` sets whether the bypass is on when the game starts.
- `[Keys] Toggle = F11` is the bypass toggle key.
- `[Keys] Locator = F10` shows or hides the ward locator.
- `[Locator] ActiveOnStart = false` sets whether the locator is shown when the game starts.
- `[Locator] Range = 100` is how far away wards are marked, in meters (10-300).
- `[Locator] MaxMarkers = 25` is the most wards marked at once, nearest first.
- `[Locator] ShowRadius = true` also shows the protected area ring on the ground.
- `[Locator] RadiusRange = 50` is how far away a ward's ground ring is shown, in meters.

## License

MIT. See `LICENSE`.
