using System.Collections.Generic;
using UnityEngine;

public class UVFlashlightController : MonoBehaviour
{
    public static UVFlashlightController Instance { get; private set; }

    [Header("Keybinding")]
    [Tooltip("ปุ่มเปิด/ปิดไฟฉาย UV (Blacklight)")]
    public KeyCode toggleKey = KeyCode.F;

    [Header("UV Light Settings")]
    public bool isUVActive = false;
    public Color uvLightColor = new Color(0.6f, 0.2f, 1.0f); // Deep Ultraviolet / Violet
    public float uvIntensity = 3.5f;
    public float uvRange = 6.0f;
    public float uvSpotAngle = 55.0f;

    [Header("Detection Cone")]
    [Tooltip("ระยะค้นหากล่องที่โดนแสง UV")]
    public float detectionDistance = 5.0f;

    public Light uvLightComponent;
    private Camera playerCam;
    private PlayerPickupSystem pickupSystem;
    private AudioSource audioSource;
    private readonly HashSet<PackageTraceApplier> currentlyIlluminatedBoxes = new HashSet<PackageTraceApplier>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(this); return; }

        playerCam = GetComponentInChildren<Camera>();
        if (playerCam == null) playerCam = Camera.main;

        pickupSystem = GetComponentInParent<PlayerPickupSystem>();
        if (pickupSystem == null) pickupSystem = GetComponent<PlayerPickupSystem>();

        SetupLightComponent();
        SetupAudio();
    }

    private void Start()
    {
        SetUVState(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleUVLight();
        }

        if (isUVActive)
        {
            ScanAndIlluminatedBoxes();
        }
    }

    public void ToggleUVLight()
    {
        SetUVState(!isUVActive);
    }

    public void SetUVState(bool active)
    {
        isUVActive = active;

        if (uvLightComponent != null)
        {
            uvLightComponent.enabled = active;
        }

        PlayClickSound(active ? 1500f : 800f);

        if (!active)
        {
            // ปิดไฟฉาย -> แจ้งทุกกล่องให้ซ่อนรอย UV
            foreach (var box in currentlyIlluminatedBoxes)
            {
                if (box != null) box.SetUVIlluminated(false);
            }
            currentlyIlluminatedBoxes.Clear();
        }
    }

    private void ScanAndIlluminatedBoxes()
    {
        HashSet<PackageTraceApplier> visibleInCone = new HashSet<PackageTraceApplier>();
        Transform camTrans = playerCam != null ? playerCam.transform : transform;

        // 1. ถ้าผู้เล่นกำลังถือกล่องอยู่ ให้กล่องที่ถือส่องติดไฟ UV เสมอ
        if (pickupSystem != null && pickupSystem.heldObject != null)
        {
            PackageTraceApplier heldApplier = pickupSystem.heldObject.GetComponent<PackageTraceApplier>();
            if (heldApplier != null)
            {
                visibleInCone.Add(heldApplier);
            }
        }

        // 2. ตรวจสอบกล่องในระยะโคนไฟฉาย UV ข้างหน้าผู้เล่น
        Collider[] hits = Physics.OverlapSphere(camTrans.position, detectionDistance);
        float cosThreshold = Mathf.Cos((uvSpotAngle * 0.5f) * Mathf.Deg2Rad);

        foreach (var hit in hits)
        {
            PackageTraceApplier applier = hit.GetComponentInParent<PackageTraceApplier>();
            if (applier == null) applier = hit.GetComponent<PackageTraceApplier>();

            if (applier != null && !visibleInCone.Contains(applier))
            {
                Vector3 toBox = (applier.transform.position - camTrans.position);
                float dist = toBox.magnitude;

                if (dist <= detectionDistance)
                {
                    Vector3 dir = toBox.normalized;
                    float dot = Vector3.Dot(camTrans.forward, dir);

                    // อยู่ในกรวยแสงไฟฉาย
                    if (dot >= cosThreshold)
                    {
                        visibleInCone.Add(applier);
                    }
                }
            }
        }

        // อัปเดตกล่องใหม่ที่เพิ่งส่องโดน
        foreach (var box in visibleInCone)
        {
            if (!currentlyIlluminatedBoxes.Contains(box))
            {
                box.SetUVIlluminated(true);
            }
        }

        // อัปเดตกล่องที่หลุดออกจากแสงไฟฉาย
        foreach (var box in currentlyIlluminatedBoxes)
        {
            if (box != null && !visibleInCone.Contains(box))
            {
                box.SetUVIlluminated(false);
            }
        }

        currentlyIlluminatedBoxes.Clear();
        foreach (var b in visibleInCone)
        {
            currentlyIlluminatedBoxes.Add(b);
        }
    }

    private void SetupLightComponent()
    {
        Transform camTrans = playerCam != null ? playerCam.transform : transform;
        Transform existing = camTrans.Find("UV_Spotlight");

        if (existing != null)
        {
            uvLightComponent = existing.GetComponent<Light>();
        }
        else
        {
            GameObject lightGo = new GameObject("UV_Spotlight");
            lightGo.transform.SetParent(camTrans, false);
            lightGo.transform.localPosition = new Vector3(0f, -0.05f, 0.2f);
            lightGo.transform.localRotation = Quaternion.identity;

            uvLightComponent = lightGo.AddComponent<Light>();
            uvLightComponent.type = LightType.Spot;
            uvLightComponent.color = uvLightColor;
            uvLightComponent.intensity = uvIntensity;
            uvLightComponent.range = uvRange;
            uvLightComponent.spotAngle = uvSpotAngle;
            uvLightComponent.shadows = LightShadows.None;
        }
    }

    private void SetupAudio()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }
    }

    private void PlayClickSound(float pitch)
    {
        if (audioSource == null) return;

        int sampleRate = 44100;
        int count = Mathf.RoundToInt(sampleRate * 0.04f);
        AudioClip clip = AudioClip.Create("UVClick", count, 1, sampleRate, false);

        float[] samples = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / sampleRate;
            float env = 1f - ((float)i / count);
            samples[i] = Mathf.Sin(2f * Mathf.PI * pitch * t) * env * 0.15f;
        }

        clip.SetData(samples, 0);
        audioSource.PlayOneShot(clip);
    }
}
