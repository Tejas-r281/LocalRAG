
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Rag.Application.Interfaces;

namespace Rag.Infrastructure.OCR;

public class TesseractOcrService : IOcrService
{
    private readonly string _executablePath;
    private readonly string _language;

    public TesseractOcrService(IConfiguration configuration)
    {
        _executablePath =
            configuration["Tesseract:ExecutablePath"]
            ?? throw new InvalidOperationException(
                "Tesseract executable path is not configured.");

        _language =
            configuration["Tesseract:Language"] ?? "eng+hin";
    }

    public async Task<string> RecognizeTextAsync(
        byte[] imageBytes,
        CancellationToken cancellationToken = default)
    {
        if (imageBytes == null || imageBytes.Length == 0)
            throw new ArgumentException("Image cannot be empty.");

        if (!File.Exists(_executablePath))
            throw new FileNotFoundException(
                "Tesseract executable was not found.",
                _executablePath);

        var tempImagePath = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid():N}.png");

        try
        {
            await File.WriteAllBytesAsync(
                tempImagePath,
                imageBytes,
                cancellationToken);

            var startInfo = new ProcessStartInfo
            {
                FileName = _executablePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            startInfo.ArgumentList.Add(tempImagePath);
            startInfo.ArgumentList.Add("stdout");
            startInfo.ArgumentList.Add("-l");
            startInfo.ArgumentList.Add(_language);
            startInfo.ArgumentList.Add("--psm");
            startInfo.ArgumentList.Add("3");

            using var process = new Process
            {
                StartInfo = startInfo
            };

            process.Start();

            var outputTask =
                process.StandardOutput.ReadToEndAsync();

            var errorTask =
                process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync(cancellationToken);

            var recognizedText = await outputTask;
            var error = await errorTask;

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Tesseract OCR failed: {error}");
            }

            return recognizedText.Trim();
        }
        finally
        {
            if (File.Exists(tempImagePath))
                File.Delete(tempImagePath);
        }
    }
}
