using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// เครื่องมือช่วยผูก reference ของเมนูใน scene (ใช้ครั้งแรก หรือหลังเพิ่ม/ย้าย object)
/// เมนู: Tools/Sector13/UI/...
/// </summary>
public static class MenuReferenceAssigner
{
    private const string MainMenuCanvasName = "MainMenuCanvas";
    private const string PauseMenuCanvasName = "PauseMenuCanvas";

    [MenuItem("Tools/Sector13/UI/Assign Main Menu References")]
    public static void AssignMainMenu()
    {
        MainMenuController controller = Object.FindAnyObjectByType<MainMenuController>();
        if (controller == null)
        {
            Debug.LogError("[MenuReferenceAssigner] ไม่พบ MainMenuController ในซีนที่เปิดอยู่");
            return;
        }

        Transform canvas = controller.transform.Find(MainMenuCanvasName);
        if (canvas == null)
        {
            Debug.LogError($"[MenuReferenceAssigner] ไม่พบ {MainMenuCanvasName} ใต้ {controller.name}");
            return;
        }

        Undo.RecordObject(controller, "Assign Main Menu References");
        SerializedObject so = new SerializedObject(controller);

        Set(so, "rootScreen", FindGameObject(canvas, "RootScreen"));
        Set(so, "slotScreen", FindGameObject(canvas, "SlotScreen"));
        Set(so, "confirmScreen", FindGameObject(canvas, "ConfirmScreen"));

        Set(so, "newGameButton", Find<Button>(canvas, "RootScreen/NewGameButton"));
        Set(so, "loadGameButton", Find<Button>(canvas, "RootScreen/LoadGameButton"));
        Set(so, "exitButton", Find<Button>(canvas, "RootScreen/ExitButton"));

        Set(so, "slotHeader", Find<TextMeshProUGUI>(canvas, "SlotScreen/Header"));
        Set(so, "slotSubHeader", Find<TextMeshProUGUI>(canvas, "SlotScreen/SubHeader"));
        Set(so, "backButton", Find<Button>(canvas, "SlotScreen/BackButton"));
        Set(so, "toastLabel", Find<TextMeshProUGUI>(canvas, "SlotScreen/Toast"));

        SerializedProperty cards = so.FindProperty("cards");
        if (cards != null)
        {
            cards.arraySize = GameSession.SlotCount;
            for (int i = 0; i < cards.arraySize; i++)
            {
                SerializedProperty card = cards.GetArrayElementAtIndex(i);
                string cardPath = $"SlotScreen/Cards/SlotCard{i + 1}";

                Set(card, "statusLabel", Find<TextMeshProUGUI>(canvas, cardPath + "/Status"));
                Set(card, "metaLabel", Find<TextMeshProUGUI>(canvas, cardPath + "/Meta"));
                Set(card, "primaryButton", Find<Button>(canvas, cardPath + "/PrimaryButton"));
                Set(card, "deleteButton", Find<Button>(canvas, cardPath + "/DeleteButton"));
            }
        }

        Set(so, "confirmTitle", Find<TextMeshProUGUI>(canvas, "ConfirmScreen/ConfirmPanel/Title"));
        Set(so, "confirmMessage", Find<TextMeshProUGUI>(canvas, "ConfirmScreen/ConfirmPanel/Message"));
        Set(so, "confirmYesButton", Find<Button>(canvas, "ConfirmScreen/ConfirmPanel/YesButton"));
        Set(so, "confirmNoButton", Find<Button>(canvas, "ConfirmScreen/ConfirmPanel/NoButton"));

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);

