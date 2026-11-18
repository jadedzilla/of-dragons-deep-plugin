# Of Dragons Deep (Dalamud plugin)

Exports the fish you have caught to the Of Dragons Deep website.

It reads your **Fishing Log**, **Spearfishing Log**, and **Achievements** straight from game memory and
exports the to a URL or JSON to import on the Of Dragons Deep website.

## Installing

1. In game, type `/xlsettings` and open the **Experimental** tab.
2. Under **Custom Plugin Repositories**, paste
   `https://raw.githubusercontent.com/jadedzilla/of-dragons-deep-plugin/main/repo.json`,
   click **+**, tick **Enabled**, and save.
3. Open `/xlplugins`, search for **Of Dragons Deep** and install it.

## Usage

| Command | What it does |
|---|---|
| `/dragonsdeep` | Opens the window (also available from the plugin installer's "Open" button) |

While the in-game **Fish Guide** is open, an **Export** button sits on its top-right corner (`...` opens
the plugin window; hide the button with *Show export button on the Fish Guide*). The plugin window
(`/dragonsdeep`) shows your fishing log and fishing achievement totals and has the same **Export** button.

Export opens a small window with a progress bar while it collects **everything at once**: your caught
fish (fishing log, plus spearfishing unless you untick it) and the game's *Fisher* achievements (133,
including ocean fishing) with progress for unfinished ones. When it's done, three buttons appear:

- **Copy link**: a link to the [tracker](https://ofdragonsdeep.jadedzilla.com/) that opens its
  Import dialog with your fish and achievements filled in.
- **Open link**: opens that link in your browser straight away.
- **Copy JSON**: the same data as one JSON string, to paste into the tracker's Import dialog or anywhere else.

## Building

Requirements: .NET 10 SDK and XIVLauncher with Dalamud installed (the build references
your local Dalamud install, `%AppData%\XIVLauncher\addon\Hooks\dev`).

```
dotnet build -c Release
```

Then in game: `/xlsettings` → **Experimental** → add the full path to
`OfDragonsDeep/bin/Release/OfDragonsDeep.dll` under *Dev Plugin Locations*, save, and enable
it from `/xlplugins` → **Dev Tools → Installed Dev Plugins**.

The data is only read when you open the window or press Refresh. Nothing is sent anywhere.
