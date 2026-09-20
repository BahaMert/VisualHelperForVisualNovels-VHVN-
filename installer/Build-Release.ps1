param(
    [Parameter(Mandatory=$true)][string]$Iscc,
    [string]$OutputDirectory,
    [switch]$SkipReaderBuild
)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
if (!$OutputDirectory) { $OutputDirectory=Join-Path $repo 'artifacts' }
$version=([IO.File]::ReadAllText((Join-Path $repo 'VERSION.txt'))).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'VERSION.txt must contain a semantic version.' }
if (!$SkipReaderBuild) {
    & (Join-Path $repo 'source\build.ps1')
    Copy-Item -LiteralPath (Join-Path $repo 'source\bin\VisualNovelHelper.exe') -Destination (Join-Path $repo 'bin\VisualNovelHelper.exe') -Force
}
$binary=Join-Path $repo 'bin\VisualNovelHelper.exe'
if ((Get-Item -LiteralPath $binary).VersionInfo.FileVersion -ne "$version.0") { throw 'Reader binary and release versions differ. Rebuild the reader.' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
& $Iscc '/Qp' "/DReleaseVersion=$version" "/DReleaseOutput=$OutputDirectory" (Join-Path $PSScriptRoot 'VHVN.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
$installer=Join-Path $OutputDirectory 'VHVN-Setup.exe'
$hash=(Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'SHA256SUMS.txt'), "$hash  VHVN-Setup.exe`n", [Text.UTF8Encoding]::new($false))
Write-Output "Built VHVN $version installer: $installer"
