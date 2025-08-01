![](logo.png)
# KinPonds for V Rising
KinPonds is a fishing enhancement mod for V Rising that allows players to transform decorative pools (Eternal Dominance DLC req) and water wells into functional fishing ponds. Create your own fishing spots and customize the fishing experience on your server.

![waterwell01](https://github.com/user-attachments/assets/79f1bf63-31ce-4763-a2ea-499ceb834aa5)
![waterwell03](https://github.com/user-attachments/assets/6c93e084-6293-4657-b1cd-c13fb8572d1b)
![Pool01](https://github.com/user-attachments/assets/57385287-b7a7-400f-971b-183939afd000)
![Pool02](https://github.com/user-attachments/assets/81474616-471d-4c51-9c5e-836455048b4c)
---

Thanks to the V Rising modding and server communities for ideas and requests!
Feel free to reach out to us on discord (odjit or zfolmt) if you have any questions or need help with the mod.

[V Rising Modding Discord](https://vrisingmods.com/discord)

## Features

- **Transform Pools into Ponds**: Convert decorative castle pools and water wells into functional fishing spots
- **Regional Fish Types**: Ponds automatically use region-appropriate fish based on their location
- **Selectable Drop Tables**: Admins can choose loot tables for ponds
- **Configurable Costs**: Set item requirements for pond creation
- **Territory Limits**: Control how many ponds can exist per territory
- **Adjustable Respawn Rates**: Configure fish respawn timing

## Commands

### Player Commands
- `.pond`
  - Converts the nearest pool into a fishing pond. Must be standing near a decorative pool or water well.
  - May require items if pond creation cost is configured by admin.
- `.pond info`
  - Shows the current pond settings including respawn time, drop table, and territory limit.


### Admin Commands
- `.pond respawn (minTime) (maxTime)`
  - Sets the minimum and maximum time between fish respawns in seconds.
- `.pond cost (item) (amount)`
  - Sets the item cost required to create a pond. Use item name or PrefabGUID. Use `clear`, set amount to 0 or specify no parameters to clear cost.
  - Example: `.pond cost Stone 10`
  - Example: `.pond cost clear`
- `.pond limit (number)`
  - Sets maximum ponds allowed per territory. Use -1 for unlimited.
- `.pond globaldrop (dropTable)`
  - Sets the global drop table used by all new ponds. Set to 0 or `clear` to return to default region-based fish.
- `.pond override (dropTable)`
  - Sets an override drop table for the pond you're looking at, not beholden to global changes. Set to `clear` to place it back on global.

## Configuration

Server admins can configure:
- **Pond Creation Cost**: Require specific items to create ponds
- **Territory Limits**: Maximum ponds per claimed territory
- **Fish Respawn Timing**: How quickly fish respawn in ponds
- **Drop Table Choice**: Override default regional fish with another loot table.
  - Drop table references: https://github.com/Odjit/KinPonds/wiki 

## Installation

1. Install [BepInEx for V Rising](https://wiki.vrisingmods.com/user/game_update.html)
2. Install [VampireCommandFramework](https://github.com/decaprime/VampireCommandFramework)
3. Place `KinPonds.dll` in your `BepInEx/plugins` folder
4. Restart your server

### Additional Notes
- If a droptable's first layer of drops does not add up to 100%, the rest is the chance of no reward.
- Updating the global droptable will effect all globally set ponds. It will not change overridden ponds.

This mod is licensed under the AGPL-3.0 license.
