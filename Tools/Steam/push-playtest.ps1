<#
.SYNOPSIS
  Upload a Windows build to the Bladehold Playtest app (5396490) through SteamCMD.

.EXAMPLE
  ./Tools/Steam/push-playtest.ps1 -BuildDir Builds/0.1.33 -Username my_builder
  ./Tools/Steam/push-playtest.ps1 -BuildDir Builds/0.1.33 -Username my_builder -SetLive testers

.NOTES
  Log in once interactively first so SteamCMD caches the credentials (Steam Guard):
    C:\Users\lance\steamcmd\steamcmd.exe +login <username> +quit
  The default branch can't be set live from SteamCMD; do that on the app's SteamPipe > Builds page.
#>
param(
    [Parameter(Mandatory)] [string] $BuildDir,
    [string] $Username = $env:STEAM_BUILDER_USER,
    [string] $SetLive = "",
    [string] $Desc = "",
    [string] $SteamCmd = "$env:USERPROFILE\steamcmd\steamcmd.exe"
)

$ErrorActionPreference = "Stop"

$AppId = "5396490"
$DepotId = "5396491"

if (-not $Username) { throw "Pass -Username or set STEAM_BUILDER_USER." }
if (-not (Test-Path $SteamCmd)) { throw "SteamCMD not found at $SteamCmd" }

$content = (Resolve-Path $BuildDir).Path
if (-not (Test-Path (Join-Path $content "Bladehold.exe"))) { throw "No Bladehold.exe in $content" }

if (-not $Desc) {
    $version = Split-Path $content -Leaf
    $commit = (git rev-parse --short HEAD 2>$null)
    $Desc = "Playtest $version ($commit)"
}

# Generated scripts and SteamCMD's build cache live outside the repo.
$work = Join-Path (Split-Path $SteamCmd) "bladehold"
$output = Join-Path $work "output"
New-Item -ItemType Directory -Force $output | Out-Null

$q = { param($s) $s.Replace('\', '\\') }
$setLiveLine = if ($SetLive) { "`t`"SetLive`" `"$SetLive`"" } else { "" }

$vdf = @"
"AppBuild"
{
	"AppID" "$AppId"
	"Desc" "$Desc"
	"ContentRoot" "$(& $q $content)"
	"BuildOutput" "$(& $q $output)"
$setLiveLine
	"Depots"
	{
		"$DepotId"
		{
			"FileMapping"
			{
				"LocalPath" "*"
				"DepotPath" "."
				"recursive" "1"
			}
			"FileExclusion" "*.pdb"
			"FileExclusion" "*_DoNotShip*"
		}
	}
}
"@

$vdfPath = Join-Path $work "app_build_$AppId.vdf"
Set-Content -Path $vdfPath -Value $vdf -Encoding ascii

Write-Host "Uploading $content to app $AppId depot $DepotId as '$Desc'"
& $SteamCmd +login $Username +run_app_build $vdfPath +quit
if ($LASTEXITCODE -ne 0) { throw "SteamCMD exited with $LASTEXITCODE" }

Write-Host "Done. Set the build live under SteamPipe > Builds for app $AppId."
