---
name: rql-mutate
description: "Change the live Unity scene with RQL set/call: bulk edits in one all-or-nothing, undoable call, saved to disk; dry_run previews."
metadata:
  generated-by: unity-mcp
  ai-game-dev: exclude
---

# RQL / Mutate

Change the live Unity Editor scene with an RQL query whose last stage is set or call. All-or-nothing: every target and value is checked before anything is written; a failure changes nothing. Undoable: one Undo step per call (Ctrl+Z; from an agent: script-execute UnityEditor.Undo.PerformUndo(), then editor | call SaveScenes() and editor | call SaveAssets()). Changed assets, scenes and the open prefab are saved to disk automatically; an untitled scene needs SaveAs(path). dry_run=true returns the receipt ($ref, member, old, new) without changing anything. Do exactly the change requested; a read-only member fails with readonly-member instead of being approximated.
  set path = expr, ...   assign members (paths may go through structs: set transform.position.y = transform.position.y + 1)
                         values see each target: $ and $index (its position in the stream entering set / call)
  call Method(args)      invoke a method the tools expose
Examples:
  objects | where name == "Player" | set transform.position = "(0, 1, 0)"
  objects | get components | where $ is Light | set intensity = intensity * 2, color = "#ffcc88"
  objects | where tag == "Enemy" | set active = false
  objects | where name starts-with "Pillar_" | set name = name.replace("Pillar_", "Column_")   bulk rename
  objects | where name starts-with "Crate" | sort name | set transform.position.x = $index * 2   spread along x
  objects | where name starts-with "Coin" | set transform.localScale = transform.localScale * 1.5   vectors / colors compute per component (a color's alpha too)
  objects | where name starts-with "Lamp" | set transform.position = transform.position + [0, 2.5, 0]
  objects | where name starts-with "Rock" | set transform.position = [transform.position.x, 0, transform.position.z] * 2   lists of numbers are vectors
  objects | where name starts-with "Enemy" and distance(transform.position, unity.find("Player").transform.position) < 10 | set active = false   (units: meters)
  objects | where name == "Drone" | set transform.position = transform.position + normalize(transform.forward) * 3   also length(v), dot(a, b), lerp(a, b, t)
  unity.asset("Assets/Mats/Red.mat") | get properties | where Key ends-with "Color" | set Value = "#ffffff"   map entries write through
  unity.asset("Assets/Mats/Red.mat") | set properties["_Color"] = "(1, 0, 0, 1)"
  unity.asset("Assets/Mats/Red.mat") | set parent = unity.asset("Assets/Mats/Base.mat")   make it a Material Variant of Base
  unity.asset("Assets/Data/Items.asset") | get rows | where Category == "Tool" | set Price = round(Price * 1.15)   data asset (ScriptableObject list rows)
  objects | where name starts-with "Pillar" | get components | where $ is MeshFilter | set sharedMesh = "Cylinder"   built-in mesh
  objects | get components | where $ is MeshRenderer and sharedMaterial == null | set sharedMaterial = "Default-Material"   built-in material
  objects | where name == "Crate" | get components | where $ is MeshRenderer | set sharedMaterial = {$type: "Material", color: "#ff8800"}   a new material (saved with the scene)
  objects | where name == "Shop" | get components | where $ is ShopStock | set item = {$type: "ItemData", price: 3}   a new data ScriptableObject
  unity.asset("Assets/Art/hero.png") | get importer | set textureType = "Sprite", spriteImportMode = "Single"   import settings (reimported)
  objects | where name == "Spawner" | set targets = append(targets, ref("GameObject#1234"))
Create / add / remove (call; the receipt's result column holds the new object's $ref for follow-up queries):
  editor | call CreateGameObject("Props")                                   new root GameObject
  editor | call CreateGameObject("Crate", ref("GameObject#1234"), "Cube")    child of a parent, optional primitive
  range(0, 300) | call editor.CreateGameObject("Cube_" + $, {primitive: "Cube", position: [($ % 15) * 2, 0, floor($ / 15) * 2]})   with max_targets=300:
                         bulk create, one call per item: a 15x20 grid in ONE request and ONE Undo step; the receipt lists the first 50 new objects' $refs (query the rest by name)
  [["Crate", [0, 0, 0]], ["Barrel", [2, 0, 0]]] | call editor.CreateGameObject($[0], {primitive: "Cube", position: $[1]})   one per entry
                         {placement}: primitive, parent: ref(…), position / rotation (Euler degrees) / scale as [x, y, z], local to the parent
  editor | call CreateGameObject({name: "Crate", primitive: "Cube", position: [1, 0, 2], scale: [2, 2, 2]})   arguments by name: one literal, any order, the rest default
  objects | where name == "Crate" | call AddComponent("Rigidbody")           add a component by type name
  objects | where name == "Old" | call Destroy()                             remove a GameObject (components: get components | where $ is X | call Destroy())
  objects | where name == "Lamp" | set transform.parent = ref("GameObject#1234")   reparent (keeps the world position)
  objects | where name == "Crate" | call Duplicate()                          copy next to it (returns the copy)
  objects | where name == "Main Camera" | call LookAt(unity.find("Player"))   aim at a GameObject (or a position "(0, 1, 0)")
Scenes, prefabs, assets, Editor state:
  editor | call OpenScene("Assets/Scenes/Level.unity")          editor | call NewScene("Assets/Scenes/New.unity")
  scenes | where path == "" | call SaveAs("Assets/Scenes/New.unity")   scenes | where name == "Level" | call SetActive()
  editor | call InstantiatePrefab("Assets/Prefabs/Enemy.prefab", {parent: ref("GameObject#1234"), position: [0, 0, 5]})
  range(0, 10) | call editor.InstantiatePrefab("Assets/Prefabs/Coin.prefab", {position: [$ * 2, 1, 0]})   ten instances, one Undo step
  objects | where name == "Crate" | call SaveAsPrefab("Assets/Prefabs/Crate.prefab")   (on a prefab instance: a Prefab Variant)
  objects | where name == "Coin_3" | call RevertPrefabOverrides()   or ApplyPrefabOverrides() (read: select prefabSource prefabOverrides)
  editor | call OpenPrefab("Assets/Prefabs/Enemy.prefab")  then  prefab | recurse children | …  then  editor | call ClosePrefab()
  editor | call CreateMaterial("Assets/Mats/Glow.mat")        editor | call MoveAsset("Assets/A.mat", "Assets/Mats/A.mat")
  editor | call RefreshAssets()   after files changed on disk: imports them, reloads a changed open scene (keeps one with unsaved edits and says so)
  editor | set selection = [ref("GameObject#1234")]         editor | set isPlaying = true   (scripts reload: retry a call that fails right after)
  editor | call AddLayer("Pickups")  then  objects | where name starts-with "Pickup" | set layerName = "Pickups"   (read: editor | select layers tags)
  editor | call AddTag("Interactive")  then  objects | where name starts-with "Barrel" | set tag = "Interactive"
  profiler | call Start()      profiler | call Save("Logs/profile.json")      (read: profiler | select memory frame)
  settings | set physics.gravity = "(0, -12, 0)", time.fixedDeltaTime = 0.01   Project Settings (saved, undoable)
  settings | set build.scenes = append(build.scenes, "Assets/Scenes/Level2.unity")   scenes in the build
Object literals build values: set settings = {speed: 2, $type: "FastSettings"}, and so do Materials and data ScriptableObjects (into a scene object; an asset or the open prefab cannot hold a new one: create the asset and pass its path). GameObjects, components and other assets are never built from a literal: pass unity.find("Name"), ref("Type#id") or an asset path. At most 200 targets (or bulk-create items) per call; max_targets raises the limit (up to 1000) for an intended bulk change. The receipt lists the assignments that change a value; "N unchanged" counts the ones whose target already held it (written anyway). A misspelt member that empties the stream of set / call fails with unknown-member (nothing changed); a real member that no matched item has is a no-op.
Agent rules: Deleting (DeleteAsset, Destroy, script-delete, or AssetDatabase / File deletes in script-execute) takes two turns unless the user named the exact items or you created them yourself in this task: first list the exact candidates (rql-query, a dry_run=true mutation or a read-only snippet), show them and end your turn; delete only that list, in a separate call, after the user confirms. Otherwise never find and delete candidates in the same turn. After project files change outside the Editor (written to disk by you, a script or the user), run editor | call RefreshAssets() before reading or changing them: it imports them and reloads an open scene whose file changed (changed .cs files recompile and the scripts reload: retry a call that fails right after). When you map a name the user misspelt to a real one (an object, component or member), say so. Unity units are meters, degrees (rotation) and seconds.

Query part: the RQL of rql-query (full guide and the sources in its description); unity.assets("t:Material"), unity.asset(path), unity.find("Name"), ref("Light#1234"). Stages: where, select, sort, first N, get <path>, recurse children. Operators: == != < > and or not, contains, starts-with, $ is Light. Paths a.b.c, list[0], map["key"]. Vectors/colors as "(1, 2, 3)" or "#ff8800"; enums as strings.

## How to Call

```powershell
[System.IO.File]::WriteAllText((Join-Path $PWD 'args.json'), @'
{
  "query": "string_value",
  "dry_run": false,
  "max_targets": 0
}
'@)
unity-mcp-cli.cmd run-tool rql-mutate --input-file args.json
```

> CLI installation and usage: see the `unity-cli` skill.
> If this task needs unavailable asset-generation or Blender tools, consult `ai-game-dev-app` for an optional integrated workflow.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `query` | `string` | Yes | RQL query ending with a set or call stage, e.g. objects | where name == "Player" | set active = false |
| `dry_run` | `boolean` | No | true = preview the receipt without changing anything |
| `max_targets` | `integer` | No | Raise the limit of 200 targets per call for an intended bulk change (1 to 1000); omit it for the default 200 |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "query": {
      "type": "string"
    },
    "dry_run": {
      "type": "boolean"
    },
    "max_targets": {
      "type": "integer"
    }
  },
  "required": [
    "query"
  ]
}
```

## Output

This tool does not return structured output.

