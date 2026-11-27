using System;
using System.Text;
using UnityEngine;

[DefaultExecutionOrder(100)]
public class PlayerHandGripIK : MonoBehaviour
{
    [Serializable]
    public class ArmIKSettings
    {
        public string gripChildName = "right arm grip";
        public Transform upperArm;
        public Transform forearm;
        public Transform hand;
        public Transform elbowHint;
        public Vector3 defaultElbowOffset = new Vector3(0.6f, -0.4f, -0.4f);
        public bool matchHandRotation = true;
        public Vector3 handRotationOffset = Vector3.zero;

        [HideInInspector] public Transform activeGrip;
        [HideInInspector] public float currentWeight;

        public bool IsValid => upperArm != null && forearm != null && hand != null;
    }

    [Header("References")]
    public PlayerPickupSystem pickupSystem;
    public Transform referenceRoot;

    [Header("Blend Settings")]
    public float blendSpeed = 12f;

    [Header("Debug")]
    public bool enableDebugLogs = true;

    [Header("Arm IK Setup")]
    public ArmIKSettings rightArm = new ArmIKSettings
    {
        gripChildName = "right arm grip",
        defaultElbowOffset = new Vector3(0.6f, -0.4f, -0.4f)
    };

    public ArmIKSettings leftArm = new ArmIKSettings
    {
        gripChildName = "left arm grip",
        defaultElbowOffset = new Vector3(-0.6f, -0.4f, -0.4f)
    };

    private GameObject lastTargetObject;

    private void Awake()
    {
        if (pickupSystem == null)
        {
            pickupSystem = GetComponentInParent<PlayerPickupSystem>();
        }

        if (referenceRoot == null)
        {
            referenceRoot = pickupSystem != null ? pickupSystem.transform : transform;
        }
    }

    private void LateUpdate()
    {
        if (pickupSystem == null)
        {
            pickupSystem = PlayerPickupSystem.Instance;
        }

        GameObject currentTarget = GetCurrentGripSourceObject();
        if (currentTarget != lastTargetObject)
        {
            lastTargetObject = currentTarget;
            RefreshGripTargets(currentTarget);
        }

        UpdateArmIK(rightArm);
        UpdateArmIK(leftArm);
    }

    private GameObject GetCurrentGripSourceObject()
    {
        if (pickupSystem != null && pickupSystem.heldObject != null)
        {
            return pickupSystem.heldObject;
        }

        InspectionTablet tablet = InspectionTablet.Instance;
        if (tablet != null && tablet.IsTabletHeld && tablet.tabletTransform != null)
        {
            return tablet.tabletTransform.gameObject;
        }

        return null;
    }

    private void RefreshGripTargets(GameObject targetObj)
    {
        if (targetObj == null)
        {
            rightArm.activeGrip = null;
            leftArm.activeGrip = null;
            return;
        }

        rightArm.activeGrip = FindGripChild(targetObj.transform, rightArm.gripChildName);
        leftArm.activeGrip = FindGripChild(targetObj.transform, leftArm.gripChildName);

        if (enableDebugLogs)
        {
            LogArmDiagnostic("RightArm", rightArm, targetObj);
            LogArmDiagnostic("LeftArm", leftArm, targetObj);
        }
    }

    private void LogArmDiagnostic(string armLabel, ArmIKSettings arm, GameObject targetObj)
    {
        if (!arm.IsValid)
        {
            Debug.LogWarning(
                $"[PlayerHandGripIK] {armLabel} ไม่ทำงาน: ใส่กระดูกไม่ครบ " +
                $"(upperArm={arm.upperArm}, forearm={arm.forearm}, hand={arm.hand})"
            );
            return;
        }

        float upperLen = Vector3.Distance(arm.upperArm.position, arm.forearm.position);
        float lowerLen = Vector3.Distance(arm.forearm.position, arm.hand.position);
        if (upperLen < 0.0001f || lowerLen < 0.0001f)
        {
            Debug.LogWarning(
                $"[PlayerHandGripIK] {armLabel} ไม่ทำงาน: ระยะกระดูกเป็น 0 " +
                $"(upperLen={upperLen:F4}, lowerLen={lowerLen:F4})"
            );
            return;
        }

        if (arm.activeGrip == null)
        {
            Debug.Log(
                $"[PlayerHandGripIK] {armLabel} ไม่พบ '{arm.gripChildName}' ใต้ '{targetObj.name}' (ข้ามแขนข้างนี้) | " +
                $"ลูกที่มี: {ListChildNames(targetObj.transform)}"
            );
            return;
        }

        Debug.Log($"[PlayerHandGripIK] {armLabel} พร้อม Snap เข้ากับ '{arm.activeGrip.name}' ใต้ '{targetObj.name}'");
    }

