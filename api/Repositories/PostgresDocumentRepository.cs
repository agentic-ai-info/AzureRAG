using System.Globalization;
using Dapper;
using Npgsql;

public class PostgresDocumentRepository : IDocumentRepository
{
    private readonly DatabaseOptions _dbOptions;

    public PostgresDocumentRepository(DatabaseOptions dbOptions)
    {
        _dbOptions = dbOptions;
    }

    public async Task EnsureDatabaseReadyAsync()
    {
        // Retry until Postgres is ready (it may still be starting up)
        NpgsqlConnection? conn = null;
        for (int attempt = 1; attempt <= 10; attempt++)
        {
            try
            {
                conn = new NpgsqlConnection(_dbOptions.ConnectionString);
                await conn.OpenAsync();
                break;
            }
            catch (NpgsqlException) when (attempt < 10)
            {
                await (conn?.DisposeAsync() ?? ValueTask.CompletedTask);
                conn = null;
                Console.WriteLine($"Postgres not ready yet, retrying in 2s (attempt {attempt}/10)...");
                await Task.Delay(2000);
            }
        }

        if (conn is null) throw new InvalidOperationException("Could not connect to Postgres after 10 attempts.");
        await using var _ = conn;

        // Enable pgvector extension
        await conn.ExecuteAsync("CREATE EXTENSION IF NOT EXISTS vector;");

        // Drop the documents table if it was created with a different vector dimension,
        // so we recreate it with the correct 1536 dims (for text-embedding-ada-002).
        await conn.ExecuteAsync(@"
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM pg_attribute a
                    JOIN pg_class c ON a.attrelid = c.oid
                    WHERE c.relname = 'documents' AND c.relkind = 'r'
                      AND a.attname = 'vector'
                      AND a.atttypmod != 1536
                ) THEN
                    DROP TABLE documents;
                END IF;
            END $$;");

        await conn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS documents (
              id SERIAL PRIMARY KEY,
              text TEXT NOT NULL,
              metadata JSONB,
              vector vector(1536),
              created_at TIMESTAMPTZ DEFAULT now()
            );");
    }

    public async Task<int> InsertAsync(string text, string metadataJson, float[] vector)
    {
        var vecArr = string.Join(",", vector.Select(f => f.ToString(CultureInfo.InvariantCulture)));

        await using var conn = new NpgsqlConnection(_dbOptions.ConnectionString);
        await conn.OpenAsync();

        var sql = $"INSERT INTO documents (text, metadata, vector) VALUES (@Text, @Metadata::jsonb, ARRAY[{vecArr}]::vector) RETURNING id";
        return await conn.ExecuteScalarAsync<int>(sql, new { Text = text, Metadata = metadataJson });
    }

    public async Task<IReadOnlyList<DocumentSearchResult>> SearchNearestAsync(float[] queryVector, int topK)
    {
        var qArr = string.Join(",", queryVector.Select(f => f.ToString(CultureInfo.InvariantCulture)));

        await using var conn = new NpgsqlConnection(_dbOptions.ConnectionString);
        await conn.OpenAsync();

        var sql = $@"SELECT id, text, metadata, vector <-> ARRAY[{qArr}]::vector AS score
                     FROM documents
                     ORDER BY score
                     LIMIT @TopK";

        var rows = await conn.QueryAsync(sql, new { TopK = topK });
        return rows
            .Select(r => new DocumentSearchResult((int)r.id, (string)r.text, (double)r.score))
            .ToList();
    }
}
