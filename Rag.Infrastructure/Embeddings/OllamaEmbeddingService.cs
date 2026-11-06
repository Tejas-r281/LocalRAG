
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Rag.Application.Interfaces;

namespace Rag.Infrastructure.Embeddings;

/// <summary>
/// Generates text embeddings by communicating with the local Ollama server.
/// </summary>
public class OllamaEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes the embedding service with an HTTP client.
    /// </summary>
    /// <param name="httpClient">HTTP client configured for Ollama.</param>
    public OllamaEmbeddingService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Converts the supplied text into a numerical embedding vector.
    /// </summary>
    /// <param name="text">Text to convert into an embedding.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A 768-dimensional embedding vector.</returns>
    public async Task<float[]> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        // Validate the input text.
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException(
                "Text cannot be empty.",
                nameof(text));
        }

        // Prepare the request for Ollama's embedding endpoint.
        var request = new OllamaEmbeddingRequest
        {
            Model = "nomic-embed-text",
            Input = text
        };

        // Send the request to Ollama.
        var response = await _httpClient.PostAsJsonAsync(
            "api/embed",
            request,
            cancellationToken);

        // Throw an exception if Ollama returns an unsuccessful status code.
        response.EnsureSuccessStatusCode();

        // Deserialize Ollama's response.
        var result = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>(
            cancellationToken: cancellationToken);

        // Validate the returned embedding.
        if (result?.Embeddings is null ||
            result.Embeddings.Length == 0 ||
            result.Embeddings[0] is null)
        {
            throw new InvalidOperationException(
                "Ollama returned an empty embedding.");
        }

        // Return the first generated embedding vector.
        return result.Embeddings[0];
    }
}

/// <summary>
/// Represents the request sent to Ollama's embedding endpoint.
/// </summary>
public class OllamaEmbeddingRequest
{
    /// <summary>
    /// Gets or sets the embedding model name.
    /// </summary>
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the input text for embedding generation.
    /// </summary>
    [JsonPropertyName("input")]
    public string Input { get; set; } = string.Empty;
}

/// <summary>
/// Represents the response received from Ollama.
/// </summary>
public class OllamaEmbeddingResponse
{
    /// <summary>
    /// Gets or sets the generated embedding vectors.
    /// </summary>
    [JsonPropertyName("embeddings")]
    public float[][] Embeddings { get; set; } = [];
}
