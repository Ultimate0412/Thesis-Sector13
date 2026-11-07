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
            Debug.LogError("[PlayerAnimator] หา clip ไม่ครบ (Idle, Walk_Forward, Walk_Backward, Run, Jump_Up, Landing, Pick) ตรวจชื่อ clip ใน rig.fbx");
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

    private static AnimationClip FindClip(string key)
    {
        AnimationClip fallback = null;
        foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (o is AnimationClip c && !c.name.StartsWith("__preview__") &&
                    (c.name == key || c.name.EndsWith("|" + key)))
                {
                    if (path.StartsWith("Assets/_Project")) return c;
                    if (fallback == null) fallback = c;
                }
            }
        }
        return fallback;
    }
}
#endif