    private static string ListChildNames(Transform root)
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        StringBuilder sb = new StringBuilder();

        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] == root) continue;
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(children[i].name);
        }

        return sb.Length > 0 ? sb.ToString() : "(ไม่มีลูก)";
    }

    private void UpdateArmIK(ArmIKSettings arm)
    {
        if (!arm.IsValid) return;

        bool hasTarget = arm.activeGrip != null && arm.activeGrip.gameObject.activeInHierarchy;
        float targetWeight = hasTarget ? 1f : 0f;
        arm.currentWeight = Mathf.MoveTowards(arm.currentWeight, targetWeight, Time.deltaTime * blendSpeed);

        if (arm.currentWeight <= 0.001f || !hasTarget) return;

        SolveTwoBoneIK(arm, arm.activeGrip, arm.currentWeight);
    }

    private void SolveTwoBoneIK(ArmIKSettings arm, Transform target, float weight)
    {
        Transform upper = arm.upperArm;
        Transform mid = arm.forearm;
        Transform tip = arm.hand;

        Quaternion origUpperRot = upper.rotation;
        Quaternion origMidRot = mid.rotation;
        Quaternion origTipRot = tip.rotation;

        Vector3 rootPos = upper.position;
        Vector3 midPos = mid.position;
        Vector3 tipPos = tip.position;
        Vector3 targetPos = target.position;

        float upperLen = Vector3.Distance(rootPos, midPos);
        float lowerLen = Vector3.Distance(midPos, tipPos);
        if (upperLen < 0.0001f || lowerLen < 0.0001f) return;

        float minReach = Mathf.Abs(upperLen - lowerLen) + 0.001f;
        float maxReach = upperLen + lowerLen - 0.001f;
        Vector3 toTarget = targetPos - rootPos;
        float targetDist = Mathf.Clamp(toTarget.magnitude, minReach, maxReach);
        Vector3 dirToTarget = toTarget.sqrMagnitude > 0.00001f ? toTarget.normalized : referenceRoot.forward;

        Vector3 hintPos = arm.elbowHint != null
            ? arm.elbowHint.position
            : rootPos + referenceRoot.TransformDirection(arm.defaultElbowOffset);

        Vector3 bendDir = Vector3.ProjectOnPlane(hintPos - rootPos, dirToTarget);
        if (bendDir.sqrMagnitude < 0.0001f)
        {
            bendDir = Vector3.ProjectOnPlane(-referenceRoot.up, dirToTarget);
        }
        bendDir.Normalize();

        float cosAngle = Mathf.Clamp(
            (upperLen * upperLen + targetDist * targetDist - lowerLen * lowerLen) / (2f * upperLen * targetDist),
            -1f,
            1f
        );
        float sinAngle = Mathf.Sqrt(Mathf.Max(0f, 1f - cosAngle * cosAngle));
        Vector3 desiredUpperDir = dirToTarget * cosAngle + bendDir * sinAngle;

        Vector3 currentUpperDir = (midPos - rootPos).normalized;
        Quaternion targetUpperRot = Quaternion.FromToRotation(currentUpperDir, desiredUpperDir) * origUpperRot;
        upper.rotation = targetUpperRot;

        Vector3 newMidPos = mid.position;
        Vector3 newTipPos = tip.position;
        Vector3 currentLowerDir = (newTipPos - newMidPos).normalized;
        Vector3 desiredLowerDir = (targetPos - newMidPos).normalized;
        Quaternion targetMidRot = Quaternion.FromToRotation(currentLowerDir, desiredLowerDir) * mid.rotation;

        Quaternion targetTipRot = target.rotation * Quaternion.Euler(arm.handRotationOffset);

        upper.rotation = Quaternion.Slerp(origUpperRot, targetUpperRot, weight);
        mid.rotation = Quaternion.Slerp(origMidRot, targetMidRot, weight);

        if (arm.matchHandRotation)
        {
            tip.rotation = Quaternion.Slerp(origTipRot, targetTipRot, weight);
        }
    }

    private static Transform FindGripChild(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrWhiteSpace(targetName)) return null;

        string normalizedTarget = NormalizeName(targetName);
        Transform[] children = root.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] == root) continue;
            if (NormalizeName(children[i].name) == normalizedTarget)
            {
                return children[i];
            }
        }

        return null;
    }

    private static string NormalizeName(string raw)
    {
        return raw.Replace(" ", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
    }
}

// --- สรุปการทำงานของโค้ด ---
// 1. เพิ่ม GetCurrentGripSourceObject() ให้ตรวจสอบทั้งวัตถุที่ถือจาก PlayerPickupSystem.heldObject และ InspectionTablet.Instance (เมื่อ IsTabletHeld เป็น true)
// 2. ใส่ [DefaultExecutionOrder(100)] เพื่อให้ LateUpdate ของระบบ IK ทำงานหลัง PlayerController หมุนกล้องเสร็จ ป้องกันอาการมือดีเลย์ 1 เฟรมเวลาเงย/ก้มกล้องขณะถือแท็บเล็ต
// 3. หากใส่เฉพาะ "left arm grip" ไว้ใต้ InspectionTabletRoot ระบบจะ Snap เฉพาะแขนซ้ายเข้ากับแท็บเล็ต และปล่อยแขนขวาเล่นอนิเมชันปกติโดยอัตโนมัติ