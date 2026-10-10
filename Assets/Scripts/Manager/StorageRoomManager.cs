using UnityEngine;

public class StorageRoomManager : MonoBehaviour
{
    public static StorageRoomManager Instance { get; private set; }

    [Header("Storage Grid Settings")]
    public int totalSlots = 10;                // จำนวน Slot ทั้งหมดในห้อง (เช่น 10 Slot)
    public Transform[] slotPositions;          // ตำแหน่ง Transform ของแต่ละ Slot เรียงตามลำดับ (0 ถึง 9)

    // อาเรย์เช็คสถานะแต่ละ Slot ว่าถูกจอง/วางของอยู่หรือยัง (false = ว่าง, true = ไม่ว่าง)
    private bool[] slotOccupied;
    private GameObject[] itemsInSlots;         // เก็บอ้างอิงวัตถุที่วางอยู่ในแต่ละ Slot

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        slotOccupied = new bool[totalSlots];
        itemsInSlots = new GameObject[totalSlots];
    }

    // ฟังก์ชันเช็คว่าวัตถุนี้ถูกจัดเก็บอยู่ใน Storage หรือไม่
    public bool IsItemInStorage(GameObject itemObj)
    {
        if (itemObj == null) return false;

        // เช็คจาก itemsInSlots
        if (itemsInSlots != null)
        {
            for (int i = 0; i < itemsInSlots.Length; i++)
            {
                if (itemsInSlots[i] == itemObj) return true;
            }
        }

        // เช็คว่า parent ของวัตถุเป็นหนึ่งใน slotPositions หรือไม่
        if (slotPositions != null)
        {
            for (int i = 0; i < slotPositions.Length; i++)
            {
                if (slotPositions[i] != null && itemObj.transform.IsChildOf(slotPositions[i]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public int GetStoredItemCount()
    {
        if (itemsInSlots == null) return 0;
        int count = 0;
        for (int i = 0; i < itemsInSlots.Length; i++)
        {
            if (itemsInSlots[i] != null) count++;
        }
        return count;
    }

    // ฟังก์ชันเช็คว่าสามารถวางไอเท็มขนาด size ลงที่ startIndex ได้หรือไม่
    public bool CanPlaceItemAt(int startIndex, int itemSize)
    {
        // 1. เช็คว่าเกินขอบเขตห้องเก็บของไหม
        if (startIndex + itemSize > totalSlots) return false;

        // 2. เช็คว่า Slot ต่อเนื่องกันว่างหมดไหม
        for (int i = 0; i < itemSize; i++)
        {
            if (slotOccupied[startIndex + i])
            {
                return false; // มีบาง Slot ถูกใช้งานอยู่ วางไม่ได้
            }
        }
        return true;
    }

    // ฟังก์ชันทำการจองและวางไอเท็มลงใน Slot
    public void PlaceItemToSlots(GameObject itemObj, int startIndex, int itemSize)
    {
        for (int i = 0; i < itemSize; i++)
        {
            int targetIndex = startIndex + i;
            slotOccupied[targetIndex] = true;
            // ให้ Slot แรกสุดเก็บตัวอ้างอิงไอเท็มหลัก ส่วน Slot ที่เหลือปล่อยว่างไว้เพื่อกันคนอื่นมาทับ
            if (i == 0) itemsInSlots[targetIndex] = itemObj;
        }

        // ย้ายวัตถุไปวางที่ตำแหน่ง Slot เริ่มต้น
        itemObj.transform.position = slotPositions[startIndex].position;
        itemObj.transform.rotation = slotPositions[startIndex].rotation;
        itemObj.transform.SetParent(slotPositions[startIndex]);
    }

    // ฟังก์ชันเคลียร์พื้นที่เมื่อหยิบของออก
    public void RemoveItemFromSlots(GameObject itemObj)
    {
        for (int i = 0; i < totalSlots; i++)
        {
            if (itemsInSlots[i] == itemObj)
            {
                ItemObject itemComp = itemObj.GetComponent<ItemObject>();
                int size = itemComp != null ? itemComp.sizeInSlots : 1;

                // ปลดล็อคสถานะ Slot ทั้งหมดที่ของชิ้นนี้เคยครอบครอง
                for (int j = 0; j < size; j++)
                {
                    if (i + j < totalSlots)
                    {
                        slotOccupied[i + j] = false;
                    }
                }
                itemsInSlots[i] = null;
                break;
            }
        }
    }
}