        Debug.Log("[MenuReferenceAssigner] ผูก reference ของ Main Menu เรียบร้อย");
    }

    [MenuItem("Tools/Sector13/UI/Assign Pause Menu References")]
    public static void AssignPauseMenu()
    {
        PauseMenuUI controller = Object.FindAnyObjectByType<PauseMenuUI>();
        if (controller == null)
        {
            Debug.LogError("[MenuReferenceAssigner] ไม่พบ PauseMenuUI ในซีนที่เปิดอยู่");
            return;
        }

        Transform canvas = controller.transform.Find(PauseMenuCanvasName);
        if (canvas == null)
        {
            Debug.LogError($"[MenuReferenceAssigner] ไม่พบ {PauseMenuCanvasName} ใต้ {controller.name}");
            return;
        }

        Undo.RecordObject(controller, "Assign Pause Menu References");
        SerializedObject so = new SerializedObject(controller);

        Set(so, "root", FindGameObject(canvas, "PauseRoot"));
        Set(so, "infoLabel", Find<TextMeshProUGUI>(canvas, "PauseRoot/PausePanel/Info"));

        Set(so, "resumeButton", Find<Button>(canvas, "PauseRoot/PausePanel/ResumeButton"));
        Set(so, "resetDayButton", Find<Button>(canvas, "PauseRoot/PausePanel/ResetDayButton"));
        Set(so, "returnButton", Find<Button>(canvas, "PauseRoot/PausePanel/ReturnButton"));

        Set(so, "confirmScreen", FindGameObject(canvas, "PauseConfirm"));
        Set(so, "confirmTitle", Find<TextMeshProUGUI>(canvas, "PauseConfirm/ConfirmPanel/Title"));
        Set(so, "confirmMessage", Find<TextMeshProUGUI>(canvas, "PauseConfirm/ConfirmPanel/Message"));
        Set(so, "confirmYesButton", Find<Button>(canvas, "PauseConfirm/ConfirmPanel/YesButton"));
        Set(so, "confirmNoButton", Find<Button>(canvas, "PauseConfirm/ConfirmPanel/NoButton"));

        Set(so, "toastLabel", Find<TextMeshProUGUI>(canvas, "Toast"));

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);

        Debug.Log("[MenuReferenceAssigner] ผูก reference ของ Pause Menu เรียบร้อย");
    }

    [MenuItem("Tools/Sector13/UI/Assign Daily Summary References")]
    public static void AssignDailySummary()
    {
        DailySummaryUI summary = Object.FindAnyObjectByType<DailySummaryUI>();
        if (summary == null)
        {
            Debug.LogError("[MenuReferenceAssigner] ไม่พบ DailySummaryUI ในซีนที่เปิดอยู่");
            return;
        }

        Transform canvas = summary.transform.parent;
        Transform panel = canvas != null ? canvas.Find("DailySummaryPanel") : null;

        if (panel == null)
        {
            Debug.LogError("[MenuReferenceAssigner] ไม่พบ Canvas/DailySummaryPanel");
            return;
        }

        Undo.RecordObject(summary, "Assign Daily Summary References");
        SerializedObject so = new SerializedObject(summary);

        Set(so, "summaryPanel", panel.gameObject);
        Set(so, "endDayHUDButton", Find<Button>(panel.parent, "EndDayHUDButton"));
        Set(so, "dayTitleText", Find<TextMeshProUGUI>(panel, "HeaderBar/TitleText"));
        Set(so, "dailyScoreText", Find<TextMeshProUGUI>(panel, "DailyScoreValue"));
        Set(so, "totalScoreText", Find<TextMeshProUGUI>(panel, "TotalScoreValue"));
        Set(so, "rejectedCountText", Find<TextMeshProUGUI>(panel, "RejectValue"));
        Set(so, "approvedCountText", Find<TextMeshProUGUI>(panel, "ApproveValue"));
        Set(so, "legalCountText", Find<TextMeshProUGUI>(panel, "LegalValue"));
        Set(so, "illegalCountText", Find<TextMeshProUGUI>(panel, "IllegalValue"));
        Set(so, "alienCountText", Find<TextMeshProUGUI>(panel, "AlienValue"));
        Set(so, "totalShippedText", Find<TextMeshProUGUI>(panel, "TotalValue"));
        Set(so, "shippedRequirementText", Find<TextMeshProUGUI>(panel, "ShippedValue"));
        Set(so, "stationFeeText", Find<TextMeshProUGUI>(panel, "StationFeeValue"));
        Set(so, "penaltyRow", FindGameObject(panel, "PenaltyRow"));
        Set(so, "penaltyText", Find<TextMeshProUGUI>(panel, "PenaltyRow/PenaltyValue"));
        Set(so, "resultText", Find<TextMeshProUGUI>(panel, "ResultText"));
        Set(so, "nextDayButton", Find<Button>(panel, "NextDayButton"));
        Set(so, "restartButton", Find<Button>(panel, "RestartButton"));
        Set(so, "mainMenuButton", Find<Button>(panel, "MainMenuButton"));

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(summary);
        EditorSceneManager.MarkSceneDirty(summary.gameObject.scene);

        AssignEndDayBlockedDialog(panel.parent);

        Debug.Log("[MenuReferenceAssigner] ผูก reference ของ Daily Summary เรียบร้อย");
    }

    [MenuItem("Tools/Sector13/UI/Assign Objective HUD References")]
    public static void AssignObjectiveHUD()
    {
        DailyObjectiveHUD hud = Object.FindAnyObjectByType<DailyObjectiveHUD>(FindObjectsInactive.Include);
        if (hud == null)
        {
            Debug.LogError("[MenuReferenceAssigner] ไม่พบ DailyObjectiveHUD ในซีนที่เปิดอยู่");
            return;
        }

        Transform panel = hud.transform;

        Undo.RecordObject(hud, "Assign Objective HUD References");
        SerializedObject so = new SerializedObject(hud);

        Set(so, "canvasGroup", panel.GetComponent<CanvasGroup>());
        Set(so, "dayTitleText", Find<TextMeshProUGUI>(panel, "Title"));
        Set(so, "shippedValueText", Find<TextMeshProUGUI>(panel, "ShippedRow/Value"));
        Set(so, "stationFeeValueText", Find<TextMeshProUGUI>(panel, "FeeRow/Value"));

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(hud);
        EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);

        Debug.Log("[MenuReferenceAssigner] ผูก reference ของ Objective HUD เรียบร้อย");
    }

    private static void AssignEndDayBlockedDialog(Transform canvas)
    {
        EndDayBlockedDialog dialog = Object.FindAnyObjectByType<EndDayBlockedDialog>();
        if (dialog == null) return;

        Transform root = dialog.transform.Find("DialogRoot");
        if (root == null)
        {
            Debug.LogError("[MenuReferenceAssigner] ไม่พบ DialogRoot ใต้ EndDayBlockedDialog");
            return;
        }

        Undo.RecordObject(dialog, "Assign End Day Dialog References");
        SerializedObject so = new SerializedObject(dialog);

        Set(so, "root", root.gameObject);
        Set(so, "titleText", Find<TextMeshProUGUI>(root, "BlockedPanel/Title"));
        Set(so, "messageText", Find<TextMeshProUGUI>(root, "BlockedPanel/Message"));
        Set(so, "backButton", Find<Button>(root, "BlockedPanel/BackButton"));
        Set(so, "confirmButton", Find<Button>(root, "BlockedPanel/ConfirmButton"));

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(dialog);
        EditorSceneManager.MarkSceneDirty(dialog.gameObject.scene);
    }

    private static GameObject FindGameObject(Transform root, string path)
    {
        Transform found = root.Find(path);
        return found != null ? found.gameObject : null;
    }

    private static T Find<T>(Transform root, string path) where T : Component
    {
        Transform found = root.Find(path);
        return found != null ? found.GetComponent<T>() : null;
    }

    private static void Set(SerializedObject so, string propertyName, Object value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property == null)
        {
            Debug.LogWarning($"[MenuReferenceAssigner] ไม่พบ field '{propertyName}'");
            return;
        }

        property.objectReferenceValue = value;
    }

    private static void Set(SerializedProperty parent, string propertyName, Object value)
    {
        SerializedProperty property = parent.FindPropertyRelative(propertyName);
        if (property == null)
        {
            Debug.LogWarning($"[MenuReferenceAssigner] ไม่พบ field '{propertyName}'");
            return;
        }

        property.objectReferenceValue = value;
    }
}
