using System;
using System.Collections.Generic;
using UnityEngine;

public enum DiscrepancyType
{
    None,
    NameTypo,
    AddressMismatch,
    SerialTampered,
    WeightMismatch
}

[System.Serializable]
public class PackageManifestData
{
    public string recipientName;
    public string destinationAddress;
    public string serialNumber;
    public string itemDescription;
    public ItemSubCategory declaredSubCategory = ItemSubCategory.Medical; // หมวดหมู่ย่อยที่เปิดเผยบนใบปะหน้าและแท็บเล็ต
    public ItemCategory declaredCategory; // เก็บไว้ประมวลผลภายใน ไม่แสดงผลบน UI
    public float declaredWeight;          // น้ำหนักที่แสดงบนแท็บเล็ต (อาจคลาดเคลื่อนหากเป็น WeightMismatch)
    public float actualWeight;            // น้ำหนักจริงของสินค้าในกล่อง

    public PackageManifestData Clone()
    {
        return new PackageManifestData
        {
            recipientName = this.recipientName,
            destinationAddress = this.destinationAddress,
            serialNumber = this.serialNumber,
            itemDescription = this.itemDescription,
            declaredSubCategory = this.declaredSubCategory,
            declaredCategory = this.declaredCategory,
            declaredWeight = this.declaredWeight,
            actualWeight = this.actualWeight
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

    public static readonly string[] StationLocations = new string[]
    {
        "Medical Facility - Ward 9",
        "Medical Facility - Clinic 2",
        "Docking Bay 4 - Cargo Hub",
        "Docking Bay 7 - Main Bay",
        "Docking Bay 11 - Deep Pier",
        "Hydroponics Dome B - Level 2",
        "Hydroponics Dome A - Level 4",
        "Reactor Core Sub-Deck 1",
        "Reactor Core Sub-Deck 3",
        "Crew Quarters - Block C-12",
        "Crew Quarters - Block A-05",
        "Research Lab Zeta - Wing 3",
        "Engineering Bay - Deck 5",
        "Outpost Alpha - Cargo Hub",
        "Central Logistics Terminal"
    };

    /// <summary>
    /// สกัดตัวอักษรพิมพ์ใหญ่ 2 ตัวแรกจากชื่อสถานที่หลัก เพื่อใช้เป็น Prefix ของ Serial Number (เช่น Medical Facility -> MF)
    /// </summary>
    public static string GetAddressPrefix(string address)
    {
        if (string.IsNullOrEmpty(address)) return "SN";

        // แยกส่วนหน้าขีด '-' เช่น "Medical Facility - Ward 9" -> "Medical Facility"
        string mainPart = address;
        if (address.Contains("-"))
        {
            mainPart = address.Split('-')[0].Trim();
        }

        // ตัดคำที่ไม่ใช่ชื่อโซนเฉพาะ
        mainPart = mainPart.Replace("Sector 13", "").Trim();

        List<char> uppers = new List<char>();
        foreach (char c in mainPart)
        {
            if (char.IsUpper(c))
            {
                uppers.Add(c);
            }
        }

        if (uppers.Count >= 2)
        {
            return $"{uppers[0]}{uppers[1]}";
        }
        else if (uppers.Count == 1)
        {
            return $"{uppers[0]}X";
        }

        return "SN";
    }

    public static PackageManifestData GenerateOfficialManifest(ItemCategory category, float weight, string itemName, ItemSubCategory subCategory = ItemSubCategory.Medical)
    {
        PackageManifestData manifest = new PackageManifestData();

        string firstName = FirstNames[UnityEngine.Random.Range(0, FirstNames.Length)];
        string lastName = LastNames[UnityEngine.Random.Range(0, LastNames.Length)];
        manifest.recipientName = $"{firstName} {lastName}";

        manifest.destinationAddress = StationLocations[UnityEngine.Random.Range(0, StationLocations.Length)];

        // รหัสซีเรียลใช้ตัวอักษรพิมพ์ใหญ่ 2 ตัวหน้าจากที่อยู่ปลายทาง (เช่น Medical Facility -> MF-XXXXX-A)
        string prefix = GetAddressPrefix(manifest.destinationAddress);
        int randomCode = UnityEngine.Random.Range(10000, 99999);
        char suffix = (char)('A' + UnityEngine.Random.Range(0, 26));
        manifest.serialNumber = $"{prefix}-{randomCode}-{suffix}";

        manifest.itemDescription = string.IsNullOrEmpty(itemName) ? "Standard Cargo" : itemName;
        manifest.declaredSubCategory = subCategory;
        manifest.declaredCategory = category;
        manifest.actualWeight = (float)Math.Round(weight, 1);
        manifest.declaredWeight = manifest.actualWeight;

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

        // สุ่มเลือก 1 ใน 4 รูปแบบข้อผิดพลาด
        DiscrepancyType[] possibleTypes = new DiscrepancyType[]
        {
            DiscrepancyType.NameTypo,
            DiscrepancyType.AddressMismatch,
            DiscrepancyType.SerialTampered,
            DiscrepancyType.WeightMismatch
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
                label.serialNumber = ApplySerialTamper(official.serialNumber, official.destinationAddress);
                break;

            case DiscrepancyType.WeightMismatch:
                // ข้อผิดพลาดเรื่องน้ำหนักจะเกิดขึ้นบนแท็บเล็ต (สำแดงน้ำหนักเท็จ)
                official.declaredWeight = ApplyWeightDiscrepancy(official.actualWeight);
                break;

            default:
                discrepancy = DiscrepancyType.None;
                break;
        }

        return label;
    }

    public static float ApplyWeightDiscrepancy(float actualWeight)
    {
        if (actualWeight <= 0f) actualWeight = 5f;

        // สุ่มให้น้ำหนักบนแท็บเล็ตคลาดเคลื่อนอย่างเห็นได้ชัด
        if (UnityEngine.Random.value < 0.5f)
        {
            // แจ้งน้ำหนักเบากว่าของจริง (ซุกซ่อนของหนัก)
            float altered = (float)Math.Round(actualWeight * 0.4f, 1);
            return Mathf.Max(0.5f, altered);
        }
        else
        {
            // แจ้งน้ำหนักเกินจริง
            float altered = (float)Math.Round(actualWeight + UnityEngine.Random.Range(3.0f, 6.0f), 1);
            return altered;
        }
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
            altered = target.Length > 4 ? target.Substring(0, target.Length - 1) : target + "n";
        }

        return typoInFirst ? $"{altered} {last}" : $"{first} {altered}";
    }

    private static string MutateStringTypo(string str)
    {
        if (string.IsNullOrEmpty(str) || str.Length < 3) return str;

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
                if (char.IsUpper(str[0]))
                {
                    newStr = char.ToUpper(newStr[0]) + (newStr.Length > 1 ? newStr.Substring(1) : "");
                }
                return newStr;
            }
        }

