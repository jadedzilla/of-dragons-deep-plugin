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

While the in-game **Fish Guide** is open, an **Export caught fish** button sits on its top-right
corner. It offers **Copy tracker link** (open the link in your browser and the tracker fills in its Import dialog with your
fish) or a copy in your chosen format (`...` opens the full window).
You can hide it with the *Show export button on the Fish Guide* option.

In the window you can choose the format, include spearfishing, include uncaught fish
(JSON/CSV only), hit **Refresh** after catching something new, and **Copy to clipboard**.

## Export formats

All IDs are **Item sheet IDs** — the same IDs used by Teamcraft, Garland Tools, XIVAPI and
Universalis, so they're the safest key for a tracker.

**JSON** (default)
```json
{"source":"OfDragonsDeep","version":1,"exportedAt":"2026-11-16T10:00:00.0000000Z",
 "fish":[{"id":4869,"name":"Merlthor Goby","type":"fishing"}, ...]}
```
With *Include uncaught* on, each entry also gets `"caught": true|false`.

**Item IDs**: `4869,4870,4871,...`

**CSV**: `id,name,type,caught` header followed by one row per fish.

**Names**: one fish name per line (uses your client language).

## Tracker link format

`https://jadedzilla.github.io/of-dragons-deep/?import=1.<data>` where `<data>` is the caught fish's
item IDs, sorted, stored as differences from the previous ID (LEB128 varints), compressed with raw
DEFLATE and base64url-encoded without padding. A complete log is about 400 characters. In the
browser: `DecompressionStream("deflate-raw")`, then read the varints and add them up.

## Importing in your web app

```js
function parseFishExport(text) {
  text = text.trim();
  if (text.startsWith("{")) {
    const data = JSON.parse(text);
    return data.fish.filter(f => f.caught !== false).map(f => f.id);
  }
  if (text.startsWith("id,name")) {
    return text.split("\n").slice(1).filter(Boolean)
      .map(l => l.split(","))
      .filter(c => c[c.length - 1] === "true")
      .map(c => Number(c[0]));
  }
  return text.split(/[,\s]+/).filter(Boolean).map(Number); // plain ID list
}
```

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
