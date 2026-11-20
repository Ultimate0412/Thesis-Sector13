using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerPickupSystem : MonoBehaviour
{
    public static PlayerPickupSystem Instance { get; private set; }

    [Header("Pickup Settings")]
    public float pickupDistance = 3f;
    public LayerMask itemLayer;         // เลเยอร์สำหรับสินค้าที่หยิบได้
    public LayerMask dropPointLayer;    // เลเยอร์สำหรับจุดวางของ (Drop Point)
    public Transform holdPosition;      // จุดยึดสินค้าหน้ากล้อง

    [HideInInspector] public GameObject heldObject;
    private float heldWeight = 0f;      // น้ำหนักที่จองไว้กับ PlayerStats สำหรับ heldObject
    private PlayerStats playerStats;
    private Camera playerCam;
    private BaseDropPoint currentHoveredDropPoint;

    // collider ของ Player และของที่ถืออยู่ (ใช้ปิดการชนกันชั่วคราวตอนถือ)
    private Collider[] playerColliders;
    private readonly List<Collider> heldColliders = new List<Collider>();

    [Header("Extraction Hold Settings")]
    [Tooltip("ระยะเวลากดปุ่ม Q ค้างเพื่อหยิบสินค้าออกจากกล่อง (วินาที)")]
    public float holdExtractDuration = 0.5f;
    private float currentHoldTimer = 0f;
    private bool isHoldingQ = false;
    private PackageBox targetHoldBox = null;

    [Header("Pickup Animation")]
    [Tooltip("เวลารอก่อนของเข้ามือ (วินาที) ถ้าเป็น 0 จะใช้ความยาวของ clip Pick อัตโนมัติ")]
    public float pickUpDelayOverride = 0f;
    private PlayerAnimationController animController;
    private bool isPickingUp = false;

    [Header("Throw Settings")]
    [Tooltip("แรงขว้างวัตถุหรือศัตรูไปข้างหน้าเมื่อกดคลิกขวา (RMB)")]
    public float throwForce = 12f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        playerColliders = GetComponentsInChildren<Collider>(true);
    }

    private void Start()
    {
        playerStats = GetComponent<PlayerStats>();
        playerCam = GetComponentInChildren<Camera>();
        animController = GetComponent<PlayerAnimationController>();
    }

    private void Update()
    {
        HandleDropPointHover();
        HandleHoldQExtraction();

        if (Input.GetKeyDown(KeyCode.Q) && !isPickingUp)
        {
            if (heldObject == null)
            {
                // ถ้าเล็งไปที่กล่องพัสดุที่เปิดอยู่และมีของข้างใน ให้เข้าสู่โหมดกด Q ค้างเพื่อหยิบของ (ป้องกันการกดผิด)
                PackageBox openBox = GetTargetOpenPackageBoxWithItem();
                if (openBox != null)
                {
                    StartHoldQ(openBox);
                }
                else
                {
                    StartPickUp();
                }
            }
            else
            {
                TryDropOrPlace();
            }
        }

        // คลิกขวา (RMB) เพื่อขว้างสิ่งที่ถืออยู่ (เช่น ขว้างศัตรูที่ติดสตั๊นลงจุดทำลาย)
        if (Input.GetMouseButtonDown(1) && heldObject != null)
        {
            if (InspectionTablet.Instance == null || !InspectionTablet.Instance.IsTabletHeld)
            {
                ThrowHeldObject();
            }
        }
    }

    private PackageBox GetTargetOpenPackageBoxWithItem()
    {
        if (playerCam == null) return null;

        Ray ray = new Ray(playerCam.transform.position, playerCam.transform.forward);
        LayerMask combinedMask = itemLayer | dropPointLayer;
        if (Physics.Raycast(ray, out RaycastHit hit, pickupDistance, combinedMask))
        {
            PackageBox box = hit.collider.GetComponentInParent<PackageBox>();
            if (box != null && box.isOpen && !box.isItemExtracted)
            {
                return box;
            }
        }
        return null;
    }

    private void StartHoldQ(PackageBox box)
    {
        isHoldingQ = true;
        targetHoldBox = box;
        currentHoldTimer = 0f;
    }

    private void HandleHoldQExtraction()
    {
        if (!isHoldingQ) return;

        // หากผู้เล่นถือของอยู่ หรือปล่อยปุ่ม Q ก่อนครบกำหนด ให้ยกเลิกการหยิบของทันที
        if (heldObject != null || Input.GetKeyUp(KeyCode.Q))
        {
            ResetHoldQ();
            return;
        }

        // หากยังกดปุ่ม Q ค้างอยู่
        if (Input.GetKey(KeyCode.Q))
        {
            PackageBox currentBox = GetTargetOpenPackageBoxWithItem();
            if (currentBox != null && currentBox == targetHoldBox)
            {
                currentHoldTimer += Time.deltaTime;
                float progress = Mathf.Clamp01(currentHoldTimer / holdExtractDuration);

                if (CrosshairUI.Instance != null)
                {
                    CrosshairUI.Instance.SetHoldProgress(progress);
                }

                if (currentHoldTimer >= holdExtractDuration)
                {
                    ExtractItemFromBox(targetHoldBox);
                    ResetHoldQ();
                }
            }
            else
            {
                // ผู้เล่นหันเป้าเล็งหนีออกจากกล่อง
                ResetHoldQ();
            }
        }
        else
        {
            ResetHoldQ();
        }
    }

    private void ResetHoldQ()
    {
        isHoldingQ = false;
        targetHoldBox = null;
        currentHoldTimer = 0f;

        if (CrosshairUI.Instance != null)
        {
            CrosshairUI.Instance.SetHoldProgress(0f);
        }
    }

    /// <summary>
    /// จองน้ำหนักสำหรับ object ที่จะถือ — คืน false ถ้าเกินโควต้า
    /// weight ถูก set เสมอ เพื่อให้ caller เอาไปใช้ย้อนของได้
    /// </summary>
    private bool TryTake(GameObject obj, out float weight)
    {
        weight = 0f;
        if (obj == null)
        {
            Debug.LogWarning("[PlayerPickupSystem] TryTake: obj เป็น null — ข้ามการจองน้ำหนัก");
            return false;
        }

        weight = Carryable.WeightOf(obj);

        if (Carryable.Resolve(obj) == null)
            Debug.LogWarning($"[PlayerPickupSystem] '{obj.name}' ไม่มี ICarryable — คิดน้ำหนัก 0 kg");

        if (!playerStats.CanCarry(weight)) return false;

        playerStats.AddWeight(weight);
        heldWeight = weight;
        return true;
    }

    /// <summary>คืนน้ำหนักที่จองไว้ — ต้องเรียกทุกครั้งที่ของออกจากมือ</summary>
    private void ReleaseHeldWeight()
    {
        // ของออกจากมือแล้ว คืนการชนกับ Player กลับมาเป็นปกติ
        RestoreHeldCollisions();

        if (heldWeight > 0f) playerStats.RemoveWeight(heldWeight);
        heldWeight = 0f;
    }

    /// <summary>
    /// ปิดการชนระหว่าง Player กับของที่กำลังถือ
    /// ป้องกัน collider ของของที่ถือ (เช่น capsule ของศัตรู) ดัน Player ถอยหลัง
    /// </summary>
    private void IgnoreHeldCollisions(GameObject held)
    {
        RestoreHeldCollisions();

        if (held == null || playerColliders == null) return;

        foreach (Collider heldCol in held.GetComponentsInChildren<Collider>(true))
        {
            if (heldCol == null) continue;

            foreach (Collider playerCol in playerColliders)
            {
                if (playerCol != null) Physics.IgnoreCollision(playerCol, heldCol, true);
            }

            heldColliders.Add(heldCol);
        }
    }

    /// <summary>
    /// ยึดของเข้าจุดถือ พร้อมใช้ HeldAnchor ของวัตถุถ้ามี
    /// (ของใหญ่ เช่น Enemy_Hammer จะได้วางเยื้องออกไปไม่บังสายตา)
    /// </summary>
    private void AttachHeldObjectToHand()
    {
        if (heldObject == null) return;

        HeldAnchor anchor = heldObject.GetComponent<HeldAnchor>();
        Transform anchorPoint = anchor != null ? anchor.anchorPoint : null;

        if (anchorPoint != null && anchorPoint.IsChildOf(heldObject.transform))
        {
            // ใช้ EmptyObject บนตัววัตถุเป็นจุดจับ:
            // คำนวณ offset ก่อน reparent แล้ววางวัตถุให้ Empty ทับจุดถือของผู้เล่นพอดี
            Vector3 gripLocalPos = heldObject.transform.InverseTransformPoint(anchorPoint.position);
            Quaternion gripLocalRot = Quaternion.Inverse(heldObject.transform.rotation) * anchorPoint.rotation;

            heldObject.transform.SetParent(holdPosition, false);
            heldObject.transform.localRotation = Quaternion.Inverse(gripLocalRot);
            heldObject.transform.localPosition = -(heldObject.transform.localRotation * gripLocalPos);
        }
        else if (anchorPoint != null)
        {
            // อ้างอิง Transform ภายนอก (เช่น Empty ใต้ Player) → เกาะที่ Empty นั้นโดยตรง
            heldObject.transform.SetParent(anchorPoint, false);
            heldObject.transform.localPosition = Vector3.zero;
            heldObject.transform.localRotation = Quaternion.identity;
        }
        else if (anchor != null)
        {
            heldObject.transform.SetParent(holdPosition, false);
            heldObject.transform.localPosition = anchor.localPosition;
            heldObject.transform.localRotation = Quaternion.Euler(anchor.localEulerAngles);
        }
        else
        {
            heldObject.transform.SetParent(holdPosition, false);
            heldObject.transform.localPosition = Vector3.zero;
            heldObject.transform.localRotation = Quaternion.identity;
        }

        IgnoreHeldCollisions(heldObject);
    }

    /// <summary>คืนการชนระหว่าง Player กับของที่เคยถือ (เรียกตอนของออกจากมือ)</summary>
    private void RestoreHeldCollisions()
    {
        if (heldColliders.Count == 0 || playerColliders == null) return;

        foreach (Collider heldCol in heldColliders)
        {
            if (heldCol == null) continue;

            foreach (Collider playerCol in playerColliders)
            {
                if (playerCol != null) Physics.IgnoreCollision(playerCol, heldCol, false);
            }
        }

        heldColliders.Clear();
    }

    /// <summary>เก็บเครื่องมือที่ถืออยู่ (แท็บเล็ต/อาวุธ) เมื่อมือต้องว่างสำหรับหยิบของ</summary>
    private static void HolsterPlayerTools()
    {
        if (InspectionTablet.Instance != null) InspectionTablet.Instance.ForceHolster();
        if (PlayerEquipment.Instance != null) PlayerEquipment.Instance.ForceHolster();
    }

    /// <summary>
    /// วางของที่ถืออยู่ลงพื้นทันที เพื่อสลับไปถือเครื่องมืออื่น (แท็บเล็ต/อาวุธ)
    /// ใช้เมื่อผู้เล่นกดปุ่มเครื่องมือขณะมือไม่ว่าง — ถือได้ทีละอย่างเท่านั้น
    /// คืน true เมื่อมือว่างแล้ว
    /// </summary>
    public bool PutAwayHeldObject()
    {
        if (heldObject == null) return true;

        DropObjectToFloor();
        return heldObject == null;
    }

    private void ExtractItemFromBox(PackageBox box)
    {
        if (box == null || !box.isOpen || box.isItemExtracted) return;

        GameObject item = box.ExtractInnerItem();
        if (item != null)
        {
            bool canAdd = TryTake(item, out _);
            if (canAdd)
            {
                heldObject = item;
                HolsterPlayerTools();

                Rigidbody rb = heldObject.GetComponent<Rigidbody>();
                if (rb != null) { rb.isKinematic = true; }

                Collider col = heldObject.GetComponent<Collider>();
                if (col != null) { col.enabled = true; }

                AttachHeldObjectToHand();

                Debug.Log($"[PlayerPickupSystem] หยิบสินค้า '{heldObject.name}' ออกจากกล่องสำเร็จ");
            }
            else
            {
                // ถ้าน้ำหนักเกิน ให้ใส่ของกลับเข้ากล่อง
                box.PutItemBack(item);
                Debug.LogWarning("[PlayerPickupSystem] น้ำหนักเกิน! ไม่สามารถหยิบสินค้าออกจากกล่องได้");
            }
        }
    }

    /// <summary>
    /// เริ่มหยิบของ: เล่นอนิเมชัน Pick ก่อน แล้วรอจนจบจึงย้ายของเข้ามือ
    /// ใช้ Ray ณ ตอนที่กดปุ่ม เพื่อให้หยิบของชิ้นที่เล็งอยู่ แม้ผู้เล่นเดินต่อระหว่างอนิเมชัน
    /// </summary>
    private void StartPickUp()
    {
        Ray ray = new Ray(playerCam.transform.position, playerCam.transform.forward);
        if (!HasPickUpTarget(ray)) return; // ไม่มีอะไรให้หยิบ ไม่ต้องเล่นอนิเมชัน
        StartCoroutine(PickUpRoutine(ray));
    }

    private IEnumerator PickUpRoutine(Ray ray)
    {
        isPickingUp = true;

        float wait = pickUpDelayOverride;
        if (animController != null)
        {
            float clipLength = animController.TriggerPick();
            if (wait <= 0f) wait = clipLength;
        }

        if (wait > 0f) yield return new WaitForSeconds(wait);

        if (heldObject == null) TryPickUp(ray);
        isPickingUp = false;
    }

    private bool HasPickUpTarget(Ray ray)
    {
        LayerMask combinedMask = itemLayer | dropPointLayer;
        if (Physics.Raycast(ray, out RaycastHit hit, pickupDistance, combinedMask)) return true;

        if (Physics.Raycast(ray, out RaycastHit enemyHit, pickupDistance))
        {
            EnemyBase e = enemyHit.collider.GetComponentInParent<EnemyBase>();
            if (e == null) e = enemyHit.collider.GetComponent<EnemyBase>();
            return e != null && e.IsStunned;
        }
        return false;
    }

    private void TryPickUp(Ray ray)
    {
        LayerMask combinedMask = itemLayer | dropPointLayer;
        bool hasHit = Physics.Raycast(ray, out RaycastHit hit, pickupDistance, combinedMask);
        if (!hasHit)
        {
            // หากไม่โดน Item หรือ DropPoint ให้ตรวจสอบว่าเล็งโดนศัตรูที่ติดสถานะสตั๊นอยู่หรือไม่
            if (Physics.Raycast(ray, out RaycastHit enemyHit, pickupDistance))
            {
                EnemyBase e = enemyHit.collider.GetComponentInParent<EnemyBase>();
                if (e == null) e = enemyHit.collider.GetComponent<EnemyBase>();
                if (e != null && e.IsStunned)
                {
                    hit = enemyHit;
                    hasHit = true;
                }
            }
        }

        if (hasHit)
        {
            // 1. ตรวจสอบว่าเล็งโดน BaseDropPoint โดยตรงหรือไม่ (เช่น โต๊ะจัดส่ง, จุดวางของ)
            BaseDropPoint dropPoint = hit.collider.GetComponentInParent<BaseDropPoint>();
            if (dropPoint != null && dropPoint.currentPlacedItem != null)
            {
                PickUpItemFromDropPoint(dropPoint);
                return;
            }

            // 2. ตรวจสอบวัตถุเป้าหมาย
            GameObject targetItem = hit.collider.gameObject;
            PackageBox parentBox = targetItem.GetComponentInParent<PackageBox>();

            // ถ้าวัตถุเป้าหมายวางอยู่บน DropPoint (เช่น ใน Storage หรือ Shipping)
            BaseDropPoint itemDropPoint = BaseDropPoint.GetDropPointHolding(targetItem);
            if (itemDropPoint == null && parentBox != null)
            {
                itemDropPoint = BaseDropPoint.GetDropPointHolding(parentBox.gameObject);
            }
            if (itemDropPoint == null)
            {
                itemDropPoint = targetItem.GetComponentInParent<BaseDropPoint>();
            }

            if (itemDropPoint != null)
            {
                PickUpItemFromDropPoint(itemDropPoint);
                return;
            }

            // 3. ตรวจสอบศัตรูที่ติดสถานะสตั๊น (EnemyBase)
            EnemyBase enemy = targetItem.GetComponentInParent<EnemyBase>();
            if (enemy == null) enemy = targetItem.GetComponent<EnemyBase>();

            if (enemy != null && enemy.IsStunned)
            {
                bool canAdd = TryTake(enemy.gameObject, out _);
                if (canAdd)
                {
                    heldObject = enemy.gameObject;
                    HolsterPlayerTools();

                    enemy.OnPickedUpByPlayer(this);

                    AttachHeldObjectToHand();

                    Debug.Log($"[PlayerPickupSystem] ยกศัตรู '{enemy.name}' ขึ้นมาถือสำเร็จ!");
                    return;
                }
                else
                {
                    Debug.LogWarning("[PlayerPickupSystem] แบกน้ำหนักไม่ไหว! ไม่สามารถยกศัตรูได้");
                    return;
                }
            }

            // 4. หยิบสินค้าทั่วไป (เช่น บนพื้น หรือบนสปาวน์เนอร์)
            ItemObject itemComponent = targetItem.GetComponent<ItemObject>();
            if (itemComponent == null)
            {
                itemComponent = targetItem.GetComponentInParent<ItemObject>();
                if (itemComponent != null)
                {
                    targetItem = itemComponent.gameObject;
                }
            }

            if (itemComponent != null)
            {
                bool canAdd = TryTake(targetItem, out _);
                if (canAdd)
                {
                    heldObject = targetItem;
                    HolsterPlayerTools();

                    Rigidbody rb = heldObject.GetComponent<Rigidbody>();
                    if (rb != null) { rb.isKinematic = true; }

                    Collider col = heldObject.GetComponent<Collider>();
                    if (col != null) { col.enabled = true; }

                    AttachHeldObjectToHand();
                }
            }
        }
    }

    private void TryDropOrPlace()
    {
        // 1. ถ้ามองจุดวางของอยู่ (DropPoint)
        if (currentHoveredDropPoint != null && currentHoveredDropPoint.currentPlacedItem == null)
        {
            if (!currentHoveredDropPoint.CanPlaceHere())
            {
                Debug.LogWarning("ไม่สามารถวางตรงนี้ได้!");
                return;
            }

            float weight = heldWeight;
            ReleaseHeldWeight();
            currentHoveredDropPoint.PlaceItem(heldObject, weight);

            heldObject = null;
            ClearHoveredDropPoint();
            return;
        }

        // 2. ถ้าถือสินค้าอยู่ แล้วเล็งไปที่ PackageBox ที่เปิดอยู่และว่างเปล่า (ใส่สินค้ากลับเข้ากล่อง)
        Ray ray = new Ray(playerCam.transform.position, playerCam.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, pickupDistance))
        {
            PackageBox targetBox = hit.collider.GetComponentInParent<PackageBox>();
            if (targetBox != null && targetBox.CanPutItemBack(heldObject))
            {
                ReleaseHeldWeight();

                targetBox.PutItemBack(heldObject);
                heldObject = null;
                return;
            }
        }

        // 3. ถ้าไม่ได้มองจุดวางหรือกล่อง ให้ทิ้งลงพื้น
        DropObjectToFloor();
    }

    private void DropObjectToFloor()
    {
        if (heldObject != null)
        {
            ReleaseHeldWeight();

            EnemyBase enemy = heldObject.GetComponent<EnemyBase>();
            if (enemy == null) enemy = heldObject.GetComponentInParent<EnemyBase>();

            if (enemy != null)
            {
                GameObject enemyObj = heldObject;
                heldObject = null;
                enemyObj.transform.SetParent(null);
                enemy.OnThrownByPlayer(Vector3.down * 1.5f);
                return;
            }

            Rigidbody rb = heldObject.GetComponent<Rigidbody>();
            if (rb != null) 
            { 
                rb.isKinematic = false; 
                NoGravityEvent.ActiveInstance?.RegisterRigidbody(rb);
            }

            Collider col = heldObject.GetComponent<Collider>();
            if (col != null) { col.enabled = true; }

            heldObject.transform.SetParent(null);
            heldObject = null;
        }
    }

    /// <summary>
    /// ขว้างวัตถุหรือศัตรูที่ถืออยู่ไปข้างหน้าด้วยแรงพุ่ง (RMB)
    /// </summary>
    public void ThrowHeldObject()
    {
        if (heldObject == null) return;

        GameObject objToThrow = heldObject;
        heldObject = null;
        ReleaseHeldWeight();

        EnemyBase enemy = objToThrow.GetComponent<EnemyBase>();
        if (enemy == null) enemy = objToThrow.GetComponentInParent<EnemyBase>();

        if (enemy != null)
        {
            objToThrow.transform.SetParent(null);
            Vector3 throwVelocity = playerCam.transform.forward * throwForce + Vector3.up * 1.8f;
            enemy.OnThrownByPlayer(throwVelocity);
            Debug.Log($"[PlayerPickupSystem] ขว้างศัตรู '{objToThrow.name}' ไปข้างหน้า!");
            return;
        }

        objToThrow.transform.SetParent(null);

        Rigidbody rb = objToThrow.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = playerCam.transform.forward * throwForce + Vector3.up * 1.5f;
            NoGravityEvent.ActiveInstance?.RegisterRigidbody(rb);
        }

        Collider col = objToThrow.GetComponent<Collider>();
        if (col != null) { col.enabled = true; }

        Debug.Log($"[PlayerPickupSystem] ขว้างวัตถุ '{objToThrow.name}' สำเร็จ!");
    }

    /// <summary>
    /// บังคับให้ผู้เล่นปล่อยวัตถุในมือ (เช่น เมื่อศัตรูหายสตั๊นแล้วสลัดหลุด)
    /// </summary>
    public void ForceDropObject(GameObject target)
    {
        if (heldObject == null || heldObject != target) return;

        ReleaseHeldWeight();

        Rigidbody rb = heldObject.GetComponent<Rigidbody>();
        if (rb != null) 
        { 
            rb.isKinematic = false; 
            NoGravityEvent.ActiveInstance?.RegisterRigidbody(rb);
        }

        Collider col = heldObject.GetComponent<Collider>();
        if (col != null) { col.enabled = true; }

        heldObject.transform.SetParent(null);
        heldObject = null;

        Debug.Log($"[PlayerPickupSystem] วัตถุ '{target.name}' หลุดจากมือผู้เล่น!");
    }

    private void HandleDropPointHover()
    {
        Ray ray = new Ray(playerCam.transform.position, playerCam.transform.forward);

        // ยิงเช็คเลเยอร์ DropPoint
        if (Physics.Raycast(ray, out RaycastHit hit, pickupDistance, dropPointLayer))
        {
            BaseDropPoint detectedDropPoint = hit.collider.GetComponentInParent<BaseDropPoint>();

            if (heldObject != null && detectedDropPoint != null && detectedDropPoint.currentPlacedItem == null)
            {
                if (currentHoveredDropPoint != detectedDropPoint)
                {
                    ClearHoveredDropPoint();
                    currentHoveredDropPoint = detectedDropPoint;
                    currentHoveredDropPoint.ShowHologram(heldObject);
                    Debug.Log("[Debug Hologram] สั่งแสดง Hologram สำเร็จ!");
                }
                return;
            }
        }
        else
        {
            // ถ้าผู้เล่นถือของอยู่แต่กวาดสายตาแล้วไม่โดน Layer DropPoint จะขึ้นเตือนตรงนี้
            if (heldObject != null && Input.GetMouseButton(1)) // หรือเช็คตอนถือของ
            {
                // Debug.Log("Raycast ยังไม่โดน Layer DropPoint เช็ค Layer ดูอีกทีนะ");
            }
        }

        ClearHoveredDropPoint();
    }

    private void ClearHoveredDropPoint()
    {
        if (currentHoveredDropPoint != null)
        {
            currentHoveredDropPoint.HideHologram();
            currentHoveredDropPoint = null;
        }
    }
    // เพิ่มฟังก์ชันนี้เข้าไปใน PlayerPickupSystem.cs
    public void PickUpItemFromDropPoint(BaseDropPoint dropPoint)
    {
        if (heldObject != null) return; // ถ้ามือถือของอยู่แล้ว ให้หยิบเพิ่มไม่ได้

        GameObject itemToTake = dropPoint.RemoveItem();
        if (itemToTake != null)
        {
            // เช็คโควต้าน้ำหนักผู้เล่น
            bool canAdd = TryTake(itemToTake, out float weight);
            if (canAdd)
            {
                heldObject = itemToTake;
                HolsterPlayerTools();

                Rigidbody rb = heldObject.GetComponent<Rigidbody>();
                if (rb != null) { rb.isKinematic = true; }

                Collider col = heldObject.GetComponent<Collider>();
                if (col != null) { col.enabled = true; } // เปิด Collider กลับมา

                AttachHeldObjectToHand();

                Debug.Log("หยิบวัตถุออกจาก Drop Point กลับขึ้นมือสำเร็จ");
            }
            else
            {
                // ถ้าแบกน้ำหนักไม่ไหว ให้วางกลับที่เดิม
                dropPoint.PlaceItem(itemToTake, weight);
                Debug.Log("น้ำหนักเกิน ไม่สามารถหยิบขึ้นมาได้!");
            }
        }
    }
    public GameObject GetHeldObject()
    {
        return heldObject;
    }
}
