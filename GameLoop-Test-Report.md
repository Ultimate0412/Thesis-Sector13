# Game Loop Test Report

Date: 2026-11-27  
Scene: `Assets/Scenes/GamePlay.unity`  
Method: Play Mode automation via Unity MCP (no manual input), plus two independent code reviewers.

## Result

The normal loop **can be completed** after the final-day fix:

`Day 1 → Day 2 → Day 3 → FINISH RUN → Main Menu`

The blocker was that Day 3 had no Day 4 `DayConfig`, so the normal End Day flow failed before showing a summary. The game now treats the last configured day as a final day: it charges the station fee, shows the final summary, and lets the player finish the run without creating a fake Day 4.

## Full 3-Day Run

Two runs were performed: one starting directly in GamePlay, and one from the real Main Menu flow.

### From Main Menu (real player flow)

`MainMenu → New Game → Slot 1 → GamePlay → Day 1 → Day 2 → Day 3 → FINISH`.

| Day | Boxes processed | Shipped / required | Score before fee | Station fee | Score after fee | Result |
|---|---:|---:|---:|---:|---:|---|
| 1 | 5 | 5 / 0 | 400 | 0 | 400 | Day 2 |
| 2 | 10 | 10 / 6 | 1,200 | 400 | 800 | Day 3 |
| 3 | 10 | 10 / 8 | 1,600 | 600 | **1,000** | **RUN COMPLETE** |

Test slot 1 was deleted afterward; the existing `slot_3.json` was preserved.

### Direct GamePlay smoke run

Test-only settings for the fast full-loop run: thief disabled and auto-save disabled. The thief was tested separately with normal settings.

| Day | Boxes processed | Shipped / required | Score before fee | Station fee | Score after fee | Result |
|---|---:|---:|---:|---:|---:|---|
| 1 | 6 (5 spawned + 1 scene box) | 6 / 0 | 500 | 0 | 500 | Day 2 |
| 2 | 10 | 10 / 6 | 1350 | 400 | 950 | Day 3 |
| 3 | 10 | 10 / 8 | 1700 | 600 | 1100 | **RUN COMPLETE** |

Final state: `currentDay = 3`, `IsRunComplete = true`, score `1100`. No Day 4 is created.

## Independent AI Playtests (blind, separate save slots)

Two independent agents were each told only “start from the Main Menu and play as far as you can”; no controls, mechanics, or prior reports were given. They ran sequentially on separate save slots.

| | Agent A (slot 1) | Agent B (slot 2) |
|---|---|---|
| Start | Main Menu → New Game → Slot 1 | Main Menu → New Game → Slot 2 |
| Day 1 | 5 spawned, 3 opened, 4 shipped, 1 stolen → 250 | 5 spawned, 3 opened, 4 shipped, 1 stolen → 250 |
| Day 2 | 9 shipped / 6, 1 stolen → 600 total | 9 shipped / 6 → 1,650 total* |
| Day 3 | 9 shipped / 8, 1 stolen → 750 total | 9 shipped / 8 → 2,650 total* |
| Final | RUN COMPLETE → Main Menu | RUN COMPLETE → Main Menu |
| Enemies | Thief once/day, 3 packages stolen, no mimic/death | Thief once, Mimic killed player, Respawn + Disposal |
| Bugs found | Final save not updated; `IsInjured` warning; thief escape logged as Error | `PipeSuck` queue/ghost parcels; mimic VFX missing material; animator params; menu typo; wrong blocked-dialog title |

\* Agent B’s 2,650 is inflated by an automation artifact: its batch lookup included deactivated/queued shipped boxes from Day 1, so Day 2 reported `19 / 6` shipped. A normal player cannot ship inactive boxes. Agent A’s 750 is the more representative normal-player result.

Both agents used runtime public actions (`spawner.Interact`, `ToggleOpenBox`, `Inspect/Scan`, stamp buttons, `PlaceItem`, `ProcessShipping`, End Day) because the MCP harness cannot inject raw OS keyboard/mouse input into Unity’s legacy Input.

### Confirmed issues from both playtests

1. **Final Day 3 result is not persisted.** Slot 1 showed Day 3 / 600 PTS after a 750 run; Slot 2 showed Day 3 / 1,650 PTS after a 2,650 run. The last autosave is the start of Day 3. The final summary says RUN COMPLETE but does not write a completed-run save.
2. **Animator parameters missing.** `IsInjured`, `TakeDamage`, `IsDead`, `Die`, `Respawn` warnings on the player Animator.
3. **Thief escape is logged at Error level** even though it is a normal gameplay outcome.

### Issues reported by Agent B (with code check)

