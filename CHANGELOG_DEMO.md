# CHANGELOG — One-Day Demo Loop

Branch: `Scene_Layout`
Original files (backups) live in `Backup/<date>-<task>/` — **outside `Assets/`** so Unity never imports them.

---

## 2026-10-11 — MCP config: switched `unity` to remote

**File:** `C:\Users\PC\.config\opencode\opencode.jsonc` (outside the project)
**Backup:** `C:\Users\PC\.config\opencode\opencode.jsonc.bak`
**SHA256 of original:** `1EDA63DAFE834A19BAE0277E46DD9A6BE595C698E432CC0181B58450AC0B0F93`

### Why

opencode was configured to spawn `gamedev-mcp-server.exe --port=8080` itself, but the Unity Editor
spawns its own instance from
`<project>\Library\mcp-server\win-x64\gamedev-mcp-server.exe`
and already holds port 8080, so the opencode instance failed to bind and crashed:

```
ERROR | Failed to bind to address http://127.0.0.1:8080: address already in use
ERROR | MCP error -32001: Request timed out
```

Result: no Unity tools in the session (only blender was visible).

### What changed

| Before | After |
|---|---|
| `"ai-game-developer": "enabled": true` | `"enabled": false` (kept as documentation for other machines) |
| `"ai-game-developer": "timeout": 10000` | `"timeout": 60000` |
| (absent) | added `"unity"` → `"type": "remote"`, `"url": "http://localhost:8080/mcp"`, `"timeout": 60000` |

The remaining `10000 → 60000`: opencode dropped the `tools/list` call at 10078ms
(log: `method 'tools/list' request handler failed in 10078.8721ms`).

### Verified

MCP `tools/list` through `http://localhost:8080/mcp` returned **38 tools**
including `script-execute`, `console-get-logs`, `screenshot-isolated`, `tests-run`,
`scene-open`, `scene-save`, `gameobject-*`.
`scene-list-opened` answered `Assets/Scenes/GamePlay.unity` correctly.

---

## 2026-10-11 — Demo loop implementation

### Phase 0 — Backups

| Original | Backup | SHA256 of backup |
|---|---|---|
| `Assets/Scenes/GamePlay.unity` | `Backup/2026-10-11-demo/GamePlay.unity.bak` | `F1E19D2A7DA41D7B754AA808BD75B840DA1048FBACE439C80AD6F823D01495ED` |
| `ProjectSettings/EditorBuildSettings.asset` | `Backup/2026-10-11-demo/EditorBuildSettings.asset.bak` | `E96724ADFB395173C781FE530D650E59CE57E5DE7B35AD25155D8554B69AF35E` |

### Phase 1 — `Assets/Scripts/Manager/DemoLoopBootstrap.cs` (new, + `.meta`)

Script GUID: `9f1b382d37173d4282862785730b0fc1`

Why it exists: the scene could not finish a day through the UI at all. The script repairs
everything at runtime instead of hand-editing the `Canvas.prefab` instance (which would break
prefab overrides).

| Problem found in `Assets/Scenes/GamePlay.unity` | Fix in `DemoLoopBootstrap` |
|---|---|
| No `EventSystem` in the scene → every UI button was unclickable | `EnsureEventSystem()` creates `EventSystem` + `StandaloneInputModule` in `Awake` |
| Stamp panel `Canvas/Image` is inactive and its buttons point at a deleted handler | `FixStampButtons()` rewrites `onClick` → `StampUIController.OnClickApprove` / `OnClickReject`, relabels to `APPROVE` / `REJECT` |
| Stamp panel is 100×100 and its two buttons overlap each other | `FixStampLayout()` resizes the panel to 460×190, places the buttons at `(±115, -46)` sized 200×60, adds an `INSPECT ITEM` title |
| `Reject (1)` (the END DAY button) was deleted from the scene (`m_RemovedGameObjects`), and the leftover `Canvas/Reject` is a bare TMP label with no `Button` | `BuildEndDayButton()` hides the orphan label and creates a real `END DAY` button anchored top-right at `(-40, -40)`, sized 230×60 |
| No HUD: day number and quotas were invisible | `BuildHud()` creates a top-left panel showing `DAY n`, `OPEN QUOTA r/t`, `SPAWN QUOTA r/t` |
| `GameDayManager.OnDayEndFailed` had zero subscribers, so a blocked END DAY did nothing | `BuildToast()` + `HandleDayEndFailed()` show the reason at the bottom of the screen for 5 seconds |
| No end-of-day screen | `HandleDayEnded()` → `ShowSummary()` shows `DAY n COMPLETE`, `SCORE`, `REJECTED`, and `PLAY AGAIN` / `QUIT` buttons |
| Cursor stayed locked after the day ended, so the summary was unusable | `FreezePlayer()` unlocks the cursor, closes the stamp menu, disables `GameDayManager`, `PlayerController`, `PlayerPickupSystem`, `PlayerInteractor`, `InspectionTablet`, `UVFlashlightController`, `CrosshairUI`, zeroes all rigidbody velocities, and hides the crosshair and prompt text |

Also fixed after the first compile pass: all `Debug.Log` / `Debug.LogWarning` strings were
rewritten from Thai to English so no non-English text ships in the project code.

### Phase 2 — `Assets/Scenes/GamePlay.unity` (2 edits)

| Before | After |
|---|---|
| `==GameManager==` component list: `Transform`, `GameDayManager`, `ScoreManager`, `ScoreUI`, `StampUIController` | added `- component: {fileID: 9900000001}` |
| no block with anchor `&9900000001` | added `--- !u!114 &9900000001` MonoBehaviour referencing script GUID `9f1b382d37173d4282862785730b0fc1` with `demoDayLimit: 1` |

Verified in the editor after `AssetDatabase` refresh: `==GameManager==` now reports
`DemoLoopBootstrap (isEnabled: True)`, and the scene reports `IsDirty: false`, `RootCount: 30`.

### Phase 3 — `ProjectSettings/EditorBuildSettings.asset`

| Before | After |
|---|---|
| `m_Scenes` = only `Assets/Scenes/Script Scrence.unity` (guid `99c9720ab356a0642a771bea13969a05`) | `Assets/Scenes/GamePlay.unity` (guid `b4e5101348387664d8ec51d39cebc4f0`) added as **index 0**, `Script Scrence` moved to index 1 |

Why: the game could not be launched from a build or from `SceneManager.LoadScene` at all.

---

## Remaining

| Phase | File | Status |
|---|---|---|
| 5a | live test inside Play Mode through Unity MCP | pending |
| 5b | `Assets/Tests/PlayMode/DemoDayLoopTests.cs` (+ asmdef) | pending |

> Every edit to an existing file is recorded above as **before → after → why**, with a link to
> the matching `.bak`.
