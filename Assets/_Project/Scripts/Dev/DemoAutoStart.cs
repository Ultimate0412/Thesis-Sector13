#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Drops the demo recorder + director into the GamePlay scene the moment Play mode
/// starts, so a take can be recorded without touching the editor by hand.
/// </summary>
[InitializeOnLoad]
public static class DemoAutoStart
{
    public const string OutputPath = "C:/Users/PC/AppData/Local/Temp/opencode/demo/sector13_demo.mp4";

    static DemoAutoStart()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode) return;

        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "GamePlay") return;

        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[DemoAutoStart] no main camera, skipping recording");
            return;
        }

        var capture = cam.gameObject.AddComponent<DemoCapture>();
        capture.outputPath = OutputPath;

        var directorGo = new GameObject("DemoDirector");
        directorGo.AddComponent<DemoDirector>();

        // The editor stops ticking the player loop when it is not the focused window,
        // which freezes the director coroutine and the capture. Force it along.
        EditorApplication.update -= PumpPlayerLoop;
        EditorApplication.update += PumpPlayerLoop;

        Debug.Log("[DemoAutoStart] recording started -> " + OutputPath);
    }

    private static void PumpPlayerLoop()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorApplication.update -= PumpPlayerLoop;
            return;
        }
        EditorApplication.QueuePlayerLoopUpdate();
    }
}
#endif
