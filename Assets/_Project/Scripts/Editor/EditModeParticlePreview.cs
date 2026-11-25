#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Keeps ParticleSystems simulating while the editor is not in Play mode, so the
/// ambient dust and smoke can be judged directly in the Scene / Game view.
/// Toggle: Tools ▸ Sector13 ▸ Edit-Mode Particle Preview.
/// </summary>
[InitializeOnLoad]
public static class EditModeParticlePreview
{
    private const string PrefKey = "Sector13.EditModeParticles";

    private static double lastTick;
    private static double accumulated;

    public static bool Enabled
    {
        get => EditorPrefs.GetBool(PrefKey, true);
        set => EditorPrefs.SetBool(PrefKey, value);
    }

    static EditModeParticlePreview()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    [MenuItem("Tools/Sector13/Edit-Mode Particle Preview")]
    private static void Toggle()
    {
        Enabled = !Enabled;
        Debug.Log("[EditModeParticlePreview] " + (Enabled ? "ON" : "OFF"));
    }

    [MenuItem("Tools/Sector13/Edit-Mode Particle Preview", true)]
    private static bool ToggleValidate()
    {
        Menu.SetChecked("Tools/Sector13/Edit-Mode Particle Preview", Enabled);
        return true;
    }

    private static void Tick()
    {
        if (!Enabled) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

        double now = EditorApplication.timeSinceStartup;
        if (lastTick <= 0)
        {
            lastTick = now;
            return;
        }

        float dt = (float)(now - lastTick);
        lastTick = now;
        if (dt <= 0f) return;

        // keep a ~30 Hz cadence so the editor stays responsive
        accumulated += dt;
        if (accumulated < 0.033) return;
        float step = (float)accumulated;
        accumulated = 0;

        bool touched = false;
        var systems = Object.FindObjectsByType<ParticleSystem>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (var ps in systems)
        {
            if (ps == null || !ps.gameObject.scene.IsValid()) continue;
            if (!ps.gameObject.activeInHierarchy) continue;

            if (!ps.isPlaying) ps.Play(false);
            ps.Simulate(step, true, false);
            touched = true;
        }

        if (touched) SceneView.RepaintAll();
    }
}
#endif
