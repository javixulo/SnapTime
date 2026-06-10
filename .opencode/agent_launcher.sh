#!/usr/bin/env bash
# Simple agent launcher wrapper/enforcer
# Usage: ./agent_launcher.sh <AgentName> <Model> "<TaskDescription>" <User>

set -euo pipefail

AGENT_NAME="$1"
MODEL_ARG="$2"
TASK_DESC="$3"
USER_NAME="$4"

AGENT_MD=".opencode/agents/$(echo "$AGENT_NAME" | tr '[:upper:]' '[:lower:]').md"
# fallback: try as provided
if [ ! -f "$AGENT_MD" ]; then
  AGENT_MD=".opencode/agents/${AGENT_NAME}.md"
fi

REQUIRED_MODEL=""
if [ -f "$AGENT_MD" ]; then
  REQUIRED_MODEL_LINE=$(grep -E "^model:\s*" "$AGENT_MD" || true)
  if [ -n "$REQUIRED_MODEL_LINE" ]; then
    REQUIRED_MODEL=$(echo "$REQUIRED_MODEL_LINE" | sed -E "s/^model:\s*//" | tr -d '"')
  fi
fi

TIMESTAMP=$(date -u +"%Y-%m-%dT%H-%M-%SZ")
RUN_DIR=".opencode/runs"
mkdir -p "$RUN_DIR"
OUT_FILE="$RUN_DIR/${AGENT_NAME}_run_${TIMESTAMP}.json"

if [ -n "$REQUIRED_MODEL" ] && [ "$REQUIRED_MODEL" != "$MODEL_ARG" ]; then
  echo "[agent_launcher] Provided model '$MODEL_ARG' does not match required '$REQUIRED_MODEL' for agent $AGENT_NAME" >&2
  # write audit with mismatch
  cat > "$OUT_FILE" <<EOF
{
  "agent": "$AGENT_NAME",
  "model_provided": "$MODEL_ARG",
  "model_required": "$REQUIRED_MODEL",
  "timestamp": "$(date -u +"%Y-%m-%dT%H:%M:%SZ")",
  "task": "$TASK_DESC",
  "user": "$USER_NAME",
  "status": "rejected",
  "reason": "model_mismatch"
}
EOF
  echo "[agent_launcher] Rejected execution due to model mismatch. Audit written to $OUT_FILE" >&2
  exit 2
fi

# Accept and write audit
cat > "$OUT_FILE" <<EOF
{
  "agent": "$AGENT_NAME",
  "model": "$MODEL_ARG",
  "timestamp": "$(date -u +"%Y-%m-%dT%H:%M:%SZ")",
  "task": "$TASK_DESC",
  "user": "$USER_NAME",
  "status": "accepted"
}
EOF

echo "$MODEL_ARG"
echo "[agent_launcher] Audit written to $OUT_FILE"

# Note: This wrapper currently only logs and enforces model parameter.
# Integrating with the actual agent runtime/Task runner requires adapting the runner to call this script.
