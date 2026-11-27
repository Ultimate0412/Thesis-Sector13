# Decal Environment — ชุดลาย + วิธีใช้ Decal Projector

> อัปเดต: ก๊อป material จากฝั่ง Package มาทำเป็นชุด Env ครบทุกลายแล้ว
> โฟลเดอร์: `Assets/_Project/Art/Materials/EnvDecals/`

---

## 1. Material ที่มีให้เลือก (17 ตัว)

ทุกตัวใช้ shader `Sector13/Decals/DecalAtlas` และชี้ไปที่ atlas เดียวกับฝั่ง Package
ชื่อ material = `M_Env_<id>` ตรงกับ id ใน `DecalLibrary`

### Evidence atlas (7 ลาย)

| Material | Atlas cell (col,row) | ใช้ทำอะไร |
|---|---|---|
| `M_Env_ClawMarks` | 0,0 | รอยกรงเล็บ |
| `M_Env_OilStain` | 1,0 | คราบน้ำมัน |
| `M_Env_WaterStain` | 2,0 | คราบน้ำ |
| `M_Env_CardboardScuff` | 3,0 | รอยเสียดสีกระดาษ |
| `M_Env_AlienSlime_UV` | 0,1 | เมือกเอเลี่ยน (เรืองแสง) |
| `M_Env_ChemicalResidue_UV` | 1,1 | สารเคมี (เรืองแสง) |
| `M_Env_Blood` | 2,1 | เลือด |

### BloodVariants atlas (10 ลาย)

| Material | Atlas cell (col,row) |
|---|---|
| `M_Env_BloodStain_Damage_A` | 0,0 |
| `M_Env_BloodStain_Damage_B` | 1,0 |
| `M_Env_BloodStain_Down_A` | 2,0 |
| `M_Env_BloodStain_Powder_A` | 3,0 |
| `M_Env_BloodStain_Side_A` | 0,1 |
| `M_Env_BloodStain_Side_B` | 1,1 |
| `M_Env_BloodStain_WaterUV_A` | 2,1 |
| `M_Env_BloodStain_WaterUV_B` | 3,1 |
| `M_Env_BloodStain_WaterUV_C` | 0,2 |
| `M_Env_BloodStain_WaterUV_D` | 1,2 |

> Material ที่ก็อปใหม่ตั้ง `_Opacity = 0.9` ให้เป็นค่าเริ่มต้นแบบ env
> 4 ตัวเดิม (`Blood`, `OilStain`, `WaterStain`, `CardboardScuff`) ยังใช้ค่าที่ปรับไว้ในฉาก

---

## 2. สร้าง Decal Projector

1. ไปที่เมนู **GameObject ▸ Rendering ▸ URP Decal Projector**
2. Component `Decal Projector` จะมีหน้าตาประมาณนี้:
   - `Scale Mode` — ปล่อยเป็น `Scale Invariant`
   - `Width`, `Height`, `Projection Depth` — ขนาดกล่องฉาย
   - `Pivot` — ตำแหน่งจุดหมุนของกล่อง (ปกติ `0, 0, 0.5`)
   - `Material` — ใส่ material ที่ต้องการ
   - `UV ▸ Tilling`, `UV ▸ Offset` — ใช้เลือกช่องใน atlas
   - `Opacity` — ความจางของ projector (คนละตัวกับ `_Opacity` ใน material)
   - `Draw Distance`, `Start Fade` — ระยะมองเห็น/เฟด

> ใน Inspector ของ URP รุ่นนี้สะกดว่า **Tilling** (ไม่ใช่ Tiling) และ **Offset**
> แต่ใน YAML/สคริปต์คือ `uvScale` / `uvBias`

---

## 3. ใส่ material + เลือกลายใน atlas (สำคัญ)

มี 2 วิธี: ใช้ `DecalProjectorBinding` (แนะนำ) หรือตั้ง material + UV เอง

### วิธีแนะนำ: DecalProjectorBinding (Plan B)

1. เลือก Decal Projector ใน Scene
2. **Add Component ▸ Sector13 ▸ Decals ▸ Decal Projector Binding**
3. `Decal Library` = `EnvDecalLibrary` (Env) หรือ `DecalLibrary` (Package)
4. `Decal Entry` = เลือกลาย
5. กด **Apply Now**

