# Ward Bypass

Version **0.1.0** for Valheim **1.0.20** with BepInEx 5.

Lets you build, remove, open, use and dig inside other players' wards. Client-only: only the player who installs it is affected; servers and other players need nothing. F11 toggles it.

**Status: Runtime-pending.** Not yet observed in a live game.

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

## Installation

Install side: **client**. Put `WardBypass.dll` in `BepInEx/plugins/WardBypass/`, or install the package with a mod manager. The server and other players need nothing. On a dedicated server the mod does nothing.

## Use

Press **F11** to turn the bypass on or off. A message in the top left corner shows the new state. The key is ignored while chat, the console, a text field, the menu, the inventory or the map is open.

## Configuration

`BepInEx/config/se.junge.valheim.wardbypass.cfg`:

- `[General] Enabled = true` turns the mod on or off. Restart after editing the file.
- `[General] ActiveOnStart = true` sets whether the bypass is on when the game starts.
- `[Keys] Toggle = F11` is the toggle key.

## License

MIT. See `LICENSE`.
