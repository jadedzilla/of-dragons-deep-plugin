# Of Dragons Deep (Dalamud plugin)

Exports the fish you have caught to the Of Dragons Deep website.

It reads your **Fishing Log** and **Spearfishing Log** straight from game memory and
exports every fish you've caught as text you can copy and paste into a web tracker.

## Installing

1. In game, type `/xlsettings` and open the **Experimental** tab.
2. Under **Custom Plugin Repositories**, paste
   `https://raw.githubusercontent.com/jadedzilla/of-dragons-deep-plugin/main/repo.json`,
   click **+**, tick **Enabled**, and save.
3. Open `/xlplugins`, search for **Of Dragons Deep** and install it.

## Releasing a new version

Actions tab → **Release** → *Run workflow*, enter a version higher than the last one (e.g. `1.0.1`)
and optionally a changelog. The workflow builds the plugin, publishes a GitHub release with
`latest.zip` and updates `repo.json`, so players get the update from the plugin installer.

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

- **Copy link**: a link to the [tracker](https://jadedzilla.github.io/of-dragons-deep/) that opens its
  Import dialog with your fish and achievements filled in.
- **Open link**: opens that link in your browser straight away.
- **Copy JSON**: the same data as one JSON string, to paste into the tracker's Import dialog or anywhere else.

Achievement progress (e.g. `312/500`) is asked from the server one achievement at a time, so the first
export takes a few seconds; it's reused for 10 minutes. Untick *Include achievement progress* to skip it.

## JSON format

```json
{"source":"OfDragonsDeep","version":2,"exportedAt":"2026-11-17T10:00:00.0000000Z",
 "fish":[4776,4869,4870],
 "achievements":[{"id":259,"complete":true},{"id":265,"complete":false,"current":12,"max":100}]}
```

`fish` are the caught fish's **Item sheet IDs** (the IDs Teamcraft, Garland Tools, XIVAPI and Universalis use).
`achievements` lists every Fisher achievement by Achievement sheet ID; `current`/`max` appear for unfinished
ones when progress is included. `achievements` is left out if they couldn't be loaded.

## Tracker link format

`https://jadedzilla.github.io/of-dragons-deep/?import=2.<data>`: `<data>` is base64url (no padding) of raw
DEFLATE-compressed LEB128 varints:

1. the number of caught fish, then their sorted item IDs as differences from the previous ID;
2. if achievements are included: their number, then per achievement (sorted by ID) the ID difference and a
   status: `0` not complete, `1` complete, `2` not complete followed by `current` and `max`.

A complete log with all achievements is a few hundred characters. In the browser:
`DecompressionStream("deflate-raw")`, then read the varints in order.

## Building

Requirements: .NET 10 SDK and XIVLauncher with Dalamud installed (the build references
your local Dalamud install, `%AppData%\XIVLauncher\addon\Hooks\dev`).

```
dotnet build -c Release
```

Then in game: `/xlsettings` → **Experimental** → add the full path to
`OfDragonsDeep/bin/Release/OfDragonsDeep.dll` under *Dev Plugin Locations*, save, and enable
it from `/xlplugins` → **Dev Tools → Installed Dev Plugins**.

## How it works

- `PlayerState.IsFishCaught(FishParameterId)` for every `FishParameter` row with `IsInLog`.
- `PlayerState.IsSpearfishCaught(SpearfishingItemId)` for `SpearfishingItem` rows 20001–29999.
- Fish rows are mapped to their `Item` row for the exported ID and name.

The data is only read when you open the window or press Refresh. Nothing is sent anywhere.