component จะเซ็ตทั้ง `material` และ `uvScale/uvBias` จาก entry เดียวกัน
และถ้าเปิด `Auto Apply` ไว้ จะดึงกลับให้อัตโนมัติเมื่อ material/UV หลุด

> ตอนนี้ projector ใน Scene ถูกผูกกับ `EnvDecalLibrary` entry `Blood` แล้ว
> เปลี่ยน entry ใน Inspector ได้เลย material + UV จะตามไปด้วย

### วิธี manual (ถ้าไม่ใช้ binding)

1. ลาก `M_Env_...` จาก Project window ไปใส่ช่อง **Material**
2. **ต้องตั้ง UV Tilling / Offset เอง** — material ไม่ได้จำว่าเป็นช่องไหน
   (`_DecalRect` ใน material ถูกตั้งให้แล้ว แต่ projector มี UV แยกอีกชุด)
3. ตั้งค่าตามสูตร:
   - **Tilling** = `0.242` (ทั้ง X และ Y)
   - **Offset** = `(0.25 × column + 0.004, 0.25 × row + 0.004)`

ตัวอย่าง:

| ลาย | Tilling (X,Y) | Offset (X,Y) |
|---|---|---|
| `M_Env_ClawMarks` | 0.242, 0.242 | 0.004, 0.004 |
| `M_Env_OilStain` | 0.242, 0.242 | 0.254, 0.004 |
| `M_Env_WaterStain` | 0.242, 0.242 | 0.504, 0.004 |
| `M_Env_CardboardScuff` | 0.242, 0.242 | 0.754, 0.004 |
| `M_Env_AlienSlime_UV` | 0.242, 0.242 | 0.004, 0.254 |
| `M_Env_ChemicalResidue_UV` | 0.242, 0.242 | 0.254, 0.254 |
| `M_Env_Blood` | 0.242, 0.242 | 0.504, 0.254 |
| `M_Env_BloodStain_Damage_A` | 0.242, 0.242 | 0.004, 0.004 |
| `M_Env_BloodStain_Damage_B` | 0.242, 0.242 | 0.254, 0.004 |
| `M_Env_BloodStain_Down_A` | 0.242, 0.242 | 0.504, 0.004 |
| `M_Env_BloodStain_Powder_A` | 0.242, 0.242 | 0.754, 0.004 |
| `M_Env_BloodStain_Side_A` | 0.242, 0.242 | 0.004, 0.254 |
| `M_Env_BloodStain_Side_B` | 0.242, 0.242 | 0.254, 0.254 |
| `M_Env_BloodStain_WaterUV_A` | 0.242, 0.242 | 0.504, 0.254 |
| `M_Env_BloodStain_WaterUV_B` | 0.242, 0.242 | 0.754, 0.254 |
| `M_Env_BloodStain_WaterUV_C` | 0.242, 0.242 | 0.004, 0.504 |
| `M_Env_BloodStain_WaterUV_D` | 0.242, 0.242 | 0.254, 0.504 |

> ถ้าใส่ Tilling = 1, Offset = 0 จะเห็น **ทั้ง atlas 16 ช่อง** ไม่ใช่ลายเดียว
> นี่คืออาการที่พบบ่อยสุดตอนลาก material ใส่ projector ตรง ๆ

> **สำหรับ preview ใน Inspector:** material ใช้ `_DecalRect` (property ซ่อน) เป็นกรอบ crop
> ตอนนี้ `_DecalRect` ถูกตั้งให้ทั้ง material ฝั่ง Env และ Package แล้ว
> ถ้าสร้าง material ใหม่จาก atlas เอง ให้ตั้ง `_DecalRect` เป็น cell เดียวกันด้วย
> ไม่งั้น thumbnail จะโชว์ทั้ง atlas

### ทางลัด: ใช้ Decal Stamper

`Tools ▸ Sector13 ▸ Decal Stamper`

- เลือกลายจากรายการ (ตอนนี้มีครบทั้ง 17)
- ตั้ง Size range
- ติ๊ก “Click in Scene View to stamp” แล้วคลิกพื้น
- tool จะสร้าง projector + ใส่ material ฝั่ง Env + ตั้ง UV ให้อัตโนมัติ

หรือใช้ `DecalScatterArea` แล้วกด **Scatter** เพื่อกระจายหลายจุดในโซน

---

## 4. ขยับ / ย่อ-ขยาย / หมุน

