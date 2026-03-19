public interface IDocumentRepository
{
    Task EnsureDatabaseReadyAsync();
    Task<int> InsertAsync(string text, string metadataJson, float[] vector);
    Task<IReadOnlyList<DocumentSearchResult>> SearchNearestAsync(float[] queryVector, int topK);
}
