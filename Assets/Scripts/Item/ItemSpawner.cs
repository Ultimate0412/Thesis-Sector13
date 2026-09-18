using UnityEngine;
using System.Collections.Generic;

public class ItemSpawner : MonoBehaviour, IInteractable
{
    [Header("Spawner Settings")]
    public Transform[] spawnSlots;            // รายการจุด Slot ทั้งหมดบนชั้นวาง (สามารถใส่ได้หลายจุด)
    public GameObject[] packagePrefabs;       // รายการ Prefab กล่องพัสดุทั้งหมด (3 ประเภท)

    // เก็บอ้างอิงกล่องที่อยู่ในแต่ละ Slot
    private GameObject[] spawnedPackages;

    private void Start()
    {
        // กำหนดขนาด Array ตามจำนวน Slot ที่ตั้งไว้
        if (spawnSlots != null && spawnSlots.Length > 0)
        {
            spawnedPackages = new GameObject[spawnSlots.Length];
        }
    }

    // --- Implement จาก IInteractable (กดปุ่ม E เพื่อสั่งเติมของที่แท่นสปอน) ---
    public string GetInteractPrompt()
    {
        return "กด E เพื่อสั่งเติมกล่องพัสดุลงในชั้น";
    }

    public void Interact(PlayerInteractor interactor)
    {
        SpawnPackagesUntilFull();
    }
    // ------------------------------------

    // ฟังก์ชันสั่งเติมสินค้าจนกว่าจะเต็มทุก Slot บนชั้น
    public void SpawnPackagesUntilFull()
    {
        if (spawnSlots == null || spawnSlots.Length == 0 || packagePrefabs.Length == 0)
        {
            Debug.LogWarning("ยังไม่ได้กำหนด Slot หรือ Prefab พัสดุใน Inspector!");
            return;
        }

        bool hasSpawnedAny = false;

        // วนลูปเช็คทุก Slot บนชั้น
        for (int i = 0; i < spawnSlots.Length; i++)
        {
            // ถ้า Slot นั้นว่างอยู่ (ไม่มีกล่อง หรือกล่องถูกหยิบไปทำลายแล้ว)
            if (spawnedPackages[i] == null)
            {
                // สุ่มเลือกกล่องพัสดุจาก Prefab ทั้งหมด (รองรับ 3 ประเภท)
                int randomIndex = Random.Range(0, packagePrefabs.Length);
                GameObject selectedPrefab = packagePrefabs[randomIndex];

                // สร้างกล่องขึ้นมาที่ตำแหน่ง Slot นั้นๆ
                GameObject newPackage = Instantiate(selectedPrefab, spawnSlots[i].position, spawnSlots[i].rotation);
                newPackage.transform.SetParent(spawnSlots[i]);

                // บันทึกเก็บไว้ใน Array ประจำ Slot
                spawnedPackages[i] = newPackage;
                hasSpawnedAny = true;
            }
        }

        if (hasSpawnedAny)
        {
            Debug.Log("เติมกล่องพัสดุลงในชั้นเรียบร้อย!");
        }
        else
        {
            Debug.Log("ชั้นวางเต็มแล้ว! ไม่มี Slot ว่างให้เติม");
        }
    }
}