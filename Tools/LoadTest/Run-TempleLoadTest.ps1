param(
    [ValidateSet('Smoke','Sweep','Refine','Heavy','Soak')][string]$Mode = 'Sweep',
    [string]$Counts = '75,100,125',
    [int]$SoakCount = 100
)
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$executable = Join-Path $projectRoot 'Builds/TempleLoadTest/TempleLoadTest.exe'
if (!(Test-Path -LiteralPath $executable)) { throw 'Build the benchmark first: SandGuard > Load Test > Build Windows Benchmark in Unity.' }
$outputDirectory = Join-Path $projectRoot ('Logs/TempleLoadTest/' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + $Mode)
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$arguments = @('-force-d3d11','-screen-fullscreen','0','-screen-width','1920','-screen-height','1080','--output',$outputDirectory,'-logFile',(Join-Path $outputDirectory 'player.log'))
switch ($Mode) {
    'Smoke' { $arguments += '--smoke' }
    'Sweep' { $arguments += @('--counts','0,25,50,100,150,200') }
    'Refine' { $arguments += @('--counts',$Counts,'--repeats','3') }
    'Heavy' { $arguments += @('--counts',$Counts,'--repeats','3','--heavy') }
    'Soak' { $arguments += @('--counts','0','--seconds','1','--soak','--soak-count',"$SoakCount") }
}
Write-Host "Running $Mode; results: $outputDirectory"
# The rendering window must remain visible and not minimized. The pipeline waits for GUI executables.
& $executable @arguments | Out-Host
if (!(Test-Path -LiteralPath (Join-Path $outputDirectory 'COMPLETE.txt'))) { throw "Run did not complete. See $outputDirectory" }
& (Join-Path $PSScriptRoot 'Summarize-TempleLoadTest.ps1') -Results (Join-Path $outputDirectory 'results.csv')
Write-Host "Finished: $outputDirectory"
