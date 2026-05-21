#!/usr/bin/env bash
set -euo pipefail

HOST_URL="http://127.0.0.1:5192"
OUTPUT_ROOT="artifacts/week12-demo-blocked"

usage() {
  cat <<'EOF'
Usage:
  Scripts/week12/capture-blocked-tool-drill.sh [--host-url URL] [--output-root DIR]

Purpose:
  Trigger the Week12 MCP/skill sample against a host where mcp.demo_echo is NOT allowlisted,
  then capture replay evidence for:
  - external_call_blocked
  - repair_plan_created
  - repair_step_*

Important:
  This script assumes the host is already running with a blocking policy.

Example host launch:
  ToolPolicy__AllowedExternalTools__0=blocked.demo_placeholder \
  '/mnt/c/Program Files/dotnet/dotnet.exe' run --project Src/SKAgent.Host/SKAgent.Host.csproj
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --host-url)
      HOST_URL="$2"
      shift 2
      ;;
    --output-root)
      OUTPUT_ROOT="$2"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
done

require_command() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Missing required command: $1" >&2
    exit 3
  fi
}

require_command curl
require_command python3

timestamp="$(date +%Y%m%d-%H%M%S)"
output_dir="${OUTPUT_ROOT}/${timestamp}"
mkdir -p "$output_dir"

if ! curl -sS --fail "${HOST_URL}/api/skills" >/dev/null 2>&1; then
  cat >&2 <<EOF
Unable to reach Host at: ${HOST_URL}

Make sure the host is running and reachable first.
EOF
  exit 5
fi

json_get() {
  local key="$1"
  local path="$2"
  python3 - "$key" "$path" <<'PY'
import json, sys
key = sys.argv[1]
path = sys.argv[2]
with open(path, "r", encoding="utf-8") as f:
    data = json.load(f)
value = data
for part in key.split("."):
    if isinstance(value, list):
        value = value[int(part)]
    else:
        value = value.get(part)
if isinstance(value, (dict, list)):
    print(json.dumps(value, ensure_ascii=False))
else:
    print(value)
PY
}

event_exists() {
  local target="$1"
  local path="$2"
  python3 - "$target" "$path" <<'PY'
import json, sys
target = sys.argv[1]
path = sys.argv[2]
with open(path, "r", encoding="utf-8") as f:
    events = json.load(f)
print("true" if any(evt.get("type") == target for evt in events) else "false")
PY
}

echo "Capturing blocked-tool drill into ${output_dir}"

curl -sS --fail \
  -X POST "${HOST_URL}/api/agent/run" \
  -H "Content-Type: application/json" \
  -d '{
    "conversationId": "demo-week12-skill-blocked-001",
    "skillName": "tech.mcp_demo",
    "input": "请用 MCP demo 路径回答这次请求，并解释工具为什么被策略阻断。"
  }' \
  > "${output_dir}/skill-blocked-run.json"

run_id="$(json_get "runId" "${output_dir}/skill-blocked-run.json")"

curl -sS --fail "${HOST_URL}/api/replay/runs/${run_id}" > "${output_dir}/skill-blocked-replay-detail.json"
curl -sS --fail "${HOST_URL}/api/replay/runs/${run_id}/events" > "${output_dir}/skill-blocked-replay-events.json"

blocked="$(event_exists "external_call_blocked" "${output_dir}/skill-blocked-replay-events.json")"
repair_plan="$(event_exists "repair_plan_created" "${output_dir}/skill-blocked-replay-events.json")"
repair_step_started="$(event_exists "repair_step_started" "${output_dir}/skill-blocked-replay-events.json")"
repair_step_completed="$(event_exists "repair_step_completed" "${output_dir}/skill-blocked-replay-events.json")"

cat > "${output_dir}/summary.md" <<EOF
# Week12 Blocked Tool Drill

- Captured At: $(date -Iseconds)
- Host URL: ${HOST_URL}
- Run ID: \`${run_id}\`

## Expected Events

- external_call_blocked: ${blocked}
- repair_plan_created: ${repair_plan}
- repair_step_started: ${repair_step_started}
- repair_step_completed: ${repair_step_completed}

## Files

- run response: \`skill-blocked-run.json\`
- replay detail: \`skill-blocked-replay-detail.json\`
- replay events: \`skill-blocked-replay-events.json\`

## Screenshot Points

- Timeline showing \`skill_selected\`
- Timeline showing \`external_call_blocked\`
- Repair panel showing failure source and repair steps
- Raw payload for blocked policy reason

## Notes

- This drill only passes when \`mcp.demo_echo\` is not allowlisted.
- If \`external_call_blocked\` is false, restart Host with a blocking policy and rerun this script.
EOF

echo "Done."
echo "Summary: ${output_dir}/summary.md"
