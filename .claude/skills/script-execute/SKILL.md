---
name: script-execute
description: "Compiles and runs C# in the Unity Editor (Roslyn): plain statements with the last expression as the result, or a full class with a static method. See the skill body for the modes, usings/include and Unity object parameters."
metadata:
  generated-by: unity-mcp
  ai-game-dev: exclude
---

# Script / Execute

## Modes

- **Snippet** (default): plain statements. The last expression is the result (`GameObject.Find("Player").name`). Own `using` lines, local classes and functions are allowed; `await` works. Usings for System, LINQ, UnityEngine, UnityEditor and AIGD are already there (`Object` and `Random` mean UnityEngine's); an unknown type name is imported automatically when exactly one namespace has it.
- **Full code**: when `csharpCode` declares class `className` with a static method `methodName`, that method runs. Write its `using` lines (`using UnityEngine;`, `using System.Linq;` …): the default usings apply to snippets only.
- `isMethodBody=true` forces snippet mode.

Compile errors point at your own lines (`snippet.cs(2,5)`).

## Result and output

- The result is bounded: a string to 20000 characters, a collection to 1000 items; a result over 131072 characters as JSON comes back as the first 20000 characters of that JSON; errors to 20000 characters and 20 compile errors (a note says what was cut). Return a selection or a summary instead of everything.
- `Debug.Log` / `Console.WriteLine` output is not part of the result: `Debug.Log` goes to the Unity Console (read it with rql-query `console | sort sequence desc | first 10 | select logType message`).
- Code runs on the main thread with a time budget of 30 s; `timeout` (in seconds) sets another one, at most 290 s and 10 s less than the tool timeout. An endless loop or a long `await` stops with `Execution failed (Timeout)` and the Editor carries on; runaway recursion fails with `Recursion too deep`. Code blocked inside one Unity call stops only once that call returns.
- Threads and tasks the code starts (`new Thread`, `Task.Run`, `ThreadPool.QueueUserWorkItem`, `Task.Factory.StartNew`, `new Task`) belong to the run: once it ends (finished or timed out) they stop at their next loop iteration or call, and a warning in the Unity Console counts the ones still running. Join or await work you start. Not stopped: timers, `async void` code after its first `await`, work blocked in a wait or a Unity call (it stops once the call returns), and threads that Unity or other libraries start.
- A `.cs` file written in a snippet compiles once imported (`AssetDatabase.ImportAsset` / `Refresh`); the scripts reload after the call: the reload waits while a snippet runs, so its reply arrives, but the snippet cannot use the new types and must not wait for the compile (`EditorApplication.isCompiling` stays true until the reload; a snippet still running 10 s after a compile is requested is stopped). A call made while the scripts compile is not run (it would hold the reload back) and says so: retry it after a few seconds, or wait with rql-query `editor | select isCompiling`. Use the new types in a later call (retry one that fails during the reload).
- Every run starts fresh: static fields of your own types do not keep values from an earlier call, except a `static readonly` field of an immutable type (string, number, enum, read-only collection): identical code sets it once per domain, so read live Editor state in a method or property, not in such an initializer.

## Deleting

Deleting (DeleteAsset, Destroy, script-delete, or AssetDatabase / File deletes in script-execute) takes two turns unless the user named the exact items or you created them yourself in this task: first list the exact candidates (rql-query, a dry_run=true mutation or a read-only snippet), show them and end your turn; delete only that list, in a separate call, after the user confirms. Otherwise never find and delete candidates in the same turn.

## usings / include

- `usings`: extra namespaces, e.g. `["MyGame.Gameplay"]`.
- `include`: helper `.cs` files or folders (recursive) inside `<Project>/.ai-game-dev/snippets` (paths relative to it), compiled with the snippet.

## Passing Unity objects as parameters

Parameters become the snippet's variables (or the method's arguments, in order):

- `UnityEngine.GameObject` — resolves an actual GameObject from value `{"instanceID": N}`, `{"name": "..."}`, or `{"path": "..."}`.
- `UnityEngine.Component` (or any component subtype) — resolves from `{"instanceID": N}`.
- `AIGD.GameObjectRef` — passes a `GameObjectRef` POCO directly; call `goRef.FindGameObject()` to resolve it.
- `AIGD.ComponentRef` — passes a `ComponentRef` POCO.
- `AIGD.ObjectRef` — passes a base `ObjectRef` POCO.

## How to Call

```powershell
[System.IO.File]::WriteAllText((Join-Path $PWD 'args.json'), @'
{
  "csharpCode": "string_value",
  "className": "string_value",
  "methodName": "string_value",
  "parameters": "string_value",
  "isMethodBody": false,
  "usings": [],
  "include": [],
  "timeout": 0
}
'@)
unity-mcp-cli.cmd run-tool script-execute --input-file args.json
```

> CLI installation and usage: see the `unity-cli` skill.
> If this task needs unavailable asset-generation or Blender tools, consult `ai-game-dev-app` for an optional integrated workflow.

## Input

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `csharpCode` | `string` | Yes | C# code. Snippet: 'var go = GameObject.Find("Player");\ngo.transform.position'. Full: 'public class Script { public static int Main() { return 42; } }'. |
| `className` | `string` | No | Full mode: the class whose static method runs (namespace-qualified or not). |
| `methodName` | `string` | No | Full mode: the static method to run. |
| `parameters` | `string` | No | Values for the snippet's / method's parameters by name, e.g. {go: ref("GameObject#12"), n: 6}. Leave empty if there are none. RQL: SerializedMemberList |
| `isMethodBody` | `boolean` | No | Force snippet mode even when the code declares 'className'. |
| `usings` | `array` | No | Extra namespaces for snippet mode, e.g. ["MyGame.Gameplay"]. |
| `include` | `array` | No | Helper .cs files or folders (recursive) compiled with the code, relative to <Project>/.ai-game-dev/snippets. |
| `timeout` | `number` | No | Time budget in seconds (default 30, at most 290 and 10 less than the tool timeout): the code stops with Timeout once it is spent. Raise it for long work, lower it to fail fast. |

### Input JSON Schema

```json
{
  "type": "object",
  "properties": {
    "csharpCode": {
      "type": "string"
    },
    "className": {
      "type": "string"
    },
    "methodName": {
      "type": "string"
    },
    "parameters": {
      "type": "string"
    },
    "isMethodBody": {
      "type": "boolean"
    },
    "usings": {
      "type": "array",
      "items": {
        "type": "string"
      }
    },
    "include": {
      "type": "array",
      "items": {
        "type": "string"
      }
    },
    "timeout": {
      "type": "number"
    }
  },
  "required": [
    "csharpCode"
  ]
}
```

## Output

This tool does not return structured output.

