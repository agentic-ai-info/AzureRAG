#!/usr/bin/env bash
set -euo pipefail

echo "Start services..."
echo "Run: docker compose up --build"

echo "After stack is healthy, create an embedding:"
echo "curl -X POST http://localhost:5001/embeddings -H 'Content-Type: application/json' -d '{\"text\":\"Hello from demo\",\"metadata\":{\"source\":\"sample\"}}'"

echo "Query:"
echo "curl -X POST http://localhost:5001/query -H 'Content-Type: application/json' -d '{\"question\":\"What is Hello from demo?\"}'"
