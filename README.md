# Azure Foundry + Postgres RAG demo

This demo shows a minimal RAG pipeline using an ASP.NET Web API, PostgreSQL, pgvector extension, and Azure Foundry client.
You can read more about this demo here: https://agentic-ai.info/implementing-rag-with-azure-foundry-net-backend-and-postgresql-vector-database/

Quick start:

1. Build and start the stack:

```bash
docker compose up --build
```

2. Create an embedding (or skip and go to Point 4. Embed a local file):

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

4. Embed a local file (use the demo text provided in `demo-data.txt`):

```bash
python3 scripts/embed_file.py scripts/demo-data.txt --source demo-data
```

Optional arguments:

```bash
python3 scripts/embed_file.py scripts/demo-data.txt --chunk-size 1200 --overlap 150 --api-base-url http://localhost:5001 --source demo-data
```

Example queries (after embedding `demo-data.txt`):

```bash
curl -X POST http://localhost:5001/query \
  -H 'Content-Type: application/json' \
  -d '{"question":"Which city did the narrator visit after Lisbon?"}'
```

```bash
curl -X POST http://localhost:5001/query \
  -H 'Content-Type: application/json' \
  -d '{"question":"What are the best month for travel?"}'
```

Notes:
- Configure Azure credentials in `.env` (copy `.env.template` as a starting point). The API will fail to start if any of the three required variables (`AZURE_FOUNDRY_API_KEY`, `AZURE_FOUNDRY_ENDPOINT`, `AZURE_FOUNDRY_EMBEDDINGS_ENDPOINT`) are missing.
- Vector search uses the `pgvector` extension with a `vector(1536)` column, matching the `text-embedding-ada-002` / `text-embedding-3-small` output dimension. If you use different embedding models the db init script needs to be adjusted.
