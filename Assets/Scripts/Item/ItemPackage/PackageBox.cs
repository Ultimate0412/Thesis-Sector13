using UnityEngine;

public enum InspectionStatus { None, Approved, Rejected }

public class PackageBox : MonoBehaviour, IInteractable
{
    [Header("Inner Item Settings")]
    [Tooltip("รายการสินค้าที่สามารถสุ่มเกิดในกล่องนี้ได้ ")]
    public GameObject[] possibleInnerItemPrefabs;
    [Tooltip("Prefab สินค้าที่บรรจุอยู่ภายในกล่อง")]
    public GameObject innerItemPrefab;
    public Transform openSlot;
    public GameObject Hath;
    public bool randomizeOnAwake = true;

    [Header("Extraction State")]
    [Tooltip("สถานะว่าสินค้าถูกนำออกจากกล่องไปหรือยัง")]
    public bool isItemExtracted = false;
    private GameObject spawnedInnerItem;
    public GameObject SpawnedInnerItem => spawnedInnerItem;

    [Header("Inspection Settings")]
    public int maxOpenPerDay = 3;
    public bool isOpen = false;
    [Tooltip("ระบุว่ากล่องนี้เคยถูกเปิดตรวจแล้วหรือไม่ (หากเคยเปิดแล้ว การเปิดซ้ำจะไม่หักโควต้า)")]
    public bool hasBeenOpened = false;

    public InspectionStatus currentStamp = InspectionStatus.None;
    public KeyCode stampMenuKey = KeyCode.R; // ปุ่มกดเปิดเมนูประทับตรา (เปลี่ยนปุ่มได้ตามต้องการ)

    [Header("Manifest & Label Settings")]
    [Tooltip("ข้อมูลในระบบฐานข้อมูลทางการของแท็บเล็ต")]
    public PackageManifestData officialDatabaseData;
    [Tooltip("ข้อมูลที่พิมพ์ลงบนใบปะหน้าจริงของกล่อง")]
    public PackageManifestData physicalLabelData;
    [Tooltip("ประเภทข้อผิดพลาด")]
    public DiscrepancyType discrepancyType = DiscrepancyType.None;
    [Tooltip("โอกาสเกิดข้อผิดพลาดบนใบปะหน้า สำหรับสินค้า Illegal และ Alien (%)")]
    [Range(0f, 100f)] public float discrepancyChance = 75f;
    [Header("Box Animation Settings")]
    [Tooltip("Animator สำหรับเล่นแอนิเมชันเปิด-ปิดกล่อง (ถ้าไม่ระบุ จะดึงจาก GameObject หรือวัตถุลูกให้อัตโนมัติ)")]
    public Animator boxAnimator;
    [Tooltip("ชื่อ Parameter ชนิด Bool ใน Animator สำหรับควบคุมสถานะเปิด-ปิด")]
    public string openParameterName = "IsOpen";
    [Tooltip("หากมี Animator จะซ่อน Hath หรือไม่ (ปิดไว้หากในแอนิเมชันเปิดฝามีโมเดลฝาอยู่แล้ว)")]
    public bool hideHathWhenAnimated = false;

    private PackageLabelView labelView;

    private void Awake()
    {
        if (boxAnimator == null)
        {
            boxAnimator = GetComponent<Animator>();
            if (boxAnimator == null) boxAnimator = GetComponentInChildren<Animator>();
        }

        InitializeInnerItem();
        InitializeManifest();
    }

    // สุ่มสินค้าใส่ในกล่องหากมีการกำหนด possibleInnerItemPrefabs
    public void InitializeInnerItem()
    {
        if (randomizeOnAwake && possibleInnerItemPrefabs != null && possibleInnerItemPrefabs.Length > 0)
        {
            int randomIndex = Random.Range(0, possibleInnerItemPrefabs.Length);
            if (possibleInnerItemPrefabs[randomIndex] != null)
            {
                innerItemPrefab = possibleInnerItemPrefabs[randomIndex];
            }
        }
    }

