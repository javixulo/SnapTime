#!/usr/bin/env bash
# Convenience runner that enforces model and launches agent wrapper.
# Usage: ./scripts/run-agent.sh <AgentName> "<TaskDescription>" <User>

set -euo pipefail

AGENT_NAME="$1"
TASK_DESC="$2"
USER_NAME="$3"

AGENT_MD=".opencode/agents/${AGENT_NAME,,}.md"
if [ ! -f "$AGENT_MD" ]; then
  AGENT_MD=".opencode/agents/${AGENT_NAME}.md"
fi

if [ ! -f "$AGENT_MD" ]; then
  echo "Agent metadata not found: $AGENT_MD" >&2
  exit 1
fi

MODEL_LINE=$(grep -E "^model:\s*" "$AGENT_MD" || true)
if [ -n "$MODEL_LINE" ]; then
  MODEL=$(echo "$MODEL_LINE" | sed -E "s/^model:\s*//" | tr -d '"')
else
  # fallback default
  MODEL="qwen2.5-coder:14b"
fi

echo "[run-agent] Launching agent $AGENT_NAME with model $MODEL"

./.opencode/agent_launcher.sh "$AGENT_NAME" "$MODEL" "$TASK_DESC" "$USER_NAME"

echo "[run-agent] Agent launched (wrapper output above). See .opencode/runs for audit file."
