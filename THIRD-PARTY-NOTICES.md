# Third-party notices

## HunterPie (Apache License 2.0)

https://github.com/HunterPie/HunterPie — Copyright HunterPie contributors.

MH World Overlay uses data derived from HunterPie, licensed under the Apache License 2.0
(full text in [`licenses/HunterPie-LICENSE.txt`](licenses/HunterPie-LICENSE.txt)):

- `MonsterData.cs`: capture thresholds and elemental weaknesses, converted from
  `HunterPie/Game/World/Data/MonsterData.xml` into a C# table (only those two fields are kept).
- `GameMemory.cs`: memory addresses and pointer chains for party damage, party members and the
  player save, taken from `HunterPie/Address/MonsterHunterWorld.421810.map`, plus the party
  member / damage table layout used by HunterPie's World integration. Reimplemented in C# for
  in-process reads.

## SharpPluginLoader (MIT)

https://github.com/Fexty12573/SharpPluginLoader — Copyright (c) Fexty12573.
Required at runtime and referenced as a NuGet package; it is not redistributed by this project.

## Monster Hunter: World

Monster Hunter: World and its icons are © CAPCOM. This project does not include or redistribute
any game files: weapon and element icons are read at runtime from the user's own copy of the game.
This project is not affiliated with or endorsed by CAPCOM.
