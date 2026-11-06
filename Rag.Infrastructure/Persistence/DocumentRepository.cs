
using Microsoft.EntityFrameworkCore;
using Rag.Application.Interfaces;
using Rag.Domain.Entities;

namespace Rag.Infrastructure.Persistence;

/// <summary>
/// Implements document metadata operations using Entity Framework Core
/// and SQLite.
/// </summary>
public class DocumentRepository : IDocumentRepository
{
    private readonly RagDbContext _dbContext;

    /// <summary>
    /// Initializes the repository with the database context.
    /// </summary>
    public DocumentRepository(RagDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Adds a new document to the database.
    /// </summary>
    public async Task AddAsync(
        Document document,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.Documents.AddAsync(
            document,
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Retrieves a document using its unique identifier.
    /// </summary>
    public async Task<Document?> GetByIdAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(
                document => document.Id == documentId,
                cancellationToken);
    }

    /// <summary>
    /// Retrieves all uploaded documents.
    /// </summary>
    public async Task<List<Document>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Documents
            .AsNoTracking()
            .OrderByDescending(document => document.UploadedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Deletes a document using its unique identifier.
    /// </summary>
    public async Task<bool> DeleteAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var document = await _dbContext.Documents
            .FirstOrDefaultAsync(
                item => item.Id == documentId,
                cancellationToken);

        if (document == null)
        {
            return false;
        }

        _dbContext.Documents.Remove(document);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }


    /// <summary>
    /// Updates an existing document's metadata in SQLite.
    /// </summary>
    public async Task UpdateAsync(
        Document document,
        CancellationToken cancellationToken = default)
    {
        _dbContext.Documents.Update(document);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