- `PipeSuck` does `SetActive(false)` after 0.45 s and `Destroy` only after the full ~1.53 s animation. If End Day is pressed while boxes are still queued, the blocked dialog sets `Time.timeScale = 0`, freezing the pipe coroutine and making active/queued boxes look like “parcels left on the floor.” Dismissing the dialog and waiting resolves it; it is not a permanent softlock. The claim “never calls Destroy” is not correct — line 309 destroys the package after the animation.
- `MimicPackage` procedural transform VFX creates a ParticleSystem while it is already playing and without a valid URP material, producing pink squares and a `ParticleSystem` assert.
- Main Menu title is `STAION 13` instead of `STATION 13`.
- The blocked-day dialog title always says `BELOW SHIPPING QUOTA`, even when the block reason is parcels on the floor.

### Test Cleanup

- Test slots 1 and 2 were deleted after the runs.
- Existing `slot_3.json` was preserved byte-for-byte.
- `GameSession.CurrentSlot` restored to 3.

## Enemies

### Thief
- Natural trigger after 5 stamps on Day 1.
- Warning phase: `IsWarningActive = true`, 4-second delay.
- Spawned `Enemy_Thief(Clone)` at `(-12.9, 0.0, -14.2)`, state `Approaching`.
- It reached a package, stole it, escaped, and destroyed it: `dailyDestroyedCount = 1`.
- On Day 2 and Day 3 the thief was killed with two `TakeStunDamage(35)` hits:
  - hit 1: `Panicking`, HP 35, package dropped
  - hit 2: `Dead`, HP 0
- The thief did not hard-block the day; the remaining packages still allowed the day to pass.

### Mimic
- `Mimic_Package` is in the Day 1 package pool and can spawn as an Alien package.
- Shipping it unopened counts as an Alien rejection (`+50`).
- Triggering `MimicPackage.TriggerTransform(Opened)` spawned `Enemy_Mimic(Clone)` with HP 80.

### Game Over
- Lethal damage: HP 0, `IsDead = true`, `GameOverUI.IsGameOverOpen = true`.
- Respawn: HP 100, `IsGameOverOpen = false`, player restored to the initial position.
- Death/Game Over did not permanently block the run.

## Final-Day Verification

- Day 3 final summary: score `1600`, station fee `600` → `1000`.
- `RequestEndDay()` returned `true` (previously `false`).
- Summary buttons: NEXT DAY hidden, RESTART hidden, FINISH RUN shown.
- Result text: `SECTOR CLEARED — RUN COMPLETE`.
- `ConfirmEndDayAndProceed()`: `IsRunComplete = true`, `currentDay` stays `3`, `OnDayEnded(3)` fired once.
- Clicking FINISH RUN loaded `MainMenu`.
- Final-day shortfall override: 0/8 shipped → penalty `800`, fee `600`, score `1600 → 200`; final summary showed `RUN COMPLETE — PENALTY APPLIED` and finished correctly.
- Normal Day 1 transition still shows NEXT DAY and advances to Day 2 with quota `10/10`.

## Code Changes

- `Assets/_Project/Scripts/Manager/GameDayManager.cs`
  - Added `IsFinalConfiguredDay` and `IsRunComplete`.
  - Final configured day skips the missing-next-DayConfig guard.
  - Added `DaySummaryResult.isFinalDay` / `runComplete`.
  - Added `ExecuteRunComplete()`; no Day 4, no quota/stat reset.
  - Final-day shortfall override also allowed.
- `Assets/_Project/Scripts/Manager/DailySummaryUI.cs`
  - Final-day title/result text.
  - NEXT DAY hidden; FINISH RUN shown; MAIN MENU on failed final day.
  - `FINISH RUN` calls completion before returning to Main Menu.
  - Action guard prevents duplicate clicks.
- `Assets/_Project/Scripts/Save/SaveSystem.cs`
  - `ReturnToMainMenu()` also unpauses audio.

## Notes / Remaining Caveats

- Live Day 1 uses `DayTest` (min shipped 0, station fee 0). `Assets/_Project/Art/Textures/Day1.asset` is not used by the current scene.
- Day 1 contains one scene `Box` at `(3.3, 0.0, -4.3)`; it must be shipped/stored or `CanEndDay` reports it as a parcel on the floor.
- The final-run completion is not persisted as a save flag; the last save remains the start-of-Day-3 autosave. This is intentional for the minimal fix.
- Auto-save and thief were disabled only for the fast full-loop automation; both were exercised separately.
- Non-blocking console warning remains: legacy `M_Decal_WaterStain` uses `Shader Graphs/Decal` and lacks `_EmissionIntensity`. It is unrelated to the game loop.
