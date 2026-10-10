using UnityEngine;

public enum ItemCategory { Legal, Illegal, Alien }

public enum ItemSubCategory
{
    Medical,        // เวชภัณฑ์และยา (Pharmaceuticals & Medical)
    Industrial,     // ชิ้นส่วนอุตสาหกรรมและเครื่องยนต์ (Industrial Machinery & Parts)
    Rations,        // อาหารและเสบียง (Food Rations & Bio-Supplies)
    Scientific,     // ตัวอย่างวิจัยและสารเคมี (Scientific Samples & Chemicals)
    Electronics     // อุปกรณ์เทคโนโลยีและชิป (Electronics & Hardware)
}

public class ItemObject : MonoBehaviour
{
    [Header("Item Properties")]
    public string itemName = "Unknown Item";
    public float itemWeight = 5f;
    public ItemCategory category = ItemCategory.Legal;
    public ItemSubCategory subCategory = ItemSubCategory.Medical;

    [Header("Custom Trace Profile (ร่องรอยที่สินค้านี้ทำให้เกิดขึ้นได้)")]
    [Tooltip("รายการร่องรอยที่สินค้านี้สามารถสร้างบนกล่องพัสดุ (เลือกได้หลายแบบใน Inspector)")]
    public TraceType[] allowedTraces;

    [Header("Size & Grid Settings")]
    public int sizeInSlots = 1;

    public static string GetSubCategoryDisplayName(ItemSubCategory sub)
    {
        switch (sub)
        {
            case ItemSubCategory.Medical: return "MEDICAL / PHARMACEUTICALS";
            case ItemSubCategory.Industrial: return "INDUSTRIAL MACHINERY";
            case ItemSubCategory.Rations: return "FOOD & BIO-RATIONS";
            case ItemSubCategory.Scientific: return "SCIENTIFIC SAMPLES";
            case ItemSubCategory.Electronics: return "ELECTRONICS / HARDWARE";
            default: return sub.ToString().ToUpper();
        }
    }
}