    public void InitializeManifest()
    {
        ItemCategory category = ItemCategory.Legal;
        float weight = 5f;
        string itemName = "Standard Freight";
        ItemSubCategory subCategory = ItemSubCategory.Medical;
        ItemObject itemObj = null;

        if (innerItemPrefab != null)
        {
            itemObj = innerItemPrefab.GetComponent<ItemObject>();
            if (itemObj != null)
            {
                category = itemObj.category;
                weight = itemObj.itemWeight;
                itemName = itemObj.itemName;
                subCategory = itemObj.subCategory;
            }
        }

        officialDatabaseData = PackageManifestData.GenerateOfficialManifest(category, weight, itemName, subCategory);

        bool shouldDiscrepancy = false;
        if (category == ItemCategory.Illegal || category == ItemCategory.Alien)
        {
            shouldDiscrepancy = Random.Range(0f, 100f) < discrepancyChance;
        }

        physicalLabelData = PackageManifestData.GeneratePhysicalLabel(
            officialDatabaseData,
            shouldDiscrepancy,
            out discrepancyType
        );

        if (labelView == null)
        {
            labelView = GetComponent<PackageLabelView>();
            if (labelView == null)
            {
                labelView = gameObject.AddComponent<PackageLabelView>();
            }
        }

        labelView.SetLabelData(physicalLabelData);

        // สุ่มสร้างร่องรอยบนกล่องพัสดุ (Decals: รอยกรงเล็บ, คราบน้ำมัน, คราบน้ำ, หรือคราบเมือก UV)
        PackageTraceApplier traceApplier = GetComponent<PackageTraceApplier>();
        if (traceApplier == null)
        {
            traceApplier = gameObject.AddComponent<PackageTraceApplier>();
        }
        traceApplier.ApplyTraces(category, itemObj);
    }

    public void SetInnerItemPrefab(GameObject prefab)
    {
        innerItemPrefab = prefab;
        InitializeManifest();
    }

    // --- Implement จาก IInteractable (กดปุ่ม E เพื่อเปิด/ปิดกล่อง หรือสแกนเมื่อถือแท็บเล็ต) ---
    public string GetInteractPrompt()
    {
        // หากกำลังถือแท็บเล็ตตรวจข้อมูลอยู่
        if (InspectionTablet.Instance != null && InspectionTablet.Instance.IsTabletHeld)
        {
            return $"[E] Scan into Tablet | [RMB] Close-up | [F] UV Light | [R] Stamp ({currentStamp})";
        }

        if (isOpen)
        {
            if (!isItemExtracted)
            {
                return $"[E] Close Box | [Hold Q] Take Item Out | [R] Stamp ({currentStamp})";
            }
            else
            {
                return $"[E] Close Box (Empty) | [Q] Pick Up Box | [R] Stamp ({currentStamp})";
            }
        }
        else
        {
            string emptyNotice = isItemExtracted ? " (Empty)" : "";
            string openLabel = "[E] Open Box";
            if (!hasBeenOpened && GameDayManager.Instance != null && !GameDayManager.Instance.CanOpenBoxToday())
            {
                openLabel = "[E] No Quota";
            }
            return $"{openLabel}{emptyNotice} | [Q] Pick Up Box | [R] Stamp ({currentStamp})";
        }
    }

    public void Interact(PlayerInteractor interactor)
    {
        // หากผู้เล่นกำลังถือแท็บเล็ตอยู่ การกด E จะเป็นการสแกนข้อมูลเข้าแท็บเล็ต
        if (InspectionTablet.Instance != null && InspectionTablet.Instance.IsTabletHeld)
        {
            InspectionTablet.Instance.ScanPackage(this);
            return;
        }

        // กด E ปกติเพื่อเปิดหรือปิดกล่องดูสินค้าภายใน
        ToggleOpenBox();
    }
    // ------------------------------------

    public void OpenStampMenu()
    {
        if (StampUIController.Instance != null)
        {
            StampUIController.Instance.OpenStampMenu(this);
        }
        else
        {
            Debug.LogWarning("ยังไม่ได้สร้าง StampUIController ในฉาก!");
        }
    }

    public void SetStamp(InspectionStatus status)
    {
        currentStamp = status;
        Debug.Log($"ประทับตรากล่องเรียบร้อย: {currentStamp}");

        PackageLabelView labelView = GetComponent<PackageLabelView>();
        if (labelView != null)
        {
            labelView.UpdateStampVisual(status);
        }
    }

    public void ToggleOpenBox()
    {
        if (!isOpen)
        {
            // หากยังไม่เคยเปิดกล่องนี้มาก่อน ต้องมีโควต้าเปิดตรวจคงเหลืออยู่
            if (!hasBeenOpened && GameDayManager.Instance != null && !GameDayManager.Instance.CanOpenBoxToday())
            {
                Debug.LogWarning("[PackageBox] โควต้าการเปิดกล่องตรวจภายในของวันนี้หมดแล้ว!");
                return;
            }
            OpenBox();
        }
        else
        {
            CloseBox();
        }
    }

    /// <summary>
    /// ส่งค่า Boolean ไปยัง Animator เพื่อสั่งเล่นแอนิเมชันเปิดหรือปิดกล่อง
    /// </summary>
    /// <param name="open">true = เล่นท่าเปิดกล่อง, false = เล่นท่าปิดกล่อง</param>
    public void SetOpenAnimation(bool open)
    {
        if (boxAnimator != null && !string.IsNullOrEmpty(openParameterName))
        {
            boxAnimator.SetBool(openParameterName, open);
        }
    }

