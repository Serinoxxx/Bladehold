param (
    [Parameter(Position = 0)]
    [ValidateSet("GetVersion", "ReadLatest", "Read", "List", "NewBuild", "AddEntry")]
    [string]$Action = "ReadLatest",

    [Parameter(Position = 1)]
    [string]$Version,

    [Parameter(Position = 2)]
    [ValidateSet("New Features", "Fixes", "Balance Changes", "General Changes")]
    [string]$Category,

    [Parameter(Position = 3)]
    [string]$Message,

    [string]$ChangelogPath = "$PSScriptRoot\..\..\..\..\CHANGELOG.md",
    [string]$ProjectSettingsPath = "$PSScriptRoot\..\..\..\..\ProjectSettings\ProjectSettings.asset"
)

$ChangelogPath = [System.IO.Path]::GetFullPath($ChangelogPath)
$ProjectSettingsPath = [System.IO.Path]::GetFullPath($ProjectSettingsPath)

function Get-CurrentBundleVersion {
    if (-not (Test-Path $ProjectSettingsPath)) {
        Write-Error "ProjectSettings.asset not found at: $ProjectSettingsPath"
        return "0.1.0"
    }
    $match = (Get-Content $ProjectSettingsPath | Select-String "^\s*bundleVersion:\s*(.+)").Matches
    if ($match -and $match.Groups.Count -gt 1) {
        return $match.Groups[1].Value.Trim()
    }
    return "0.1.0"
}

switch ($Action) {
    "GetVersion" {
        $ver = Get-CurrentBundleVersion
        Write-Output $ver
    }

    "List" {
        if (-not (Test-Path $ChangelogPath)) {
            Write-Warning "CHANGELOG.md does not exist yet at $ChangelogPath"
            exit 0
        }
        $lines = Get-Content $ChangelogPath
        $versions = $lines | Select-String "^##\s+\[(.*?)\]\s*-\s*(.*)"
        if (-not $versions) {
            Write-Output "No versions found in CHANGELOG.md"
            exit 0
        }
        foreach ($v in $versions) {
            $m = $v.Matches[0]
            Write-Output ("v{0} ({1})" -f $m.Groups[1].Value, $m.Groups[2].Value)
        }
    }

    "ReadLatest" {
        if (-not (Test-Path $ChangelogPath)) {
            Write-Warning "CHANGELOG.md does not exist yet at $ChangelogPath"
            exit 0
        }
        $content = Get-Content $ChangelogPath -Raw
        $sections = [regex]::Split($content, "(?m)^(?=##\s+\[)")
        foreach ($sec in $sections) {
            if ($sec -match "^##\s+\[") {
                Write-Output $sec.Trim()
                break
            }
        }
    }

    "Read" {
        if (-not (Test-Path $ChangelogPath)) {
            Write-Warning "CHANGELOG.md does not exist yet at $ChangelogPath"
            exit 0
        }
        if ([string]::IsNullOrWhiteSpace($Version)) {
            $Version = Get-CurrentBundleVersion
        }
        $content = Get-Content $ChangelogPath -Raw
        $pattern = "(?ms)(##\s+\[" + [regex]::Escape($Version) + "\].*?)(?=(^##\s+\[)|\z)"
        $match = [regex]::Match($content, $pattern)
        if ($match.Success) {
            Write-Output $match.Groups[1].Value.Trim()
        } else {
            Write-Warning "Version [$Version] not found in CHANGELOG.md"
        }
    }

    "NewBuild" {
        if ([string]::IsNullOrWhiteSpace($Version)) {
            $Version = Get-CurrentBundleVersion
        }
        $today = (Get-Date).ToString("yyyy-MM-dd")
        $template = @"
## [$Version] - $today

### New Features

### Fixes

### Balance Changes

### General Changes

"@

        if (-not (Test-Path $ChangelogPath)) {
            $header = "# Bladehold - Changelog & Patch Notes`n`n"
            Set-Content -Path $ChangelogPath -Value ($header + $template) -Encoding utf8
            Write-Output "Created CHANGELOG.md with section for [$Version]"
            exit 0
        }

        $existing = Get-Content $ChangelogPath -Raw
        if ($existing -match [regex]::Escape("## [$Version]")) {
            Write-Warning "Version [$Version] already exists in CHANGELOG.md"
            exit 0
        }

        # Insert after top title / preamble
        $pattern = "(?m)^(#\s+.*?\n+)"
        if ($existing -match $pattern) {
            $updated = [regex]::Replace($existing, $pattern, "`$1" + $template + "`n")
        } else {
            $updated = $template + "`n" + $existing
        }
        Set-Content -Path $ChangelogPath -Value $updated -Encoding utf8
        Write-Output "Added new section for build [$Version] in CHANGELOG.md"
    }

    "AddEntry" {
        if ([string]::IsNullOrWhiteSpace($Message)) {
            Write-Error "Message parameter is required for AddEntry"
            exit 1
        }
        if ([string]::IsNullOrWhiteSpace($Category)) {
            Write-Error "Category parameter is required (New Features, Fixes, Balance Changes, General Changes)"
            exit 1
        }
        if ([string]::IsNullOrWhiteSpace($Version)) {
            $Version = Get-CurrentBundleVersion
        }

        if (-not (Test-Path $ChangelogPath)) {
            & $PSCommandPath "NewBuild" -Version $Version -ChangelogPath $ChangelogPath -ProjectSettingsPath $ProjectSettingsPath
        }

        $content = Get-Content $ChangelogPath -Raw
        if (-not ($content -match [regex]::Escape("## [$Version]"))) {
            & $PSCommandPath "NewBuild" -Version $Version -ChangelogPath $ChangelogPath -ProjectSettingsPath $ProjectSettingsPath
            $content = Get-Content $ChangelogPath -Raw
        }

        # Find category under this version
        $vPattern = "(?ms)(##\s+\[" + [regex]::Escape($Version) + "\].*?)(?=(^##\s+\[)|\z)"
        $vMatch = [regex]::Match($content, $vPattern)
        if (-not $vMatch.Success) {
            Write-Error "Could not locate version section [$Version]"
            exit 1
        }

        $vText = $vMatch.Groups[1].Value
        $catHeader = "### " + $Category
        if ($vText -contains $catHeader -or $vText -match [regex]::Escape($catHeader)) {
            $catPattern = "(?m)(" + [regex]::Escape($catHeader) + "\s*\r?\n)"
            $newVText = [regex]::Replace($vText, $catPattern, "`$1- $Message`n", 1)
            $content = $content.Replace($vText, $newVText)
            Set-Content -Path $ChangelogPath -Value $content -Encoding utf8
            Write-Output "Added entry to [$Version] -> [$Category]"
        } else {
            Write-Error "Category [$Category] not found under version [$Version]"
            exit 1
        }
    }
}
