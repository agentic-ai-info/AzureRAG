using System.Text;
using System.Text.Json;

public class AzureFoundryClient
{
    private readonly string _apiKey;
    private readonly string _chatEndpoint;
    private readonly string _embeddingsEndpoint;
    private readonly HttpClient _http = new();

    public AzureFoundryClient()
    {
        _apiKey = Environment.GetEnvironmentVariable("AZURE_FOUNDRY_API_KEY")
            ?? throw new InvalidOperationException("AZURE_FOUNDRY_API_KEY is not set.");
        _chatEndpoint = Environment.GetEnvironmentVariable("AZURE_FOUNDRY_ENDPOINT")
            ?? throw new InvalidOperationException("AZURE_FOUNDRY_ENDPOINT is not set.");
        _embeddingsEndpoint = Environment.GetEnvironmentVariable("AZURE_FOUNDRY_EMBEDDINGS_ENDPOINT")
            ?? throw new InvalidOperationException("AZURE_FOUNDRY_EMBEDDINGS_ENDPOINT is not set.");
    }

    public async Task<float[]> GetEmbeddingAsync(string text)
    {
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
}
