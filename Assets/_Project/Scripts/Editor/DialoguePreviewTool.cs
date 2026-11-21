using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// เครื่องมือสลับการแสดงบับเบิ้ลบทสนทนาใน Scene view
/// - ปกติ DialoguePanel จะถูกซ่อนตอนเริ่มเล่น (Awake) แต่ใน Editor จะแสดงไว้เพื่อจัดวางตำแหน่ง
/// - ใช้เมนูนี้เพื่อซ่อน/แสดงชั่วคราวเมื่อบับเบิ้ลบังการมองเห็นวัตถุอื่น
/// </summary>
public static class DialoguePreviewTool
{
    private const string MenuRoot = "Tools/Sector13/Dialogue/";

    [MenuItem(MenuRoot + "Show Preview In Scene", false, 10)]
    public static void ShowPreview()
    {
        SetPreview(true);
    }

    [MenuItem(MenuRoot + "Hide Preview In Scene", false, 11)]
    public static void HidePreview()
    {
        SetPreview(false);
    }

    private static void SetPreview(bool visible)
    {
        List<DialogueHUD> huds = FindAllHUDs();
        if (huds.Count == 0)
        {
            Debug.Log("[DialoguePreviewTool] ไม่พบ DialogueHUD ใน Scene ที่เปิดอยู่");
            return;
        }

        string undoLabel = visible ? "Show Dialogue Preview" : "Hide Dialogue Preview";
        int changed = 0;

        foreach (DialogueHUD hud in huds)
        {
            if (hud == null || hud.dialoguePanel == null) continue;

            Undo.RecordObject(hud.dialoguePanel, undoLabel);
            if (hud.canvasGroup != null)
            {
                Undo.RecordObject(hud.canvasGroup, undoLabel);
                hud.canvasGroup.alpha = visible ? 1f : 0f;
            }

            hud.dialoguePanel.SetActive(visible);

            EditorUtility.SetDirty(hud.dialoguePanel);
            if (hud.canvasGroup != null) EditorUtility.SetDirty(hud.canvasGroup);
            changed++;
        }

        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null)
        {
            EditorSceneManager.MarkSceneDirty(stage.scene);
        }
        else
        {
            Scene active = SceneManager.GetActiveScene();
            if (active.IsValid()) EditorSceneManager.MarkSceneDirty(active);
        }

        Debug.Log($"[DialoguePreviewTool] {(visible ? "แสดง" : "ซ่อน")} บับเบิ้ลบทสนทนา {changed} จุดใน Scene view");
    }

    private static List<DialogueHUD> FindAllHUDs()
    {
        List<DialogueHUD> result = new List<DialogueHUD>();

        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null)
        {
            foreach (GameObject root in stage.scene.GetRootGameObjects())
            {
                result.AddRange(root.GetComponentsInChildren<DialogueHUD>(true));
            }

            return result;
        }

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                result.AddRange(root.GetComponentsInChildren<DialogueHUD>(true));
            }
        }

        return result;
    }
}
