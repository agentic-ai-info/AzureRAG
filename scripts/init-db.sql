-- Initialize DB: attempt to enable pgvector if available, create documents table.
CREATE EXTENSION IF NOT EXISTS pgvector;

CREATE TABLE IF NOT EXISTS documents (
  id SERIAL PRIMARY KEY,
  text TEXT NOT NULL,
  metadata JSONB,
  vector vector(8),
  created_at TIMESTAMPTZ DEFAULT now()
);
