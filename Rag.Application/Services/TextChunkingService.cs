
using System.Text.RegularExpressions;
using Rag.Domain.Entities;

namespace Rag.Application.Services;

/// <summary>
/// Splits extracted document text into smaller overlapping chunks
/// to prepare the content for embedding generation and vector storage.
/// </summary>
public class TextChunkingService
{
    /// <summary>
    /// Splits the supplied text into sentence-based chunks.
    /// </summary>
    /// <param name="text">Extracted text from a document page.</param>
    /// <param name="documentId">Unique identifier of the original document.</param>
    /// <param name="pageNumber">Page number from which the text was extracted.</param>
    /// <param name="sentencesPerChunk">Maximum sentences in each chunk.</param>
    /// <param name="overlapSentences">Number of sentences shared between adjacent chunks.</param>
    /// <returns>A list of document chunks.</returns>
    public List<DocumentChunk> CreateChunks(
        string text,
        Guid documentId,
        int pageNumber,
        int sentencesPerChunk = 2,
        int overlapSentences = 1)
    {
        // Return an empty list if the input text is empty.
        if (string.IsNullOrWhiteSpace(text))
            return [];

        // Validate the chunk size.
        if (sentencesPerChunk <= 0)
            throw new ArgumentException(
                "Sentences per chunk must be positive.");

        // Overlap must be smaller than the chunk size.
        if (overlapSentences < 0 ||
            overlapSentences >= sentencesPerChunk)
        {
            throw new ArgumentException(
                "Overlap must be non-negative and smaller than chunk size.");
        }

        // Split the input text into individual sentences.
        var sentences = Regex.Split(
                text.Trim(),
                @"(?<=[.!?])\s+")
            .Where(sentence => !string.IsNullOrWhiteSpace(sentence))
            .Select(sentence => sentence.Trim())
            .ToList();

        // Create a collection to store the generated chunks.
        var chunks = new List<DocumentChunk>();

        // Calculate how many sentences to move forward for each chunk.
        int step = sentencesPerChunk - overlapSentences;

        // Generate chunks until all sentences are processed.
        for (int start = 0; start < sentences.Count; start += step)
        {
            // Select the sentences belonging to the current chunk.
            var chunkSentences = sentences
                .Skip(start)
                .Take(sentencesPerChunk)
                .ToList();

            // Stop if no sentences are available.
            if (chunkSentences.Count == 0)
                break;

            // Create and populate the document chunk.
            chunks.Add(new DocumentChunk
            {
                DocumentId = documentId,
                ChunkIndex = chunks.Count,
                PageNumber = pageNumber,
                StartSentenceIndex = start,
                EndSentenceIndex = start + chunkSentences.Count - 1,
                Content = string.Join(" ", chunkSentences)
            });

            // Stop when the final sentence has been included.
            if (start + sentencesPerChunk >= sentences.Count)
                break;
        }

        return chunks;
    }
}
