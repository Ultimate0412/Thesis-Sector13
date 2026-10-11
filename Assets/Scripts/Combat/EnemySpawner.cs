using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ระบบสปอนศัตรูสำหรับทดสอบ (Debug Enemy Spawner)
/// กดปุ่มที่กำหนด (เช่น KeyCode.P) แล้วศัตรูจะเกิดในตำแหน่ง Transform ที่กำหนดไว้ในฉาก
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    public static EnemySpawner Instance;

    public enum SpawnSelectionMode
    {
        Cycle,   // สลับตำแหน่งตามลำดับ (1 -> 2 -> 3 -> ...)
        Random,  // สุ่มจากตำแหน่งที่มี
        All      // สปอนพร้อมกันทุกจุด
    }

    [Header("Enemy Prefab Settings")]
    [Tooltip("Prefab ของศัตรูที่จะสปอน (เช่น Enemy_Hammer)")]
    public GameObject enemyPrefab;

    [Header("Spawn Points (ตำแหน่งเกิดที่กำหนด)")]
    [Tooltip("จุดเกิดทั้งหมดในฉาก (ลาก Empty GameObject มาใส่ได้หลายจุด)")]
    public Transform[] spawnPoints;

    [Tooltip("รูปแบบการเลือกจุดเกิด")]
    public SpawnSelectionMode selectionMode = SpawnSelectionMode.Cycle;

    [Header("Debug Controls")]
    [Tooltip("ปุ่มสำหรับกดสปอนศัตรูทันที (Default = P)")]
    public KeyCode debugSpawnKey = KeyCode.P;

    [Tooltip("ปุ่มสำหรับล้าง/ทำลายศัตรูทั้งหมดที่สปอนออกมา (Default = KeyCode.None)")]
    public KeyCode debugClearKey = KeyCode.None;

    [Header("Spawn Limits & Behavior")]
    [Tooltip("จำนวนศัตรูสูงสุดที่มีชีวิตอยู่ในฉากพร้อมกัน (-1 = ไม่จำกัด)")]
    public int maxAliveEnemies = 5;

    [Tooltip("ให้ศัตรูหันหน้าตามมุมหมุน (Rotation) ของจุดเกิดหรือไม่")]
    public bool matchSpawnRotation = true;

    [Header("Feedback / Effects")]
    [Tooltip("เสียงเมื่อศัตรูเกิด")]
    public AudioClip spawnSound;
    [Tooltip("Prefab เอฟเฟกต์ควันหรือแสงเมื่อสปอน")]
    public GameObject spawnVfxPrefab;

    // รายการศัตรูที่กำลังมีชีวิตอยู่ในฉาก
    private List<GameObject> activeEnemies = new List<GameObject>();
    private int currentSpawnIndex = 0;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Update()
    {
        // 1. ตรวจสอบการกดปุ่มสปอนศัตรู
        if (debugSpawnKey != KeyCode.None && Input.GetKeyDown(debugSpawnKey))
        {
            SpawnEnemy();
        }

        // 2. ตรวจสอบการกดปุ่มล้างศัตรู
        if (debugClearKey != KeyCode.None && Input.GetKeyDown(debugClearKey))
        {
            ClearAllEnemies();
        }
    }

    /// <summary>
    /// ฟังก์ชันสั่งสปอนศัตรูตามโหมดที่กำหนด (เรียกผ่านโค้ด, ปุ่ม UI, หรือ ContextMenu ได้)
    /// </summary>
    [ContextMenu("Spawn Enemy Now")]
    public GameObject SpawnEnemy()
    {
        if (enemyPrefab == null)
        {
            Debug.LogError("[EnemySpawner] ไม่สามารถสปอนได้: ยังไม่ได้กำหนด enemyPrefab ใน Inspector!");
            return null;
        }

        CleanDeadEnemiesList();

        if (maxAliveEnemies > 0 && activeEnemies.Count >= maxAliveEnemies)
        {
            Debug.LogWarning($"[EnemySpawner] จำนวนศัตรูเต็มโควต้าสูงสุดแล้ว ({activeEnemies.Count}/{maxAliveEnemies}) ไม่สามารถสปอนเพิ่มได้");
            return null;
        }

        if (selectionMode == SpawnSelectionMode.All && spawnPoints != null && spawnPoints.Length > 1)
        {
            GameObject lastSpawned = null;
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                if (maxAliveEnemies > 0 && activeEnemies.Count >= maxAliveEnemies) break;
                lastSpawned = SpawnAtTransform(spawnPoints[i]);
            }
            return lastSpawned;
        }

        Transform targetPoint = GetNextSpawnPoint();
        return SpawnAtTransform(targetPoint);
    }

    /// <summary>
    /// สปอนศัตรู ณ ตำแหน่งเฉพาะเจาะจงตาม Index ของ spawnPoints
    /// </summary>
    public GameObject SpawnEnemyAtIndex(int index)
    {
        if (spawnPoints == null || index < 0 || index >= spawnPoints.Length)
        {
            Debug.LogWarning($"[EnemySpawner] Index จุดเกิดไม่ถูกต้อง: {index}");
            return null;
        }

        return SpawnAtTransform(spawnPoints[index]);
    }

    /// <summary>
    /// ดึงจุดเกิดจุดถัดไปตามโหมด Cycle หรือ Random
    /// </summary>
    public Transform GetNextSpawnPoint()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            // หากไม่มีการกำหนด spawnPoints ให้ใช้ตำแหน่งของ GameObject ตัว Spawner เอง
            return transform;
        }

        Transform selected = null;

        if (selectionMode == SpawnSelectionMode.Random)
        {
            int randIndex = Random.Range(0, spawnPoints.Length);
            selected = spawnPoints[randIndex];
        }
        else // Cycle
        {
            selected = spawnPoints[currentSpawnIndex % spawnPoints.Length];
            currentSpawnIndex = (currentSpawnIndex + 1) % spawnPoints.Length;
        }

        return selected != null ? selected : transform;
    }

    private GameObject SpawnAtTransform(Transform spawnPoint)
    {
        Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : transform.position;
        Quaternion spawnRot = (matchSpawnRotation && spawnPoint != null) ? spawnPoint.rotation : Quaternion.identity;

        GameObject spawned = Instantiate(enemyPrefab, spawnPos, spawnRot);
        activeEnemies.Add(spawned);

        // เล่นเอฟเฟกต์เสียงและ VFX
        if (spawnSound != null)
        {
            AudioSource.PlayClipAtPoint(spawnSound, spawnPos);
        }
        if (spawnVfxPrefab != null)
        {
            Instantiate(spawnVfxPrefab, spawnPos, spawnRot);
        }

        Debug.Log($"<color=#FF5AA5>[EnemySpawner]</color> สปอนศัตรู <b>{spawned.name}</b> สำเร็จ! ณ ตำแหน่ง {spawnPos} (จำนวนศัตรูปัจจุบัน: {activeEnemies.Count})");
        return spawned;
    }

    /// <summary>
    /// ทำลายศัตรูทั้งหมดที่ถูกสปอนออกมา
    /// </summary>
    [ContextMenu("Clear All Enemies")]
    public void ClearAllEnemies()
    {
        CleanDeadEnemiesList();
        int count = activeEnemies.Count;
        foreach (var enemy in activeEnemies)
        {
            if (enemy != null)
            {
                if (Application.isPlaying) Destroy(enemy);
                else DestroyImmediate(enemy);
            }
        }
        activeEnemies.Clear();
        Debug.Log($"[EnemySpawner] ล้างศัตรูทั้งหมดในฉากเรียบร้อย ({count} ตัว)");
    }

    private void CleanDeadEnemiesList()
    {
        activeEnemies.RemoveAll(item => item == null);
    }

    // วาด Gizmos จุดเกิดใน Editor Scene View เพื่อให้มองเห็นและปรับตำแหน่งได้ง่าย
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.7f);

        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                Transform pt = spawnPoints[i];
                if (pt != null)
                {
                    Gizmos.DrawWireSphere(pt.position, 0.6f);
                    Gizmos.DrawLine(pt.position, pt.position + pt.forward * 1.5f);
                }
            }
        }
        else
        {
            Gizmos.DrawWireSphere(transform.position, 0.6f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.5f);
        }
    }
}
