# Thesis-Sector13 — Skill Profile & 5-Day Learning Plan

> สร้างจาก session ประเมินความรู้ (grilling 9 ข้อ) + การตรวจโปรเจกต์จริง
> โปรเจกต์: Unity 6000.5.10f1 · URP 17.5 · FPS ธีม industrial facility

---

## 1. เป้าหมาย

- สายงานที่ต้องการ: **3D Environment Artist** (ไม่ใช่ VFX)
- VFX ที่ต้องการ: เอฟเฟกต์เล็ก ๆ 3 อย่าง
  1. โดนตีแล้วมีดาวขึ้นหัว
  2. ไอน้ำรั่วจากท่อ
  3. ชนถังน้ำแล้วน้ำหกเต็มพื้น
- เงื่อนไข: ต้องการความรู้ที่ **ต่อยอดไปโปรเจกต์อื่นได้** ไม่ใช่แค่ทำอันนี้จบ
- เวลา: **5 วัน** (จริง 6 วัน — วันที่ 6 เป็น buffer)

## 2. ระดับรวม

**3D Generalist ระดับเริ่ม–กลาง สาย Environment/Props** — ผลิต asset ได้จริง (วางโมเดล · ทำโมเดล · ทำสี)
แต่ยังไม่เข้าใจ "ทำไม" เชิงเทคนิคของ pipeline และยังไม่เคยทำ effect/optimize อย่างเป็นระบบ

## 3. จุดแข็ง

| ทักษะ | ระดับ | หมายเหตุ |
|---|---|---|
| ขึ้นรูป/แก้โมเดล 3D (Maya) — hard-surface | ใช้ได้ | ทำเองจริง |
| Organic (low poly) | พื้นฐาน | ทำได้เล็กน้อย |
| วาง layout ฉาก / level | ใช้ได้ | ทำเองจริง |
| Material/Texture (Substance Painter) | ใช้เป็น | ใช้ export preset Unity URP มาตลอด |
| UV padding / dilation / bleeding | ดีกว่าค่าเฉลี่ย | อธิบายได้เอง |
| ศัพท์ performance เชิงแนวคิด | รู้ 7 ตัว | Draw Call · Batching · Overdraw · LOD · Occlusion Culling · Lightmap texel · VRAM |
| การใช้เครื่องมือ + AI ช่วยงาน | ดี | ComfyUI · Painter · Designer · Maya · Unity MCP · Blender MCP |
| Alpha stamp library | พร้อม | Painter มี alpha 845 ไฟล์ (.sbsar 831 / .png 10 / .svg 4) |

## 4. กำลังพัฒนา

- Lighting / Lightmap bake — พื้นฐาน (รู้ว่า bake + Global Volume), UV2 เดาถูก
- Rig / Animation — แตะมาบ้าง รู้ paint weight (skinning)
- Asset pipeline Maya -> Unity — mesh ผ่าน แต่ animation ไม่ผ่าน (Humanoid)
- Substance bake troubleshooting — เจอ high->low สีไม่ติด, ปัญหา transparency
- Shader Graph — มีฐานจากตัวกรอง TikTok แต่ยังไม่เข้าใจ

## 5. ช่องว่าง และ Blind Spots

| # | ขาด | ผลกระทบ |
|---|---|---|
| 1 | เลเยอร์ Emitter (Particle / VFX Graph) — ไม่มีเลย | ทำ effect ไม่ได้ |
| 2 | Shader Graph (ได้แค่ Multiply/Add/Lerp) | บล็อก effect + น้ำ |
| 3 | **PBR misconception: คิดว่า Smoothness = Roughness** | material กลับด้านแบบไม่รู้ตัว |
| 4 | สับสน data vs color (sRGB) | import ผิด ค่าเพี้ยน |
| 5 | ไม่รู้จัก Profiler / Frame Debugger | optimize ไม่ได้ |
| 6 | ทักษะวินิจฉัย (ชอบเดา / "bake ใหม่ก่อน") | เสียเวลากับปัญหาเดิม |
| 7 | แยก texture padding กับ lightmap padding ไม่ออก | แก้ผิดจุด |
| 8 | ศัพท์ VFX (flipbook, billboard, erosion) | ค้นต่อ/สื่อสารไม่ได้ |
| 9 | เขียน C# เองไม่ได้ | ใช้ AI เขียน + ฝึกอ่าน (ตามที่ตกลง) |
| 10 | ฝีมือ 2D | ใช้ alpha stamp แทนได้ ไม่วิกฤต |
| 11 | น้ำ / ของเหลว | ยังไม่เคยทำ |

