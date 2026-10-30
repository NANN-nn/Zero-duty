param([string]$OutputDirectory = 'F:/CodexTemp/EndOfShift-StateTests')
$ErrorActionPreference = 'Stop'
# Compile the existing pure state tests against Unity's bundled Mono profile.
# No project assets are imported or modified by this standalone check.
$projectRoot = if ($PSScriptRoot) {
    [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
} else { (Get-Location).Path }
$unityData = 'C:/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Data'
$dotnetPath = Join-Path $unityData 'NetCoreRuntime/dotnet.exe'
$compilerPath = Join-Path $unityData 'DotNetSdkRoslyn/csc.dll'
$monoPath = Join-Path $unityData 'MonoBleedingEdge/bin/mono.exe'
$profilePath = Join-Path $unityData 'MonoBleedingEdge/lib/mono/4.8-api'
$testCases = @('GasPressureState', 'GhostApproachProfile', 'MonitorFaultState', 'RobotCollectionState', 'StationLanguageCatalog')
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$results = @()
foreach ($testCase in $testCases) {
    $target = Join-Path $OutputDirectory ($testCase + '.exe')
    $source = Join-Path $projectRoot ('Assets/scripts/Gameplay/' + $testCase + '.cs')
    $test = Join-Path $projectRoot ('ArtSource/Tests/' + $testCase + 'Tests.cs')
    & $dotnetPath $compilerPath '-nologo' '-noconfig' '-nostdlib' '-target:exe' `
        ('-r:' + (Join-Path $profilePath 'mscorlib.dll')) `
        ('-r:' + (Join-Path $profilePath 'System.dll')) `
        ('-r:' + (Join-Path $profilePath 'System.Core.dll')) `
        ('-out:' + $target) $source $test
    if ($LASTEXITCODE -ne 0) { throw "Compilation failed: $testCase" }
    $result = & $monoPath $target
    if ($LASTEXITCODE -ne 0) { throw "Test failed: $testCase" }
    $results += ($testCase + ': ' + ($result -join ' '))
    Write-Output $results[-1]
}
$results | Out-File -LiteralPath (Join-Path $OutputDirectory 'results.txt') -Encoding utf8
