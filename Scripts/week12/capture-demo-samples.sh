#!/usr/bin/env bash
set -euo pipefail

HOST_URL="http://127.0.0.1:5192"
OUTPUT_ROOT="artifacts/week12-demo"
VOICE_FILE=""
RUN_VOICE="0"

usage() {
  cat <<'EOF'
Usage:
  Scripts/week12/capture-demo-samples.sh [--host-url URL] [--output-root DIR] [--voice-file PATH]

Examples:
  Scripts/week12/capture-demo-samples.sh
  Scripts/week12/capture-demo-samples.sh --voice-file voice-sample.wav
  Scripts/week12/capture-demo-samples.sh --host-url http://127.0.0.1:5192 --output-root artifacts/week12-demo

What it does:
  1. Captures GET /api/skills
  2. Runs a text chat sample
  3. Runs a daily suggestion sample
  4. Runs a tech.mcp_demo skill sample
  5. Optionally runs a voice sample if --voice-file is provided
  6. Fetches replay detail + events for each run
  7. Writes a markdown summary with runIds, screenshot points, and demo narration
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
    --voice-file)
      VOICE_FILE="$2"
      RUN_VOICE="1"
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

if [[ "$RUN_VOICE" == "1" && ! -f "$VOICE_FILE" ]]; then
  echo "Voice file not found: $VOICE_FILE" >&2
  exit 4
fi

timestamp="$(date +%Y%m%d-%H%M%S)"
output_dir="${OUTPUT_ROOT}/${timestamp}"
mkdir -p "$output_dir"

check_host() {
  if curl -sS --fail "${HOST_URL}/api/skills" >/dev/null 2>&1; then
    return 0
  fi

  cat >&2 <<EOF
Unable to reach Host at: ${HOST_URL}

Hints:
- Make sure SKAgent.Host is already running.
- If Host is running in a different shell boundary, run this script in the same environment.
- In mixed Windows + WSL setups, localhost forwarding may not be available.
- You can override the URL explicitly with: --host-url http://<reachable-host>:5192
EOF
  exit 5
}

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
        try:
            value = value[int(part)]
        except Exception:
            value = None
            break
    elif isinstance(value, dict):
        value = value.get(part)
    else:
        value = None
        break
if value is None:
    sys.exit(1)
if isinstance(value, (dict, list)):
    print(json.dumps(value, ensure_ascii=False))
else:
    print(value)
PY
}

post_json() {
  local name="$1"
  local endpoint="$2"
  local payload="$3"
  local target="${output_dir}/${name}.json"
  curl -sS --fail \
    -X POST "${HOST_URL}${endpoint}" \
    -H "Content-Type: application/json" \
    -d "$payload" \
    > "$target"
  echo "$target"
}

get_json() {
  local name="$1"
  local endpoint="$2"
  local target="${output_dir}/${name}.json"
  curl -sS --fail "${HOST_URL}${endpoint}" > "$target"
  echo "$target"
}

capture_replay() {
  local prefix="$1"
  local run_id="$2"
  get_json "${prefix}-replay-detail" "/api/replay/runs/${run_id}" >/dev/null
  get_json "${prefix}-replay-events" "/api/replay/runs/${run_id}/events" >/dev/null
}

echo "Capturing Week12 demo samples into ${output_dir}"

check_host

skills_json="$(get_json "skills" "/api/skills")"

text_json="$(post_json "text-run" "/api/agent/run" '{
  "conversationId": "demo-week12-chat-001",
  "input": "请总结当前项目 Week11 已完成了什么，并给出接下来 Week12 的一个最小推进建议。"
}')"
text_run_id="$(json_get "runId" "$text_json")"
capture_replay "text-run" "$text_run_id"

daily_json="$(post_json "daily-run" "/api/suggestions/daily:run" '{
  "conversationId": "demo-week12-daily-001"
}')"
daily_run_id="$(json_get "runId" "$daily_json")"
capture_replay "daily-run" "$daily_run_id"

skill_json="$(post_json "skill-run" "/api/agent/run" '{
  "conversationId": "demo-week12-skill-001",
  "skillName": "tech.mcp_demo",
  "input": "请用最小演示路径说明 MCP demo tool 是如何接入统一 external tool / audit / replay 链路的。"
}')"
skill_run_id="$(json_get "runId" "$skill_json")"
capture_replay "skill-run" "$skill_run_id"

voice_run_id=""
if [[ "$RUN_VOICE" == "1" ]]; then
  voice_json="${output_dir}/voice-run.json"
  curl -sS --fail \
    -X POST "${HOST_URL}/api/voice/run" \
    -F "conversationId=demo-week12-voice-001" \
    -F "audio=@${VOICE_FILE}" \
    > "$voice_json"
  voice_run_id="$(json_get "runId" "$voice_json")"
  capture_replay "voice-run" "$voice_run_id"
fi

cat > "${output_dir}/summary.md" <<EOF
# Week12 Demo Sample Capture

- Captured At: $(date -Iseconds)
- Host URL: ${HOST_URL}
- Output Directory: ${output_dir}

## Run IDs

- Text run: \`${text_run_id}\`
- Daily run: \`${daily_run_id}\`
- Skill run: \`${skill_run_id}\`
$(if [[ -n "$voice_run_id" ]]; then echo "- Voice run: \`${voice_run_id}\`"; else echo "- Voice run: not captured"; fi)

## Files

- skills: \`skills.json\`
- text run response: \`text-run.json\`
- text replay detail: \`text-run-replay-detail.json\`
- text replay events: \`text-run-replay-events.json\`
- daily run response: \`daily-run.json\`
- daily replay detail: \`daily-run-replay-detail.json\`
- daily replay events: \`daily-run-replay-events.json\`
- skill run response: \`skill-run.json\`
- skill replay detail: \`skill-run-replay-detail.json\`
- skill replay events: \`skill-run-replay-events.json\`
$(if [[ -n "$voice_run_id" ]]; then printf '%s\n' "- voice run response: \`voice-run.json\`" "- voice replay detail: \`voice-run-replay-detail.json\`" "- voice replay events: \`voice-run-replay-events.json\`"; fi)

## Screenshot Points

### Text Run

- Replay run detail overview
- Timeline showing \`run_started -> prompt_composed -> plan_created -> step_* -> run_completed\`
- Prompt panel

### Daily Run

- Suggestions list entry with runId
- Replay entry for the daily run

### Skill Run

- \`GET /api/skills\` response
- Timeline showing \`skill_selected\`
- Skill panel showing \`name / displayName / source / recommendedTools\`
- Timeline showing \`external_call_started / external_call_finished\` if present

### Voice Run

$(if [[ -n "$voice_run_id" ]]; then printf '%s\n' "- Response metadata with transcript/audio content type" "- Replay timeline showing voice events"; else echo "- Voice sample was not captured in this run."; fi)

## Narration Notes

- Text run: prove the main runtime path still works without any skill.
- Replay: prove observability remains the same SSOT, not a side log.
- Daily: prove Week8 capability still reuses the same replay/index pipeline.
- Voice: prove voice is an orchestration layer on top of the same runtime.
- Skill/MCP: prove \`skill_selected -> planner hint -> external tool policy -> replay\` is one unified path.

## Follow-up

- Add Replay UI screenshots into this folder or a sibling evidence folder.
- If you need a blocked-tool drill, temporarily remove \`mcp.demo_echo\` from the allowlist and repeat only the skill sample.
EOF

echo "Done."
echo "Summary: ${output_dir}/summary.md"
