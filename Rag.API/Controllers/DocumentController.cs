
using Microsoft.AspNetCore.Mvc;
using Rag.Application.Interfaces;
using Rag.Application.Models;
using Rag.Domain.Entities;
using Rag.Application.Services;

namespace Rag.API.Controllers;

/// <summary>
/// Provides endpoints for document ingestion and retrieval.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DocumentController : ControllerBase
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStore _vectorStore;
    private readonly PdfIngestionService _ingestionService;
    private readonly TextChunkingService _textChunkingService;

    private readonly IDocumentRepository _documentRepository;

    /// <summary>
    /// Initializes the controller with required services.
    /// </summary>
    public DocumentController(
        IEmbeddingService embeddingService,
        IVectorStore vectorStore,
        TextChunkingService textChunkingService,
        PdfIngestionService ingestionService,
        IDocumentRepository documentRepository)
    {
        _embeddingService = embeddingService;
        _vectorStore = vectorStore;
        _textChunkingService = textChunkingService;
        _ingestionService = ingestionService;
        _documentRepository = documentRepository;
    }

    /// <summary>
    /// Generates embeddings and stores text chunks in Qdrant.
    /// </summary>
    [HttpPost("ingest-test")]
    public async Task<IActionResult> IngestTest(
        [FromBody] DocumentIngestionRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest("Text cannot be empty.");

        // Generate one ID for the original document.
        var documentId = Guid.NewGuid();

        // Split the original text into smaller overlapping chunks.
        var chunks = _textChunkingService.CreateChunks(
            request.Text,
            documentId,
            pageNumber: 1,
            sentencesPerChunk: 2,
            overlapSentences: 1);

        // Process every chunk independently.
        foreach (var chunk in chunks)
        {
            // Generate an embedding for this individual chunk.
            var embedding = await _embeddingService.GenerateEmbeddingAsync(
                chunk.Content,
                cancellationToken);

            // Store the chunk and its embedding in Qdrant.
            await _vectorStore.UpsertAsync(
                chunk,
                embedding,
                cancellationToken);
        }

        return Ok(new
        {
            Message = "Document successfully split and stored.",
            DocumentId = documentId,
            TotalChunks = chunks.Count,
            Chunks = chunks.Select(chunk => new
            {
                chunk.Id,
                chunk.ChunkIndex,
                chunk.Content
            })
        });
    }

    /// <summary>
    /// Uploads a PDF, extracts its text, generates embeddings,
    /// stores chunks in Qdrant, and registers metadata in SQLite.
    /// </summary>
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadPdf(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        // Check whether a file was supplied.
        if (file == null || file.Length == 0)
            return BadRequest("Please select a PDF file.");

        // Restrict uploads to PDF files.
        if (!Path.GetExtension(file.FileName)
            .Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Only PDF files are allowed.");
        }

        // Limit the upload size to 20 MB.
        const long maxFileSize = 20 * 1024 * 1024;

        if (file.Length > maxFileSize)
            return BadRequest("PDF file size cannot exceed 20 MB.");

        // Open the uploaded PDF as a stream.
        await using var stream = file.OpenReadStream();

        // Execute the complete PDF ingestion pipeline.
        Document document = await _ingestionService.IngestPdfAsync(
            stream,
            Path.GetFileName(file.FileName),
            cancellationToken);

        // Return document metadata to the API consumer.
        return Ok(new
        {
            Message = "PDF processed successfully.",
            DocumentId = document.Id,
            FileName = document.FileName,
            TotalChunks = document.TotalChunks,
            Status = document.Status
        });
    }

    /// <summary>
    /// Searches Qdrant for chunks relevant to the supplied question.
    /// </summary>
    [HttpPost("search-test")]
    public async Task<IActionResult> SearchTest(
        [FromBody] DocumentSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest("Question cannot be empty.");

        // Generate embedding for the user's question.
        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(
            request.Question,
            cancellationToken);

        // Search Qdrant for similar chunks.
        var results = await _vectorStore.SearchAsync(
            queryEmbedding,
            request.Limit,
            cancellationToken);

        return Ok(new
        {
            Question = request.Question,
            ResultsCount = results.Count,
            Results = results
        });
    }



    /// <summary>
    /// Retrieves all uploaded documents from SQLite.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllDocuments(
        CancellationToken cancellationToken)
    {
        var documents = await _documentRepository.GetAllAsync(
            cancellationToken);

        return Ok(documents);
    }

    /// <summary>
    /// Retrieves a document using its unique ID.
    /// </summary>
    [HttpGet("{documentId:guid}")]
    public async Task<IActionResult> GetDocumentById(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var document = await _documentRepository.GetByIdAsync(
            documentId,
            cancellationToken);

        if (document == null)
            return NotFound($"Document with ID {documentId} was not found.");

        return Ok(document);
    }



    /// <summary>
    /// Deletes a document and all its associated vectors.
    /// </summary>
    [HttpDelete("{documentId:guid}")]
    public async Task<IActionResult> DeleteDocument(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        // Step 1: Check whether the document exists in SQLite.
        var document = await _documentRepository.GetByIdAsync(
            documentId,
            cancellationToken);

        if (document == null)
        {
            return NotFound(new
            {
                Message = "Document not found.",
                DocumentId = documentId
            });
        }

        // Step 2: Delete all associated chunks from Qdrant.
        // If this fails, SQLite metadata is retained.
        await _vectorStore.DeleteByDocumentIdAsync(
            documentId,
            cancellationToken);

        // Step 3: Delete document metadata from SQLite.
        bool deleted = await _documentRepository.DeleteAsync(
            documentId,
            cancellationToken);

        if (!deleted)
        {
            return StatusCode(500, new
            {
                Message = "Vectors were deleted, but document metadata could not be deleted.",
                DocumentId = documentId
            });
        }

        return Ok(new
        {
            Message = "Document and its associated vectors deleted successfully.",
            DocumentId = documentId,
            FileName = document.FileName
        });
    }


}

/// <summary>
/// Represents a document ingestion request.
/// </summary>
public class DocumentIngestionRequest
{
    /// <summary>
    /// Gets or sets the document text.
    /// </summary>
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// Represents a document search request.
/// </summary>
public class DocumentSearchRequest
{
    /// <summary>
    /// Gets or sets the user's search question.
    /// </summary>
    public string Question { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the maximum number of results to retrieve.
    /// </summary>
    public int Limit { get; set; } = 3;
}
