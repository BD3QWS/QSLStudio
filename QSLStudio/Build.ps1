param(
    [Parameter(Mandatory=$true)][string]$FrameworkRefs,
    [string]$OutputDir = (Join-Path $PSScriptRoot 'dist')
)
$ErrorActionPreference = 'Stop'
$qslCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $qslCompiler)) { throw 'The Windows .NET Framework C# compiler was not found.' }
$qslRefDir = (Resolve-Path -LiteralPath $FrameworkRefs).Path
$qslFramework = @('mscorlib', 'System', 'System.Core', 'System.Drawing', 'System.Windows.Forms', 'System.Xml', 'System.Xml.Linq')
$qslSources = @('Program.cs','CardProject.cs','LegacyExample.cs','CardDrawing.cs','PracticalTemplates.cs','PortraitTemplates.cs','CardFiles.cs','PcbFiles.cs','GerberZip.cs','MaterialDesign.cs','MaterialStudio.cs','Glass.cs','Controls.cs','StudioMenu.cs','Editor.cs','Inspector.cs','EditorActions.cs','Dialogs.cs','OutputDialogs.cs','PictureDialog.cs','LogoLibrary.cs','Stations.cs','Storage.cs','InstallMaintenance.cs','Adif.cs','LogDialogs.cs')
$qslResources = @('ARRAM.png','URV-ABRA.png','MARC.jpg','UserManual.txt')
$qslOutput = (New-Item -ItemType Directory -Path $OutputDir -Force).FullName
$qslExe = Join-Path $qslOutput 'QSLStudio.exe'
$qslArgs = @('/nologo','/target:winexe','/platform:x86','/langversion:5','/subsystemversion:4.00','/noconfig','/nostdlib+','/optimize+','/codepage:65001',
    ('/win32manifest:' + (Join-Path $PSScriptRoot 'app.manifest')), ('/win32icon:' + (Join-Path $PSScriptRoot 'QSLStudio.ico')))
foreach ($qslName in $qslFramework) { $qslArgs += '/r:' + (Join-Path $qslRefDir ($qslName + '.dll')) }
foreach ($qslName in $qslResources) { $qslArgs += '/resource:' + (Join-Path $PSScriptRoot $qslName) + ',' + $qslName }
$qslFiles = @($qslSources | ForEach-Object { Join-Path $PSScriptRoot $_ })
$qslPreviewDir = (New-Item -ItemType Directory -Path (Join-Path $qslOutput 'previews') -Force).FullName
$qslPreviewBuilder = Join-Path $qslPreviewDir 'BuildPreviews.exe'
try {
    & $qslCompiler @qslArgs '/define:BUILD_PREVIEWS' ('/out:' + $qslPreviewBuilder) @qslFiles
    if ($LASTEXITCODE -ne 0) { throw ('Preview builder compile failed: ' + $LASTEXITCODE) }
    $qslGenerated = Start-Process -FilePath $qslPreviewBuilder -ArgumentList ('"' + $qslPreviewDir + '"') -WindowStyle Hidden -Wait -PassThru
    if ($qslGenerated.ExitCode -ne 0) { throw ('Preview generation failed: ' + $qslGenerated.ExitCode) }
} finally {
    if (Test-Path -LiteralPath $qslPreviewBuilder) { Remove-Item -LiteralPath $qslPreviewBuilder }
}
$qslPreviews = @(Get-ChildItem -LiteralPath $qslPreviewDir -Filter 'preview-*.jpg')
$qslTemplateCount = [int][regex]::Match((Get-Content -LiteralPath (Join-Path $PSScriptRoot 'CardProject.cs') -Raw), 'BuiltInCount\s*=\s*(\d+)').Groups[1].Value
if ($qslTemplateCount -lt 1 -or $qslPreviews.Count -ne 2 * $qslTemplateCount) { throw 'The build should contain both side previews for each template.' }
foreach ($qslPreview in $qslPreviews) { $qslArgs += '/resource:' + $qslPreview.FullName + ',' + $qslPreview.Name }
& $qslCompiler @qslArgs ('/out:' + $qslExe) @qslFiles
if ($LASTEXITCODE -ne 0) { throw ('Compile failed: ' + $LASTEXITCODE) }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'App.config') -Destination ($qslExe + '.config') -Force
Write-Output $qslExe
