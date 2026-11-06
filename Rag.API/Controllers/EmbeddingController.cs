
using Microsoft.AspNetCore.Mvc;
using Rag.Application.Interfaces;

namespace Rag.API.Controllers;

/// <summary>
/// Provides endpoints for testing text embedding generation.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class EmbeddingController : ControllerBase
{
    private readonly IEmbeddingService _embeddingService;

    /// <summary>
    /// Initializes the controller with the embedding service.
    /// </summary>
    public EmbeddingController(IEmbeddingService embeddingService)
    {
        _embeddingService = embeddingService;
    }

    /// <summary>
    /// Generates an embedding for the supplied text.
    /// </summary>
    [HttpPost("test")]
    public async Task<IActionResult> TestEmbedding(
        [FromBody] EmbeddingTestRequest request,
        CancellationToken cancellationToken)
    {
        // Validate the incoming text.
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return BadRequest("Text cannot be empty.");
        }

        // Generate the embedding using Ollama.
        var embedding = await _embeddingService.GenerateEmbeddingAsync(
            request.Text,
            cancellationToken);

        // Return the vector dimensions and sample values.
        return Ok(new
        {
            Message = "Embedding generated successfully.",
            Dimensions = embedding.Length,
            SampleValues = embedding.Take(5)
        });
    }
}

/// <summary>
/// Represents the request body for embedding generation.
/// </summary>
public class EmbeddingTestRequest
{
    /// <summary>
    /// Gets or sets the text that needs to be converted into an embedding.
    /// </summary>
    public string Text { get; set; } = string.Empty;
}
