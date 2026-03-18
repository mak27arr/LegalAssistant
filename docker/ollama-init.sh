#!/bin/sh
set -e
set -u

MODEL="${OLLAMA_MODEL:-nomic-embed-text}"

echo "Pulling model: ${MODEL}"
while :; do
  if ollama pull "$MODEL"; then
    break
  fi

  echo "pull failed, retrying in 2s..."
  sleep 2
done

echo "Done"
