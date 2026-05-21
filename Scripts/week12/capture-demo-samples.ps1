param(
    [string]$HostUrl = "http://localhost:5192",
    [string]$OutputRoot = "artifacts/week12-demo",
    [string]$VoiceFile = ""
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

function Get-Json {
    param(
        [Parameter(Mandatory = $true)] [string] $Uri,
        [Parameter(Mandatory = $true)] [string] $Path
    )

    $response = Invoke-RestMethod -Uri $Uri -Method Get
    Save-Json -Value $response -Path $Path
    return $response
}

function Post-Json {
    param(
        [Parameter(Mandatory = $true)] [string] $Uri,
        [Parameter(Mandatory = $true)] $Body,
        [Parameter(Mandatory = $true)] [string] $Path
    )

    $jsonBody = $Body | ConvertTo-Json -Depth 20
    $response = Invoke-RestMethod -Uri $Uri -Method Post -ContentType "application/json" -Body $jsonBody
    Save-Json -Value $response -Path $Path
    return $response
}

function Capture-Replay {
    param(
        [Parameter(Mandatory = $true)] [string] $RunId,
        [Parameter(Mandatory = $true)] [string] $Prefix,
        [Parameter(Mandatory = $true)] [string] $Dir
    )

    Get-Json -Uri "${HostUrl}/api/replay/runs/$RunId" -Path (Join-Path $Dir "$Prefix-replay-detail.json") | Out-Null
    Get-Json -Uri "${HostUrl}/api/replay/runs/$RunId/events" -Path (Join-Path $Dir "$Prefix-replay-events.json") | Out-Null
}

function Invoke-CurlFileUpload {
    param(
        [Parameter(Mandatory = $true)] [string] $Uri,
        [Parameter(Mandatory = $true)] [string] $ConversationId,
        [Parameter(Mandatory = $true)] [string] $AudioPath,
        [Parameter(Mandatory = $true)] [string] $OutputPath
    )

    $resolvedAudioPath = (Resolve-Path $AudioPath).ProviderPath
    $resolvedOutputPath = [System.IO.Path]::GetFullPath($OutputPath)
    $curlArgs = @(
        "-sS",
        "--fail",
        "-X", "POST",
        $Uri,
        "-F", "conversationId=$ConversationId",
        "-F", "audio=@$resolvedAudioPath",
        "-o", $resolvedOutputPath
    )

    $process = Start-Process -FilePath "curl.exe" -ArgumentList $curlArgs -Wait -PassThru -NoNewWindow
    if ($process.ExitCode -ne 0) {
        throw "curl.exe failed while uploading voice sample to $Uri (exit code: $($process.ExitCode))."
    }

    if (-not (Test-Path $resolvedOutputPath)) {
        throw "Voice response file was not created: $resolvedOutputPath"
    }
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

Write-Host "Capturing Week12 demo samples into $outputDir"

$skills = Get-Json -Uri "${HostUrl}/api/skills" -Path (Join-Path $outputDir "skills.json")

$textBody = @{
    conversationId = "demo-week12-chat-001"
    input = "Summarize what Week11 completed and suggest one smallest useful Week12 next step."
}
$textRun = Post-Json -Uri "${HostUrl}/api/agent/run" -Path (Join-Path $outputDir "text-run.json") -Body $textBody
Capture-Replay -RunId $($textRun.runId) -Prefix "text-run" -Dir $outputDir

$dailyBody = @{
    conversationId = "demo-week12-daily-001"
}
$dailyRun = Post-Json -Uri "${HostUrl}/api/suggestions/daily:run" -Path (Join-Path $outputDir "daily-run.json") -Body $dailyBody
Capture-Replay -RunId $($dailyRun.runId) -Prefix "daily-run" -Dir $outputDir

$skillBody = @{
    conversationId = "demo-week12-skill-001"
    skillName = "tech.mcp_demo"
    input = "Use the smallest demo path to explain how the MCP demo tool goes through the unified external tool audit and replay path."
}
$skillRun = Post-Json -Uri "${HostUrl}/api/agent/run" -Path (Join-Path $outputDir "skill-run.json") -Body $skillBody
Capture-Replay -RunId $($skillRun.runId) -Prefix "skill-run" -Dir $outputDir

$voiceRunId = $null
if (-not [string]::IsNullOrWhiteSpace($VoiceFile)) {
    if (-not (Test-Path $VoiceFile)) {
        throw "Voice file not found: $VoiceFile"
    }

    $voiceJsonPath = Join-Path $outputDir "voice-run.json"
    Invoke-CurlFileUpload `
        -Uri "${HostUrl}/api/voice/run" `
        -ConversationId "demo-week12-voice-001" `
        -AudioPath $VoiceFile `
        -OutputPath $voiceJsonPath

    $voiceRun = Get-Content -Path $voiceJsonPath -Raw | ConvertFrom-Json
    $voiceRunId = $voiceRun.runId
    Capture-Replay -RunId $voiceRunId -Prefix "voice-run" -Dir $outputDir
}

$voiceRunDisplay = if ($voiceRunId) { $voiceRunId } else { "not captured" }
$voiceNarration = if ($voiceRunId) { "- Response metadata and replay voice events" } else { "- Voice sample was not captured in this run." }
$textRunDisplay = $textRun.runId
$dailyRunDisplay = $dailyRun.runId
$skillRunDisplay = $skillRun.runId

$summary = @"
# Week12 Demo Sample Capture

- Captured At: $(Get-Date -Format o)
- Host URL: $HostUrl
- Output Directory: $outputDir

## Run IDs

- Text run: $textRunDisplay
- Daily run: $dailyRunDisplay
- Skill run: $skillRunDisplay
- Voice run: $voiceRunDisplay

## Suggested Screenshot Points

### Text Run

- Replay detail overview
- Timeline showing run_started -> prompt_composed -> plan_created -> step_* -> run_completed
- Prompt panel

### Daily Run

- Suggestions list entry with runId
- Replay entry for the daily run

### Skill Run

- GET /api/skills response
- Timeline showing skill_selected
- Skill panel showing name / displayName / source / recommendedTools
- Timeline showing external_call_started / external_call_finished

### Voice Run

$voiceNarration

## Narration Notes

- Text run: prove the main runtime path still works without any skill.
- Replay: prove observability remains the same SSOT, not a side log.
- Daily: prove Week8 capability still reuses the same replay/index pipeline.
- Voice: prove voice is an orchestration layer on top of the same runtime.
- Skill/MCP: prove skill_selected -> planner hint -> external tool policy -> replay is one unified path.
"@

Set-Content -Path (Join-Path $outputDir "summary.md") -Value $summary -Encoding UTF8

Write-Host "Done."
Write-Host "Summary: $(Join-Path $outputDir 'summary.md')"
