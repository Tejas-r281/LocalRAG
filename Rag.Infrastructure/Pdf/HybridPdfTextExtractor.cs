
using PDFtoImage;
using Rag.Application.Interfaces;

namespace Rag.Infrastructure.Pdf;

public class HybridPdfTextExtractor : IPdfTextExtractor
{
    private readonly PdfPigTextExtractor _pdfPigExtractor;
    private readonly IOcrService _ocrService;

    public HybridPdfTextExtractor(
        PdfPigTextExtractor pdfPigExtractor,
        IOcrService ocrService)
    {
        _pdfPigExtractor = pdfPigExtractor;
        _ocrService = ocrService;
    }

    public async Task<IReadOnlyList<ExtractedPage>> ExtractPagesAsync(
        Stream pdfStream,
        CancellationToken cancellationToken = default)
    {
        if (pdfStream == null || !pdfStream.CanRead)
        {
            throw new ArgumentException(
                "A readable PDF stream is required.",
                nameof(pdfStream));
        }

        // Keep the original PDF bytes available for both extractors.
        using var pdfMemoryStream = new MemoryStream();

        await pdfStream.CopyToAsync(
            pdfMemoryStream,
            cancellationToken);

        var pdfBytes = pdfMemoryStream.ToArray();

        // First attempt: extract native PDF text using PdfPig.
        using var textStream = new MemoryStream(pdfBytes);

        var pages = await _pdfPigExtractor.ExtractPagesAsync(
            textStream,
            cancellationToken);

        var finalPages = new List<ExtractedPage>();

        foreach (var page in pages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // If native text exists, don't run OCR.
            if (!string.IsNullOrWhiteSpace(page.Text))
            {
                finalPages.Add(page);
                continue;
            }

            // No native text: render this PDF page to PNG.
            var tempImagePath = Path.Combine(
                Path.GetTempPath(),
                $"{Guid.NewGuid():N}.png");

            try
            {
                using var renderStream = new MemoryStream(pdfBytes);

                Conversion.SavePng(
                    imageFilename: tempImagePath,
                    pdfStream: renderStream,
                    page: page.PageNumber - 1);

                // Read the rendered PNG.
                var imageBytes = await File.ReadAllBytesAsync(
                    tempImagePath,
                    cancellationToken);

                // Second attempt: recognise text using Tesseract.
                var ocrText = await _ocrService.RecognizeTextAsync(
                    imageBytes,
                    cancellationToken);

                finalPages.Add(new ExtractedPage
                {
                    PageNumber = page.PageNumber,
                    Text = ocrText
                });
            }
            finally
            {
                if (File.Exists(tempImagePath))
                    File.Delete(tempImagePath);
            }
        }

        return finalPages;
    }
}
