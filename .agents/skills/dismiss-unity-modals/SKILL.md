---
name: dismiss-unity-modals
description: Use when Unity Editor appears stuck, frozen, or unresponsive to MCP commands due to blocking modal dialog boxes (e.g. package trust prompts, registry imports, missing signatures). Diagnoses Editor.log and automatically dismisses modal popups via Windows Win32 API messages.
---

# Dismiss Unity Modal Dialogs & Unfreeze Editor

When Unity Editor is open but `unityMCP` tools time out with messages like `timed out waiting for editor readiness` or `Unity session not ready (ping not answered)`, the Editor main thread is usually blocked by a Win32 modal dialog box (such as a Package Manager confirmation, Scoped Registry import, or Missing Signature prompt).

Follow these steps to diagnose and automatically dismiss modal dialogs.

## 1. Diagnose via Editor.log

Check `%LOCALAPPDATA%\Unity\Editor\Editor.log` for recent modal window invocations:

```powershell
Get-Content "$env:LOCALAPPDATA\Unity\Editor\Editor.log" -Tail 200 | Select-String "ShowModal|CustomDisplayDialog|InProjectPackagesMonitor"
```

If modal entries are present, Unity's UI main loop is paused waiting for user input.

## 2. Automated Modal Dismissal Script

Run this PowerShell script via `run_command` to enumerate all Unity child/modal windows and send `VK_RETURN` and `VK_SPACE` events to dismiss them:

```powershell
powershell -ExecutionPolicy Bypass -Command @"
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

\$procs = Get-Process Unity
foreach (\$proc in \$procs) {
    \$wins = [WindowUtils]::GetProcessWindows(\$proc.Id)
    foreach (\$w in \$wins) {
        \$sb = New-Object System.Text.StringBuilder 256
        [WindowUtils]::GetWindowText(\$w, \$sb, 256) | Out-Null
        \$title = \$sb.ToString()
        \$cb = New-Object System.Text.StringBuilder 256
        [WindowUtils]::GetClassName(\$w, \$cb, 256) | Out-Null
        \$cls = \$cb.ToString()

        if (\$cls -eq '#32770' -or (\$title -and \$title -notlike '*Bladehold Test Scene*')) {
            Write-Host "Dismissing modal window: '\$title' (Class: \$cls)..."
            [WindowUtils]::SetForegroundWindow(\$w)
            Start-Sleep -Milliseconds 200
            [WindowUtils]::PostMessage(\$w, [WindowUtils]::WM_KEYDOWN, [IntPtr][WindowUtils]::VK_RETURN, [IntPtr]0)
            [WindowUtils]::PostMessage(\$w, [WindowUtils]::WM_KEYUP, [IntPtr][WindowUtils]::VK_RETURN, [IntPtr]0)
            Start-Sleep -Milliseconds 200
            [WindowUtils]::PostMessage(\$w, [WindowUtils]::WM_KEYDOWN, [IntPtr][WindowUtils]::VK_SPACE, [IntPtr]0)
            [WindowUtils]::PostMessage(\$w, [WindowUtils]::WM_KEYUP, [IntPtr][WindowUtils]::VK_SPACE, [IntPtr]0)
        }
    }
}
"@
```

## 3. Verify Responsiveness

Confirm Unity is active and unblocked by testing `unityMCP`'s `read_console`:

Call `read_console` tool with `{"action": "get", "count": 5}`. If log entries are returned, the Editor main thread is active and responsive.
