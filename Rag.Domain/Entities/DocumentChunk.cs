namespace Rag.Domain.Entities;

public class DocumentChunk
{
    /// <summary>
    /// Gets or sets the unique identifier of this document chunk.
    /// A new GUID is generated automatically when the chunk is created.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the identifier of the original document
    /// from which this chunk was extracted.
    /// </summary>
    public Guid DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the actual text content stored in this chunk.
    /// This text will be converted into an embedding vector.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sequential position of this chunk
    /// within the original document.
    /// </summary>
    public int ChunkIndex { get; set; }

    /// <summary>
    /// Gets or sets the PDF page number from which this chunk originated.
    /// </summary>
    public int PageNumber { get; set; }

    /// <summary>
    /// Gets or sets the index of the first sentence included in this chunk.
    /// </summary>
    public int StartSentenceIndex { get; set; }

    /// <summary>
    /// Gets or sets the index of the last sentence included in this chunk.
    /// </summary>
    public int EndSentenceIndex { get; set; }
}