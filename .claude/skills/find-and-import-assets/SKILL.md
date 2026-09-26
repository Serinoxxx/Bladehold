---
name: find-and-import-assets
description: Use when a feature needs a sound, icon, texture, model or prefab that isn't in the project — searches the local Asset Inventory database via Unity MCP, proposes candidates for Lance to approve, then imports the approved ones.
---

# Find and import assets via Asset Inventory

`Assets/AssetInventory/` (vendored, don't modify) indexes Lance's purchased packages in a local SQLite DB. Query and extract from it through Unity MCP `execute_code`. The Editor must be open (`/unity-editor-mcp`).

## Step 1: check the project first

Search before importing: `Assets/Bladehold/Audio/` (sfx by category), `Assets/Bladehold/Art/` (`Icons/`, `UI/`, `Backgrounds/`), `Assets/Synty/`, `Assets/Third Party/`. Use what's already there.

## Step 2: search the Asset Inventory DB

```csharp
string searchTerm = "footstep";   // keyword
string ext = ".wav";              // ".wav", ".png", ".prefab", ... or "" for any
var files = AssetInventory.DBAdapter.DB.Table<AssetInventory.AssetFile>()
    .Where(f => f.FileName.Contains(searchTerm) && (ext == "" || f.FileName.EndsWith(ext)))
    .Take(10).ToList();
var sb = new System.Text.StringBuilder();
foreach (var f in files)
{
    var asset = AssetInventory.DBAdapter.DB.Table<AssetInventory.Asset>().Where(a => a.Id == f.AssetId).FirstOrDefault();
    sb.AppendLine("FileID " + f.Id + " | " + f.FileName + " | " + (asset != null ? asset.DisplayName : "?") + " | " + f.Path);
}
return sb.ToString();
```

## Step 3: get approval

**Never import without Lance's explicit OK.** List the top candidates (file, format, source package, proposed destination folder) and ask which to take, or whether to search again.

## Step 4: import the approved file

`AssetInfo.CopyFrom(Asset, AssetFile)` is internal, hence the reflection:

```csharp
int fileId = 68979;                                // approved FileID
string dest = "Assets/Bladehold/Audio/Footsteps";  // target folder
var file = AssetInventory.DBAdapter.DB.Table<AssetInventory.AssetFile>().Where(f => f.Id == fileId).FirstOrDefault();
if (file == null) return "FileID not found";
var asset = AssetInventory.DBAdapter.DB.Table<AssetInventory.Asset>().Where(a => a.Id == file.AssetId).FirstOrDefault();
var info = new AssetInventory.AssetInfo(asset);
typeof(AssetInventory.AssetInfo).GetMethod("CopyFrom",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null,
        new[] { typeof(AssetInventory.Asset), typeof(AssetInventory.AssetFile) }, null)
    .Invoke(info, new object[] { asset, file });
var task = AssetInventory.AI.CopyTo(info, dest);
task.Wait();
return "Imported to " + task.Result;
```

## Step 5: wire and verify

1. `refresh_unity`, then `read_console`.
2. Wire it the house way: audio and VFX go on an `MMF_Player` feedback on the prefab (`/feel-integration`), never `AudioSource.PlayOneShot` from code. Icons: Sprite (2D and UI) import, then `/generate-sprite-variants` step 3 for draft cards. Models: an authored prefab, never assembled in code.
3. Anything left for the Editor goes in the plan's `plans/editor/` checklist (`/editor-wiring-todo`). New assets and their `.meta` files belong in the commit.
