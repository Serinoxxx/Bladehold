# Lists (default) or dismisses native modal dialogs owned by Unity Editor processes.
# Usage:  pwsh -File dismiss-unity-modals.ps1            # list only
#         pwsh -File dismiss-unity-modals.ps1 -Dismiss   # press Enter, then Space, on each dialog
# Enter triggers the dialog's DEFAULT button (e.g. "Save" on a save prompt), so list first and
# only dismiss when that default is acceptable. Only standard dialog windows (class #32770) are
# touched, never the main Editor window.
param([switch]$Dismiss)

Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public static class UnityDialogs {
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hWnd, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr w, IntPtr l);

    public static List<IntPtr> Visible(uint processId) {
        var list = new List<IntPtr>();
        EnumWindows((h, l) => {
            uint pid; GetWindowThreadProcessId(h, out pid);
            if (pid == processId && IsWindowVisible(h)) list.Add(h);
            return true;
        }, IntPtr.Zero);
        return list;
    }
    public static string Text(IntPtr h)  { var s = new StringBuilder(256); GetWindowText(h, s, 256); return s.ToString(); }
    public static string Class(IntPtr h) { var s = new StringBuilder(256); GetClassName(h, s, 256); return s.ToString(); }
}
'@

$WM_KEYDOWN = 0x0100; $WM_KEYUP = 0x0101; $VK_RETURN = 0x0D; $VK_SPACE = 0x20
$found = 0
foreach ($proc in Get-Process Unity -ErrorAction SilentlyContinue) {
    foreach ($w in [UnityDialogs]::Visible([uint32]$proc.Id)) {
        if ([UnityDialogs]::Class($w) -ne '#32770') { continue }
        $found++
        $title = [UnityDialogs]::Text($w)
        if (-not $Dismiss) { Write-Host "Modal dialog (pid $($proc.Id)): '$title'"; continue }
        Write-Host "Dismissing '$title' (pid $($proc.Id))"
        [void][UnityDialogs]::SetForegroundWindow($w)
        Start-Sleep -Milliseconds 200
        foreach ($vk in @($VK_RETURN, $VK_SPACE)) {
            [void][UnityDialogs]::PostMessage($w, $WM_KEYDOWN, [IntPtr]$vk, [IntPtr]::Zero)
            [void][UnityDialogs]::PostMessage($w, $WM_KEYUP, [IntPtr]$vk, [IntPtr]::Zero)
            Start-Sleep -Milliseconds 200
        }
    }
}
if ($found -eq 0) { Write-Host "No native Unity dialogs found." }