        int swapIdx = UnityEngine.Random.Range(1, str.Length - 1);
        char[] chars = str.ToCharArray();
        char temp = chars[swapIdx];
        chars[swapIdx] = chars[swapIdx + 1];
        chars[swapIdx + 1] = temp;
        return new string(chars);
    }

    private static string ApplyAddressMismatch(string originalAddress)
    {
        List<string> options = new List<string>(StationLocations);
        options.Remove(originalAddress);
        if (options.Count > 0)
        {
            return options[UnityEngine.Random.Range(0, options.Count)];
        }
        return originalAddress + " - Altered";
    }

    private static string ApplySerialTamper(string originalSerial, string destinationAddress)
    {
        string[] parts = originalSerial.Split('-');
        if (parts.Length != 3)
        {
            return originalSerial + "-ERR";
        }

        float roll = UnityEngine.Random.value;

        // รูปแบบ 1: ปลอมแปลง Prefix 2 ตัวหน้าให้ไม่ตรงกับสถานที่ปลายทาง (เช่น ไป Medical Facility แต่รหัสเป็น EB หรือ DB)
        if (roll < 0.45f)
        {
            string currentPrefix = parts[0];
            string[] possiblePrefixes = new string[] { "MF", "DB", "HD", "RC", "CQ", "RL", "EB", "OA", "CL" };
            List<string> otherPrefixes = new List<string>(possiblePrefixes);
            otherPrefixes.Remove(currentPrefix);

            string forgedPrefix = otherPrefixes[UnityEngine.Random.Range(0, otherPrefixes.Count)];
            return $"{forgedPrefix}-{parts[1]}-{parts[2]}";
        }
        // รูปแบบ 2: สลับตัวเลข 2 ตัวในรหัส
        else if (roll < 0.8f)
        {
            char[] digits = parts[1].ToCharArray();
            if (digits.Length >= 2)
            {
                int idx1 = UnityEngine.Random.Range(0, digits.Length - 1);
                char tmp = digits[idx1];
                digits[idx1] = digits[idx1 + 1];
                digits[idx1 + 1] = tmp;
            }
            return $"{parts[0]}-{new string(digits)}-{parts[2]}";
        }
        // รูปแบบ 3: เปลี่ยนตัวอักษรลงท้าย
        else
        {
            char currentSuffix = parts[2][0];
            char newSuffix = (char)('A' + ((currentSuffix - 'A' + 3) % 26));
            return $"{parts[0]}-{parts[1]}-{newSuffix}";
        }
    }

    #endregion
}
