# Azure Foundry + Postgres RAG demo

This demo shows a minimal RAG pipeline using an ASP.NET Web API, PostgreSQL (vector stored as JSONB for demo), and a placeholder Azure Foundry client.

Quick start:

1. Build and start the stack:

```bash
docker compose up --build
```

2. Create an embedding:

```bash
curl -X POST http://localhost:5001/embeddings \
  -H 'Content-Type: application/json' \
  -d '{"text":"Hello from demo","metadata":{"source":"sample"}}'
```

3. Query:

```bash
curl -X POST http://localhost:5001/query \
  -H 'Content-Type: application/json' \
  -d '{"question":"What is Hello from demo?"}'
```

4. Embed a local file:

```bash
python3 scripts/embed_file.py README.md
```

Optional arguments:

```bash
python3 scripts/embed_file.py README.md --chunk-size 1200 --overlap 150 --api-base-url http://localhost:5001
```

Notes:
- This scaffold uses a deterministic local embedding generator when `AZURE_FOUNDRY_API_KEY` is not provided. Replace with a real Foundry endpoint and API key by setting `AZURE_FOUNDRY_ENDPOINT` and `AZURE_FOUNDRY_API_KEY` in `docker-compose.yml` or environment.
- For production-style vector search, install and enable the `pgvector` extension and store vectors in `vector` column. The current SQL uses `jsonb` as a safe fallback for demo purposes.
