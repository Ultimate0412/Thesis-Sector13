using UnityEngine;

public enum ItemCategory { Legal, Illegal, Alien }

public class ItemObject : MonoBehaviour
{
    [Header("Item Properties")]
    public string itemName = "Unknown Item";
    public float itemWeight = 5f;
    public ItemCategory category = ItemCategory.Legal;
}