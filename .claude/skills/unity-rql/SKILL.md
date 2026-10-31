---
name: unity-rql
description: |-
  How to read and change anything in the Unity Editor with RQL (rql-query / rql-mutate / rql-schema): the query
  language, operators, sources (scene objects, assets, scenes, prefabs, console logs, Editor state, Project Settings), Editor operations
  (create, add components, scenes, prefabs, assets, selection, Play Mode) and what installed extensions add.
metadata:
  generated-by: unity-mcp
  ai-game-dev: exclude
---
# RQL — query and change the Unity Editor

RQL is one pipeline language for the whole Editor: find objects, read members, filter, aggregate, and change them in one
call. Prefer one precise query over many exploratory calls.

- `rql-query` — read-only queries, compact table results.
- `rql-mutate` — queries ending with `set` / `call`; all-or-nothing, undoable (Ctrl+Z), `dry_run: true` previews.
- `rql-schema` — sources and component types in the scene; `rql-schema(type)` lists one type's members and methods.

```bash
unity-mcp-cli run-tool rql-query --input '{"query": "objects | where name contains \"Enemy\" | select $ref name"}'
unity-mcp-cli run-tool rql-mutate --input '{"query": "objects | where name == \"Player\" | set active = false"}'
```

## Language

```
RQL: source | stage | stage ...
Sources: objects (every GameObject in the loaded scenes, inactive included), roots, scenes, selection, prefab (the prefab open in prefab editing mode), console (Editor log entries), shaders, componentTypes, profiler (memory / rendering / frame snapshots; call Start / Stop / Save), tests (test methods; run a selection with tests-run query), testResults (last run), editor (Editor state and operations), settings (Project Settings: physics, time, quality, player, build). Assets: unity.assets("t:Material") or unity.assets("t:Prefab", "Assets/Enemies"), unity.asset("Assets/Mats/Red.mat"). ref("Light#1234") resolves a $ref; unity.find("Player") or unity.find("Level/Pillar_1") a GameObject by unique name or path.
Stages:
  where <condition>                  filter (a boolean: where sharedMaterial != null, not where sharedMaterial)
  select a b.c alias=(expr)          project columns (wrap computed expressions in parentheses)
  sort <expr> [desc], ...            stable sort (computed keys in parentheses: sort (a * b) desc)
  first N | skip N | count
  get <path>                         replace each item by a member (lists are flattened)
  recurse children [max-depth=N] [include-self=false]   walk the hierarchy (input items included by default)
  distinct [expr] | sum expr | min expr | max expr | avg expr   (distinct keeps whole items; values: get tag | distinct)
  group-by expr                      rows key|count; group-by a, b (or kind=$type) gives one column per key, then count;
                                     items = the group's items: … | where $ is Light | group-by type | select key count avg=(items.avg(intensity))
Expressions: == != < <= > >= and or not + - * / %; contains, starts-with, ends-with (case-insensitive); matches "regex"; x in [a, b]; x is null; x is not null; $ is Light (includes subclasses); has(member); c ? a : b or if(c, a, b) (only the taken branch runs; where a stage takes one expression, wrap it: select kind=($ is Light ? "light" : "other")). Literals: numbers, "strings", true/false/null, lists [a, b], objects {x: 1, y: 2}. Text functions: lower upper len, replace(s, from, to) (case-insensitive), replace-regex(s, pattern, repl) ($1 = group 1), substring(s, start[, length]), split(s, sep), trim(s), pad(v, width[, fill]) — also as methods: name.replace("Old", "New"), name.substring(0, 4).
Paths: a.b.c, list[0], map["key"]; .member on a list maps over its elements. List methods: .any(pred) .all(pred) .count([pred]) .sum(e) .avg(e) .min(e) .max(e) .first([pred]); inside them bare names refer to the element, falling back to the outer item.
Meta: $ (current item), $ref (stable id such as Light#1234), $type, $path (the item's place in this query, e.g. roots[0].children[2]), $index, $depth. Enums compare to strings (type == "Spot", or the Inspector's name: shadowCastingMode == "Two Sided"); == on strings is case-sensitive.
Unity: GameObject has components (list), children (list), active, name, path (hierarchy path "Level/Props/Crate"), tag, layer, layerName, transform (position, localPosition, eulerAngles, localScale, parent; call LookAt(target) turns it toward a GameObject or a position). Component members are the C# members (public + [SerializeField]); find them via objects | get components | where $ is Light. Vectors/colors print as (x, y, z); write "(1, 2, 3)", [1, 2, 3] or {x: 1, y: 2, z: 3}; colors also "#ff8800". An asset's file: assetPath; its import settings: get importer. Material shader properties: properties["_Color"] (the main color / texture: color, mainTexture); a Material Variant has a parent. A Mesh value also takes a primitive's name ("Cylinder"), a Material value a built-in material's ("Default-Material"). Prefab instances: prefabSource, prefabOverrides (all of the instance's on its root, an object's own on a part). Destroyed objects read as null; a GameObject's missingReferences lists references to deleted assets or objects (where missingReferences.count() > 0). Navigate with paths or get; select only projects output columns.
Results come back as compact rows (header line, then col|col). Long results are truncated — refine with where/first or use count. Unknown members produce warnings with suggestions; rql-schema(type) lists a type's members.
Language rules:
- Conditions are booleans. where, and / or / not, ?: and the predicates of .any() .all() .count() .first() take a comparison or has(), never a bare value: where material != null (or has(material)), where count > 0, where name != "", where len(tags) > 0. A number, string, object or list there fails with type-mismatch; null counts as false.
- cond ? a : b (the same as if(cond, a, b)) picks a value and evaluates only the taken branch: set y = (y < 0 ? 0 : y). Inside select, wrap computed values in parentheses: select kind = (intensity > 2 ? "bright" : "dim").
- Lists compare by value, in order: tags == ["a", "b"], x in [[1, 2], [3, 4]]; distinct and group-by merge equal lists.
- group-by gives rows with key (or one column per key), count and the group's items: group-by kind | select key, count, avg = items.avg(weight); also items.sum(e), items.min(e), items.max(e), items.count(pred), items.first().name. Group by the item's type with $type (group-by $type); a bare type is a member that most items do not have.
- Receipts list only real changes: a set row whose value stayed the same is left out and counted as unchanged, so no rows with N unchanged means the values already matched.
- call Method(args) runs the method of each item (the items must be objects). call source.Method(args) runs the method of that one source object once per stream item, with $, $index, $path and $depth in the arguments being the item's; every call counts against the target limit, and every argument is checked before the first call (a dry run checks without calling).
- Bulk create: range(start, end[, step]) streams numbers (end excluded), and a list literal streams its elements, so one call creates many objects: range(0, 150) | call editor.CreateGameObject("Cube_" + $, {primitive: "Cube", position: [($ % 15) * 2, 0, floor($ / 15) * 2]}) builds a 15-wide grid, [["Crate", [0, 0, 0]], ["Barrel", [2, 0, 0]]] | call editor.CreateGameObject($[0], {primitive: "Cube", position: $[1]}) one object per entry. The receipt has one row per call with the $ref of the created object.
- Vectors and colors the host marks as component-wise compute component by component: set Scale = Scale * 1.5, Position + [0, 1, 0], Position - Offset, -Velocity, v / 2. Adding a number to a vector is a type-mismatch (add [x, y, z] instead); quaternions and other types without that mark stay type-mismatch. Lists of numbers are vectors too: [x, 0, z] * k, [1, 2] + [3, 4]. Vector functions work on both: length(v) (or v.length(), magnitude), normalize(v), distance(a, b), dot(a, b), lerp(a, b, t); len(x) stays the size of a string or list.
- Trigonometry is in radians: sin, cos, tan, asin, acos, atan, atan2(y, x), pi(), deg(x), rad(x); trigonometric results are rounded (sin(pi()) is 0). In a set value or call argument $count is the number of items entering the stage ($index runs 0 to $count - 1), so set position = [5 * cos(2 * pi() * $index / $count), 0, 5 * sin(2 * pi() * $index / $count)] lays the items out on a circle of radius 5.
- A missing map entry reads as null: props["k"] == null and props["k"] is null are true without the entry, and has(props["k"]) tests whether it exists. A misspelt member is not null: it stays unknown and is reported.
- Arguments by name: call Method({name: value, ...}) with one object literal that no overload takes as a whole binds each key to the parameter of that name; parameters left out keep their defaults, and an unknown name fails listing the parameters: range(0, 10) | call editor.CreateGameObject({name: "Tile" + $, primitive: "Cube", position: [$ * 2, 0, 0]}). Engine objects (scene objects, nodes, actors) are never built from an object literal: pass an existing one, e.g. ref("Type#id"). Value-like assets and owned subobjects that the host marks are built from a typed literal: set mesh = {$type: "TypeName", size: 2}.

```

