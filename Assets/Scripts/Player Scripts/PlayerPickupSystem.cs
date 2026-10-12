using UnityEngine;

public class PlayerPickupSystem : MonoBehaviour
{
    [Header("Pickup Settings")]
    public float pickupDistance = 3f;
    public LayerMask itemLayer;         // เลเยอร์สำหรับสินค้าที่หยิบได้
    public LayerMask dropPointLayer;    // เลเยอร์สำหรับจุดวางของ (Drop Point)
    public Transform holdPosition;      // จุดยึดสินค้าหน้ากล้อง

    [HideInInspector] public GameObject heldObject;
    private PlayerStats playerStats;
    private Camera playerCam;
    private BaseDropPoint currentHoveredDropPoint;

    [Header("Extraction Hold Settings")]
    [Tooltip("ระยะเวลากดปุ่ม Q ค้างเพื่อหยิบสินค้าออกจากกล่อง (วินาที)")]
    public float holdExtractDuration = 0.5f;
    private float currentHoldTimer = 0f;
    private bool isHoldingQ = false;
    private PackageBox targetHoldBox = null;

    [Header("Throw Settings")]
    [Tooltip("แรงขว้างวัตถุหรือศัตรูไปข้างหน้าเมื่อกดคลิกขวา (RMB)")]
    public float throwForce = 12f;

    private void Start()
    {
        playerStats = GetComponent<PlayerStats>();
        playerCam = GetComponentInChildren<Camera>();
    }

