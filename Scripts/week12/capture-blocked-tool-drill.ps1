param(
    [string]$HostUrl = "http://localhost:5192",
    [string]$OutputRoot = "artifacts/week12-demo-blocked"
)

$ErrorActionPreference = "Stop"

function Save-Json {
    param(
        [Parameter(Mandatory = $true)] $Value,
        [Parameter(Mandatory = $true)] [string] $Path
    )

    $dir = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($dir)) {
        New-Item -ItemType Directory -Force -Path $dir | Out-Null
    }

    $Value | ConvertTo-Json -Depth 20 | Set-Content -Path $Path -Encoding UTF8
}

function Event-Exists {
    param(
        [Parameter(Mandatory = $true)] $Events,
        [Parameter(Mandatory = $true)] [string] $Type
    )

    return $null -ne ($Events | Where-Object { $_.type -eq $Type } | Select-Object -First 1)
}

try {
    Invoke-RestMethod -Uri "${HostUrl}/api/skills" -Method Get | Out-Null
}
catch {
    throw "Unable to reach Host at $HostUrl. Start SKAgent.Host first, then rerun this script."
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$outputDir = Join-Path $OutputRoot $timestamp
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

Write-Host "Capturing blocked-tool drill into $outputDir"

$run = Invoke-RestMethod -Uri "${HostUrl}/api/agent/run" -Method Post -ContentType "application/json" -Body (@{
    conversationId = "demo-week12-skill-blocked-001"
    skillName = "tech.mcp_demo"
    input = "Use the MCP demo path for this request and explain why the tool is blocked by policy."
} | ConvertTo-Json -Depth 20)
Save-Json -Value $run -Path (Join-Path $outputDir "skill-blocked-run.json")

$detail = Invoke-RestMethod -Uri "${HostUrl}/api/replay/runs/$($run.runId)" -Method Get
$events = Invoke-RestMethod -Uri "${HostUrl}/api/replay/runs/$($run.runId)/events" -Method Get
Save-Json -Value $detail -Path (Join-Path $outputDir "skill-blocked-replay-detail.json")
Save-Json -Value $events -Path (Join-Path $outputDir "skill-blocked-replay-events.json")

$summary = @"
# Week12 Blocked Tool Drill

- Captured At: $(Get-Date -Format o)
- Host URL: $HostUrl
- Run ID: `$($run.runId)`

## Expected Events

- external_call_blocked: $(Event-Exists -Events $events -Type "external_call_blocked")
- repair_plan_created: $(Event-Exists -Events $events -Type "repair_plan_created")
- repair_step_started: $(Event-Exists -Events $events -Type "repair_step_started")
- repair_step_completed: $(Event-Exists -Events $events -Type "repair_step_completed")

## Suggested Screenshot Points

- Timeline showing `skill_selected`
- Timeline showing `external_call_blocked`
- Repair panel showing failure source and repair steps
- Raw payload for blocked policy reason

## Notes

- This drill only passes when `mcp.demo_echo` is not allowlisted.
- If `external_call_blocked` is false, restart Host with a blocking policy and rerun this script.
"@

Set-Content -Path (Join-Path $outputDir "summary.md") -Value $summary -Encoding UTF8

Write-Host "Done."
Write-Host "Summary: $(Join-Path $outputDir 'summary.md')"