## Changing things (rql-mutate)

```
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
```

Agent rules: Deleting (DeleteAsset, Destroy, script-delete, or AssetDatabase / File deletes in script-execute) takes two turns unless the user named the exact items or you created them yourself in this task: first list the exact candidates (rql-query, a dry_run=true mutation or a read-only snippet), show them and end your turn; delete only that list, in a separate call, after the user confirms. Otherwise never find and delete candidates in the same turn. After project files change outside the Editor (written to disk by you, a script or the user), run editor | call RefreshAssets() before reading or changing them: it imports them and reloads an open scene whose file changed (changed .cs files recompile and the scripts reload: retry a call that fails right after). When you map a name the user misspelt to a real one (an object, component or member), say so. Unity units are meters, degrees (rotation) and seconds.


## Sources

| Source | What |
|---|---|
| `componentTypes` | component types that can be added (name fullName namespace) |
| `console` | captured Editor log entries, oldest first (last 1000): logType message timestamp stackTrace sequence |
| `editor` | Editor state (isPlaying, isPaused, selection …) and operations: CreateGameObject, OpenScene, NewScene, SaveScenes, InstantiatePrefab, OpenPrefab / SavePrefab / ClosePrefab, CopyAsset / MoveAsset / DeleteAsset / CreateFolder, CreateMaterial, SaveAssets, RefreshAssets, AddTag / AddLayer, ClearConsole, ExecuteMenuItem (rql-schema "editor" for signatures) |
| `lighting` | the active scene's lighting: environment (ambient, fog, skybox, sun), Lighting Settings asset, bake (rql-schema "lighting" for members and calls) |
| `objects` | every GameObject in the loaded scenes, depth-first, inactive ones included |
| `prefab` | root GameObject of the prefab open in prefab editing mode (none otherwise) |
| `profiler` | Unity profiler: enabled supported memory rendering script frame modules (live snapshots, MB / ms); calls Start, Stop, ClearFrames, EnableModule, Save, Load (rql-schema "profiler" for signatures) |
| `roots` | root GameObjects of the loaded scenes |
| `scenes` | loaded scenes |
| `selection` | objects selected in the Editor |
| `settings` | Project Settings pages, writable and undoable, saved to ProjectSettings/: physics (gravity, defaultContactOffset, solver iterations …), time (fixedDeltaTime, timeScale …), quality (levels, currentLevel, vSyncCount, shadows …), player (productName, companyName, bundleVersion, colorSpace …), build (scenes in the build), e.g. settings \| select physics.gravity time.fixedDeltaTime |
| `shaders` | every shader the Editor knows (built-in and project): name supported hasErrors |
| `testResults` | results of the last tests-run: name status durationMs message stackTrace |
| `tests` | test methods (fullName name fixture namespace assembly mode categories parameterized explicit ignored); run a selection with tests-run query, e.g. tests \| where fixture == "PlayerTests" |
| `unity.asset(…)` | unity.asset(path): the main asset at a path, e.g. "Assets/Mats/Red.mat" |
| `unity.assets(…)` | unity.assets(filter, folder?): AssetDatabase search, e.g. "t:Material", "t:Prefab Enemy" (at most 200, loaded as needed) |
| `unity.find(…)` | unity.find(name): the GameObject with that unique name or hierarchy path ("Level/Pillar_1"), e.g. set Follow = unity.find("Player") |

## Installed RQL modules

- `com.IvanMurzak.Unity.MCP.Editor.Rql.Modules.UnityCoreModule`
- `com.IvanMurzak.Unity.MCP.Editor.Rql.ToolTypesModule`
- `com.IvanMurzak.Unity.MCP.Editor.Rql.Mathematics.MathematicsModule`
- `com.IvanMurzak.Unity.MCP.Editor.Rql.Lighting.LightingModule`
- `com.IvanMurzak.Unity.MCP.Editor.Rql.Physics3D.PhysicsModule`


> CLI installation and usage: see the `unity-cli` skill.
> If this task needs unavailable asset-generation or Blender tools, consult `ai-game-dev-app` for an optional integrated workflow.

