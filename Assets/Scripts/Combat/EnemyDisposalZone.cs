using UnityEngine;

/// <summary>
/// พื้นที่ทำลายทิ้ง (Disposal Zone / Incinerator)
/// เมื่อศัตรูที่ติดสตั๊นถูกยกมาโยนหรือตกลงมาในพื้นที่นี้ ศัตรูจะถูกทำลายทิ้งอย่างถาวร
/// </summary>
[RequireComponent(typeof(Collider))]
public class EnemyDisposalZone : MonoBehaviour
{
    [Header("Disposal Settings")]
    [Tooltip("เอฟเฟกต์ไฟ/ควันเมื่อศัตรูถูกทำลาย")]
    public GameObject disposalVfxPrefab;

    [Tooltip("เสียงการทำลายศัตรู")]
    public AudioClip disposalAudioClip;

    [Tooltip("ต้องการให้ทำลายเฉพาะศัตรูที่ติดสตั๊นหรือไม่")]
    public bool requireStunned = true;

    private void Awake()
    {
        // รับประกันว่าเป็น Trigger Collider
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        EnemyBase enemy = other.GetComponentInParent<EnemyBase>();
        if (enemy == null) enemy = other.GetComponent<EnemyBase>();

        if (enemy != null && !enemy.IsDead)
        {
            // ถ้ากำหนดว่าต้องติดสตั๊นเท่านั้น
            if (requireStunned && !enemy.IsStunned)
            {
                Debug.Log($"[{gameObject.name}] ศัตรู {enemy.name} ยังไม่ติดสตั๊น จึงยังไม่ถูกทำลาย");
                return;
            }

            DisposeEnemy(enemy);
        }
    }

    private void DisposeEnemy(EnemyBase enemy)
    {
        Vector3 pos = enemy.transform.position;

        if (disposalVfxPrefab != null)
        {
            Instantiate(disposalVfxPrefab, pos, Quaternion.identity);
        }

        if (disposalAudioClip != null)
        {
            AudioSource.PlayClipAtPoint(disposalAudioClip, pos);
        }

        Debug.Log($"<color=green>[Disposal Zone] กำจัดศัตรู '{enemy.name}' สำเร็จ!</color>");
        enemy.OnDisposed();
    }
}
