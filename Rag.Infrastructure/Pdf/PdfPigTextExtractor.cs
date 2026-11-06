
using Rag.Application.Interfaces;
using UglyToad.PdfPig;

namespace Rag.Infrastructure.Pdf;

public class PdfPigTextExtractor
{
    public Task<IReadOnlyList<ExtractedPage>> ExtractPagesAsync(
        Stream pdfStream,
        CancellationToken cancellationToken = default)
    {
        if (pdfStream == null || !pdfStream.CanRead)
        {
            throw new ArgumentException(
                "A readable PDF stream is required.",
                nameof(pdfStream));
        }

        var extractedPages = new List<ExtractedPage>();

        using var document = PdfDocument.Open(pdfStream);

        foreach (var page in document.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();

            extractedPages.Add(new ExtractedPage
            {
                PageNumber = page.Number,
                Text = page.Text ?? string.Empty
            });
        }

        return Task.FromResult<IReadOnlyList<ExtractedPage>>(
            extractedPages);
    }
}
