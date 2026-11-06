namespace Rag.Application.Models;

/// <summary>
/// Represents a document chunk retrieved from the vector database.
/// </summary>
public class RetrievedChunk
{
    /// <summary>
    /// Gets or sets the unique identifier of the retrieved chunk.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the original document identifier.
    /// </summary>
    public Guid DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the text content of the retrieved chunk.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the chunk's position in the original document.
    /// </summary>
    public int ChunkIndex { get; set; }

    /// <summary>
    /// Gets or sets the original PDF page number.
    /// </summary>
    public int PageNumber { get; set; }

    /// <summary>
    /// Gets or sets the similarity score returned by Qdrant.
    /// </summary>
    public float Score { get; set; }
}