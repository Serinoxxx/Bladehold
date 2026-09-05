Add-Type -TypeDefinition '
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Generic;

public class WindowUtils {
    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int GetClassName(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    public const uint WM_KEYDOWN = 0x0100;
    public const uint WM_KEYUP = 0x0101;
    public const int VK_RETURN = 0x0D;
    public const int VK_SPACE = 0x20;

    public static List<IntPtr> GetProcessWindows(uint processId) {
        List<IntPtr> windows = new List<IntPtr>();
        EnumWindows((hWnd, lParam) => {
            uint pid;
            GetWindowThreadProcessId(hWnd, out pid);
            if (pid == processId && IsWindowVisible(hWnd)) {
                windows.Add(hWnd);
            }
            return true;
        }, IntPtr.Zero);
        return windows;
    }
}
'

$procs = Get-Process Unity
foreach ($proc in $procs) {
    $wins = [WindowUtils]::GetProcessWindows($proc.Id)
    foreach ($w in $wins) {
        $sb = New-Object System.Text.StringBuilder 256
        [WindowUtils]::GetWindowText($w, $sb, 256) | Out-Null
        $title = $sb.ToString()
        $cb = New-Object System.Text.StringBuilder 256
        [WindowUtils]::GetClassName($w, $cb, 256) | Out-Null
        $cls = $cb.ToString()

        if ($cls -eq '#32770' -or ($title -and $title -notlike '*Bladehold*')) {
            Write-Host "Dismissing modal window: '$title' (Class: $cls)..."
            [WindowUtils]::SetForegroundWindow($w)
            Start-Sleep -Milliseconds 200
            [WindowUtils]::PostMessage($w, [WindowUtils]::WM_KEYDOWN, [IntPtr][WindowUtils]::VK_RETURN, [IntPtr]0)
            [WindowUtils]::PostMessage($w, [WindowUtils]::WM_KEYUP, [IntPtr][WindowUtils]::VK_RETURN, [IntPtr]0)
            Start-Sleep -Milliseconds 200
            [WindowUtils]::PostMessage($w, [WindowUtils]::WM_KEYDOWN, [IntPtr][WindowUtils]::VK_SPACE, [IntPtr]0)
            [WindowUtils]::PostMessage($w, [WindowUtils]::WM_KEYUP, [IntPtr][WindowUtils]::VK_SPACE, [IntPtr]0)
        }
    }
}
