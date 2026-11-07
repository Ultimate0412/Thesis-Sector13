#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// สร้าง Animator Controller ของผู้เล่นจาก clip ใน rig อัตโนมัติ
/// เมนู: Tools > Player > Create Animator Controller
/// วางสคริปต์นี้ในโฟลเดอร์ชื่อ Editor
/// </summary>
public static class CreatePlayerAnimatorController
{
    private const string OutputPath = "Assets/_Project/Animator/PlayerAnimator.controller";

    // walkSpeed / runSpeed ใน PlayerController (4 / 7)
    private const float WalkThreshold = 4f / 7f;

    [MenuItem("Tools/Player/Create Animator Controller")]
    public static void Create()
    {
        AnimationClip idle = FindClip("Idle");
        AnimationClip walkF = FindClip("Walk_Forward");
        AnimationClip walkB = FindClip("Walk_Backward");
        AnimationClip run = FindClip("Run");
        AnimationClip jump = FindClip("Jump_Up");
        AnimationClip landing = FindClip("Landing");
        AnimationClip pick = FindClip("Pick");

        if (idle == null || walkF == null || walkB == null || run == null ||
            jump == null || landing == null || pick == null)
        {
            var names = new System.Collections.Generic.List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets")) continue;
                foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (o is AnimationClip c && !c.name.StartsWith("__preview__")) names.Add(c.name);
            }
            Debug.LogError("[PlayerAnimator] หา clip ไม่ครบ (Idle, Walk_Forward, Walk_Backward, Run, Jump_Up, Landing, Pick) clip ที่พบในโปรเจกต์: " + string.Join(", ", names));
            return;
        }

        var controller = AnimatorController.CreateAnimatorControllerAtPath(OutputPath);

        // Parameters
        controller.AddParameter("MoveZ", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsRunning", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Pick", AnimatorControllerParameterType.Trigger);
        // ค่าเริ่มต้นของ IsGrounded = true
        var ps = controller.parameters;
        for (int i = 0; i < ps.Length; i++)
            if (ps[i].name == "IsGrounded") ps[i].defaultBool = true;
        controller.parameters = ps;

        // Locomotion Blend Tree (1D บน MoveZ)
        controller.CreateBlendTreeInController("Locomotion", out BlendTree tree);
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "MoveZ";
        tree.useAutomaticThresholds = false;
        tree.AddChild(walkB, -1f);
        tree.AddChild(idle, 0f);
        tree.AddChild(walkF, WalkThreshold);
        tree.AddChild(run, 1f);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        AnimatorState loco = null;
        foreach (var cs in sm.states)
            if (cs.state.name == "Locomotion") loco = cs.state;
        sm.defaultState = loco;

        AnimatorState jumpState = sm.AddState("Jump", new Vector3(300, 120));
        jumpState.motion = jump;
        AnimatorState landState = sm.AddState("Landing", new Vector3(300, 200));
        landState.motion = landing;
        AnimatorState pickState = sm.AddState("Pick", new Vector3(300, 280));
        pickState.motion = pick;

        // Locomotion -> Jump (ไม่แตะพื้น: กระโดด/ตกจากขอบ)
        var t = loco.AddTransition(jumpState);
        t.hasExitTime = false; t.duration = 0.08f;
        t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsGrounded");

        // Jump -> Landing (แตะพื้น)
        t = jumpState.AddTransition(landState);
        t.hasExitTime = false; t.duration = 0.05f;
        t.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");

        // Landing -> Locomotion (เล่นจบ)
        t = landState.AddTransition(loco);
        t.hasExitTime = true; t.exitTime = 0.85f; t.duration = 0.15f;

        // Landing -> Jump (ลอยอีกครั้งก่อนจบ)
        t = landState.AddTransition(jumpState);
        t.hasExitTime = false; t.duration = 0.05f;
        t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsGrounded");

        // Any State -> Pick (Trigger)
        t = sm.AddAnyStateTransition(pickState);
        t.hasExitTime = false; t.duration = 0.1f; t.canTransitionToSelf = false;
        t.AddCondition(AnimatorConditionMode.If, 0, "Pick");