**Blind spots อันตรายสุด (แก้ก่อน)**
1. **PBR inversion** — ใส่ roughness ในช่องที่ Unity คิดว่าเป็น smoothness -> เงา/ด้านกลับด้านทั้งฉาก
2. **เดา + ไม่วัด** — Humanoid ไม่ทำงาน กับ bake ไม่ติด คือปัญหาเดียวกัน (ไม่มี checklist + ไม่ isolate)
3. **Conflate padding** — แก้ผิดที่แล้วสรุปว่าเทคนิคใช้ไม่ได้

## 6. หลักคิด 9 ข้อที่ต้องติดตัว

1. Effect = 3 เลเยอร์ (Texture -> Emitter -> Shading)
2. วัดก่อนแก้ (Profiler / Frame Debugger) ไม่ใช่ "bake ใหม่ก่อน"
3. Data vs Color (sRGB ON/OFF) — ใช้ทุกโปรแกรม
4. Smoothness = 1 − Roughness และ packing ต่างกันต่อเอนจิน
5. Padding 2 ชนิด: texture padding != lightmap UV padding
6. Overdraw / texel density = ภาษากลางงานศิลปะเรียลไทม์
7. เพิ่มความดูดีด้วย shader/เทกซ์เจอร์ ไม่ใช่ poly
8. ลำดับงานศิลปะ: reference -> blockout -> detail -> polish (และ AI เป็นผู้ช่วยที่ต้องรีวิวได้)
9. **ความซ้ำ = ศัตรูของ environment** — trimsheet ใช้ซ้ำโดยธรรมชาติ ต้องกลบด้วย decal scatter · vertex blend · macro variation ไม่ใช่ทำ texture ใหม่ทุกชิ้น

## 7. แผนเรียน 5 วัน (+1 สำรอง)

