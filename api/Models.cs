using System.Text.Json;

public record EmbeddingRequest(string Text, JsonElement? Metadata);
public record QueryRequest(string Question, int? TopK);
public record DocumentRow(int id, string text, JsonElement? metadata, JsonElement? vector);

public class DatabaseOptions { public string ConnectionString { get; }
    public DatabaseOptions(string cs) => ConnectionString = cs;
}

public static class VectorUtils
{
    public static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length == 0 || b.Length == 0) return 0f;
        var len = Math.Min(a.Length, b.Length);
        double dot = 0, na = 0, nb = 0;
        for (int i = 0; i < len; i++) { dot += a[i] * b[i]; na += a[i] * a[i]; nb += b[i] * b[i]; }
        if (na == 0 || nb == 0) return 0f;
        return (float)(dot / (Math.Sqrt(na) * Math.Sqrt(nb)));
    }
}
