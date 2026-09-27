$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Path (Join-Path $PSScriptRoot 'bin') -Force | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object FullName)
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$refs=@('System.Runtime.dll','System.Runtime.WindowsRuntime.dll','System.Threading.Tasks.dll','System.Runtime.InteropServices.WindowsRuntime.dll') | ForEach-Object { '/r:'+(Join-Path $framework $_) }
$metadata=@('Windows.Foundation.winmd','Windows.Media.winmd','Windows.Graphics.winmd','Windows.Storage.winmd','Windows.Globalization.winmd') | ForEach-Object { '/r:'+(Join-Path (Join-Path $env:WINDIR 'System32\WinMetadata') $_) }
$icon = Join-Path (Split-Path -Parent $PSScriptRoot) 'assets\VHVN.ico'
& $compiler /nologo /target:winexe /platform:x64 /optimize+ ('/win32icon:' + $icon) ('/out:' + (Join-Path $PSScriptRoot 'bin\VisualNovelHelper.exe')) /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:Microsoft.CSharp.dll $refs $metadata $sources
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
