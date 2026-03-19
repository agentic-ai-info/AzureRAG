using System.Text;
using System.Text.Json;

public class AzureFoundryClient
{
    private readonly string? _apiKey = Environment.GetEnvironmentVariable("AZURE_FOUNDRY_API_KEY");
    // Full URL to the chat completions deployment, e.g. .../deployments/gpt-4.1-mini/chat/completions?api-version=...
    private readonly string? _chatEndpoint = Environment.GetEnvironmentVariable("AZURE_FOUNDRY_ENDPOINT");
    // Full URL to the embeddings deployment, e.g. .../deployments/text-embedding-3-small/embeddings?api-version=...
    private readonly string? _embeddingsEndpoint = Environment.GetEnvironmentVariable("AZURE_FOUNDRY_EMBEDDINGS_ENDPOINT");
    private readonly HttpClient _http = new();

    public AzureFoundryClient() { }

    private bool IsDemo =>
        string.IsNullOrEmpty(_apiKey) || _apiKey == "placeholder" ||
        string.IsNullOrEmpty(_embeddingsEndpoint) || _embeddingsEndpoint.Contains("replace-with");

    public async Task<float[]> GetEmbeddingAsync(string text)
    {
        if (IsDemo)
            return GenerateDeterministicEmbedding(text, 1536);

        var req = new { input = text };
        var reqJson = JsonSerializer.Serialize(req);
        using var httpReq = new HttpRequestMessage(HttpMethod.Post, _embeddingsEndpoint);
        httpReq.Headers.Add("api-key", _apiKey);
        httpReq.Content = new StringContent(reqJson, Encoding.UTF8, "application/json");
        var res = await _http.SendAsync(httpReq);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var emb = doc.RootElement.GetProperty("data")[0].GetProperty("embedding");
        return emb.EnumerateArray().Select(e => (float)e.GetDouble()).ToArray();
    }

    public async Task<string> GetCompletionAsync(string question, string context)
    {
        if (IsDemo)
            return $"[demo answer] Question: {question}\nContext:\n{context}";

        var messages = new object[]
        {
            new { role = "system", content = "You are a helpful assistant. Answer questions based only on the provided context." },
            new { role = "user", content = $"Context:\n{context}\n\nQuestion: {question}" }
        };
        var req = new { messages };
        var reqJson = JsonSerializer.Serialize(req);
        using var httpReq = new HttpRequestMessage(HttpMethod.Post, _chatEndpoint);
        httpReq.Headers.Add("api-key", _apiKey);
        httpReq.Content = new StringContent(reqJson, Encoding.UTF8, "application/json");
        var res = await _http.SendAsync(httpReq);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;
    }

    private static float[] GenerateDeterministicEmbedding(string text, int dim)
    {
        var seed = text.Aggregate(0, (a, c) => a * 31 + c);
        var rnd = new Random(seed);
        var v = new float[dim];
        for (int i = 0; i < dim; i++) v[i] = (float)(rnd.NextDouble() * 2 - 1);
        return v;
    }
}