### ขนาด
ใช้ช่องใน component เท่านั้น:
- `Width` = ความกว้างแนวแกน X
- `Height` = ความสูงแนวแกน Y
- `Projection Depth` = ความลึกตามแกน Z

> `Scale Mode = Scale Invariant` (ค่า default) **ไม่สนใจ Transform Scale**
> เพราะฉะนั้นใช้เครื่องมือ Scale (R) กับ Transform จะไม่ทำให้ decal ใหญ่ขึ้น
> ให้ปรับ Width/Height/Projection Depth แทน
> ถ้าอยากให้Transform scale มีผล ให้เปลี่ยนเป็น `Inherit from Hierarchy`

### ตำแหน่ง / ทิศ
- ใช้ Move (W) และ Rotate (E) ตามปกติ
- Decal ฉายไปตามแกน **+Z (ลูกศรสีน้ำเงิน)**
  - พื้น: หมุน X = 90° ให้ +Z ชี้ลง
  - ผนัง: หมุนให้ +Z ชี้เข้าผนัง
- วาง pivot ให้กล่องฉายคร่อมผิว:
  - `pivot.z` ต้องเป็น **ครึ่งหนึ่งของ Projection Depth** (`depth / 2`)
  - ค่า default `(0,0,0.5)` ใช้ได้กับ depth 1.0 เท่านั้น
  - ถ้าใช้ depth 0.1 ให้ตั้ง pivot = `(0,0,0.05)`; ถ้าไม่ตั้ง กล่องจะลอยอยู่หน้าผิว 0.45 ม. และ decal จะไม่ขึ้น
  - วาง transform ไว้ที่ผิว + 0.02 ม. แล้วให้ volume ลึกเข้าไปตาม depth

### หมุนลายบนผิว
- หมุนรอบแกน **Z local** (ลูกศรสีน้ำเงิน) = ลายหมุนบนพื้น
- ใช้โหมด Local ของ gizmo เพื่อไม่ให้หมุนหลุดระนาบ

### ปรับกล่องโดยไม่ขยับ object
เลือก Decal Projector แล้วดู toolbar ใน Scene view:
- **Scale** — ปรับขอบเขตกล่อง
- **Crop** — ปรับขอบเขตพร้อมครอป UV
- **Pivot / UV** — ขยับ pivot/UV โดยไม่ย้ายกล่องฉาย

---

## 5. ปรับ material ให้เป็นลายของ Env

เลือก material ใน `EnvDecals` แล้วปรับใน Inspector:

| Property | ความหมาย | ค่าแนะนำสำหรับ Env |
|---|---|---|
| `_BaseColor` | tint ของลาย | สีดิน/น้ำมัน/เลือดตามต้องการ |
| `_Opacity` | ความทึบของลาย | 0.8–0.9 (อย่าให้แน่นเท่า package) |
| `_HeightStrength` | ความลึกจาก normal (AO ปลอม) | 0.2–0.5; รอยกรงเล็บ 0.8–0.9 |
| `_Saturation` | ความอิ่มสี | 1.2–1.6 |
| `_Brightness` | ความสว่าง | 0.8–1.0 |
| `_Variation` | ความแปรปรวนของลาย | 0.2–0.5 |
| `_MetallicScale` | metallic จาก mask | 1 |
| `_SmoothnessScale` | smoothness จาก mask | 1 |
| `_EmissionColor` | สีเรืองแสง | ใช้กับลาย UV |
| `_EmissionIntensity` | ความแรงเรืองแสง | 0 = ปิด; 1–1.5 = เรืองแสง |

### Live preview ใน Inspector

material ที่ใช้ shader `Sector13/Decals/DecalAtlas` จะมี thumbnail ด้านบน Inspector
และอัปเดตทันทีเมื่อแก้ค่า:

- `_BaseColor` → สีของลาย
- `_Opacity` → ความจาง (เห็นเป็นความทึบของลายบนพื้นหลังเข้ม)
- `_Brightness` → ความสว่าง
- `_Saturation` → ความอิ่มสี
- `_EmissionColor` / `_EmissionIntensity` → แสงเรืองซ้อนบนลาย

thumbnail ใช้ `_DecalRect` (ซ่อน) เป็นกรอบ crop ของ atlas
ส่วน `_HeightStrength`, normal, metallic และ smoothness **ไม่แสดงใน thumbnail 2D**
ต้องดูผลจริงใน Scene view / Game view

### Exact Preview (หน้าต่างแยก)

