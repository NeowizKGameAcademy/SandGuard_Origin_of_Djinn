param([Parameter(Mandatory=$true)][string]$Results)
$ErrorActionPreference = 'Stop'
$rows = @(Import-Csv -LiteralPath $Results)
$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('# DesertTemple V6 load test')
$lines.Add('')
$lines.Add('CombatProxy excludes real tower search/projectiles. Churn is reported separately from steady live-count capacity. GPU timing may be unavailable. This report does not certify wave completion.')
$lines.Add('')
$lines.Add('| Scenario | View | Mix | Count | Repeat | FPS | P95 ms | P99 ms | Alive min | Off NavMesh | Rendered/frames | Pass |')
$lines.Add('|---|---|---|---:|---:|---:|---:|---:|---:|---:|---|---|')
foreach ($row in $rows) {
    $lines.Add("| $($row.scenario) | $($row.view) | $($row.mix) | $($row.count) | $($row.repeat) | $($row.avg_fps) | $($row.p95_ms) | $($row.p99_ms) | $($row.alive_min) | $($row.off_navmesh_max) | $($row.rendered_frames)/$($row.frames) | $($row.p95_60fps_pass) |")
}
$lines.Add('')
$qualified = @()
foreach ($group in ($rows | Where-Object { [int]$_.count -gt 0 -and $_.scenario -ne 'Churn' } | Group-Object count)) {
    $cases = @($group.Group)
    $coverage = @($cases | ForEach-Object { "$($_.scenario)/$($_.view)" } | Sort-Object -Unique)
    $bad = @($cases | Where-Object { $_.p95_60fps_pass -ne 'True' -or [double]::Parse($_.seconds,[cultureinfo]::InvariantCulture) -lt 59 })
    if ($coverage.Count -eq 6 -and $bad.Count -eq 0) { $qualified += [int]$group.Name }
}
if ($qualified.Count -gt 0) {
    $maximum = ($qualified | Measure-Object -Maximum).Maximum
    $lines.Add("Highest tested passing count: $maximum. Provisional budget with 25% reserve: $([math]::Floor($maximum * .75)).")
    $lines.Add('Repeat the boundary three times, verify heavy mix and ten-minute soak before adopting this budget. It is a tested count, not an interpolated hardware limit.')
} else { $lines.Add('No capacity recommendation: no count passed all six steady-state scenario/view combinations for at least 60 seconds. Smoke runs never establish capacity.') }
$report = Join-Path (Split-Path -Parent $Results) 'summary.md'
$lines | Set-Content -LiteralPath $report -Encoding utf8
Write-Host "Report: $report"