    private void OpenBox()
    {
        isOpen = true;
        SetOpenAnimation(true);

        if (Hath != null && (boxAnimator == null || hideHathWhenAnimated))
        {
            Hath.SetActive(false);
        }

        // หักโควต้าเปิดกล่องเฉพาะการเปิดครั้งแรกเท่านั้น
        if (!hasBeenOpened)
        {
            hasBeenOpened = true;
            if (GameDayManager.Instance != null)
            {
                GameDayManager.Instance.ConsumeOpenQuota();
            }
        }

        // หากยังไม่ได้นำสินค้าออก ให้สร้างหรือเปิดแสดงสินค้า
        if (!isItemExtracted)
        {
            if (spawnedInnerItem == null && innerItemPrefab != null && openSlot != null)
            {
                spawnedInnerItem = Instantiate(innerItemPrefab, openSlot.position, openSlot.rotation, openSlot);

                // ตั้งค่า Layer ให้ตรงกับกล่อง เพื่อให้ Raycast ตรวจจับได้
                SetLayerRecursively(spawnedInnerItem, gameObject.layer);

                // ปิดฟิสิกส์ระหว่างวางอยู่ในกล่อง
                Rigidbody rb = spawnedInnerItem.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                }

                Collider col = spawnedInnerItem.GetComponent<Collider>();
                if (col != null)
                {
                    col.enabled = true;
                }
            }
            else if (spawnedInnerItem != null)
            {
                spawnedInnerItem.SetActive(true);
            }
        }
    }

    private void CloseBox()
    {
        isOpen = false;
        SetOpenAnimation(false);

        if (spawnedInnerItem != null && !isItemExtracted)
        {
            spawnedInnerItem.SetActive(false);
        }
        if (Hath != null && (boxAnimator == null || hideHathWhenAnimated))
        {
            Hath.SetActive(true);
        }
    }

    // --- ฟังก์ชันสำหรับการนำสินค้าออกจากกล่อง (Extract) ---
    public GameObject ExtractInnerItem()
    {
        if (isItemExtracted || spawnedInnerItem == null) return null;

        GameObject item = spawnedInnerItem;
        isItemExtracted = true;
        spawnedInnerItem = null;

        // ปลดการยึดติดกับ openSlot
        item.transform.SetParent(null);

        // เปิดการทำงานของ Rigidbody และ Collider
        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
        }

        Collider col = item.GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = true;
        }

        Debug.Log($"[PackageBox] นำสินค้า '{item.name}' ออกจากกล่องสำเร็จ กล่องอยู่ในสถานะกล่องเปล่า");
        return item;
    }

    // --- ฟังก์ชันสำหรับการนำสินค้าใส่กลับเข้ากล่อง (Put Back) ---
    public bool CanPutItemBack(GameObject item)
    {
        if (item == null) return false;
        if (!isOpen) return false; // กล่องต้องเปิดอยู่
        if (!isItemExtracted) return false; // ต้องเป็นกล่องที่ของถูกนำออกไปแล้ว (กล่องเปล่า)
        if (item.GetComponent<ItemObject>() == null) return false; // ต้องเป็นไอเทม
        return true;
    }

    public bool PutItemBack(GameObject item)
    {
        if (!CanPutItemBack(item)) return false;

        isItemExtracted = false;
        spawnedInnerItem = item;

        // นำไปวางและยึดกับ openSlot
        if (openSlot != null)
        {
            item.transform.SetParent(openSlot);
            item.transform.localPosition = Vector3.zero;
            item.transform.localRotation = Quaternion.identity;
        }
        else
        {
            item.transform.SetParent(transform);
            item.transform.localPosition = Vector3.zero;
            item.transform.localRotation = Quaternion.identity;
        }

        // ปรับ Rigidbody ให้เป็น Kinematic ป้องกันกล่องกระเด็น
        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
        }

        Collider col = item.GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = true;
        }

        // ปรับ Layer ให้ตรงกับกล่อง
        SetLayerRecursively(item, gameObject.layer);

        Debug.Log($"[PackageBox] นำสินค้า '{item.name}' ใส่กลับเข้ากล่องเรียบร้อย");
        return true;
    }

    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (obj == null) return;
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            if (child != null)
            {
                SetLayerRecursively(child.gameObject, newLayer);
            }
        }
    }

    [ContextMenu("Preview Manifest & Traces")]
    public void PreviewManifestAndTraces()
    {
        InitializeInnerItem();
        InitializeManifest();
    }

    [ContextMenu("Test Animation: Open (Bool = true)")]
    public void TestAnimationOpen() => SetOpenAnimation(true);

    [ContextMenu("Test Animation: Close (Bool = false)")]
    public void TestAnimationClose() => SetOpenAnimation(false);
}