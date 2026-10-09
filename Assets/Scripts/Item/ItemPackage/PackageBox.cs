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
    private PackageLabelView labelView;

    private void Awake()
    {
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
            return $"[E] Open Box{emptyNotice} | [Q] Pick Up Box | [R] Stamp ({currentStamp})";
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

    private void Update()
    {
        if (Input.GetKeyDown(stampMenuKey))
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null && Vector3.Distance(transform.position, player.transform.position) <= 3.5f)
            {
                OpenStampMenu();
            }
        }
    }

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
    }

    public void ToggleOpenBox()
    {
        if (!isOpen)
        {
            if (GameDayManager.Instance != null && !GameDayManager.Instance.CanOpenBoxToday())
            {
                Debug.Log("โควต้าการเปิดกล่องตรวจภายในของวันนี้หมดแล้ว!");
                return;
            }
            OpenBox();
        }
        else
        {
            CloseBox();
        }
    }

    private void OpenBox()
    {
        isOpen = true;
        if (Hath != null) Hath.SetActive(false);

        if (GameDayManager.Instance != null)
        {
            GameDayManager.Instance.ConsumeOpenQuota();
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
        if (spawnedInnerItem != null && !isItemExtracted)
        {
            spawnedInnerItem.SetActive(false);
        }
        if (Hath != null) Hath.SetActive(true);
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
}