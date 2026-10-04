# สรุปการแก้ไขและปรับปรุง PlayerController เป็น Rigidbody Physics

---

## 1. ปัญหาเดิมและสาเหตุ (The Issue & Root Cause)

### 📌 ปัญหาที่พบ
* เมื่อผู้เล่นกดกระโดด (Spacebar) แล้ว**ไม่ได้กดปุ่มเดิน (WASD)** หรือปล่อยปุ่มเดินกลางอากาศ **ตัวละครจะลอยค้างกลางอากาศทันที**
* เมื่อยืนนิ่งอยู่เฉยๆ บางครั้งกดกระโดดไม่ได้

### 🔍 สาเหตุในโค้ดเดิม
1. **โค้ดเดิมใช้ `CharacterController`** ร่วมกับ Finite State Machine (`IdleState`, `WalkState`, `RunState`)
2. การคำนวณแรงโน้มถ่วง (`moveDirection.y += gravity * Time.deltaTime`) และคำสั่ง `controller.Move()` ถูกบรรจุไว้ในฟังก์ชัน `ApplyMovement()`
3. ใน `WalkState` และ `RunState` มีการเรียก `ApplyMovement()` ปกติ แต่ใน **`IdleState` ไม่มีการเรียก `ApplyMovement()` เลย**
4. เมื่อกระโดดแล้วปล่อยปุ่มเดิน ระบบจะสลับเข้าสู่ `IdleState` ทันที ทำให้คำสั่ง `controller.Move()` หยุดทำงาน ตัวละครจึงถูกแช่แข็งค้างเติ่งอยู่กลางอากาศ

---

## 2. แนวทางการแก้ไขที่เลือก (Selected Approach)

### ✅ ตัวเลือก A: เปลี่ยนไปใช้ Rigidbody + CapsuleCollider (Physics-Based)
เปลี่ยนจากการควบคุมด้วย `CharacterController` ไปเป็นระบบฟิสิกส์ **Rigidbody** อย่างเต็มรูปแบบ
* **เหตุผล:** โปรเจกต์นี้มีระบบกล่องสินค้า พัสดุ และการหยิบ/วางของ (`ItemObject`, `PackageBox`) ที่เป็นวัตถุฟิสิกส์ การให้ตัวละครใช้ Rigidbody จะทำให้การชน การผลัก และแรงโน้มถ่วงทำงานร่วมกันได้อย่างสมจริงตามเอนจิน PhysX ของ Unity

---

## 3. รายละเอียดการแก้ไขสคริปต์ (Script Changes)

### 3.1 [`PlayerController.cs`](file:///d:/homework/Thesis-Sector13/Assets/Scripts/Player%20Scripts/PlayerController.cs)
* **เปลี่ยน Component Requirements:**
  ```csharp
  [RequireComponent(typeof(Rigidbody))]
  [RequireComponent(typeof(CapsuleCollider))]
  [RequireComponent(typeof(PlayerStats))]
  ```
* **ตั้งค่า Rigidbody อัตโนมัติใน `Awake()`:**
  * `rb.freezeRotation = true;` (ป้องกันตัวละครล้มกลิ้งเมื่อชนสิ่งกีดขวาง)
  * `rb.collisionDetectionMode = CollisionDetectionMode.Continuous;` (ป้องกันตัวละครเดินทะลุกำแพง)
  * `rb.interpolation = RigidbodyInterpolation.Interpolate;` (ทำให้การขยับและกล้องสมูท ไม่กระตุก)
  * ใส่ `PhysicsMaterial` ไร้แรงเสียดทาน (Frictionless) ให้กับ CapsuleCollider อัตโนมัติ เพื่อป้องกันตัวละครติดขอบผนังเวลาเดินเบียดหรือกระโดดชน
* **ระบบตรวจจับพื้น (Ground Check):**
  * ใช้ `Physics.SphereCastAll` ยิงตรวจจับพื้นรอบฐาน Capsule Collider
  * คัดกรองไม่ให้ตรวจจับโดน Collider ของตัวผู้เล่นเอง
