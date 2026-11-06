namespace Rag.Application.Interfaces;

/// <summary>
/// Defines the contract for generating answers using a language model.
/// </summary>
public interface IChatService
{
    /// <summary>
    /// Generates an answer using the supplied prompt.
    /// </summary>
    Task<string> GenerateAnswerAsync(
        string prompt,
        CancellationToken cancellationToken = default);
}