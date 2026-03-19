-- Initialize DB: attempt to enable pgvector if available, create documents table.
CREATE EXTENSION IF NOT EXISTS pgvector;

CREATE TABLE IF NOT EXISTS documents (
  id SERIAL PRIMARY KEY,
  text TEXT NOT NULL,
  metadata JSONB,
  vector vector(1536),
  created_at TIMESTAMPTZ DEFAULT now()
);
