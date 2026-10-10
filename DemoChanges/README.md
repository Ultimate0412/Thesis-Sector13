# DemoChanges — สรุปทุกอย่างที่ถูกเปลี่ยนสำหรับเดโม่ 1 วัน

โฟลเดอร์นี้ใช้ตอน **merge เข้า branch อื่น** เพื่อดูว่าอะไรถูกเพิ่ม/แก้ไปบ้าง
เอกสารนี้เขียนไว้ให้เทียบกับต้นฉบับได้ทันที โดยไม่ต้องไล่ diff เอง

- Branch ที่ทำงาน: `Scene_Layout`
- Base commit: `0f5516e` (Merge main into Scene_Layout)
- วันที่: 2026-10-11
- ต้นฉบับของไฟล์ที่ถูกแก้: `before/`
- เวอร์ชันปัจจุบันหลังแก้: `after/`
- diff แบบ git: `diff.patch`
- รายชื่อไฟล์: `git-status.txt`

---

## 1. ไฟล์ใหม่ (สร้างขึ้นทั้งหมด)

| ไฟล์ | คืออะไร |
|---|---|
| `Assets/Scripts/Manager/DemoLoopBootstrap.cs` (+`.meta`) | สคริปต์หลักที่ซ่อมทุกอย่างให้ loop จบได้ใน 1 วัน (ดูหัวข้อ 2) |
| `Assets/Tests/PlayMode/DemoDayLoopTests.cs` | PlayMode test 5 เคส สำหรับ loop |
| `Assets/Tests/PlayMode/DemoDayLoopTests.PlayMode.asmdef` | asmdef ของ test (อ้าง `UnityEngine.UI` + `Unity.TextMeshPro`) |
| `Assets/Tests.meta`, `Assets/Tests/` | โฟลเดอร์ test (Unity สร้าง meta ให้) |
| `Backup/2026-10-11-demo/GamePlay.unity.bak` | สำเนาต้นฉบับของฉากก่อนแก้ |
| `Backup/2026-10-11-demo/EditorBuildSettings.asset.bak` | สำเนาต้นฉบับของ build settings |
| `CHANGELOG_DEMO.md` | ชี้มาที่เอกสารนี้ |

## 2. ไฟล์ที่แก้ (เจตนา)

### 2.1 `Assets/Scenes/GamePlay.unity`

| ค่าเดิม | ค่าใหม่ | ทำไม |
|---|---|---|
| `==GameManager==` มี 5 component | เพิ่ม `DemoLoopBootstrap` (fileID `9900000001`) | ให้สคริปต์เดโม่ทำงานตอนเกมเริ่ม |
| ไม่มีบล็อก `&9900000001` | เพิ่ม `--- !u!114 &9900000001` อ้าง script guid `9f1b382d37173d4282862785730b0fc1` | ผูกสคริปต์เข้ากับ GameObject |

### 2.2 `ProjectSettings/EditorBuildSettings.asset`

| ค่าเดิม | ค่าใหม่ | ทำไม |
|---|---|---|
| `m_Scenes` มีแค่ `Assets/Scenes/Script Scrence.unity` | เพิ่ม `Assets/Scenes/GamePlay.unity` (guid `b4e5101348387664d8ec51d39cebc4f0`) เป็น **index 0**, Script Scrence เลื่อนเป็น index 1 | เกมโหลดฉากเองไม่ได้เลยถ้าไม่อยู่ใน build settings (`SceneManager.LoadScene` ตอนกด PLAY AGAIN) |

### 2.3 `Assets/PrefabPrtotype/Player.prefab`

| ค่าเดิม | ค่าใหม่ | ทำไม |
|---|---|---|
| `Player` (มี Rigidbody) → `Main Camera` เป็นลูกโดยตรง | เพิ่ม `CameraPivot` ระหว่างกลาง: `Player` → `CameraPivot` → `Main Camera` (`CameraPivot` local pos `(0, 0.472, 0)`, Main Camera local pos `(0,0,0)`) | แก้กล้องกระตุกเวลาหัน (ดูหัวข้อ 3) |

