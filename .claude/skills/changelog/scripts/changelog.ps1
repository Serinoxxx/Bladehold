param (
    [Parameter(Position = 0)]
    [ValidateSet("GetVersion", "ReadLatest", "Read", "List", "NewBuild", "AddEntry")]
    [string]$Action = "ReadLatest",

    [string]$Version,

    [ValidateSet("New Features", "Fixes", "Balance Changes", "General Changes")]
    [string]$Category,

    [string]$Message,

    [string]$ChangelogPath = "$PSScriptRoot\..\..\..\..\CHANGELOG.md",
    [string]$ProjectSettingsPath = "$PSScriptRoot\..\..\..\..\ProjectSettings\ProjectSettings.asset"
)

# CHANGELOG.md is CRLF, UTF-8 without BOM. Everything here is line-based and
# writes back with the file's own line ending so edits never mix CRLF/LF.

$ErrorActionPreference = "Stop"
$ChangelogPath = [System.IO.Path]::GetFullPath($ChangelogPath)
$ProjectSettingsPath = [System.IO.Path]::GetFullPath($ProjectSettingsPath)
$Categories = @("New Features", "Fixes", "Balance Changes", "General Changes")
$VersionHeading = '^##\s+\[(.*?)\]\s*-\s*(.*)$'

function Get-CurrentBundleVersion {
    if (-not (Test-Path $ProjectSettingsPath)) { throw "ProjectSettings.asset not found at: $ProjectSettingsPath" }
    $m = Select-String -Path $ProjectSettingsPath -Pattern '^\s*bundleVersion:\s*(.+)$' | Select-Object -First 1
    if (-not $m) { throw "bundleVersion not found in $ProjectSettingsPath" }
    return $m.Matches[0].Groups[1].Value.Trim()
}

function Read-Changelog {
    if (-not (Test-Path $ChangelogPath)) { throw "CHANGELOG.md not found at $ChangelogPath" }
    $raw = [System.IO.File]::ReadAllText($ChangelogPath)
    $nl = if ($raw.Contains("`r`n")) { "`r`n" } else { "`n" }
    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.AddRange([string[]]($raw -split "\r?\n"))
    return @{ Lines = $lines; NewLine = $nl }
}

function Write-Changelog($doc) {
    $text = [string]::Join($doc.NewLine, $doc.Lines)
    [System.IO.File]::WriteAllText($ChangelogPath, $text, [System.Text.UTF8Encoding]::new($false))
}

# Returns @(startIndex, endIndexExclusive) of a version section, or $null.
function Find-Section($lines, [string]$ver) {
    $start = -1
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match $VersionHeading) {
            if ($start -ge 0) { return @($start, $i) }
            if ($Matches[1] -eq $ver) { $start = $i }
        }
    }
    if ($start -ge 0) { return @($start, $lines.Count) }
    return $null
}

function Add-BuildSection($doc, [string]$ver) {
    if (Find-Section $doc.Lines $ver) { return $false }
    $today = (Get-Date).ToString("yyyy-MM-dd")
    $block = @("## [$ver] - $today", "")
    foreach ($c in $Categories) { $block += @("### $c", "") }
    # Insert above the first existing version heading (newest first), else append.
    $at = $doc.Lines.Count
    for ($i = 0; $i -lt $doc.Lines.Count; $i++) {
        if ($doc.Lines[$i] -match $VersionHeading) { $at = $i; break }
    }
    $doc.Lines.InsertRange($at, [string[]]$block)
    return $true
}

if ([string]::IsNullOrWhiteSpace($Version) -and $Action -ne "ReadLatest" -and $Action -ne "List") {
    $Version = Get-CurrentBundleVersion
}

switch ($Action) {
    "GetVersion" { Write-Output $Version }

    "List" {
        $doc = Read-Changelog
        foreach ($l in $doc.Lines) {
            if ($l -match $VersionHeading) { Write-Output ("v{0} ({1})" -f $Matches[1], $Matches[2]) }
        }
    }

    "ReadLatest" {
        $doc = Read-Changelog
        foreach ($l in $doc.Lines) {
            if ($l -match $VersionHeading) { $Version = $Matches[1]; break }
        }
        if (-not $Version) { Write-Warning "No versions found in CHANGELOG.md"; exit 0 }
        $s = Find-Section $doc.Lines $Version
        Write-Output (($doc.Lines[$s[0]..($s[1] - 1)] -join "`n").Trim())
    }

    "Read" {
        $doc = Read-Changelog
        $s = Find-Section $doc.Lines $Version
        if (-not $s) { Write-Warning "Version [$Version] not found in CHANGELOG.md"; exit 0 }
        Write-Output (($doc.Lines[$s[0]..($s[1] - 1)] -join "`n").Trim())
    }

    "NewBuild" {
        $doc = Read-Changelog
        if (Add-BuildSection $doc $Version) {
            Write-Changelog $doc
            Write-Output "Added section for [$Version]"
        } else {
            Write-Warning "Version [$Version] already exists in CHANGELOG.md"
        }
    }

    "AddEntry" {
        if ([string]::IsNullOrWhiteSpace($Message)) { throw "-Message is required for AddEntry" }
        if ([string]::IsNullOrWhiteSpace($Category)) { throw "-Category is required: $($Categories -join ', ')" }
        $doc = Read-Changelog
        [void](Add-BuildSection $doc $Version)
        $s = Find-Section $doc.Lines $Version

        $cat = -1
        for ($i = $s[0] + 1; $i -lt $s[1]; $i++) {
            if ($doc.Lines[$i].Trim() -eq "### $Category") { $cat = $i; break }
        }
        if ($cat -lt 0) {
            # Category heading missing from this section: add it at the section end.
            $end = $s[1]
            while ($end -gt $s[0] + 1 -and [string]::IsNullOrWhiteSpace($doc.Lines[$end - 1])) { $end-- }
            $doc.Lines.InsertRange($end, [string[]]@("", "### $Category", ""))
            $cat = $end + 1
            $s = Find-Section $doc.Lines $Version
        }

        # Append after the last non-blank line of the category block.
        $next = $s[1]
        for ($i = $cat + 1; $i -lt $s[1]; $i++) {
            if ($doc.Lines[$i] -match '^#{2,3}\s') { $next = $i; break }
        }
        $last = $cat
        for ($i = $cat + 1; $i -lt $next; $i++) {
            if (-not [string]::IsNullOrWhiteSpace($doc.Lines[$i])) { $last = $i }
        }
        $entry = "- " + $Message.Trim().TrimStart('-', ' ')
        if ($last -eq $cat) {
            # Empty category: keep the blank line under the heading.
            if ($cat + 1 -lt $doc.Lines.Count -and [string]::IsNullOrWhiteSpace($doc.Lines[$cat + 1])) {
                $doc.Lines.Insert($cat + 2, $entry)
                if ($cat + 3 -lt $doc.Lines.Count -and -not [string]::IsNullOrWhiteSpace($doc.Lines[$cat + 3])) {
                    $doc.Lines.Insert($cat + 3, "")
                }
            } else {
                $doc.Lines.InsertRange($cat + 1, [string[]]@("", $entry))
            }
        } else {
            $doc.Lines.Insert($last + 1, $entry)
        }
        Write-Changelog $doc
        Write-Output "Added entry to [$Version] -> [$Category]"
    }
}