`Tools ▸ Sector13 ▸ Decal Studio ▸ Decal Preview`

- เลือก Material หรือ Decal Projector ใน Scene → หน้าต่างจะตามให้
- ปุ่ม **Env Library** / **Package Library** + dropdown **Entry**
- พรีวิวใช้ **URP Decal Projector ตัวจริง** + แสงหลัก/ambient ของฉาก
- ถ้าเลือก Decal Projector ใน Scene จะมี toggle **ใช้ไฟ Scene รอบ ๆ projector**
  (คัดลอก point/spot ที่อยู่ใกล้สุดสูงสุด 8 ดวง)
- toggle **ใช้ mesh/material ของพื้นจริง (ทดลอง)** — ใช้ mesh/material ใต้ projector
  (ค่าเริ่มต้นปิด เพราะ UV/pivot ของ mesh จริงอาจทำให้พื้นในพรีวิวไม่ตรงมุมกล้อง)
- เห็น normal / height / metallic / smoothness / emission ตาม shader จริง
- แก้ material ใน Inspector → พรีวิวอัปเดต
- ปิดหน้าต่าง → rig ชั่วคราวถูกลบ

> หมายเหตุ: ถ้าเลือก material ตรง ๆ (ไม่ได้เลือก projector) พรีวิวจะใช้
> Directional Light หลัก + ambient ของฉากอย่างเดียว
> แต่ material / shader / UV / emission เป็นของจริง

### ทำ material ใหม่
1. คลิกขวาใน `EnvDecals` ▸ **Create ▸ Material**
2. เลือก shader **`Sector13/Decals/DecalAtlas`**
3. ใส่ texture:
   - `_BaseMap` = `Evidence_Albedo.png` หรือ `BloodVariants_Albedo.png` (หรือ PNG เดี่ยว)
   - `_NormalMap` = atlas normal
   - `_MaskMap` = atlas mask
4. ตั้ง `_BaseColor`, `_Opacity`, `_HeightStrength` ตามต้องการ
5. ถ้าใช้ `DecalProjectorBinding` ไม่ต้องตั้ง UV เอง — เลือก entry แล้ว Apply
   ถ้าใช้ manual ให้ตั้ง UV Tilling/Offset ที่ projector ตามช่องที่ต้องการ

หรือก็อป material เดิมแล้วแก้:
- คลิกขวา `M_Env_...` ▸ **Duplicate** (Ctrl+D)
- เปลี่ยนชื่อและค่า
- ใช้ projector ตัวเดิมได้เลย

> Material เป็น asset ที่ใช้ร่วมกัน: แก้สี/opacity จะกระทบทุก projector ที่ใช้ material นั้น
> ถ้าอยากได้หลาย variation ให้ duplicate material หรือใช้ material variant

---

## 6. ข้อควรระวังในโปรเจกต์นี้

- **Scene View อาจไม่แสดง decal** ในโปรเจกต์นี้ (เคยทดสอบแล้ว) — ให้กด Play แล้วดูใน Game View
- Decal เป็น **DBuffer / screen-space** จึงไม่ฉายลงบนวัตถุโปร่งใส
- ถ้า decal ไม่ขึ้นบนวัตถุบางชิ้น ให้เช็ค `Rendering Layers` ของ projector กับ `Decal Layers` ใน URP Asset/Renderer
- `Projection Depth` ต้องลึกพอที่จะตัดผ่านผิว แต่อย่าให้ทะลุไปโดนผนังอีกด้าน
- ถ้าลายเบลอ/มีขอบ atlas ติดมา ให้ลด Tilling ลงเล็กน้อย (เช่น 0.24) หรือเพิ่ม inset เป็น 0.006
- ฝั่ง Package ยังใช้ material ชุดเดิม (`M_Decal_*` / `DecalLibrary`) ไม่ถูกกระทบ
- `DecalTools.cs` ถูกแก้ให้หา `M_Env_<entry.id>` อัตโนมัติแล้ว ดังนั้น Stamper/Scatter จะใช้ material ฝั่ง Env ให้ทุกลาย

---

## 7. หลังแก้ไฟล์จากภายนอก Unity

ถ้า Unity ยังไม่เห็น material ใหม่:
1. กลับไปที่หน้าต่าง Unity
2. กด **Ctrl+R** (Refresh) หรือคลิกในหน้าต่าง Unity เพื่อให้ auto-refresh
3. รอ compile เสร็จ แล้วเปิด `EnvDecals` อีกครั้ง

