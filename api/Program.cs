using System.Data;
using System.Text.Json;
using Dapper;
using Npgsql;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var connStr = builder.Configuration.GetConnectionString("Default") ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default") ?? "Host=postgres;Port=5432;Database=rag_demo;Username=postgres;Password=postgres";
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        builder.Services.AddSingleton(new DatabaseOptions(connStr));
        builder.Services.AddSingleton<AzureFoundryClient>();

        await EnsureDatabaseReadyAsync(connStr);

        var app = builder.Build();

        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

        app.MapPost("/embeddings", async (HttpRequest request, DatabaseOptions dbOpts, AzureFoundryClient foundry) =>
        {
            var body = await JsonSerializer.DeserializeAsync<EmbeddingRequest>(request.Body, jsonOptions);
            if (body is null || string.IsNullOrWhiteSpace(body.Text))
            {
                return Results.BadRequest(new { error = "Request body must include a non-empty 'text' field." });
            }

            var vector = await foundry.GetEmbeddingAsync(body.Text);

            // Build array literal for insertion, using invariant culture
            var vecArr = string.Join(",", vector.Select(f => f.ToString(System.Globalization.CultureInfo.InvariantCulture)));

            await using var conn = new NpgsqlConnection(dbOpts.ConnectionString);
            await conn.OpenAsync();
            var sql = $"INSERT INTO documents (text, metadata, vector) VALUES (@Text, @Metadata::jsonb, ARRAY[{vecArr}]::vector) RETURNING id";
            var metadataJson = body.Metadata.HasValue ? body.Metadata.Value.GetRawText() : "{}";
            var id = await conn.ExecuteScalarAsync<int>(sql, new { Text = body.Text, Metadata = metadataJson });
            return Results.Ok(new { id });
        });

        app.MapPost("/query", async (HttpRequest request, DatabaseOptions dbOpts, AzureFoundryClient foundry) =>
        {
            var body = await JsonSerializer.DeserializeAsync<QueryRequest>(request.Body, jsonOptions);
            if (body is null || string.IsNullOrWhiteSpace(body.Question))
            {
                return Results.BadRequest(new { error = "Request body must include a non-empty 'question' field." });
            }

            var qVec = await foundry.GetEmbeddingAsync(body.Question);

            // build a Postgres array literal for the query vector using invariant culture
            var qArr = string.Join(",", qVec.Select(f => f.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            var topK = body.TopK ?? 3;

            await using var conn = new NpgsqlConnection(dbOpts.ConnectionString);
            await conn.OpenAsync();
            var sql = $@"SELECT id, text, metadata, vector <-> ARRAY[{qArr}]::vector AS score
                         FROM documents
                         ORDER BY score
                         LIMIT @TopK";

            var rows = await conn.QueryAsync(sql, new { TopK = topK });
            var selected = rows.Select(r => new { id = (int)r.id, text = (string)r.text, metadata = r.metadata, score = (double)r.score }).ToList();

            var context = string.Join("\n---\n", selected.Select(s => s.text));
            var answer = await foundry.GetCompletionAsync(body.Question, context);
            return Results.Ok(new { answer, sources = selected.Select(s => new { s.id, s.score }) });
        });

        await app.RunAsync();
    }

    private static async Task EnsureDatabaseReadyAsync(string connectionString)
    {
        // Retry until Postgres is ready (it may still be starting up)
        NpgsqlConnection? conn = null;
        for (int attempt = 1; attempt <= 10; attempt++)
        {
            try
            {
                conn = new NpgsqlConnection(connectionString);
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
}