* **การคำนวณการเคลื่อนที่ (Movement & Jump):**
  * ดักจับการกดปุ่มกระโดดใน `Update()` เพื่อไม่ให้พลาด Frame Input
  * คำนวณความเร็วใน `FixedUpdate()`:
    * ใช้ **`rb.linearVelocity`** (มาตรฐานใหม่ของ Unity 6)
    * แกนแนวนอน `(X, Z)`: เคลื่อนที่ตามปุ่ม WASD และความเร็วของ State ปัจจุบัน
    * แกนแนวดิ่ง `(Y)`: ปล่อยให้แรงโน้มถ่วงของ Unity ดึงตัวละครลงมาอย่างต่อเนื่องตามธรรมชาติ
    * เมื่อกระโดด: ใช้สูตรฟิสิกส์ $v_y = \sqrt{2 \cdot g \cdot h}$ เพื่อให้ได้ความสูงการกระโดดที่แม่นยำ

### 3.2 [`IdleState.cs`](file:///d:/homework/Thesis-Sector13/Assets/Scripts/Player%20Scripts/Player%20State/IdleState.cs)
* เพิ่มคำสั่ง:
  ```csharp
  player.ApplyMovement(0f);
  ```
  เพื่อกำหนดให้เป้าหมายความเร็วแนวราบเป็น 0 ขณะยืนนิ่ง แต่ระบบฟิสิกส์แนวดิ่งยังคงทำงานตามปกติ

---

## 4. การปรับปรุง GameObject ใน Scene ผ่าน Unity MCP

ในฉาก **`Assets/Scenes/Script Scrence.unity`** บน GameObject **`Player`**:
1. **ลบ** Component `CharacterController` ออกเรียบร้อยแล้ว
2. **เพิ่ม** Component `Rigidbody`
3. ตั้งค่า Rigidbody Properties:
   * `useGravity = true`
   * `freezeRotation = true`
   * `collisionDetectionMode = Continuous`
   * `interpolation = Interpolate`
4. บันทึก Scene เรียบร้อยแล้ว

---

## 5. ผลลัพธ์หลังการปรับปรุง (Results)

| ฟังก์ชัน | ก่อนแก้ไข | หลังแก้ไข |
| :--- | :--- | :--- |
| **กระโดดแล้วปล่อยปุ่มเดิน** | ❌ ตัวละครลอยค้างกลางอากาศ | ✅ ตัวละครตกลงสู่พื้นตามแรงโน้มถ่วงอย่างราบรื่น |
| **กระโดดขณะยืนนิ่ง (Idle)** | ❌ กระโดดไม่ได้ | ✅ กด Spacebar กระโดดได้ตามปกติ |
| **การชนและผลักดันวัตถุ** | ⚠️ ต้องเขียนสคริปต์เสริม | ✅ ผลักกล่องสินค้าและพัสดุฟิสิกส์ได้สมจริง |
| **การเดินเบียดกำแพง** | ⚠️ อาจมีอาการติดผนัง | ✅ มี Frictionless Material ป้องกันการติดผนัง |

---

## 6. พารามิเตอร์ที่สามารถปรับแต่งได้ใน Inspector

* **`walkSpeed`** (ค่าเริ่มต้น: 4) - ความเร็วเดิน
* **`runSpeed`** (ค่าเริ่มต้น: 7) - ความเร็ววิ่ง
* **`jumpHeight`** (ค่าเริ่มต้น: 1.2) - ความสูงในการกระโดด (เมตร)
* **`groundCheckDistance`** (ค่าเริ่มต้น: 0.15) - ระยะเผื่อในการเช็คพื้นใต้ฝ่าเท้า
* **`groundMask`** (ค่าเริ่มต้น: Everything) - เลเยอร์ที่นับว่าเป็นพื้น