> 4 material เดิมถูก rename โดยคง GUID เดิมไว้:
> `M_Env_Oil` → `M_Env_OilStain`,
> `M_Env_Water` → `M_Env_WaterStain`,
> `M_Env_Scuff` → `M_Env_CardboardScuff`
> ฉากที่อ้างอิงอยู่จึงไม่ขาด

---

## 8. Runtime decal setup (Screen Space) — สำคัญ

โปรเจกต์นี้ใช้ **Screen Space** ไม่ใช่ DBuffer:

- `PC_Renderer` ▸ DecalRendererFeature: **Technique = Screen Space**, **Decal Layers = Off**
  - `PC_RPAsset` ยังปิด **Light Layers** อยู่ ถ้าเปิด Decal Layers ทั้งที่ Light Layers ปิด
    rendering-layer texture จะว่างและ layer mask จะ clip decal ทั้งหมดทิ้ง
  - shader `DecalAtlas` จึง **ไม่ทำ layer mask** ใน screen-space pass (วางของให้อยู่ใน volume แทน)
- `Sector13/Decals/DecalAtlas` มี pass `DecalScreenSpaceProjector` (unlit blend)
- Package `Projection Depth` = **0.1** (clamp 0.08–0.25)
- **Pivot ต้องเป็น `(0, 0, depth/2)` = `(0, 0, 0.05)` ไม่ใช่ 0.5**
  - URP เลื่อน volume ไปข้างหน้าตาม `pivot.z` เป็นหน่วยเมตร (ไม่คูณ size)
  - ถ้า depth = 0.1 แต่ pivot = 0.5 กล่องฉายจะลอยอยู่หน้าผิว **0.45 ม.** → decal ไม่แสดงบนกล่อง
  - สูตรที่ถูก: transform อยู่ที่ผิว + 0.02 ม., pivot = depth/2 → volume เริ่มที่ผิวและลึกเข้าไป 0.1 ม.
- Package renderers: Rendering Layer **2**; Package projectors: **2**
- Environment renderers/projectors: Rendering Layer **1** (ตอนนี้ใช้เป็นข้อมูลอ้างอิง ไม่ได้ถูก clip)
- Authored `DecalSlot`: `pivot = (0,0,0.05)`, `startAngleFade = 45`, `endAngleFade = 65`
  - screen-space pass ไม่ได้ใช้ angle fade จริง; การวาง slot ให้อยู่กลางหน้าและอยู่ในขอบจึงสำคัญกว่า
- `AlignSlotToFace` เคารพหน้าที่วางไว้ (Top/Front/Left/Right) และหลบด้านที่มีใบปะหน้า
- Slot ถูกเปิดไว้ใน prefab เพื่อดู layout; `PackageTraceApplier.Awake` จะซ่อนตอนเข้า Play
- Approve spawn chance = 90% (ปรับได้ใน `TraceSpawnTable`)

> ถ้า decal หายทั้งที่ material/UV ถูก ให้เช็ค 2 อย่างนี้ก่อน:
> 1) `pivot.z` ต้องเท่ากับ `size.z / 2`
> 2) อย่าเปิด Decal Layers while Light Layers = Off (ดูข้อ 8 ด้านบน)


> Screen-space pass นี้เป็นแบบ unlit blend: สี decal จะไม่ถูกแสงมากระทบ เหมาะกับรอยสกปรกบนกล่อง แต่ถ้าต้องการ normal/PBR ให้ย้ายไปใช้ Shader Graph Decal

---

## 9. ของในกล่อง (`PackageBox.possibleInnerItemPrefabs`)

| Prefab กล่อง | หมวด | รายการที่ใส่ |
|---|---|---|
| `Box.prefab` | Legal | Items/Legal* + `Props/Legal_PF` ทั้งหมด |
| `Box (1).prefab` | Illegal | `Items/ilegal` (ยังไม่มี `Illegal_PF`) |
| `Box (2).prefab` | Alien | `Items/Alien` + `Props/Alien_PF` ทั้งหมด |
| `Legal_Package.prefab` | Legal | `Items/Legal` + `Props/Legal_PF` ทั้งหมด |
| `Alien_Package.prefab` | Alien | `Items/Alien` + `Props/Alien_PF` ทั้งหมด |
| `Mimic_Package.prefab` | Alien | `Items/Alien` + `Props/Alien_PF` ทั้งหมด |

