
namespace Rag.Domain.Entities;

/// <summary>
/// Represents an uploaded document in the RAG application.
/// </summary>
public class Document
{
    /// <summary>
    /// Gets or sets the unique identifier of the document.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the original uploaded filename.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the date and time when the document was uploaded.
    /// </summary>
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the total number of chunks created from the document.
    /// </summary>
    public int TotalChunks { get; set; }

    /// <summary>
    /// Gets or sets the document processing status.
    /// </summary>
    public string Status { get; set; } = "Uploaded";
}
