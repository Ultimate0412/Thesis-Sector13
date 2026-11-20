# Decal Studio (Sector13)

ชุดเครื่องมือ decal สำหรับเกมตรวจพัสดุ — รวม “รอยหลักฐาน” หลายแบบไว้ใน atlas เดียว แล้วเอาไปฉายบนกล่องผ่าน URP Decal Projector

> สถานะ: **D0–D3 บางส่วนเสร็จ** (shader / atlas builder / library / tint) — ยังไม่ได้ผูกเข้า prefab กล่องจริงและยังไม่ได้ยืนยันภาพใน Play mode

---

## ไฟล์ที่มี

```
Assets/_Project/DecalStudio/
├─ Shaders/
│   └─ DecalAtlas.shader          DBuffer decal shader (เขียนมือ ไม่ใช้ Shader Graph)
├─ Runtime/
│   ├─ DecalAtlas.cs              ScriptableObject: 3 หน้า texture + entries
│   ├─ DecalLibrary.cs            คลัง + ค้นหาด้วย TraceType + Apply() ให้ projector
│   └─ DecalStudioPlayTest.cs     self-test ใน Play mode (ทำงานเมื่อมีไฟล์ธง)
├─ Editor/
│   ├─ DecalAtlasBuildSettings.cs ตั้งค่า build (ขนาด/cell/padding/grayscale/flipV + sources)
│   ├─ DecalAtlasBuilder.cs       รวม atlas + สร้าง material + DecalAtlas
│   ├─ DecalAtlasWindow.cs        หน้าต่าง UI + tint preset
│   └─ DecalStudioMenu.cs         เมนูลัด build ด้วยค่า default
├─ Settings/AtlasBuildSettings.asset
└─ Art/Atlases/
    ├─ Evidence.asset             atlas (6 ช่องที่ใช้อยู่)
    ├─ Evidence_Albedo/Normal/Mask.png
    └─ Materials/M_Decal_*.mat    1 material ต่อ 1 ช่อง (มี tint ของตัวเอง)
```

## Channel packing

| หน้า | ช่อง | เก็บ |
|---|---|---|
| **Albedo** (sRGB) | RGB | luminance ของรอย (grayscale) |
| | A | coverage (ความทึบของรอย) |
| **Normal** (linear) | RG | normal XY (decode Z ใน shader) |
| | B | height (พื้นเรียบ = 0.5) |
| **Mask** (linear) | R | metallic |
| | G | occlusion (AO) |
| | B | smoothness |
| | A | MAOS blend |

URP DBuffer เก็บ `(metallic, occlusion, smoothness, MAOSAlpha)` ตาม `DBuffer.hlsl` — shader เขียนลงครบทั้ง 4 ค่า

## วิธีใช้

1. เปิด `Tools ▸ Sector13 ▸ Decal Studio ▸ Atlas Builder`
2. เลือกวิธีเติม source:
   - **Scan external folders** — สแกน `externalSourceRoot` (default `D:/.../2DAlpha/Decal`) หา `<ชื่อ>/Textures/<ชื่อ>_Albedo.png`
   - **Fill from Assets/Textures/Traces** — ใช้ texture เดิมใน Unity
   - หรือใส่ Texture2D เองทีละช่อง
3. ตั้ง `atlasSize` (default 2048), `cellSize` (512), `padding` (16) → grid 4×4 = 16 ช่อง
4. กด **Build Atlas** → ได้ `Evidence.asset` + PNG 3 หน้า + material ต่อช่อง
5. ในเกม: ใส่ `DecalLibrary` (ที่ชี้ไปยัง atlas) ในช่อง `decalLibrary` ของ `PackageTraceApplier`
   - ถ้าไม่ใส่ → ใช้ material 6 ตัวเดิม (fallback)
   - ถ้าใส่ → decal จะใช้ material + `uvScale/uvBias` จาก atlas

### Source จาก Substance (โฟลเดอร์ภายนอก)

`DecalSourceEntry` รองรับ `externalFolder` — builder จะอ่านไฟล์ตาม pattern:

```
<externalFolder>/<id>_Albedo.png
<externalFolder>/<id>_Normal.png
<externalFolder>/<id>_Height.png
<externalFolder>/<id>_Mask.png
```

ถ้า mask ที่ได้มาเป็น Roughness → ติ๊ก `maskIsRoughness` (builder แปลงเป็น smoothness ให้)

