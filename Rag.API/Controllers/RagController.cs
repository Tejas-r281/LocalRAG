
using Microsoft.AspNetCore.Mvc;
using Rag.Application.Services;

namespace Rag.API.Controllers;

/// <summary>
/// Provides endpoints for Retrieval Augmented Generation.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RagController : ControllerBase
{
    private readonly RagQuestionAnsweringService _ragService;

    /// <summary>
    /// Initializes the RAG controller.
    /// </summary>
    public RagController(RagQuestionAnsweringService ragService)
    {
        _ragService = ragService;
    }

    /// <summary>
    /// Answers a question using information retrieved from Qdrant.
    /// Optionally searches within a specific document.
    /// </summary>
    [HttpPost("ask")]
    public async Task<IActionResult> Ask(
        [FromBody] RagQuestionRequest request,
        CancellationToken cancellationToken)
    {
        // Validate the question.
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return BadRequest("Question cannot be empty.");
        }

        // Validate the optional document ID.
        if (request.DocumentId.HasValue &&
            request.DocumentId.Value == Guid.Empty)
        {
            return BadRequest("Document ID must be a valid GUID.");
        }

        // Execute the RAG question-answering workflow.
        var result = await _ragService.AskAsync(
            request.Question,
            cancellationToken,
            request.DocumentId);

        return Ok(result);
    }
}

/// <summary>
/// Represents a question submitted to the RAG application.
/// </summary>
public class RagQuestionRequest
{
    /// <summary>
    /// Gets or sets the user's question.
    /// </summary>
    public string Question { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional document ID.
    /// When null, all uploaded documents are searched.
    /// </summary>
    public Guid? DocumentId { get; set; }
}
