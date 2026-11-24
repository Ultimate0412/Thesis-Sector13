using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Sends real OS-level keyboard and mouse input (Win32 SendInput) so the game's
/// legacy Input Manager sees it exactly as if a person were at the keyboard.
/// Windows only.
/// </summary>
public static class WinInput
{
    // ------------------------------------------------------------------ win32
    private const uint INPUT_MOUSE = 0;
    private const uint INPUT_KEYBOARD = 1;

    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_SCANCODE = 0x0008;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;

    private const uint MOUSEEVENTF_MOVE = 0x0001;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUTUNION
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public INPUTUNION u;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hWnd, ref POINT point);

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    private static int inputSize = Marshal.SizeOf(typeof(INPUT));

    // ------------------------------------------------------------------ focus
    public static IntPtr UnityWindow()
    {
        try { return Process.GetCurrentProcess().MainWindowHandle; } catch { return IntPtr.Zero; }
    }

    public static bool FocusUnity()
    {
        IntPtr h = UnityWindow();
        if (h == IntPtr.Zero) return false;
        ShowWindow(h, 9); // SW_RESTORE

        if (GetForegroundWindow() == h) return true;

        // Windows blocks focus theft from a background process. A synthetic ALT
        // keystroke temporarily lifts that restriction for the calling thread.
        KeyDown(0x12); // VK_MENU
        KeyUp(0x12);
        System.Threading.Thread.Sleep(30);

        bool ok = SetForegroundWindow(h);
        System.Threading.Thread.Sleep(120);
        return ok && GetForegroundWindow() == h;
    }

    public static bool IsUnityFocused()
    {
        IntPtr h = UnityWindow();
        return h != IntPtr.Zero && GetForegroundWindow() == h;
    }

    // ------------------------------------------------------------------ keyboard
    /// <summary>Virtual key code, e.g. 'W' is 0x57, VK_ESCAPE 0x1B, VK_SPACE 0x20.</summary>
    public static void KeyDown(int vk)
    {
        Send(KeyInput((ushort)vk, 0));
    }

    public static void KeyUp(int vk)
    {
        Send(KeyInput((ushort)vk, KEYEVENTF_KEYUP));
    }

    public static void Tap(int vk, int holdMs = 60)
    {
        KeyDown(vk);
        System.Threading.Thread.Sleep(holdMs);
        KeyUp(vk);
    }

    /// <summary>Holds a key for a duration; pass a callback to poll while held.</summary>
    public static System.Collections.IEnumerator Hold(int vk, float seconds)
    {
        KeyDown(vk);
        float t = 0f;
        while (t < seconds) { t += Time.deltaTime; yield return null; }
        KeyUp(vk);
    }

    // ------------------------------------------------------------------ mouse
    public static void MouseMoveRelative(int dx, int dy)
    {
        var inp = new INPUT { type = INPUT_MOUSE };
        inp.u.mi = new MOUSEINPUT { dx = dx, dy = dy, dwFlags = MOUSEEVENTF_MOVE };
        Send(inp);
    }

    public static void MouseMoveAbsolute(int screenX, int screenY)
    {
        // SendInput absolute coords are 0..65535 across the primary screen
        int w = Mathf.Max(1, Screen.currentResolution.width);
        int h = Mathf.Max(1, Screen.currentResolution.height);
        int nx = (int)((screenX + 0.5f) * 65535f / w);
        int ny = (int)((screenY + 0.5f) * 65535f / h);
        var inp = new INPUT { type = INPUT_MOUSE };
        inp.u.mi = new MOUSEINPUT { dx = nx, dy = ny, dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE };
        Send(inp);
    }

    public static void LeftClick()
    {
        var d = new INPUT { type = INPUT_MOUSE };
        d.u.mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_LEFTDOWN };
        Send(d);
        System.Threading.Thread.Sleep(40);
        var u = new INPUT { type = INPUT_MOUSE };
        u.u.mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_LEFTUP };
        Send(u);
    }

    // ------------------------------------------------------------------ plumbing
    private static INPUT KeyInput(ushort vk, uint flags)
    {
        var inp = new INPUT { type = INPUT_KEYBOARD };
        inp.u.ki = new KEYBDINPUT { wVk = vk, wScan = 0, dwFlags = flags };
        return inp;
    }

    public static uint LastSendResult { get; private set; }

    private static void Send(INPUT input)
    {
        var arr = new INPUT[] { input };
        LastSendResult = SendInput(1, arr, inputSize);
    }
}
