using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

public enum WindowSearchMode
{
    IncludeMinimized,
    ExcludeMinimized
}

public delegate bool AddWindowCallback(string title, string windowClass, string exe);

public class WindowInfo
{
    public string Title { get; set; }
    public string Class { get; set; }
    public string Executable { get; set; }
    public override string ToString()
    {
        return $"({Executable}) {Title}";
    }
}

// Reference: https://github.com/obsproject/obs-studio/blob/master/libobs/util/windows/window-helpers.c

public static class WindowHelper
{
    // P/Invoke declarations
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLengthW(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextW(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassNameW(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, uint processId);

    [DllImport("psapi.dll", CharSet = CharSet.Unicode)]
    private static extern uint GetProcessImageFileNameW(IntPtr hProcess, StringBuilder lpImageFileName, int nSize);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr hObject);

    // Constants
    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;
    private const uint WS_EX_TOOLWINDOW = 0x00000080;
    private const uint WS_CHILD = 0x40000000;
    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    private const uint GW_CHILD = 5;
    private const uint GW_HWNDNEXT = 2;
    private const int MAX_PATH = 260;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left, top, right, bottom;
    }

    // Main function to fill window list
    public static void FillWindowList(List<WindowInfo> list, WindowSearchMode mode, AddWindowCallback callback = null)
    {
        EnumWindows((hWnd, lParam) =>
        {
            if (!CheckWindowValid(hWnd, mode))
                return true;

            var exe = GetWindowExe(hWnd);
            if (string.IsNullOrEmpty(exe) || IsMicrosoftInternalWindowExeAndExcludeExe(exe))
                return true;

            var title = GetWindowTitle(hWnd);
            if (exe.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(title))
                return true;

            var windowClass = GetWindowClass(hWnd);

            if (callback != null && !callback(title, windowClass, exe))
                return true;

            list.Add(new WindowInfo
            {
                Title = title,
                Class = windowClass,
                Executable = exe
            });

            return true;
        }, IntPtr.Zero);
    }

    // Helper methods
    private static bool CheckWindowValid(IntPtr hWnd, WindowSearchMode mode)
    {
        if (!IsWindowVisible(hWnd))
            return false;

        if (mode == WindowSearchMode.ExcludeMinimized && IsIconic(hWnd))
            return false;

        GetClientRect(hWnd, out RECT rect);
        var styles = (uint)GetWindowLongPtr(hWnd, GWL_STYLE).ToInt64();
        var exStyles = (uint)GetWindowLongPtr(hWnd, GWL_EXSTYLE).ToInt64();

        if ((exStyles & WS_EX_TOOLWINDOW) != 0)
            return false;
        if ((styles & WS_CHILD) != 0)
            return false;
        if (mode == WindowSearchMode.ExcludeMinimized && (rect.bottom == 0 || rect.right == 0))
            return false;

        return true;
    }

    private static string GetWindowTitle(IntPtr hWnd)
    {
        int len = GetWindowTextLengthW(hWnd);
        if (len == 0) return string.Empty;
        var sb = new StringBuilder(len + 1);
        GetWindowTextW(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private static string GetWindowClass(IntPtr hWnd)
    {
        var sb = new StringBuilder(256);
        GetClassNameW(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private static string GetWindowExe(IntPtr hWnd)
    {
        GetWindowThreadProcessId(hWnd, out uint pid);
        if (pid == (uint)Process.GetCurrentProcess().Id)
            return null;

        IntPtr hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (hProcess == IntPtr.Zero)
            return null;

        var sb = new StringBuilder(MAX_PATH);
        if (GetProcessImageFileNameW(hProcess, sb, sb.Capacity) == 0)
        {
            CloseHandle(hProcess);
            return null;
        }
        CloseHandle(hProcess);

        string path = sb.ToString();
        int lastSlash = path.LastIndexOf('\\');
        return lastSlash >= 0 ? path.Substring(lastSlash + 1) : path;
    }

    private static readonly string[] InternalMicrosoftExesExact = new[]
    {
        "startmenuexperiencehost.exe",
        "applicationframehost.exe",
        "peopleexperiencehost.exe",
        "shellexperiencehost.exe",
        "microsoft.notes.exe",
        "systemsettings.exe",
        "textinputhost.exe",
        "searchapp.exe",
        "video.ui.exe",
        "searchui.exe",
        "lockapp.exe",
        "cortana.exe",
        "gamebar.exe",
        "tabtip.exe",
        "time.exe"
    };

    private static readonly string[] InternalMicrosoftExesPartial = new[]
    {
        "windowsinternal"
    };

    private static readonly string[] ExcludeExes = new[]
    {
        "explorer.exe",
        "taskhostw.exe",
        "dwm.exe",
        "ctfmon.exe",
        "audiodg.exe",
        "svchost.exe",
        "conhost.exe",
        "fontdrvhost.exe",
        "RuntimeBroker.exe",
        "CalculatorApp.exe",
        "RtkUWP.exe",
        "brave.exe",
        "firefox.exe",
        "chrome.exe",
        "msedge.exe",
        "opera.exe",
        "Discord.exe",
    };

    private static bool IsMicrosoftInternalWindowExeAndExcludeExe(string exe)
    {
        foreach (var exact in InternalMicrosoftExesExact)
            if (string.Equals(exe, exact, StringComparison.OrdinalIgnoreCase))
                return true;
        foreach (var partial in InternalMicrosoftExesPartial)
            if (exe.StartsWith(partial, StringComparison.OrdinalIgnoreCase))
                return true;
        foreach (var exclude in ExcludeExes)
            if (string.Equals(exe, exclude, StringComparison.OrdinalIgnoreCase))
                return true;

        return false;
    }
}