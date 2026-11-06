
using Rag.Application.Interfaces;
using Rag.Domain.Entities;

namespace Rag.Application.Services;

/// <summary>
/// Coordinates PDF ingestion into the RAG application.
/// </summary>
public class PdfIngestionService
{
    private readonly IPdfTextExtractor _pdfTextExtractor;
    private readonly TextChunkingService _textChunkingService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStore _vectorStore;
    private readonly IDocumentRepository _documentRepository;

    /// <summary>
    /// Initializes the PDF ingestion service with its dependencies.
    /// </summary>
    public PdfIngestionService(
        IPdfTextExtractor pdfTextExtractor,
        TextChunkingService textChunkingService,
        IEmbeddingService embeddingService,
        IVectorStore vectorStore,
        IDocumentRepository documentRepository)
    {
        _pdfTextExtractor = pdfTextExtractor;
        _textChunkingService = textChunkingService;
        _embeddingService = embeddingService;
        _vectorStore = vectorStore;
        _documentRepository = documentRepository;
    }

    /// <summary>
    /// Extracts PDF text, creates chunks, generates embeddings,
    /// stores chunks in Qdrant, and tracks document metadata in SQLite.
    /// </summary>
    public async Task<Document> IngestPdfAsync(
        Stream pdfStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        // Generate one unique ID for this document.
        Guid documentId = Guid.NewGuid();

        // Register the document in SQLite before processing begins.
        var document = new Document
        {
            Id = documentId,
            FileName = fileName,
            UploadedAt = DateTime.UtcNow,
            TotalChunks = 0,
            Status = "Processing"
        };

        await _documentRepository.AddAsync(
            document,
            cancellationToken);

        try
        {
            // Extract text from every PDF page.
            var pages = await _pdfTextExtractor.ExtractPagesAsync(
    pdfStream,
    cancellationToken);

            // Keep track of successfully stored chunks.
            int totalChunks = 0;

            foreach (var page in pages)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Skip pages containing no readable text.
                if (string.IsNullOrWhiteSpace(page.Text))
                    continue;

                // Divide page text into smaller chunks.
                var chunks = _textChunkingService.CreateChunks(
                    page.Text,
                    documentId,
                    page.PageNumber);

                foreach (DocumentChunk chunk in chunks)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Generate an embedding for the chunk.
                    float[] embedding =
                        await _embeddingService.GenerateEmbeddingAsync(
                            chunk.Content,
                            cancellationToken);

                    // Store the chunk and its embedding in Qdrant.
                    await _vectorStore.UpsertAsync(
                        chunk,
                        embedding,
                        cancellationToken);

                    totalChunks++;
                }
            }

            // Validate that the PDF generated at least one chunk.
            if (totalChunks == 0)
            {
                throw new InvalidOperationException(
                    "No readable text was extracted from the PDF. " +
                    "The PDF may be scanned or contain unsupported text.");
            }

            // Update SQLite only when chunks have been successfully stored.
            document.TotalChunks = totalChunks;
            document.Status = "Completed";

            await _documentRepository.UpdateAsync(
                document,
                cancellationToken);

            return document;
        }
        catch
        {
            // Record the failure even if the original request was cancelled.
            document.Status = "Failed";
            document.TotalChunks = 0;

            await _documentRepository.UpdateAsync(
                document,
                CancellationToken.None);

            // Preserve the original exception.
            throw;
        }
    }
}
