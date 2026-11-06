namespace Rag.Application.Interfaces;

/// <summary>
/// Defines the contract for generating vector embeddings from text.
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Converts the supplied text into a numerical embedding vector.
    /// </summary>
    /// <param name="text">Text to convert into an embedding.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A numerical vector representing the text.</returns>
    Task<float[]> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default);
}