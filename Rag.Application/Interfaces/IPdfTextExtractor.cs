
namespace Rag.Application.Interfaces;

/// <summary>
/// Defines the contract for extracting text from PDF documents.
/// </summary>
public interface IPdfTextExtractor
{
    Task<IReadOnlyList<ExtractedPage>> ExtractPagesAsync(
        Stream pdfStream,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents text extracted from a single PDF page.
/// </summary>
public class ExtractedPage
{
    public int PageNumber { get; set; }

    public string Text { get; set; } = string.Empty;
}
