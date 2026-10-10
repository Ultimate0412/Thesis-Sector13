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
    public IEnumerator Demo_Loop_Full_Day_Flow()
    {
        yield return SceneManager.LoadSceneAsync("GamePlay");
        yield return null;
        yield return null;

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
        Transform hud = canvas.transform.Find("DemoHud");
        Assert.IsNotNull(hud, "Demo HUD missing");
        Assert.IsNotNull(canvas.transform.Find("DemoToast"), "Demo toast missing");
        StringAssert.StartsWith("DAY", FindLabel(hud, "DayLine").text);
        StringAssert.Contains("QUOTA", FindLabel(hud, "OpenLine").text);
        StringAssert.Contains("QUOTA", FindLabel(hud, "SpawnLine").text);

        Transform score = canvas.transform.Find("Score");
        Transform reject = canvas.transform.Find("Reject");
        Assert.IsNotNull(score, "Score label missing");
        Assert.IsNotNull(reject, "Reject counter missing");
        Assert.IsTrue(score.gameObject.activeSelf, "Score label must stay visible");
        Assert.IsTrue(reject.gameObject.activeSelf,
            "Reject counter must stay visible, ScoreUI writes into it");
        Assert.AreEqual(Vector2.zero, score.GetComponent<RectTransform>().anchorMin,
            "Score should be anchored bottom-left");
        Assert.AreEqual(new Vector2(1f, 0f), reject.GetComponent<RectTransform>().anchorMin,
            "Reject counter should be anchored bottom-right");
        Assert.AreEqual(new Vector2(1f, 1f),
            canvas.transform.Find("EndDayButton").GetComponent<RectTransform>().anchorMin,
            "END DAY should stay top-right");

        Transform panel = canvas.transform.Find("Image");
        Assert.IsNotNull(panel, "Stamp menu panel missing");
        Assert.AreEqual(new Vector2(460f, 190f), panel.GetComponent<RectTransform>().sizeDelta,
            "Stamp panel was not resized");
        Button[] stampButtons = panel.GetComponentsInChildren<Button>(true);
        Assert.AreEqual(2, stampButtons.Length, "Stamp panel should have exactly two buttons");
        bool foundApprove = false;
        bool foundReject = false;
        foreach (Button b in stampButtons)
        {
            TMP_Text label = b.GetComponentInChildren<TMP_Text>(true);
            Assert.IsNotNull(label, "Stamp button '" + b.gameObject.name + "' has no label");
            Assert.AreEqual(new Vector2(200f, 60f), b.GetComponent<RectTransform>().sizeDelta,
                "Stamp button '" + label.text + "' has the wrong size");
            if (label.text == "APPROVE") foundApprove = true;
            if (label.text == "REJECT") foundReject = true;
        }
        Assert.IsTrue(foundApprove, "APPROVE button missing");
        Assert.IsTrue(foundReject, "REJECT button missing");

        Transform toast = canvas.transform.Find("DemoToast");
        Assert.IsFalse(toast.gameObject.activeSelf, "Toast should start hidden");
        Button endDay = canvas.transform.Find("EndDayButton").GetComponent<Button>();

        System.Type packageBox = ProjectType("PackageBox");
        Assert.IsNotNull(packageBox, "PackageBox type not found in Assembly-CSharp");
        GameObject box = new GameObject("TestBoxOnFloor");
        box.transform.position = new Vector3(5f, 1f, 5f);
        box.AddComponent(packageBox);

        endDay.onClick.Invoke();
        Assert.IsTrue(toast.gameObject.activeSelf,
            "Toast should appear when the day cannot be ended");
        Assert.IsNotEmpty(FindLabel(toast, "ToastText").text,
            "Toast must show why the day could not be ended");
        Behaviour dayManager = (Behaviour)manager.GetComponent("GameDayManager");
        Assert.IsTrue(dayManager.enabled, "GameDayManager must stay enabled when the day cannot end");

        box.SetActive(false);
        endDay.onClick.Invoke();
        yield return null;

        Transform summary = canvas.transform.Find("DemoSummary");
        Assert.IsNotNull(summary, "Summary screen was not created");
        Assert.IsTrue(summary.gameObject.activeSelf, "Summary screen should be visible");
        StringAssert.AreEqualIgnoringCase("DAY 1 COMPLETE", FindLabel(summary, "SummaryTitle").text);
        StringAssert.Contains("SCORE", FindLabel(summary, "SummaryBody").text);
        Assert.IsNotNull(summary.Find("PlayAgainButton"), "PLAY AGAIN button missing");
        Assert.IsNotNull(summary.Find("QuitButton"), "QUIT button missing");
        Assert.IsFalse(dayManager.enabled, "GameDayManager should be frozen on the summary screen");
        Assert.AreEqual(CursorLockMode.None, Cursor.lockState,
            "Cursor must be unlocked on the summary screen");

        summary.Find("PlayAgainButton").GetComponent<Button>().onClick.Invoke();
        yield return null;
        yield return null;
        yield return null;
        yield return null;

        GameObject managerAfter = GameObject.Find(GameManagerName);
        Assert.IsNotNull(managerAfter, "Scene did not reload");
        Assert.IsNotNull(managerAfter.GetComponent("DemoLoopBootstrap"),
            "Bootstrap missing after reload");
        Behaviour dayManagerAfter = (Behaviour)managerAfter.GetComponent("GameDayManager");
        Assert.IsTrue(dayManagerAfter.enabled, "GameDayManager should run again after reload");
        Assert.IsNull(CanvasRoot().transform.Find("DemoSummary"),
            "Summary screen should not survive a scene reload");
        Assert.IsNotNull(CanvasRoot().transform.Find("EndDayButton"),
            "END DAY button missing after reload");
        Assert.IsNotNull(CanvasRoot().transform.Find("Score"), "Score label missing after reload");
        Assert.IsTrue(CanvasRoot().transform.Find("Reject").gameObject.activeSelf,
            "Reject counter missing after reload");
    }
}