| วัน | โมดูล | เรียนอะไร | ผลลัพธ์ที่ต้องได้ |
|---|---|---|---|
| 1 | M0 + M1a | ฐาน: sRGB data/color, PBR packing, Profiler เบื้องต้น, Shader Graph (Time · Tiling&Offset · Sample Texture 2D · Lerp · Step) | ต่อ shader ที่ขยับ UV ได้เอง + **Macro Variation** (กลบความซ้ำ) |
| 2 | M1b + M2a | Scene Depth + Depth Fade (soft particle), Particle System: emission / lifetime / size-color-alpha over lifetime / blend mode | **ไอน้ำรั่วจากท่อ** ใน Sector13 |
| 3 | M2b + M4 | ปรับไอน้ำให้เนียน + set dressing ด้วย Decal + **Decal Scatter (สุ่ม)** + **Vertex Color Blending** | **Decal Playground** (20 จุด) + พื้น/ผนังที่ดูไม่แบน |
| 4 | M3 + M5 | Flipbook / spritesheet / atlas padding, trigger logic (AI เขียน C#) | **ดาวขึ้นหัวตอนโดนตี** |
| 5 | M6 + M7 | น้ำ (fake): flow map + normal + ripple decal + splash | **น้ำหกเมื่อชนถัง** + วัด overdraw |
| 6 | สำรอง | เก็บงาน · ทำ effect ใหม่เองโดยไม่มีคนสอน (transfer test) · portfolio note | พิสูจน์ว่าต่อยอดได้ |

**ถ้าต้องตัด ให้ตัดตามลำดับนี้ (ท้ายสุดตัดก่อน):** M6 (น้ำ) -> M3 (flipbook) -> M5 (ดาว) -> M7 (optimize)
**ห้ามตัด:** M0, M1, M2, M4

**นิยามโมดูล (ปรับใหม่)**
- **M1a** — Shader Graph พื้นฐาน: Time · Tiling&Offset (Panner) · Sample Texture 2D · Lerp · Step/Smoothstep
- **M1b** — Scene Depth + Depth Fade (soft particle) · **Macro Variation** (ไทล์สเกลใหญ่ทับกลบความซ้ำ)
- **M2** — Particle System: emission · lifetime · size/color/alpha over lifetime · burst · blend mode
- **M3** — Flipbook / spritesheet / atlas · padding กันขอบ
- **M4** — Decal Playground · **Decal Scatter (สุ่ม offset/rotation/scale/texture)** · **Vertex Color Blending** (ProBuilder)
- **M5** — Feedback VFX ขั้นต่ำ: trigger logic (AI เขียน C#)
- **M6** — น้ำ (fake): flow map + normal + ripple decal + splash
- **M7** — Optimize: Profiler + Frame Debugger + overdraw

## 8. สิ่งที่จะไม่สอน (กันเข้าใจผิดเรื่องขอบเขต)

| ไม่สอน | เพราะ |
|---|---|
| เขียน C# เอง / HLSL / CustomRenderTexture | AI เขียน แล้วคุณอ่านตรวจ |
| Substance Designer | เลื่อน (แผนสำรอง: 3 สูตรเท่านั้น) |
| EmberGen / Houdini simulation | ไม่จำเป็นกับ 3 effect ที่ต้องการ |
| Rig / Animation retarget | คนละสาย (มีชีท 1 หน้าเก็บไว้) |
| LOD / chunk streaming ระดับสตูดิโอ | เกินงบเวลา |

## 9. วิธีเรียน และการวัดผล

1. ทุกโมดูลจบด้วย artifact จริงใน Sector13 (ไม่ใช่ tutorial)
2. ฝึกวินิจฉัยทุกครั้งที่พัง: **isolate -> วัด -> แก้**
3. Feynman rule: โค้ดที่ AI เขียน ต้องอธิบายกลับได้ก่อนไปต่อ
4. การบ้าน transfer ท้ายโมดูล (เช่น เรียนไอน้ำ -> ทำควันไฟเอง)
5. ผ่านเมื่อ: หยิบ effect ใหม่ 1 อันที่ไม่มีในคลิป แล้วทำได้เอง

---

## 10. Cheatsheet ติดตัว

### 10.1 sRGB (Import Settings)

| ตั้ง sRGB = OFF (data) | ตั้ง sRGB = ON (สี) |
|---|---|
| Normal · Metallic · Roughness/Smoothness · Height · AO · mask ใด ๆ | Albedo / Base Color · Emission |

### 10.2 Unity URP — Channel Packing

| ไฟล์ | ช่อง | ความหมาย |
|---|---|---|
| `*_AlbedoTransparency` | RGB | Base Color (sRGB ON) |
| | A | Opacity / Alpha |
| `*_MetallicSmoothness` | R | Metallic |
| | G, B | ไม่ใช้ |
| | A | Smoothness (sRGB OFF) |
| `*_Normal` | RGB | Tangent-space normal (sRGB OFF) |

**Smoothness = 1 − Roughness**

แปลง ORM (Unreal / glTF) -> Unity URP

| ORM | -> URP |
|---|---|
| R = AO | ย้ายไปช่อง Occlusion (URP อ่านช่อง G) |
| G = Roughness | invert (1−x) แล้วย้ายไปช่อง A |
| B = Metallic | ย้ายไปช่อง R ตรง ๆ |

### 10.3 Decal (ใช้งานจริงในโปรเจกต์นี้)

- Texture = **RGB (สี) + A (mask)** — สีของคราบอยู่ใน RGB, รูปร่างอยู่ใน alpha
- ต้องมี padding/dilation ที่ขอบ ไม่งั้นเกิด halo ตอน mip
- Import: sRGB **ON** · Alpha Is Transparency **ON** · BC7 (ลด banding)
- สร้างใน Painter: mesh **Plane** + template **PBR - Metallic Roughness Alpha-blend** -> Fill layer + alpha stamp ใน mask -> Export preset **Unity URP (Metallic Standard)**
- Painter alpha เป็น `.sbsar` — ลากเข้า Unity ไม่ได้ ต้อง export เป็น PNG ก่อน
- กับดัก: Screen Space decal ไม่เกาะ transparent object · projection เป็นกล่อง (คุม Projection Depth) · Draw Order / Opacity ตอนซ้อน

### 10.4 Effect = 3 เลเยอร์

- **L1 Texture** → ภาพ (billboard / flipbook)
- **L2 Emitter** → เกิด / ตาย / เคลื่อนที่ / ขนาด / สี ของอนุภาคหลายใบ (นี่คือสิ่งที่ทำให้เป็น "ควัน" ไม่ใช่ "รูป")
- **L3 Shading** → blend mode, soft particle (depth fade), alpha erosion, sorting

เทคนิคที่ต้องมี
- Fade over lifetime (คุม alpha/size/color over lifetime)
- Alpha erosion / dissolve (ใช้ noise ตัด alpha)
- Soft Particles: `fade = saturate((sceneDepth − pixelDepth) / fadeDistance)`

### 10.5 Performance

- ศัตรูตัวจริง = **Overdraw** (จำนวน layer x พิกเซลที่กินจอ) ไม่ใช่จำนวน particle
- GPU sim ถูกกว่าเมื่อ > ~100 particles และพฤติกรรมง่าย; CPU เมื่อต้อง query world data
- Emitter น้อยกว่า = ดีกว่า; ใช้ Lightweight/Stateless emitter; Ribbon แพง; Light renderer แพงมาก (ต้อง shadowless)
- LOD: ระยะไกลเปลี่ยน 3D particle -> flipbook
- เครื่องมือ: **Profiler** (CPU vs GPU bound) · **Frame Debugger / Rendering Debugger** (draw call, overdraw, Shader Complexity view)

### 10.6 ทำไมแมพดูแบน — เทคนิคกันความซ้ำ

Trimsheet ไม่ได้ทำให้สวย — มันลด material/draw call และคุม texel density ให้สม่ำเสมอ
ส่วน "ความแบน/ความซ้ำ" ต้องแก้ด้วยเทคนิคด้านล่าง (เรียงตามผลลัพธ์จริง)

| # | เทคนิค | ทำอะไร |
|---|---|---|
| 1 | **Decal scatter แบบ procedural** | สุ่มวางรอยด้วย rule (ผนังล่าง / ใกล้ท่อ / ใกล้ประตู) + สุ่ม offset·rotation·scale·texture จาก atlas |
| 2 | **Vertex color blending** | ผสม 2–4 material ใน 1 shader ด้วย vertex color (ทราย->หิน->โคลน, คราบตามความสูง/มุม) |
| 3 | **Prop / foliage overlay + AO + lighting** | วางของเล็กทับ + AO + แสง ให้มิติ |
| 4 | **Macro / micro variation + detail map** | ไทล์สเกลใหญ่ทับกลบ pattern + detail ความละเอียดสูงทับอีกชั้น |
| 5 | **Anti-tiling / texture bombing / hex tiling** | สุ่ม UV ต่อ tile -> ตาไม่จับความซ้ำ |
| 6 | **World-space / mesh decal** | decal ยึดกับโลก (URP: DBuffer) แทน Screen Space |

- ส่วนใหญ่ **ไม่ได้วาดมือในเอนจิน** — ที่เห็นคือ shader blend หลาย layer ด้วย mask/parameter
- ยกเว้น **vertex paint จริง ๆ** ซึ่งใช้กับ terrain/foliage/level art — Unity วาดได้ด้วย **ProBuilder** (มีในโปรเจกต์แล้ว)
- **ทำได้ใน 5 วัน:** ข้อ 1 · 2 · 3 · 4 · 6 (เวอร์ชันง่าย) — ตัดข้อ 5

### 10.7 Glossary ไทย–อังกฤษ

| ไทย | อังกฤษ |
|---|---|
| การ์ดแบน / ระนาบอนุภาค | Billboard / Particle Card |
| ภาพเคลื่อนไหวแบบต่อช่อง | Flipbook / Spritesheet / Texture Sheet Animation |
| การจางตามอายุ | Fade over lifetime |
| การกัดด้วย noise | Alpha Erosion / Dissolve |
| อนุภาคทับซ้อน | Overdraw |
| ความละเอียดเทกซ์เจอร์ต่อพื้นที่ | Texel density |
| การขยายขอบ texture | Padding / Dilation |
| อนุภาค (ระบบ) | Particle System / VFX Graph / Emitter |
| ของเหลวไหลแบบวางทับ | Flow map |
| สคริปต์วางรอย / ภาพซ้อนทับ | Decal |
| ตั้งค่าผิดแล้วเงา/ด้านกลับ | Smoothness/Roughness inversion |
| การดึงภาพรวมหลายช่องสี | Channel packing (ORM / MetallicSmoothness) |
| แผนเทกซ์เจอร์แบบแถบ (ใช้ซ้ำ) | Trimsheet |
| รวมเทกซ์เจอร์หลายชิ้นเป็นภาพเดียว | Texture atlas |
| สุ่มตำแหน่งลายกันตา | Texture bombing / Stochastic tiling |
| สุ่มหมุน UV กันลายเรียงตรงกัน | Anti-tiling / Hex tiling |
| ฉายลายจาก 3 แกน | Triplanar mapping |
| ผสมวัสดุด้วยสีจุดยอด | Vertex color blending / Vertex painting |
| แผนรอยยึดกับโลก | World-space decal (DBuffer) |
| แผนรอยแบบโพลิกอน | Mesh decal |
| ลายไทล์สเกลใหญ่กลบความซ้ำ | Macro variation |
| แผนลายละเอียดทับชั้นบน | Detail map |
| ผสมวัสดุเป็นชั้น | Material layering |
| กระจายด้วยกฎ/โพรซีเยอร์ | Procedural scatter / PCG |

---

## 11. สถานะโปรเจกต์ (reference)

**ทำไว้แล้ว**
- URP Decal Renderer Feature เปิดอยู่ (`Assets/Settings/PC_Renderer.asset`) — technique Automatic, Forward+, maxDrawDistance 1000
- Decal materials 6 ตัว (`Assets/Materials/Traces/`) ใช้ shader `Universal Render Pipeline/Decal`
- Decal textures 6 ใบ (`Assets/Textures/Traces/`) — import ถูกต้องแล้ว (sRGB ON, Alpha Is Transparency ON, 2048)
- ระบบ spawn decal ตอน runtime (`Scripts/Item/ItemPackage/PackageTraceApplier.cs`) + กลไก UV decal (`UVFlashlightController.cs`)
- Modular kit: Floor / Wall / Pillar / Jamb / Door frame + prop prefabs
- Texture pipeline: Substance Painter preset Unity URP (AlbedoTransparency / MetallicSmoothness / Normal / Emission)
- โค้ดทั้งหมด (FSM ผู้เล่น, combat, item) เขียนโดยคนอื่น/AI — บทบาทเจ้าของโปรเจกต์คือ วางโมเดล · ทำโมเดล · ทำสี

**ยังไม่มี**
- effect / particle ใด ๆ
- VFX Graph / Particle System prefab
- effect บรรยากาศ (ไอน้ำ ฝุ่น หมอก)

## 12. ค้าง / ยังไม่ได้ข้อสรุป

- ระดับ retopo / subdivision ของงานโมเดล
- สัดส่วนงานที่ทำเอง vs ทีม (โดยประมาณ)
- วันที่ส่งงานจริง (deadline)

---

## 13. ความคืบหน้า / ขั้นถัดไป

**เสร็จแล้ว**
- **M0 (บางส่วน)** — ไล่ตรวจ import settings ทั้งโปรเจกต์ (34 image assets) และแก้ 9 ไฟล์ที่ผิด
  - `MetallicSmoothness` 8 ไฟล์: `sRGBTexture 1 -> 0`
  - `Shelf_low_Tex_Marking2_Normal`: `sRGBTexture 1 -> 0` + `textureType 0 -> 1` (ต้นเหตุที่ Unity เด้ง "Fix Now")
  - ไม่พบตัวอื่นหลุด · decal และ albedo/emission ตั้งถูกอยู่แล้ว
- สร้างเอกสารแผนนี้

**ขั้นถัดไป**
1. ตั้ง Preset `TextureImporter` ใน Preset Manager กันปัญหา sRGB เกิดซ้ำ
2. **M1a** — Shader Graph พื้นฐาน + **Macro Variation**
3. **M4** — Decal Playground -> Decal Scatter (สุ่ม) -> Vertex Color Blending (ProBuilder)
4. **M2** — Particle System + soft particle -> ไอน้ำรั่วจากท่อ
