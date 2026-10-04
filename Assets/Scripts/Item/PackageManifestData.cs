using System;
using System.Collections.Generic;
using UnityEngine;

public enum DiscrepancyType
{
    None,
    NameTypo,
    AddressMismatch,
    SerialTampered
}

[System.Serializable]
public class PackageManifestData
{
    public string recipientName;
    public string destinationAddress;
    public string serialNumber;
    public string itemDescription;
    public ItemCategory declaredCategory;
    public float declaredWeight;

    public PackageManifestData Clone()
    {
        return new PackageManifestData
        {
            recipientName = this.recipientName,
            destinationAddress = this.destinationAddress,
            serialNumber = this.serialNumber,
            itemDescription = this.itemDescription,
            declaredCategory = this.declaredCategory,
            declaredWeight = this.declaredWeight
        };
    }

    #region Manifest Generation

    private static readonly string[] FirstNames = new string[]
    {
        "Marcus", "Elena", "Silas", "Lyra", "Jax",
        "Kaelen", "Sarah", "Novak", "Orion", "Zane",
        "Mira", "Talon", "Corin", "Aria", "Darek",
        "Vance", "Kira", "Rylan", "Selene", "Victor"
    };

    private static readonly string[] LastNames = new string[]
    {
        "Vance", "Cross", "Sterling", "Mercer", "Chen",
        "Kowalski", "Vex", "Rivera", "Steele", "Blackwood",
        "Holt", "Thorne", "Frost", "Drakos", "Ryder",
        "Ashford", "Valen", "Sinclair", "Voss", "Moran"
    };

    private static readonly string[] StationLocations = new string[]
    {
        "Sector 13 - Docking Bay 4",
        "Sector 13 - Docking Bay 7",
        "Sector 13 - Docking Bay 11",
        "Hydroponics Dome B - Level 2",
        "Hydroponics Dome A - Level 4",
        "Reactor Core Sub-Deck 1",
        "Reactor Core Sub-Deck 3",
        "Crew Quarters - Block C-12",
        "Crew Quarters - Block A-05",
        "Research Lab Zeta - Wing 3",
        "Medical Facility - Ward 9",
        "Engineering Bay - Deck 5",
        "Outpost Alpha - Cargo Hub",
        "Central Logistics Terminal"
    };

    public static PackageManifestData GenerateOfficialManifest(ItemCategory category, float weight, string itemName)
    {
        PackageManifestData manifest = new PackageManifestData();

        string firstName = FirstNames[UnityEngine.Random.Range(0, FirstNames.Length)];
        string lastName = LastNames[UnityEngine.Random.Range(0, LastNames.Length)];
        manifest.recipientName = $"{firstName} {lastName}";

        manifest.destinationAddress = StationLocations[UnityEngine.Random.Range(0, StationLocations.Length)];

        int randomCode = UnityEngine.Random.Range(10000, 99999);
        char suffix = (char)('A' + UnityEngine.Random.Range(0, 26));
        manifest.serialNumber = $"SN-{randomCode}-{suffix}";

        manifest.itemDescription = string.IsNullOrEmpty(itemName) ? "Standard Cargo" : itemName;
        manifest.declaredCategory = category;
        manifest.declaredWeight = (float)Math.Round(weight, 1);

        return manifest;
    }

    public static PackageManifestData GeneratePhysicalLabel(
        PackageManifestData official,
        bool createDiscrepancy,
        out DiscrepancyType discrepancy)
    {
        PackageManifestData label = official.Clone();

        if (!createDiscrepancy)
        {
            discrepancy = DiscrepancyType.None;
            return label;
        }

        // สุ่มเลือก 1 ใน 3 ข้อผิดพลาด
        DiscrepancyType[] possibleTypes = new DiscrepancyType[]
        {
            DiscrepancyType.NameTypo,
            DiscrepancyType.AddressMismatch,
            DiscrepancyType.SerialTampered
        };

        discrepancy = possibleTypes[UnityEngine.Random.Range(0, possibleTypes.Length)];

        switch (discrepancy)
        {
            case DiscrepancyType.NameTypo:
                label.recipientName = ApplyNameTypo(official.recipientName);
                break;

            case DiscrepancyType.AddressMismatch:
                label.destinationAddress = ApplyAddressMismatch(official.destinationAddress);
                break;

            case DiscrepancyType.SerialTampered:
                label.serialNumber = ApplySerialTamper(official.serialNumber);
                break;

            default:
                discrepancy = DiscrepancyType.None;
                break;
        }

        return label;
    }

