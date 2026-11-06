
using Rag.Domain.Entities;

namespace Rag.Application.Interfaces;

/// <summary>
/// Defines operations for managing document metadata.
/// </summary>
public interface IDocumentRepository
{
    /// <summary>
    /// Adds a new document to the database.
    /// </summary>
    /// <param name="document">Document to be added.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AddAsync(
        Document document,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a document using its unique identifier.
    /// </summary>
    /// <param name="documentId">Unique document identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The document if found; otherwise null.</returns>
    Task<Document?> GetByIdAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all uploaded documents.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of documents.</returns>
    Task<List<Document>> GetAllAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a document using its unique identifier.
    /// </summary>
    /// <param name="documentId">Unique document identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the document was found and deleted.</returns>
    Task<bool> DeleteAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);


    /// <summary>
    /// Updates an existing document's metadata.
    /// </summary>
    Task UpdateAsync(
        Document document,
        CancellationToken cancellationToken = default);
}
