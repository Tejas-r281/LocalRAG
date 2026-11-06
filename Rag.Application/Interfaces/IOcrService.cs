
namespace Rag.Application.Interfaces;

public interface IOcrService
{
    Task<string> RecognizeTextAsync(
        byte[] imageBytes,
        CancellationToken cancellationToken = default);
}
