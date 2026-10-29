# MH World Overlay

**A lightweight in-game HUD for Monster Hunter: World (PC) — monster health, capture threshold,
elemental weaknesses and a party damage meter, drawn directly inside the game.**

**No external applications.** Nothing to run alongside the game: no companion app, no second window,
no browser tab, no always-on-top desktop overlay. It is a small plugin (a ~30 KB DLL) loaded by the
game itself through [SharpPluginLoader](https://github.com/Fexty12573/SharpPluginLoader) — you just
start Monster Hunter: World from Steam as usual and the HUD is there.

![MH World Overlay](docs/screenshot.png)

*(Preview with sample data)*

## Features

- **Health of the monster you are fighting** — appears as soon as you hit a large monster and fades
  out 30 seconds after your last hit. Hit another monster and the HUD switches to it.
- **Quest target first** — when several large monsters are close together, the HUD sticks to the
  quest target: hitting an intruder (or a turf war, or a teammate's hits) won't steal the panel.
  It moves on only when the target is slain or captured, or after 30 seconds without hits.
- **Capture threshold marker** — a `/` on the health bar shows exactly where the monster becomes
  capturable (the moment the skull icon appears in game). Hidden for monsters that can't be captured.
- **Elemental weaknesses** — the game's own element icons, next to the monster's health bar.
- **Party damage meter** — one bar per hunter with name, damage (or DPS), share of the total and the
  game's weapon icon. It can be hidden entirely if you only want the monster's health.
  - **DPS** is calculated from the moment each hunter joined the quest (not the quest timer), so
    hunters who join later get comparable numbers.
  - In multiplayer it uses the per-player damage the game itself records (quest total).
  - Solo it shows the health the monster has lost (this includes your Palico and the environment).
- **Scales with your resolution** — sizes follow your screen height (1080p, 1440p, 4K, ultrawide…),
  with an extra size slider in the settings.
- **Looks like the game's HUD** — thin bars shaped like MHW's own health and stamina bars: an arrow
  tip on the monster bar (like your health), a diamond tip on the hunter bars (like stamina) and the
  closing bracket on the right.
- **Never gets in the way** — transparent, top-right corner, and it never takes mouse or keyboard
  input, so it can't block your controls.
- **Uses the game's own icons** — read at runtime from your installed copy of the game; no game files
  are shipped with this project.
- **Read-only** — it only reads game data. It never changes damage, items, quests or matchmaking.

Colour scheme: the monster bar is black, other hunters are white, you are grey. Just like the game's
health bar, the health the monster has just lost stays highlighted for a moment and then drains away.

## Settings

Press **F9** in game to open the SharpPluginLoader menu, then open **MH World Overlay**:

| Option | What it does |
|---|---|
| Show party damage meter | Untick to show only the monster's health, capture marker and weaknesses. |
| Damage meter shows | `Damage`, `DPS` or `Damage + DPS`. |
| Size | Extra scale on top of the automatic one (1.00x = default). |

Changes are applied immediately and saved to
`nativePC\plugins\CSharp\MHWorldOverlay\MHWorldOverlay.json`.

## Requirements

| What | Version | Notes |
|---|---|---|
| Monster Hunter: World (Steam, with or without Iceborne) | latest (**421810**) | On other versions the party damage meter turns itself off; everything else keeps working as long as SharpPluginLoader supports that version. |
| [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (x64) | 8.0.x | Needed by SharpPluginLoader (one-time install). |
| [SharpPluginLoader](https://github.com/Fexty12573/SharpPluginLoader/releases) | **2.0.0** | The in-game plugin loader. It is a DLL in the game folder, not a separate program. |

Tested with DirectX 11. SharpPluginLoader also supports DirectX 12, but the overlay has not been
tested there yet.

## Installation

> Close the game first. To find the game folder: in Steam, right-click Monster Hunter: World →
> **Manage** → **Browse local files**. It is usually
> `C:\Program Files (x86)\Steam\steamapps\common\Monster Hunter World`.

### 1. Install the .NET 8 Desktop Runtime

Download **.NET Desktop Runtime 8.0.x** for **Windows x64** from
https://dotnet.microsoft.com/download/dotnet/8.0 and install it. Skip this step if you already have
.NET 8 (Runtime or SDK).

To check, open Command Prompt and run `dotnet --list-runtimes`: you should see a line starting with
`Microsoft.WindowsDesktop.App 8.0.`.

### 2. Install SharpPluginLoader

1. Go to https://github.com/Fexty12573/SharpPluginLoader/releases and download
   `SharpPluginLoader-2.0.0-win32.zip`.
2. Extract **everything** in the zip into the game folder, so that you get:
   ```
   Monster Hunter World\
   ├─ MonsterHunterWorld.exe
   ├─ winmm.dll                         ← from SharpPluginLoader
   └─ nativePC\plugins\CSharp\Loader\    ← from SharpPluginLoader
   ```

### 3. Install MH World Overlay

1. Download `MHWorldOverlay-x.y.z.zip` from the [Releases](../../releases) page.
2. Extract **everything** in the zip into the game folder. The result must be:
   ```
   Monster Hunter World\
   └─ nativePC\plugins\CSharp\MHWorldOverlay\MHWorldOverlay.dll
   ```

### 4. Play

Start Monster Hunter: World from Steam as usual. The HUD appears in the top-right corner as soon as
you hit a large monster.

> **A black console window (cmd) opens with the game?** That's normal and harmless: it is
> SharpPluginLoader's log window, not a virus and not part of this overlay. To hide it, close the
> game, open `loader-config.json` in the game folder (SharpPluginLoader creates it on the first
> launch) and change `"logcmd": true` to `"logcmd": false`. Set it back to `true` only if you need
> to see errors.

## Check that it works

After starting the game at least once, open
`nativePC\plugins\CSharp\Loader\SharpPluginLoader.log` in the game folder and look for:

```
Loading plugin MHWorldOverlay
[MH World Overlay] Versione 421810: danni del gruppo attivi
```

(The second line means "version 421810: party damage enabled".)

You can also press **F9** in game: it opens the SharpPluginLoader menu (press F9 again to close it).

## Update

Close the game and extract the new release zip into the game folder, overwriting
`MHWorldOverlay.dll`.

## Uninstall

- **Overlay only**: delete the folder `nativePC\plugins\CSharp\MHWorldOverlay`.
- **SharpPluginLoader too**: delete `winmm.dll` in the game folder and the folder
  `nativePC\plugins\CSharp`.

## Troubleshooting

| Problem | Fix |
|---|---|
| The HUD doesn't show up | It only appears after you hit a **large** monster. Check the log (see above): if `Loading plugin MHWorldOverlay` is missing, the DLL is not in the right folder. |
| The log file is empty or missing | SharpPluginLoader is not installed correctly: check `winmm.dll` in the game folder and the .NET 8 Desktop Runtime. |
| Log says `versione diversa da 421810, danni del gruppo spenti` | Your game version differs from the supported one: monster health works, per-player damage doesn't (solo you still see the monster's lost health). |
| Another mod disables itself, or the game closes on startup | Some mods that hook the same game functions (for example **Seamless Co-op**) are not compatible with SharpPluginLoader. Disable the other mod. |
| A black console (cmd) window stays open while playing | It's SharpPluginLoader's log window. In `loader-config.json` (game folder) set `"logcmd": false`, then restart the game. |
| Icons are missing | Icons come from your copy of the game: verify the game files in Steam (Properties → Installed Files → Verify integrity). |

## FAQ

**Is this like HunterPie?**
It shows a focused subset of that kind of information, but it works differently: there is no
desktop application to launch and keep open. Everything runs inside the game process and is drawn by
the game's own renderer.

**Does it work online?**
Yes, the damage meter is designed for multiplayer. It only reads data and sends nothing over the
network. As with any PC mod, use it at your own risk.

**Can I move or resize it?**
Not yet — it is fixed to the top-right corner.

## Build from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```
dotnet build -c Release
```

The DLL is in `bin\Release\net8.0\MHWorldOverlay.dll`. To check the layout without going on a hunt,
there is a preview with sample data: `dotnet build -c Release -p:DefineConstants=PREVIEW`.

## Disclaimer

- Use at your own risk. The overlay only **reads** game data.
- Unofficial fan project, not affiliated with or endorsed by CAPCOM. Monster Hunter: World and its
  icons are © CAPCOM.

## Credits

- [SharpPluginLoader](https://github.com/Fexty12573/SharpPluginLoader) by Fexty12573 (MIT).
- [HunterPie](https://github.com/HunterPie/HunterPie) (Apache 2.0): monster data (capture thresholds,
  weaknesses) and memory addresses for party damage. Details in
  [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## License

[MIT](LICENSE)

---

## Italiano (in breve)

HUD dentro il gioco per **Monster Hunter: World** (PC): vita del mostro che stai combattendo con la
soglia di cattura, debolezze elementali e danni (o DPS) di ogni cacciatore con l'icona dell'arma.
Le barre ricordano quelle di vita e stamina del gioco e si adattano alla risoluzione dello schermo.
Con **F9** → MH World Overlay puoi nascondere i danni del gruppo, scegliere danni/DPS e la dimensione.
Se ci sono più grandi mostri vicini, il pannello resta sull'obiettivo della missione.
**Nessuna applicazione esterna**: è un plugin caricato dal gioco tramite SharpPluginLoader, avvii il
gioco da Steam e basta.

**Installazione:** installa il [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
(x64) → estrai [SharpPluginLoader 2.0.0](https://github.com/Fexty12573/SharpPluginLoader/releases)
nella cartella del gioco → estrai `MHWorldOverlay-x.y.z.zip` dalla pagina [Releases](../../releases)
nella cartella del gioco (risultato: `nativePC\plugins\CSharp\MHWorldOverlay\MHWorldOverlay.dll`) →
avvia il gioco. I danni del gruppo richiedono la versione 421810. Non compatibile con mod che
modificano le stesse funzioni del gioco (per esempio Seamless Co-op).

**Si apre una finestra nera (cmd) insieme al gioco?** È normale e innocua: è la finestra dei
messaggi di SharpPluginLoader, non un virus e non fa parte dell'overlay. Per toglierla chiudi il
gioco, apri `loader-config.json` nella cartella del gioco e cambia `"logcmd": true` in
`"logcmd": false`.