สเปก preset ดูที่ `D:\...\2DAlpha\Decal\_presets\Sector13_Decal_1024.md`

## Tint (สีต่อชนิดรอย)

atlas เก็บเป็น grayscale → สีมาจาก `_BaseColor` ของ material แต่ละช่อง
แก้สีได้ที่ material (`M_Decal_<ชื่อ>`) หรือแก้ `tint` ใน settings แล้ว build ใหม่

| รอย | สีเริ่มต้น |
|---|---|
| WaterStain | 0.46, 0.38, 0.27 |
| OilStain | 0.14, 0.11, 0.08 |
| ClawMarks | 0.33, 0.26, 0.19 |
| CardboardScuff | 0.55, 0.48, 0.38 |
| AlienSlime_UV | 0.35, 1.00, 0.50 (emissive) |
| ChemicalResidue_UV | 0.30, 0.85, 1.00 (emissive) |
| BloodStain | 0.36, 0.06, 0.05 |
| AcidBurn | 0.55, 0.62, 0.24 |
| Frost | 0.82, 0.92, 1.00 |
| TapeResidue | 0.50, 0.45, 0.34 |

## Height → cavity (AO ปลอม)

shader ใช้ความชันของ normal (`length(normalXY)`) เป็นตัววัดความลึก แล้วคูณเข้าช่อง occlusion
- `_HeightStrength` (0–1) เปิด/ปิดผล
- `_HeightContrast` (0.1–6) ความไว
- ไม่เพิ่ม texture / ไม่เพิ่ม draw call / ไม่เพิ่ม texture tap

## ระบบ spawn ร่องรอย (ใหม่)

`PackageTraceApplier` อ่านค่าจาก **`TraceSpawnTable`** (`Assets/_Project/DecalStudio/Resources/TraceSpawnTable.asset`)

```
VerdictType: Approve / Return / Illegal / Alien / OpenBox
  └ VerdictSpawnRule
        spawnChancePercent, minTypes..maxTypes
        └ TraceSpawnRule[]  (traceType, weight, minCount..maxCount, visibility)
              └ TracePlacementProfile   ← กำหนดตำแหน่ง
                    allowedFaces (Top/Bottom/Front/Back/Left/Right)
                    avoidLabelFace, margin, edgeChance, gravityFlow,
                    minSpacing, spinRange, sizeRange
```

- **สุ่มแบบคงที่ต่อกล่อง**: seed ถูกเลือกครั้งแรกที่ `ApplyTraces` แล้วเก็บไว้ → กล่องเดิมได้รอยเดิม (ไม่ reroll ตอน reload)
  ตั้ง `traceSeed > 0` เพื่อล็อกค่าตายตัว
- ถ้าไม่ผูก `spawnTable` ใน prefab จะโหลดจาก `Resources/TraceSpawnTable` อัตโนมัติ
- ถ้าลบตารางออก → กลับไปใช้โหมด Legacy (settings 3 ชุด + material 6 ตัว)

แก้ไขได้จาก Inspector ทั้งหมด: เลือก asset ใน `Art/Spawn/Placement/TP_*.asset` หรือเปิด `TraceSpawnTable`

## Self-test ใน Play mode
```powershell
New-Item -ItemType Directory -Force Logs\DecalTest | Out-Null
"flag" | Set-Content Logs\DecalTest\run.flag
```
กด Play ~5 วินาที → เกมจะวางแผง 2 อันหน้ากล้อง (ซ้าย = material เดิม, ขวา = atlas) แล้วบันทึก `Logs/DecalTest/play.png`

## ข้อจำกัดที่รู้อยู่

- **ยังไม่ผูก library เข้า prefab กล่อง** (`Legal_Package`, `Alien_Package`, `Box*.prefab`) — ต้องตัดสินใจก่อนว่าจะใช้ atlas แทน material เดิมทั้งหมด
- **Scene View / edit mode ไม่แสดง decal** ในโปรเจกต์นี้ (ทดสอบแล้วทั้ง material เดิมและใหม่) → ต้องตรวจใน Play mode เท่านั้น
- `TraceType` เพิ่ม `BloodStain / AcidBurn / Frost / TapeResidue` แล้ว และตารางใหม่ใช้ครบทั้ง 10 ชนิด
- ยังไม่มี `PackageEvidence` (ให้ tablet อ่านว่าเจอหลักฐานอะไร) และยังไม่ทำ placement แบบกันรอยทับกันจริง (ใช้ `minSpacing` เป็นค่าไว้ก่อน)
