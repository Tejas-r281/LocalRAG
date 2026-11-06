using Rag.Application.Models;
using Rag.Domain.Entities;

namespace Rag.Application.Interfaces;

/// <summary>
/// Defines operations for storing and searching document vectors.
/// </summary>
public interface IVectorStore
{
    /// <summary>
    /// Stores a document chunk and its embedding vector.
    /// </summary>
    /// <param name="chunk">Document chunk with its metadata.</param>
    /// <param name="embedding">Numerical embedding vector.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpsertAsync(
        DocumentChunk chunk,
        float[] embedding,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for document chunks similar to the supplied vector.
    /// Optionally restricts the search to a specific document.
    /// </summary>
    /// <param name="queryEmbedding">Embedding of the user's question.</param>
    /// <param name="limit">Maximum number of results.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="documentId">Optional document ID filter.</param>
    /// <returns>Relevant document chunks with similarity scores.</returns>
    Task<List<RetrievedChunk>> SearchAsync(
        float[] queryEmbedding,
        int limit = 3,
        CancellationToken cancellationToken = default,
        Guid? documentId = null);


    /// <summary>
    /// Deletes all vector points belonging to a document.
    /// </summary>
    Task DeleteByDocumentIdAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);
}