
using Microsoft.AspNetCore.Mvc;
using Rag.Application.Interfaces;

namespace Rag.API.Controllers;

[ApiController]
[Route("api/ocr")]
public class OcrController : ControllerBase
{
    private readonly IOcrService _ocrService;

    public OcrController(IOcrService ocrService)
    {
        _ocrService = ocrService;
    }

    [HttpPost("test")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> TestOcr(
        IFormFile image,
        CancellationToken cancellationToken)
    {
        if (image == null || image.Length == 0)
            return BadRequest("Please upload an image.");

        var allowedTypes = new[]
        {
            "image/png",
            "image/jpeg"
        };

        if (!allowedTypes.Contains(image.ContentType))
            return BadRequest("Only PNG and JPEG images are supported.");

        await using var stream = image.OpenReadStream();
        using var memoryStream = new MemoryStream();

        await stream.CopyToAsync(memoryStream, cancellationToken);

        var text = await _ocrService.RecognizeTextAsync(
            memoryStream.ToArray(),
            cancellationToken);

        return Ok(new
        {
            FileName = image.FileName,
            ExtractedText = text,
            CharacterCount = text.Length
        });
    }
}
