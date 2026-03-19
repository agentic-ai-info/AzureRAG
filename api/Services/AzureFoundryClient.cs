using System.Text;
using System.Text.Json;

public class AzureFoundryClient
{
    private readonly string? _apiKey = Environment.GetEnvironmentVariable("AZURE_FOUNDRY_API_KEY");
    private readonly string? _endpoint = Environment.GetEnvironmentVariable("AZURE_FOUNDRY_ENDPOINT");
    private readonly HttpClient _http = new();

    public AzureFoundryClient() { }

    public async Task<float[]> GetEmbeddingAsync(string text)
    {
        // If user hasn't configured Foundry, return a deterministic pseudo-embedding for demo
        if (string.IsNullOrEmpty(_apiKey) || _apiKey == "placeholder")
        {
            return GenerateDeterministicEmbedding(text, 8);
        }

        // TODO: call real Azure Foundry embeddings endpoint
        var req = new { input = text };
        var reqJson = JsonSerializer.Serialize(req);
        using var httpReq = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(_endpoint!), "/embeddings"));
        httpReq.Headers.Add("Authorization", $"Bearer {_apiKey}");
        httpReq.Content = new StringContent(reqJson, Encoding.UTF8, "application/json");
        var res = await _http.SendAsync(httpReq);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        // Expect a path like: data[0].embedding
        var emb = doc.RootElement.GetProperty("data")[0].GetProperty("embedding");
        var list = emb.EnumerateArray().Select(e => (float)e.GetDouble()).ToArray();
        return list;
    }

    public async Task<string> GetCompletionAsync(string prompt, string context)
    {
        if (string.IsNullOrEmpty(_apiKey) || _apiKey == "placeholder")
        {
            return $"[demo answer] Question: {prompt}\nContext:\n{context}";
        }

        // TODO: call real Azure Foundry LLM endpoint
        var req = new { prompt = prompt, context = context };
        var reqJson = JsonSerializer.Serialize(req);
        using var httpReq = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(_endpoint!), "/completions"));
        httpReq.Headers.Add("Authorization", $"Bearer {_apiKey}");
        httpReq.Content = new StringContent(reqJson, Encoding.UTF8, "application/json");
        var res = await _http.SendAsync(httpReq);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("text").GetString() ?? string.Empty;
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
