using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class DemoDayLoopTests
{
    private const string GameManagerName = "==GameManager==";

    private static System.Type ProjectType(string typeName)
    {
        return System.Type.GetType(typeName + ", Assembly-CSharp");
    }

    private static IEnumerator WaitForGamePlay()
    {
        yield return SceneManager.LoadSceneAsync("GamePlay");
        yield return null;
        yield return null;

        GameObject canvasRoot = GameObject.Find("Canvas");
        Assert.IsNotNull(canvasRoot, "Canvas root not found after loading GamePlay");
    }

    private static Canvas CanvasRoot()
    {
        return GameObject.Find("Canvas").GetComponent<Canvas>();
    }

    private static TMP_Text FindLabel(Transform root, string name)
    {
        foreach (TMP_Text t in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (t.name == name) return t;
        }
        Assert.Fail("Label '" + name + "' not found under '" + root.name + "'");
        return null;
    }

    [UnityTest]
    public IEnumerator Bootstrap_Builds_Demo_UI_On_Start()
    {
        yield return WaitForGamePlay();

        Assert.IsNotNull(Object.FindAnyObjectByType<EventSystem>(),
            "EventSystem missing, UI buttons would be unclickable");
        Assert.IsNotNull(Object.FindAnyObjectByType<StandaloneInputModule>(),
            "StandaloneInputModule missing");

        GameObject manager = GameObject.Find(GameManagerName);
        Assert.IsNotNull(manager, GameManagerName + " not found");
        Assert.IsNotNull(manager.GetComponent("DemoLoopBootstrap"),
            "DemoLoopBootstrap is not attached to " + GameManagerName);

        Canvas canvas = CanvasRoot();
        Assert.IsNotNull(canvas.transform.Find("EndDayButton"), "END DAY button missing");
        Assert.IsNotNull(canvas.transform.Find("DemoHud"), "Demo HUD missing");
        Assert.IsNotNull(canvas.transform.Find("DemoToast"), "Demo toast missing");
        Assert.IsFalse(canvas.transform.Find("Reject").gameObject.activeSelf,
            "The orphan 'Reject' label should be hidden");

        Transform hud = canvas.transform.Find("DemoHud");
        Assert.IsNotNull(FindLabel(hud, "DayLine"), "Day line missing");
        StringAssert.StartsWith("DAY", FindLabel(hud, "DayLine").text);
        StringAssert.Contains("QUOTA", FindLabel(hud, "OpenLine").text);
        StringAssert.Contains("QUOTA", FindLabel(hud, "SpawnLine").text);
    }

    [UnityTest]
    public IEnumerator Stamp_Panel_Has_Usable_Approve_And_Reject_Buttons()
    {
        yield return WaitForGamePlay();

        Transform panel = CanvasRoot().transform.Find("Image");
        Assert.IsNotNull(panel, "Stamp menu panel missing");
        Assert.AreEqual(new Vector2(460f, 190f), panel.GetComponent<RectTransform>().sizeDelta,
            "Stamp panel was not resized");

        Button[] buttons = panel.GetComponentsInChildren<Button>(true);
        Assert.AreEqual(2, buttons.Length, "Stamp panel should have exactly two buttons");

        bool foundApprove = false;
        bool foundReject = false;
        foreach (Button b in buttons)
        {
            TMP_Text label = b.GetComponentInChildren<TMP_Text>(true);
            Assert.IsNotNull(label, "Button '" + b.gameObject.name + "' has no label");
            if (label.text == "APPROVE")
            {
                foundApprove = true;
                Assert.AreEqual(new Vector2(-115f, -46f), b.GetComponent<RectTransform>().anchoredPosition);
            }
            else if (label.text == "REJECT")
            {
                foundReject = true;
                Assert.AreEqual(new Vector2(115f, -46f), b.GetComponent<RectTransform>().anchoredPosition);
            }
            Assert.AreEqual(new Vector2(200f, 60f), b.GetComponent<RectTransform>().sizeDelta,
                "Stamp button '" + label.text + "' has the wrong size");
        }

        Assert.IsTrue(foundApprove, "APPROVE button missing");
        Assert.IsTrue(foundReject, "REJECT button missing");
    }

    [UnityTest]
    public IEnumerator End_Day_Is_Blocked_While_A_Box_Is_Left_On_The_Floor()
    {
        yield return WaitForGamePlay();

        Canvas canvas = CanvasRoot();
        Transform toast = canvas.transform.Find("DemoToast");
        Assert.IsFalse(toast.gameObject.activeSelf, "Toast should start hidden");

        System.Type packageBox = ProjectType("PackageBox");
        Assert.IsNotNull(packageBox, "PackageBox type not found in Assembly-CSharp");

        GameObject box = new GameObject("TestBoxOnFloor");
        box.transform.position = new Vector3(5f, 1f, 5f);
        box.AddComponent(packageBox);

        canvas.transform.Find("EndDayButton").GetComponent<Button>().onClick.Invoke();

        Assert.IsTrue(toast.gameObject.activeSelf,
            "Toast should appear when the day cannot be ended");
        StringAssert.Contains("Cannot end the day", FindLabel(toast, "ToastText").text);

        GameObject manager = GameObject.Find(GameManagerName);
        Behaviour dayManager = (Behaviour)manager.GetComponent("GameDayManager");
        Assert.IsTrue(dayManager.enabled, "GameDayManager must stay enabled when the day cannot end");

        Object.Destroy(box);
        yield return null;
    }

    [UnityTest]
    public IEnumerator End_Day_With_A_Clear_Floor_Ends_The_Day_And_Shows_Summary()
    {
        yield return WaitForGamePlay();

        Canvas canvas = CanvasRoot();
        canvas.transform.Find("EndDayButton").GetComponent<Button>().onClick.Invoke();
        yield return null;

        Transform summary = canvas.transform.Find("DemoSummary");
        Assert.IsNotNull(summary, "Summary screen was not created");
        Assert.IsTrue(summary.gameObject.activeSelf, "Summary screen should be visible");

        StringAssert.AreEqualIgnoringCase("DAY 1 COMPLETE", FindLabel(summary, "SummaryTitle").text);
        StringAssert.Contains("SCORE", FindLabel(summary, "SummaryBody").text);
        Assert.IsNotNull(summary.Find("PlayAgainButton"), "PLAY AGAIN button missing");
        Assert.IsNotNull(summary.Find("QuitButton"), "QUIT button missing");

        GameObject manager = GameObject.Find(GameManagerName);
        Behaviour dayManager = (Behaviour)manager.GetComponent("GameDayManager");
        Assert.IsFalse(dayManager.enabled, "GameDayManager should be frozen on the summary screen");

        Assert.IsFalse(Cursor.lockState.Equals(CursorLockMode.Locked),
            "Cursor must be unlocked on the summary screen");
    }

    [UnityTest]
    public IEnumerator Play_Again_Reloads_The_Scene_With_Fresh_State()
    {
        yield return WaitForGamePlay();

        Canvas canvas = CanvasRoot();
        canvas.transform.Find("EndDayButton").GetComponent<Button>().onClick.Invoke();
        yield return null;

        Transform summary = CanvasRoot().transform.Find("DemoSummary");
        Assert.IsNotNull(summary, "Summary screen was not created");
        summary.Find("PlayAgainButton").GetComponent<Button>().onClick.Invoke();

        yield return null;
        yield return null;
        yield return null;
        yield return null;

        GameObject manager = GameObject.Find(GameManagerName);
        Assert.IsNotNull(manager, "Scene did not reload");
        Assert.IsNotNull(manager.GetComponent("DemoLoopBootstrap"),
            "Bootstrap missing after reload");

        Behaviour dayManager = (Behaviour)manager.GetComponent("GameDayManager");
        Assert.IsTrue(dayManager.enabled, "GameDayManager should be running again after reload");

        Canvas reloaded = CanvasRoot();
        Assert.IsNull(reloaded.transform.Find("DemoSummary"),
            "Summary screen should not survive a scene reload");
        Assert.IsNotNull(reloaded.transform.Find("EndDayButton"), "END DAY button missing after reload");
    }
}
