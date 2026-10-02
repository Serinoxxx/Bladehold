<#
.SYNOPSIS
    Forces the open Unity Editor to refresh (and, with -Reload, recompile + domain-reload) without anyone
    at the keyboard, e.g. so MCP for Unity's auto-start hook runs again.

.DESCRIPTION
    Unity only refreshes when its window GAINS focus, and a touched timestamp alone isn't a change.
    -Reload drops a throwaway editor script (Editor/McpKickTemp.cs) so the refresh compiles and reloads.
    Focus is bounced to Explorer and back, with an Alt tap first so Windows allows SetForegroundWindow
    from a background process. Run it again with -Cleanup to delete the temp script (another reload).

.EXAMPLE
    pwsh -File .claude/skills/unity-editor-mcp/scripts/kick-unity-reload.ps1 -Reload
    pwsh -File .claude/skills/unity-editor-mcp/scripts/kick-unity-reload.ps1 -Cleanup
#>
param(
    [switch]$Reload,
    [switch]$Cleanup,
    [string]$ProjectPath = "C:\Users\lance\source\repos\My project"
)

$temp = Join-Path $ProjectPath "Assets\Bladehold\Bladehold Scripts\Editor\McpKickTemp.cs"
if ($Reload) {
    Set-Content -Path $temp -Encoding UTF8 -Value "// Temporary: forces a domain reload (kick-unity-reload.ps1). Delete with -Cleanup.`ninternal static class McpKickTemp { }"
}
if ($Cleanup) {
    Remove-Item "$temp*" -Force -ErrorAction SilentlyContinue
}

Add-Type @'
using System; using System.Runtime.InteropServices;
public static class KickFocus {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, int flags, int extra);
    public static void Focus(IntPtr h) { keybd_event(0x12, 0, 0, 0); keybd_event(0x12, 0, 2, 0); ShowWindow(h, 9); SetForegroundWindow(h); }
}
'@

$unity = Get-Process Unity -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if ($null -eq $unity) { Write-Error "No Unity Editor window found."; exit 1 }
$other = (Get-Process explorer | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1).MainWindowHandle

if ($other) { [KickFocus]::Focus($other); Start-Sleep -Milliseconds 800 }
[KickFocus]::Focus($unity.MainWindowHandle)
Write-Output "Focused '$($unity.MainWindowTitle)' (pid $($unity.Id)). Watch Editor.log for 'Reloading assemblies' / 'Session connected'."
