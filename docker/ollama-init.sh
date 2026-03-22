#!/bin/sh
set -e
set -u

EMBEDDINGS_MODEL="${OLLAMA_EMBEDDINGS_MODEL:-${OLLAMA_MODEL:-nomic-embed-text}}"
LLM_MODEL="${OLLAMA_LLM_MODEL:-}"

pull() {
  m="$1"
  [ -z "$m" ] && return 0
  echo "Pulling model: ${m}"
  while :; do
    if ollama pull "$m"; then
      break
    fi
    echo "pull failed, retrying in 2s..."
    sleep 2
  done
}

pull "$EMBEDDINGS_MODEL"
pull "$LLM_MODEL"

echo "Done"