หมายเหตุ:
- `Props/Alien_PF` ถูกเปลี่ยน `ItemObject.category` จาก Illegal → **Alien** และตั้งชื่อ เช่น Xeno Generator / Xeno Capsule / Embryo Jar
- `randomizeOnAwake = true` ทุกกล่อง → ตอน Awake จะสุ่มจาก possible list
- `Box (1)` ยังใช้ได้เฉพาะ `ilegal` เพราะโปรเจกต์ยังไม่มีโฟลเดอร์ `Illegal_PF`
- ฉาก `GamePlay.unity` ยังไม่ถูก save; ถ้ามี package ที่วางอยู่ในซีนและมี override `possible` เก่า ให้กด Revert หรือตั้งค่าใหม่

---

## 10. `Illegal_Package` (ใหม่)

สร้างเป็น **Prefab Variant ของ `Legal_Package`** เพื่อใช้โมเดล PaperBox / ใบปะหน้า / ItemPos ชุดเดียวกับ package ที่มีชื่ออื่น:

- path: `Assets/_Project/Prefabs/Gameplay/Illegal_Package.prefab`
- model: `Lid_Group`, `PaperBox` (`BoxAsset_mat01`), `ItemPos`, `ShippingLabelCanvas`, `DecalSlots`
- root `ItemObject.category` = **Illegal**
- `PackageBox.innerItemPrefab` = `Items/ilegal.prefab`
- `possibleInnerItemPrefabs` = `Items/ilegal` + `Props/Illgel_PF` ทั้ง 10 ตัว
- `randomizeOnAwake = true`, `discrepancyChance = 100`
- `PackageTraceApplier.currentCategory = Illegal`, `currentVerdict = Illegal`
- renderer/projector masks = 2, projection depth 0.8
- เพิ่มเข้า `DayTest` / `Day2` / `Day3`, `Switch_PF.packagePrefabs` และ `SavePrefabDatabase`

### `Props/Illgel_PF`
เดิม prefab ในโฟลเดอร์นี้ยังไม่มี `ItemObject`; เพิ่มให้ครบ 10 ตัวแล้ว:

| Prefab | ItemObject |
|---|---|
| Big_Egg | Illegal Bio Egg (Biological) |
| Bomb | Improvised Bomb (Industrial, weapon) |
| Bong | Contraband Bong (Medical) |
| Drug | Illegal Narcotics (Medical) |
| Human_Remain | Human Remains (Biological) |
| Human_Skull | Human Skull (Biological) |
| Ijector | Illegal Injector (Medical) |
| Med | Black Market Meds (Medical) |
| Mid_Egg | Illegal Bio Egg (M) (Biological) |
| Small_Egg | Illegal Bio Egg (S) (Biological) |

เพิ่ม `BoxCollider` ให้ทุกตัวเพื่อให้หยิบ/ชนได้หลังนำออกจากกล่อง

> `Box (1).prefab` ยังเก็บไว้สำหรับ save เก่า/fallback และอัปเดต possible list ให้เหมือนกันแล้ว

---

## 11. ไม่ให้ Decal ลงหน้า Shipping Label

หน้า label ของ package คือ **local -Z** (`ShippingLabelCanvas`)

สิ่งที่ตั้งไว้:
- `TracePlacementProfile` ทุกตัว: `avoidLabelFace = true` และ **ตัด `Back` ออกจาก `allowedFaces`**
- `PackageTraceApplier.GetRandomTraceTransform()`: ไม่สุ่มหน้า Back แล้ว (เหลือ Top / Front / Left / Right)
- `DecalSlot` authored: ตรวจแล้วไม่มี slot อยู่บนหน้า label; `AlignSlotToFace()` จะ flip กลับถ้า slot ถูกวางบนหน้า label
- `Projection Depth` ลดเหลือ **0.1** และ `pivot = (0,0,0.05)` (depth/2) — volume เริ่มที่ผิวและลึกเข้าไป 0.1 ม. จึงไม่ทะลุไปหน้าด้านหลัง/label และไม่ลอยออกมาข้างหน้า

> ถ้าเพิ่ม slot ใหม่ ให้วางบน Top / Front / Left / Right เท่านั้น, ตั้ง `pivot = depth/2` และอย่าตั้ง Depth เกิน 0.25