    private static string ApplyNameTypo(string fullName)
    {
        string[] parts = fullName.Split(' ');
        if (parts.Length < 2) return fullName + "x";

        string first = parts[0];
        string last = parts[1];

        // สุ่มเลือกว่าจะสร้าง Typo ที่ชื่อต้นหรือนามสกุล
        bool typoInFirst = UnityEngine.Random.value < 0.5f;
        string target = typoInFirst ? first : last;

        string altered = MutateStringTypo(target);
        if (altered == target)
        {
            // หากไม่มีการเปลี่ยน ให้เติมตัวอักษรหรือตัดออก
            altered = target.Length > 4 ? target.Substring(0, target.Length - 1) : target + "n";
        }

        return typoInFirst ? $"{altered} {last}" : $"{first} {altered}";
    }

    private static string MutateStringTypo(string str)
    {
        if (string.IsNullOrEmpty(str) || str.Length < 3) return str;

        // รายการตัวอักษรที่มักสะกดผิดหรือสลับกัน
        Dictionary<string, string> replacements = new Dictionary<string, string>
        {
            { "c", "k" },
            { "k", "c" },
            { "v", "w" },
            { "w", "v" },
            { "ph", "f" },
            { "f", "ph" },
            { "y", "i" },
            { "i", "y" },
            { "s", "z" },
            { "z", "s" },
            { "e", "a" },
            { "a", "e" },
            { "th", "ht" },
            { "ck", "k" }
        };

        string lower = str.ToLower();
        foreach (var pair in replacements)
        {
            if (lower.Contains(pair.Key))
            {
                int index = lower.IndexOf(pair.Key);
                string newStr = str.Remove(index, pair.Key.Length).Insert(index, pair.Value);
                // คงตัวพิมพ์ใหญ่ตัวแรกไว้ถ้ามี
                if (char.IsUpper(str[0]))
                {
                    newStr = char.ToUpper(newStr[0]) + (newStr.Length > 1 ? newStr.Substring(1) : "");
                }
                return newStr;
            }
        }

        // ถ้าไม่มี pattern ด้านบน ให้สลับอักษร 2 ตัวกลางคำ
        int swapIdx = UnityEngine.Random.Range(1, str.Length - 1);
        char[] chars = str.ToCharArray();
        char temp = chars[swapIdx];
        chars[swapIdx] = chars[swapIdx + 1];
        chars[swapIdx + 1] = temp;
        return new string(chars);
    }

    private static string ApplyAddressMismatch(string originalAddress)
    {
        // เลือกที่อยู่อื่นที่ไม่ซ้ำกับที่อยู่เดิม
        List<string> options = new List<string>(StationLocations);
        options.Remove(originalAddress);
        if (options.Count > 0)
        {
            return options[UnityEngine.Random.Range(0, options.Count)];
        }
        return originalAddress + " - Modified";
    }

    private static string ApplySerialTamper(string originalSerial)
    {
        // รูปร่าง: SN-XXXXX-Z
        if (!originalSerial.StartsWith("SN-") || originalSerial.Length < 9)
        {
            return originalSerial + "-ERR";
        }

        // ทางเลือกที่ 1: สลับตัวเลข 2 ตัวในรหัส (เช่น SN-48201-A -> SN-42801-A)
        // ทางเลือกที่ 2: เปลี่ยนตัวอักษรท้ายสุด
        if (UnityEngine.Random.value < 0.6f)
        {
            char[] arr = originalSerial.ToCharArray();
            // ตัวเลขอยู่ระหว่าง index 3 ถึง 7
            int idx1 = UnityEngine.Random.Range(3, 7);
            int idx2 = idx1 + 1;
            char tmp = arr[idx1];
            arr[idx1] = arr[idx2];
            arr[idx2] = tmp;
            return new string(arr);
        }
        else
        {
            char currentSuffix = originalSerial[originalSerial.Length - 1];
            char newSuffix = (char)('A' + ((currentSuffix - 'A' + 3) % 26));
            return originalSerial.Substring(0, originalSerial.Length - 1) + newSuffix;
        }
    }

    #endregion
}
