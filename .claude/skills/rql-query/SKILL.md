---
name: rql-query
description: "Query the live Unity scene (GameObjects, components, assets) with RQL in ONE call: filter, project, sort, count, walk hierarchies. Far fewer calls/tokens than reading objects one by one."
metadata:
  generated-by: unity-mcp
  ai-game-dev: exclude
---

# RQL / Query

Run a read-only RQL query against the live Unity Editor scene and return compact rows. One precise query beats many exploratory calls. Changes need rql-mutate.

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


Examples:
  objects | where name contains "Enemy" and active | select $ref name transform.position
  objects | get components | where $ is Light and intensity > 2 | select gameObject.name intensity color
  roots | recurse children | where components.count() == 1 | select $path name
  unity.assets("t:Material") | select name shader.name properties["_Color"]
  console | where logType == "Error" | sort sequence desc | first 5 | select message
  scenes | select name path isDirty rootCount
  componentTypes | where name contains "Collider" | select name
  editor | select isPlaying isPaused selection
  settings | select physics.gravity time.fixedDeltaTime quality.levels

## How to Call

```powershell
[System.IO.File]::WriteAllText((Join-Path $PWD 'args.json'), @'
{
  "query": "string_value"
}
'@)
unity-mcp-cli.cmd run-tool rql-query --input-file args.json
```

> CLI installation and usage: see the `unity-cli` skill.
> If this task needs unavailable asset-generation or Blender tools, consult `ai-game-dev-app` for an optional integrated workflow.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `query` | `string` | Yes | RQL query, e.g. objects | where $ is ... | select $ref name |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "query": {
      "type": "string"
    }
  },
  "required": [
    "query"
  ]
}
```

## Output

This tool does not return structured output.