    private void Update()
    {
        HandleDropPointHover();
        HandleHoldQExtraction();

        if (Input.GetKeyDown(KeyCode.Q))
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
                    TryPickUp();
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

    private void ExtractItemFromBox(PackageBox box)
    {
        if (box == null || !box.isOpen || box.isItemExtracted) return;

        GameObject item = box.ExtractInnerItem();
        if (item != null)
        {
            ItemObject itemComp = item.GetComponent<ItemObject>();
            if (itemComp == null) itemComp = item.GetComponentInParent<ItemObject>();
            float weight = itemComp != null ? itemComp.itemWeight : 5f;

            bool canAdd = playerStats.AddWeight(weight);
            if (canAdd)
            {
                heldObject = item;
                if (InspectionTablet.Instance != null) InspectionTablet.Instance.ForceHolster();

                Rigidbody rb = heldObject.GetComponent<Rigidbody>();
                if (rb != null) { rb.isKinematic = true; }

                Collider col = heldObject.GetComponent<Collider>();
                if (col != null) { col.enabled = true; }

                heldObject.transform.SetParent(holdPosition);
                heldObject.transform.localPosition = Vector3.zero;
                heldObject.transform.localRotation = Quaternion.identity;

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

    private void TryPickUp()
    {
        Ray ray = new Ray(playerCam.transform.position, playerCam.transform.forward);
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
                bool canAdd = playerStats.AddWeight(enemy.enemyWeight);
                if (canAdd)
                {
                    heldObject = enemy.gameObject;
                    if (InspectionTablet.Instance != null) InspectionTablet.Instance.ForceHolster();

                    enemy.OnPickedUpByPlayer(this);

                    heldObject.transform.SetParent(holdPosition);
                    heldObject.transform.localPosition = Vector3.zero;
                    heldObject.transform.localRotation = Quaternion.identity;

                    Debug.Log($"[PlayerPickupSystem] ยกศัตรู '{enemy.name}' ขึ้นมาถือสำเร็จ!");
                    return;
                }
                else
                {
                    Debug.LogWarning("[PlayerPickupSystem] แบกน้ำหนักไม่ไหว! ไม่สามารถยกศัตรูได้");
                    return;
                }
            }

            // 3.5 ตรวจสอบอาวุธ (MeleeWeapon เช่น ท่อนไม้)
            MeleeWeapon weapon = targetItem.GetComponentInParent<MeleeWeapon>();
            if (weapon == null) weapon = targetItem.GetComponent<MeleeWeapon>();

            if (weapon != null)
            {
                float weight = weapon.weaponWeight;
                ItemObject itemComp = weapon.GetComponent<ItemObject>();
                if (itemComp != null) weight = itemComp.itemWeight;

                bool canAdd = playerStats.AddWeight(weight);
                if (canAdd)
                {
                    heldObject = weapon.gameObject;
                    if (InspectionTablet.Instance != null) InspectionTablet.Instance.ForceHolster();

                    Rigidbody rb = heldObject.GetComponent<Rigidbody>();
                    if (rb != null) { rb.isKinematic = true; }

                    Collider col = heldObject.GetComponent<Collider>();
                    if (col != null) { col.enabled = true; }

                    heldObject.transform.SetParent(holdPosition);
                    heldObject.transform.localPosition = Vector3.zero;
                    heldObject.transform.localRotation = Quaternion.identity;

                    Debug.Log($"[PlayerPickupSystem] หยิบอาวุธ '{weapon.weaponName}' ขึ้นมาถือสำเร็จ!");
                    return;
                }
                else
                {
                    Debug.LogWarning("[PlayerPickupSystem] แบกน้ำหนักไม่ไหว! ไม่สามารถหยิบอาวุธได้");
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
                bool canAdd = playerStats.AddWeight(itemComponent.itemWeight);
                if (canAdd)
                {
                    heldObject = targetItem;
                    if (InspectionTablet.Instance != null) InspectionTablet.Instance.ForceHolster();

                    Rigidbody rb = heldObject.GetComponent<Rigidbody>();
                    if (rb != null) { rb.isKinematic = true; }

                    Collider col = heldObject.GetComponent<Collider>();
                    if (col != null) { col.enabled = true; }

                    heldObject.transform.SetParent(holdPosition);
                    heldObject.transform.localPosition = Vector3.zero;
                    heldObject.transform.localRotation = Quaternion.identity;
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

            ItemObject item = heldObject.GetComponent<ItemObject>();
            float weight = item != null ? item.itemWeight : 0f;

            playerStats.RemoveWeight(weight);
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
                ItemObject item = heldObject.GetComponent<ItemObject>();
                float weight = item != null ? item.itemWeight : 0f;
                playerStats.RemoveWeight(weight);

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
            EnemyBase enemy = heldObject.GetComponent<EnemyBase>();
            if (enemy == null) enemy = heldObject.GetComponentInParent<EnemyBase>();

            if (enemy != null)
            {
                playerStats.RemoveWeight(enemy.enemyWeight);
                GameObject enemyObj = heldObject;
                heldObject = null;
                enemyObj.transform.SetParent(null);
                enemy.OnThrownByPlayer(Vector3.down * 1.5f);
                return;
            }

            ItemObject item = heldObject.GetComponent<ItemObject>();
            if (item != null)
            {
                playerStats.RemoveWeight(item.itemWeight);
            }
            else
            {
                MeleeWeapon weapon = heldObject.GetComponent<MeleeWeapon>();
                if (weapon == null) weapon = heldObject.GetComponentInParent<MeleeWeapon>();
                if (weapon != null)
                {
                    playerStats.RemoveWeight(weapon.weaponWeight);
                }
            }

            Rigidbody rb = heldObject.GetComponent<Rigidbody>();
            if (rb != null) { rb.isKinematic = false; }

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

        EnemyBase enemy = objToThrow.GetComponent<EnemyBase>();
        if (enemy == null) enemy = objToThrow.GetComponentInParent<EnemyBase>();

        if (enemy != null)
        {
            playerStats.RemoveWeight(enemy.enemyWeight);
            objToThrow.transform.SetParent(null);
            Vector3 throwVelocity = playerCam.transform.forward * throwForce + Vector3.up * 1.8f;
            enemy.OnThrownByPlayer(throwVelocity);
            Debug.Log($"[PlayerPickupSystem] ขว้างศัตรู '{objToThrow.name}' ไปข้างหน้า!");
            return;
        }

        ItemObject item = objToThrow.GetComponent<ItemObject>();
        if (item != null)
        {
            playerStats.RemoveWeight(item.itemWeight);
        }
        else
        {
            MeleeWeapon weapon = objToThrow.GetComponent<MeleeWeapon>();
            if (weapon == null) weapon = objToThrow.GetComponentInParent<MeleeWeapon>();
            if (weapon != null)
            {
                playerStats.RemoveWeight(weapon.weaponWeight);
            }
        }

        objToThrow.transform.SetParent(null);

        Rigidbody rb = objToThrow.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = playerCam.transform.forward * throwForce + Vector3.up * 1.5f;
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

        EnemyBase enemy = heldObject.GetComponent<EnemyBase>();
        if (enemy == null) enemy = heldObject.GetComponentInParent<EnemyBase>();

        if (enemy != null)
        {
            playerStats.RemoveWeight(enemy.enemyWeight);
        }
        else
        {
            ItemObject item = heldObject.GetComponent<ItemObject>();
            if (item != null) playerStats.RemoveWeight(item.itemWeight);
        }

        Rigidbody rb = heldObject.GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = false; }

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
            // คำนวณน้ำหนัก (ถ้าเป็นกล่องพัสดุหรือไอเท็ม ให้ดึงน้ำหนักตามจริง)
            // หมายเหตุ: ถ้ากล่องพัสดุไม่มีน้ำหนักเฉพาะ ให้กำหนดค่าคงที่หรือดึงจาก ItemObject ด้านใน
            float weight = 5f;
            ItemObject itemComp = itemToTake.GetComponent<ItemObject>();
            if (itemComp != null) weight = itemComp.itemWeight;

            // เช็คโควต้าน้ำหนักผู้เล่น
            bool canAdd = playerStats.AddWeight(weight);
            if (canAdd)
            {
                heldObject = itemToTake;
                if (InspectionTablet.Instance != null) InspectionTablet.Instance.ForceHolster();

                Rigidbody rb = heldObject.GetComponent<Rigidbody>();
                if (rb != null) { rb.isKinematic = true; }

                Collider col = heldObject.GetComponent<Collider>();
                if (col != null) { col.enabled = true; } // เปิด Collider กลับมา

                heldObject.transform.SetParent(holdPosition);
                heldObject.transform.localPosition = Vector3.zero;
                heldObject.transform.localRotation = Quaternion.identity;

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