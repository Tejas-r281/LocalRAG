using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Rag.Application.Interfaces;

namespace Rag.Infrastructure.Chat;

/// <summary>
/// Generates answers using a local Ollama chat model.
/// </summary>
public class OllamaChatService : IChatService
{
    private readonly HttpClient _httpClient;

    public OllamaChatService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Sends a prompt to Qwen and returns its generated answer.
    /// </summary>
    public async Task<string> GenerateAnswerAsync(
        string prompt,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Prompt cannot be empty.");

        var request = new OllamaChatRequest
        {
            Model = "qwen2.5:7b",
            Messages =
            [
                new OllamaChatMessage
                {
                    Role = "user",
                    Content = prompt
                }
            ],
            Stream = false
        };

        var response = await _httpClient.PostAsJsonAsync(
            "api/chat",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content
            .ReadFromJsonAsync<OllamaChatResponse>(
                cancellationToken: cancellationToken);

        return result?.Message?.Content
            ?? throw new InvalidOperationException(
                "Ollama returned an empty answer.");
    }
}

/// <summary>
/// Represents a request sent to Ollama's chat endpoint.
/// </summary>
public class OllamaChatRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<OllamaChatMessage> Messages { get; set; } = [];

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }
}

/// <summary>
/// Represents an individual chat message.
/// </summary>
public class OllamaChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// Represents the response received from Ollama.
/// </summary>
public class OllamaChatResponse
{
    [JsonPropertyName("message")]
    public OllamaChatMessage? Message { get; set; }
}