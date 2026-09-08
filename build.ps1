$ErrorActionPreference = 'Stop'
$q = Join-Path $env:LOCALAPPDATA 'Programs\QuickLook'
$fx = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
& "$fx\csc.exe" /nologo /target:library "/out:$PSScriptRoot\QuickLook.Plugin.OriginPreview.dll" "/reference:$q\QuickLook.Common.dll" "/reference:$fx\WPF\PresentationFramework.dll" "/reference:$fx\WPF\PresentationCore.dll" "/reference:$fx\WPF\WindowsBase.dll" "/reference:$fx\WPF\WindowsFormsIntegration.dll" /reference:System.Xml.Linq.dll /reference:System.Xaml.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "$PSScriptRoot\src\Plugin.cs"
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed' }

