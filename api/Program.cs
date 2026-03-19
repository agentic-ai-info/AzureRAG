using System.Data;
using System.Text.Json;
using Dapper;
using Npgsql;

// Models and helpers moved to Models.cs

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var connStr = builder.Configuration.GetConnectionString("Default") ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default") ?? "Host=postgres;Port=5432;Database=rag_demo;Username=postgres;Password=postgres";

        builder.Services.AddSingleton(new DatabaseOptions(connStr));
        builder.Services.AddSingleton<AzureFoundryClient>();

        var app = builder.Build();

        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

        app.MapPost("/embeddings", async (HttpRequest request, DatabaseOptions dbOpts, AzureFoundryClient foundry) =>
        {
            var body = await JsonSerializer.DeserializeAsync<EmbeddingRequest>(request.Body) ?? throw new Exception("invalid body");
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
            var body = await JsonSerializer.DeserializeAsync<QueryRequest>(request.Body) ?? throw new Exception("invalid body");
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
}
