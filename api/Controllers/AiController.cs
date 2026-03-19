using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("")]
public class AiController : ControllerBase
{
    private readonly IDocumentRepository _repository;
    private readonly AzureFoundryClient _foundry;

    public AiController(IDocumentRepository repository, AzureFoundryClient foundry)
    {
        _repository = repository;
        _foundry = foundry;
    }

    [HttpPost("embeddings")]
    public async Task<IActionResult> CreateEmbedding([FromBody] EmbeddingRequest? body)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.Text))
        {
            return BadRequest(new { error = "Request body must include a non-empty 'text' field." });
        }

        var vector = await _foundry.GetEmbeddingAsync(body.Text);
        var metadataJson = body.Metadata.HasValue ? body.Metadata.Value.GetRawText() : "{}";
        var id = await _repository.InsertAsync(body.Text, metadataJson, vector);

        return Ok(new { id });
    }

    [HttpPost("query")]
    public async Task<IActionResult> Query([FromBody] QueryRequest? body)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.Question))
        {
            return BadRequest(new { error = "Request body must include a non-empty 'question' field." });
        }

        var qVec = await _foundry.GetEmbeddingAsync(body.Question);
        var topK = body.TopK ?? 3;
        var selected = await _repository.SearchNearestAsync(qVec, topK);

        var context = string.Join("\n---\n", selected.Select(s => s.Text));
        var answer = await _foundry.GetCompletionAsync(body.Question, context);

        return Ok(new { answer, sources = selected.Select(s => new { id = s.Id, score = s.Score }) });
    }
}