        // Pick -> Locomotion
        t = pickState.AddTransition(loco);
        t.hasExitTime = true; t.exitTime = 0.9f; t.duration = 0.15f;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Selection.activeObject = controller;
        Debug.Log("[PlayerAnimator] สร้าง Controller สำเร็จที่ " + OutputPath);
    }

    /// <summary>
    /// ย้าย Pick ไปไว้ใน Layer "RightArm" ที่มี Avatar Mask เฉพาะแขนขวา
    /// ทำให้ท่า Pick เล่นแค่แขนขวา ส่วนขา/ลำตัว/แขนซ้ายยังเป็นท่าเดินจาก Base Layer
    /// รันซ้ำได้ (จะสร้าง Layer ใหม่ทับของเดิม)
    /// </summary>
    [MenuItem("Tools/Player/Setup Right Arm Pick Layer")]
    public static void SetupRightArmPickLayer()
    {
        string[] guids = AssetDatabase.FindAssets("PlayerAnimator t:AnimatorController");
        if (guids.Length == 0)
        {
            Debug.LogError("[PlayerAnimator] ไม่พบ PlayerAnimator.controller");
            return;
        }

        string controllerPath = AssetDatabase.GUIDToAssetPath(guids[0]);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);

        // 1) Avatar Mask แบบ Transform (rig เป็น Generic จึงใช้ Humanoid mask ไม่ได้)
        string modelPath = FindModelPath();
        if (modelPath == null)
        {
            Debug.LogError("[PlayerAnimator] ไม่พบโมเดล PlayerWithAnim2 (fbx) สำหรับสร้าง mask");
            return;
        }
        Transform modelRoot = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath).transform;

        string folder = System.IO.Path.GetDirectoryName(controllerPath).Replace('\\', '/');
        string maskPath = folder + "/RightArmMask.mask";
        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);
        bool newMask = mask == null;
        if (newMask) mask = new AvatarMask();

        mask.transformCount = 0;
        mask.AddTransformPath(modelRoot, true);

        int activeCount = 0;
        var activeNames = new System.Collections.Generic.List<string>();
        var allPaths = new System.Collections.Generic.List<string>();
        for (int i = 0; i < mask.transformCount; i++)
        {
            string path = mask.GetTransformPath(i);
            allPaths.Add(path);
            bool on = IsRightArmPath(path);
            mask.SetTransformActive(i, on);
            if (on) { activeCount++; activeNames.Add(path); }
        }

        if (activeCount == 0)
        {
            Debug.LogError("[PlayerAnimator] ไม่พบกระดูกแขนขวาในโมเดล กระดูกที่พบ: " + string.Join(", ", allPaths));
            return;
        }

        if (newMask) AssetDatabase.CreateAsset(mask, maskPath);
        EditorUtility.SetDirty(mask);
        Debug.Log("[PlayerAnimator] Mask เปิด " + activeCount + " กระดูก: " + string.Join(", ", activeNames));

        // 2) พารามิเตอร์ Pick
        bool hasPickParam = false;
        foreach (var p in controller.parameters)
            if (p.name == "Pick") hasPickParam = true;
        if (!hasPickParam) controller.AddParameter("Pick", AnimatorControllerParameterType.Trigger);

        // 3) เอา state Pick ออกจาก Base Layer (เก็บ clip ไว้ใช้ต่อ)
        AnimationClip pickClip = null;
        AnimatorStateMachine baseSm = controller.layers[0].stateMachine;
        foreach (var cs in baseSm.states)
        {
            if (cs.state.name == "Pick")
            {
                pickClip = cs.state.motion as AnimationClip;
                baseSm.RemoveState(cs.state); // transition ที่เกี่ยวข้องถูกลบไปด้วย
                break;
            }
        }
        if (pickClip == null) pickClip = FindClip("Pick");
        if (pickClip == null) { Debug.LogError("[PlayerAnimator] ไม่พบ clip Pick"); return; }

        // 4) ลบ Layer RightArm เดิม (ถ้ามี) แล้วสร้างใหม่
        for (int i = controller.layers.Length - 1; i >= 1; i--)
            if (controller.layers[i].name == "RightArm") controller.RemoveLayer(i);

        controller.AddLayer("RightArm");
        AnimatorControllerLayer[] layers = controller.layers;
        AnimatorControllerLayer layer = layers[layers.Length - 1];
        layer.avatarMask = mask;
        layer.defaultWeight = 1f;
        layer.blendingMode = AnimatorLayerBlendingMode.Override;
        controller.layers = layers;

        // 5) state ใน Layer: Empty (ไม่ทับท่าจาก Base Layer) <-> Pick
        AnimatorStateMachine sm = layer.stateMachine;
        AnimatorState empty = sm.AddState("Empty", new Vector3(300, 100));
        sm.defaultState = empty;
        AnimatorState pickState = sm.AddState("Pick", new Vector3(300, 200));
        pickState.motion = pickClip;

        var t = sm.AddAnyStateTransition(pickState);
        t.hasExitTime = false;
        t.hasFixedDuration = true;
        t.duration = 0.1f;
        t.canTransitionToSelf = false;
        t.AddCondition(AnimatorConditionMode.If, 0, "Pick");

        t = pickState.AddTransition(empty);
        t.hasExitTime = true;
        t.exitTime = 0.95f;
        t.hasFixedDuration = true;
        t.duration = 0.1f;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("[PlayerAnimator] สร้าง Layer RightArm + Avatar Mask สำเร็จ (" + maskPath + ")");
    }

    private static string FindModelPath()
    {
        foreach (string guid in AssetDatabase.FindAssets("PlayerWithAnim2 t:GameObject"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetImporter.GetAtPath(path) is ModelImporter) return path;
        }
        return null;
    }

    // กระดูกในสายแขนขวา: ชื่อมี ".R" และเป็นส่วนของ shoulder/arm/hand/นิ้ว (ไม่นับขา/ใบหน้า)
    private static bool IsRightArmPath(string path)
    {
        string[] armKeys = { "shoulder", "clavicle", "arm", "hand", "palm", "thumb", "f_index", "f_middle", "f_ring", "f_pinky" };
        foreach (string segment in path.Split('/'))
        {
            string n = segment.ToLowerInvariant();
            if (!n.Contains(".r")) continue;
            if (n.Contains("leg") || n.Contains("thigh") || n.Contains("shin") || n.Contains("foot")) continue;
            foreach (string key in armKeys)
                if (n.Contains(key)) return true;
        }
        return false;
    }

    private static AnimationClip FindClip(string key)
    {
        AnimationClip fallback = null;
        foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.StartsWith("Assets")) continue; // ข้ามคลิปจาก Packages
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (o is AnimationClip c && !c.name.StartsWith("__preview__"))
                {
                    // เทียบเฉพาะชื่อหลังเครื่องหมาย | และไม่สนตัวพิมพ์เล็กใหญ่ เช่น "rig|Idle" -> "idle"
                    string shortName = c.name.Substring(c.name.LastIndexOf('|') + 1);
                    if (string.Equals(shortName, key, System.StringComparison.OrdinalIgnoreCase))
                    {
                        if (path.StartsWith("Assets/_Project")) return c;
                        if (fallback == null) fallback = c;
                    }
                }
            }
        }
        return fallback;
    }
}
#endif