### 2.4 `Assets/Scripts/Player Scripts/PlayerController.cs`

| ค่าเดิม | ค่าใหม่ | ทำไม |
|---|---|---|
| `transform.rotation *= Quaternion.Euler(0, MouseX * lookSpeed, 0)` (เขียนทับ transform ของ Rigidbody ทุกเฟรม) | `rotationY += MouseX * lookSpeed` แล้ว `cameraPivot.localRotation = Euler(0, rotationY, 0)` | Rigidbody ตั้ง `Interpolate` อยู่ → มันเขียน transform ทับก่อน render ทำให้หันแล้วกระตุก |
| — | เพิ่มฟิลด์ `public Transform cameraPivot` + `rotationY` | ใช้หมุน yaw |
| — | `cameraPivot = playerCamera.parent` ใน `Awake()` ถ้ายังไม่ถูกตั้ง | หา pivot อัตโนมัติ ไม่ต้องตั้งค่าใน Inspector |
| `transform.forward/right` ใช้เป็นทิศเดิน | ใช้ `cameraPivot.forward/right` | ทิศเดินอิงทิศที่หันจริง |

## 3. ต้นเหตุกล้องกระตุก (สรุป)

`Player` ถือ Rigidbody ที่ตั้ง `Interpolation = Interpolate` และล็อกการหมุน
โค้ดเดิมเขียน `transform.rotation` ของ Rigidbody เองทุกเฟรมใน `Update()`
แต่ interpolation ของ Unity จะคำนวณ transform ใหม่จาก physics step **ก่อน render**
→ ค่าที่เขียนไปถูกทับ/ตีกัน = ภาพกระตุกเวลาหัน

แก้โดยย้าย yaw ไปที่ `CameraPivot` ซึ่งเป็น **ลูก** ของ Rigidbody
(interpolation เขียนแค่ transform ของ Rigidbody ไม่แตะลูก) → Rigidbody ไม่ถูกเขียนทับอีก
ยืนยันแล้ว: จำลองหันกล้อง 37° แล้ว `player.rotation` ยังเป็น `(0,0,0)`

## 4. ไฟล์ที่ถูกแก้ "โดยอัตโนมัติ" (ไม่ใช่เจตนา) — ควรตรวจก่อน merge

| ไฟล์ | ใครแก้ | หมายเหตุ |
|---|---|---|
| `.mcp.json` | Unity MCP plugin | เพิ่ม entry `ai-game-developer` ชี้ `http://localhost:8080/p/...` |
| `opencode.json` (ที่ root โปรเจกต์) | Unity MCP plugin | ไฟล์เดียวกันในรูปแบบ opencode config |
| `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset` | Unity | regenerate font atlas ระหว่างรัน play |
| `ProjectSettings/Packages/com.unity.probuilder/Settings.json` | Unity | ค่า setting ของ ProBuilder |
| `.opencode/` | ชุด skills | มี `node_modules` อยู่ข้างใน ไม่ควร commit |

> แนะนำ: ไฟล์ในหัวข้อนี้ **อย่า commit รวม** ถ้าไม่ตั้งใจ

## 5. สิ่งที่ "ไม่ได้" แก้

- `Assets/Scripts/Manager/GameDayManager.cs` — แก้ชั่วคราวแล้ว **revert กลับเป็นต้นฉบับแล้ว** (ไม่มีการแปลภาษา)
- ข้อความภาษาไทยในโค้ดทั้งหมด — คงไว้ตามเดิม
- `Canvas.prefab` — ไม่แตะ (ตำแหน่ง Score/Reject ย้ายตอน runtime ใน `DemoLoopBootstrap` แทน เพื่อไม่ให้กระทบ `Script Scrence.unity` ที่ใช้ prefab เดียวกัน)

## 6. วิธีเอาไปใช้

```powershell
# ดูว่าเปลี่ยนอะไร
git apply --stat DemoChanges/diff.patch

# เทียบไฟล์ทีละไฟล์
Compare-Object (Get-Content DemoChanges/before/<path>) (Get-Content DemoChanges/after/<path>)
```
