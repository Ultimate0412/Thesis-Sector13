#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public class PlayerAnimatorGenerator : EditorWindow
{
    private enum LocomotionMode
    {
        MoveZAndIsRunning,
        SpeedFromPlayerController
    }

    private const string DefaultFbxPath = "Assets/_Project/Art/Animations/Animation/Player/PlayerRigging.fbx";
    private const string DefaultMaskPath = "Assets/_Project/Art/Animations/Animation/Player/RightArmMask.mask";
    private const string DefaultOutputPath = "Assets/_Project/Art/Animations/Animation/Player/PlayerAnimator_New.controller";

    private GameObject sourceFbx;
    private AvatarMask armMask;
    private string outputPath = DefaultOutputPath;
    private LocomotionMode locomotionMode = LocomotionMode.MoveZAndIsRunning;
    private bool assignMaskToArmLayer = true;

    [MenuItem("Tools/Animation/Generate Player Animator Controller")]
    public static void OpenWindow()
    {
        PlayerAnimatorGenerator window = GetWindow<PlayerAnimatorGenerator>("Player Animator Generator");
        window.minSize = new Vector2(440f, 230f);
    }

    private void OnEnable()
    {
        if (sourceFbx == null)
        {
            sourceFbx = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultFbxPath);
        }

        if (armMask == null)
        {
            armMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(DefaultMaskPath);
        }
    }

    private void OnGUI()
    {
        GUILayout.Space(8f);
        sourceFbx = (GameObject)EditorGUILayout.ObjectField("Source FBX", sourceFbx, typeof(GameObject), false);
        armMask = (AvatarMask)EditorGUILayout.ObjectField("Arm Avatar Mask", armMask, typeof(AvatarMask), false);
        assignMaskToArmLayer = EditorGUILayout.Toggle("Use Mask on Arm Layer", assignMaskToArmLayer);
        locomotionMode = (LocomotionMode)EditorGUILayout.EnumPopup("Locomotion Mode", locomotionMode);
        outputPath = EditorGUILayout.TextField("Output Path", outputPath);

        GUILayout.Space(12f);

        using (new EditorGUI.DisabledScope(sourceFbx == null || string.IsNullOrWhiteSpace(outputPath)))
        {
            if (GUILayout.Button("Generate Animator Controller", GUILayout.Height(34f)))
            {
                GenerateController();
            }
        }
    }

    private void GenerateController()
    {
        string fbxPath = AssetDatabase.GetAssetPath(sourceFbx);
        if (string.IsNullOrEmpty(fbxPath))
        {
            EditorUtility.DisplayDialog("Error", "ไม่พบไฟล์ FBX ที่เลือก", "OK");
            return;
        }

        Dictionary<string, AnimationClip> clips = LoadClipsFromFbx(fbxPath);
        if (clips.Count == 0)
        {
            EditorUtility.DisplayDialog("Error", "ไม่พบ AnimationClip ภายในไฟล์ FBX นี้", "OK");
            return;
        }

        string directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(outputPath))
        {
            AssetDatabase.DeleteAsset(outputPath);
        }

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(outputPath);

        AddParameters(controller);
        BuildBaseLayer(controller, clips);
        BuildArmLayer(controller, clips);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = controller;
        EditorGUIUtility.PingObject(controller);

        Debug.Log($"[PlayerAnimatorGenerator] สร้าง Animator Controller สำเร็จที่: {outputPath} (พบ {clips.Count} คลิป)");
    }

    private static Dictionary<string, AnimationClip> LoadClipsFromFbx(string fbxPath)
    {
        Dictionary<string, AnimationClip> clipMap = new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);

        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
            {
                string cleanName = GetCleanClipName(clip.name);
                if (!clipMap.ContainsKey(cleanName))
                {
                    clipMap.Add(cleanName, clip);
                }
            }
        }

        return clipMap;
    }

    private static string GetCleanClipName(string rawName)
    {
        int pipeIndex = rawName.LastIndexOf('|');
        return pipeIndex >= 0 ? rawName.Substring(pipeIndex + 1) : rawName;
    }

    private static AnimationClip FindClip(Dictionary<string, AnimationClip> clips, string clipKey)
    {
        clips.TryGetValue(clipKey, out AnimationClip clip);
        return clip;
    }

    private static void AddParameters(AnimatorController controller)
    {
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("MoveZ", AnimatorControllerParameterType.Float);
        controller.AddParameter(new AnimatorControllerParameter
        {
            name = "IsGrounded",
            type = AnimatorControllerParameterType.Bool,
            defaultBool = true
        });
        controller.AddParameter("IsRunning", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Pick", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("IsBatonEquipped", AnimatorControllerParameterType.Bool);
        controller.AddParameter("BatonAttack", AnimatorControllerParameterType.Trigger);
    }

    private void BuildBaseLayer(AnimatorController controller, Dictionary<string, AnimationClip> clips)
    {
        AnimatorStateMachine sm = controller.layers[0].stateMachine;

        AnimatorState idle = CreateState(sm, "Idle", FindClip(clips, "Idle"), new Vector3(260f, 120f, 0f));
        AnimatorState walkForward = CreateState(sm, "Walk_Forward", FindClip(clips, "Walk_Forward"), new Vector3(540f, 40f, 0f));
        AnimatorState walkBackward = CreateState(sm, "Walk_Backward", FindClip(clips, "Walk_Backward"), new Vector3(540f, 200f, 0f));
        AnimatorState run = CreateState(sm, "Run", FindClip(clips, "Run"), new Vector3(820f, 120f, 0f));
        AnimatorState jumpUp = CreateState(sm, "Jump_Up", FindClip(clips, "Jump_Up"), new Vector3(260f, -80f, 0f));
        AnimatorState landing = CreateState(sm, "Landing", FindClip(clips, "Landing"), new Vector3(540f, -80f, 0f));

        sm.defaultState = idle;

        if (locomotionMode == LocomotionMode.MoveZAndIsRunning)
        {
            AddTransition(idle, walkForward, false, 0.1f,
                (AnimatorConditionMode.Greater, 0.1f, "MoveZ"),
                (AnimatorConditionMode.IfNot, 0f, "IsRunning"));

            AddTransition(idle, walkBackward, false, 0.1f,
                (AnimatorConditionMode.Less, -0.1f, "MoveZ"));

            AddTransition(idle, run, false, 0.1f,
                (AnimatorConditionMode.If, 0f, "IsRunning"));

            AddTransition(walkForward, idle, false, 0.1f,
                (AnimatorConditionMode.Less, 0.1f, "MoveZ"),
                (AnimatorConditionMode.Greater, -0.1f, "MoveZ"));

            AddTransition(walkForward, walkBackward, false, 0.1f,
                (AnimatorConditionMode.Less, -0.1f, "MoveZ"));

            AddTransition(walkForward, run, false, 0.1f,
                (AnimatorConditionMode.If, 0f, "IsRunning"));

            AddTransition(walkBackward, idle, false, 0.1f,
                (AnimatorConditionMode.Greater, -0.1f, "MoveZ"),
                (AnimatorConditionMode.Less, 0.1f, "MoveZ"));

            AddTransition(walkBackward, walkForward, false, 0.1f,
                (AnimatorConditionMode.Greater, 0.1f, "MoveZ"),
                (AnimatorConditionMode.IfNot, 0f, "IsRunning"));

            AddTransition(walkBackward, run, false, 0.1f,
                (AnimatorConditionMode.Greater, 0.1f, "MoveZ"),
                (AnimatorConditionMode.If, 0f, "IsRunning"));

            AddTransition(run, walkForward, false, 0.1f,
                (AnimatorConditionMode.IfNot, 0f, "IsRunning"),
                (AnimatorConditionMode.Greater, 0.1f, "MoveZ"));

            AddTransition(run, walkBackward, false, 0.1f,
                (AnimatorConditionMode.Less, -0.1f, "MoveZ"));

            AddTransition(run, idle, false, 0.1f,
                (AnimatorConditionMode.IfNot, 0f, "IsRunning"),
                (AnimatorConditionMode.Less, 0.1f, "MoveZ"),
                (AnimatorConditionMode.Greater, -0.1f, "MoveZ"));
        }
        else
        {
            AddTransition(idle, walkForward, false, 0.1f,
                (AnimatorConditionMode.Greater, 0.1f, "Speed"));

            AddTransition(walkForward, idle, false, 0.1f,
                (AnimatorConditionMode.Less, 0.1f, "Speed"));

            AddTransition(walkForward, run, false, 0.1f,
                (AnimatorConditionMode.Greater, 5.0f, "Speed"));

            AddTransition(run, walkForward, false, 0.1f,
                (AnimatorConditionMode.Less, 5.0f, "Speed"),
                (AnimatorConditionMode.Greater, 0.1f, "Speed"));

            AddTransition(run, idle, false, 0.1f,
                (AnimatorConditionMode.Less, 0.1f, "Speed"));
        }

        AnimatorStateTransition anyToJump = sm.AddAnyStateTransition(jumpUp);
        anyToJump.hasExitTime = false;
        anyToJump.canTransitionToSelf = false;
        anyToJump.duration = 0.08f;
        anyToJump.AddCondition(AnimatorConditionMode.If, 0f, "Jump");

        AddTransition(idle, jumpUp, false, 0.08f, (AnimatorConditionMode.IfNot, 0f, "IsGrounded"));
        AddTransition(walkForward, jumpUp, false, 0.08f, (AnimatorConditionMode.IfNot, 0f, "IsGrounded"));
        AddTransition(walkBackward, jumpUp, false, 0.08f, (AnimatorConditionMode.IfNot, 0f, "IsGrounded"));
        AddTransition(run, jumpUp, false, 0.08f, (AnimatorConditionMode.IfNot, 0f, "IsGrounded"));

        AddTransition(jumpUp, landing, false, 0.08f, (AnimatorConditionMode.If, 0f, "IsGrounded"));

        AnimatorStateTransition landingToIdle = landing.AddTransition(idle);
        landingToIdle.hasExitTime = true;
        landingToIdle.exitTime = 0.85f;
        landingToIdle.duration = 0.1f;
    }

    private void BuildArmLayer(AnimatorController controller, Dictionary<string, AnimationClip> clips)
    {
        controller.AddLayer("Arm");
        AnimatorControllerLayer[] layers = controller.layers;
        layers[1].defaultWeight = 1f;
        layers[1].blendingMode = AnimatorLayerBlendingMode.Override;
        layers[1].avatarMask = assignMaskToArmLayer ? armMask : null;
        controller.layers = layers;

        AnimatorStateMachine sm = controller.layers[1].stateMachine;

        AnimatorState empty = CreateState(sm, "Empty", null, new Vector3(280f, 120f, 0f));
        AnimatorState pick = CreateState(sm, "Pick", FindClip(clips, "Pick"), new Vector3(280f, 240f, 0f));
        AnimatorState batonStart = CreateState(sm, "baton_Start", FindClip(clips, "baton_Start"), new Vector3(280f, -20f, 0f));
        AnimatorState batonIdle = CreateState(sm, "baton_Idle", FindClip(clips, "baton_Idle"), new Vector3(580f, -20f, 0f));
        AnimatorState batonAttack = CreateState(sm, "baton_Attack", FindClip(clips, "baton_Attack"), new Vector3(580f, -150f, 0f));

        sm.defaultState = empty;

        AnimatorStateTransition anyToPick = sm.AddAnyStateTransition(pick);
        anyToPick.hasExitTime = false;
        anyToPick.canTransitionToSelf = false;
        anyToPick.duration = 0.08f;
        anyToPick.AddCondition(AnimatorConditionMode.If, 0f, "Pick");

        AnimatorStateTransition pickToEmpty = pick.AddTransition(empty);
        pickToEmpty.hasExitTime = true;
        pickToEmpty.exitTime = 0.9f;
        pickToEmpty.duration = 0.1f;

        AddTransition(empty, batonStart, false, 0.1f,
            (AnimatorConditionMode.If, 0f, "IsBatonEquipped"));

        AnimatorStateTransition startToIdle = batonStart.AddTransition(batonIdle);
        startToIdle.hasExitTime = true;
        startToIdle.exitTime = 0.9f;
        startToIdle.duration = 0.1f;

        AddTransition(batonStart, empty, false, 0.1f,
            (AnimatorConditionMode.IfNot, 0f, "IsBatonEquipped"));

        AddTransition(batonIdle, empty, false, 0.1f,
            (AnimatorConditionMode.IfNot, 0f, "IsBatonEquipped"));

        AddTransition(batonIdle, batonAttack, false, 0.08f,
            (AnimatorConditionMode.If, 0f, "BatonAttack"));

        AnimatorStateTransition attackToIdle = batonAttack.AddTransition(batonIdle);
        attackToIdle.hasExitTime = true;
        attackToIdle.exitTime = 0.9f;
        attackToIdle.duration = 0.1f;

        AddTransition(batonAttack, empty, false, 0.1f,
            (AnimatorConditionMode.IfNot, 0f, "IsBatonEquipped"));
    }

    private static AnimatorState CreateState(AnimatorStateMachine sm, string name, AnimationClip clip, Vector3 position)
    {
        AnimatorState state = sm.AddState(name, position);
        state.motion = clip;
        return state;
    }

    private static AnimatorStateTransition AddTransition(
        AnimatorState from,
        AnimatorState to,
        bool hasExitTime,
        float duration,
        params (AnimatorConditionMode mode, float threshold, string param)[] conditions)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = hasExitTime;
        transition.duration = duration;

        for (int i = 0; i < conditions.Length; i++)
        {
            transition.AddCondition(conditions[i].mode, conditions[i].threshold, conditions[i].param);
        }

        return transition;
    }
}
#endif

// --- สรุปการทำงานของโค้ด ---
// 1. สร้างหน้าต่างเครื่องมือผ่านเมนู Tools -> Animation -> Generate Player Animator Controller เพื่อเจน Animator Controller ใหม่จาก PlayerRigging.fbx
// 2. เชื่อมคลิป Base Layer (Idle, Walk_Forward, Walk_Backward, Run, Jump_Up, Landing) และ Arm Layer (Empty, Pick, baton_Start, baton_Idle, baton_Attack) พร้อมตั้ง Weight = 1 ให้อัตโนมัติ
// 3. วางคำสั่ง #endif และปีกกาปิดคลาสไว้ครบถ้วนเพื่อป้องกัน Error CS1002, CS1513 และ CS1027