using UnityEngine;

public class ItemSpawner : MonoBehaviour
{
    [Header("Spawner Settings")]
    public Transform spawnSlot;             // จุด Slot สำหรับวางกล่องพัสดุ
    public GameObject[] packagePrefabs;     // รายการ Prefab กล่องพัสดุทั้งหมด

    [HideInInspector] public GameObject currentSpawnedPackage;

    // ฟังก์ชันสั่งสปอนกล่องพัสดุ
    public void SpawnNewPackage()
    {
        // เช็คว่าใน Slot มีกล่องค้างอยู่ไหม (1 สล็อตต่อ 1 ชิ้น)
        if (currentSpawnedPackage != null)
        {
            Debug.Log("สล็อตนี้ยังมีกล่องพัสดุอยู่ ไม่สามารถสปอนใหม่ได้!");
            return;
        }

        if (packagePrefabs.Length == 0 || spawnSlot == null) return;

        // สุ่มเลือกกล่องพัสดุจาก Array
        int randomIndex = Random.Range(0, packagePrefabs.Length);
        GameObject selectedPrefab = packagePrefabs[randomIndex];

        // สร้างกล่องขึ้นมาที่ตำแหน่ง Spawn Slot
        currentSpawnedPackage = Instantiate(selectedPrefab, spawnSlot.position, spawnSlot.rotation);
        currentSpawnedPackage.transform.SetParent(spawnSlot);

        Debug.Log("สปอนกล่องพัสดุสำเร็จ");
    }
}