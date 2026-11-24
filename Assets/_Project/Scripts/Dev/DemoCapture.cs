#if UNITY_EDITOR
using System.IO;
using UnityEditor.Media;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Records the Game View (main camera + HUD) to an H.264 MP4 while the demo runs.
/// Editor-only; lives in the Dev folder and is added at runtime.
/// </summary>
[RequireComponent(typeof(Camera))]
public class DemoCapture : MonoBehaviour
{
    public int width = 1920;
    public int height = 1080;
    public int fps = 30;
    public string outputPath = "demo.mp4";

    private MediaEncoder encoder;
    private RenderTexture rt;
    private Texture2D tex;
    private float startTime;
    private float nextFrame;
    private int frames;

    public bool IsRecording => encoder != null;

    private void Start()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
        if (File.Exists(outputPath)) File.Delete(outputPath);

        rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        rt.Create();
        tex = new Texture2D(width, height, TextureFormat.RGBA32, false);

        var attrs = new VideoTrackAttributes
        {
            frameRate = new MediaRational(fps),
            width = (uint)width,
            height = (uint)height,
            includeAlpha = false,
            bitRateMode = UnityEditor.VideoBitrateMode.High
        };

        encoder = new MediaEncoder(outputPath, attrs);
        startTime = Time.unscaledTime;
        nextFrame = startTime;
        Debug.Log("[DemoCapture] recording -> " + outputPath);
    }

    private void LateUpdate()
    {
        if (encoder == null) return;
        if (Time.unscaledTime < nextFrame) return;

        // keep a fixed output cadence; if the machine drops frames the clip stays
        // real-time because the timestamp is the wall clock
        nextFrame += 1f / fps;

        ScreenCapture.CaptureScreenshotIntoRenderTexture(rt);

        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
        tex.Apply(false);
        RenderTexture.active = prev;

        float t = Time.unscaledTime - startTime;
        // 1/fps time base: MediaTime(count, 1, fps) == count / fps seconds
        encoder.AddFrame(tex, new MediaTime(frames, 1u, (uint)fps));
        frames++;
        if (t < 0f) frames = 0;
    }

    private void OnDestroy()
    {
        Close();
    }

    public void Close()
    {
        if (encoder == null) return;
        try { encoder.Dispose(); } catch { }
        encoder = null;
        if (rt != null) { rt.Release(); Destroy(rt); rt = null; }
        if (tex != null) { Destroy(tex); tex = null; }
        Debug.Log("[DemoCapture] stopped, frames=" + frames);
    }
}
#endif
