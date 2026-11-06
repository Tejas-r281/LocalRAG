
using System.Text.RegularExpressions;
using Rag.Domain.Entities;

namespace Rag.Application.Services;

/// <summary>
/// Splits extracted document text into meaningful, overlapping chunks.
/// Uses paragraph-aware and sentence-aware splitting.
/// </summary>
public class TextChunkingService
{
    public List<DocumentChunk> CreateChunks(
        string text,
        Guid documentId,
        int pageNumber,
        int targetChunkCharacters = 1200,
        int overlapCharacters = 200)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        if (targetChunkCharacters <= 0)
            throw new ArgumentException(
                "Target chunk size must be positive.");

        if (overlapCharacters < 0 ||
            overlapCharacters >= targetChunkCharacters)
        {
            throw new ArgumentException(
                "Overlap must be non-negative and smaller than chunk size.");
        }

        // Normalize line endings and whitespace.
        text = text
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Trim();

        // Split text into paragraphs.
        var paragraphs = Regex.Split(text, @"\n\s*\n")
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => Regex.Replace(p.Trim(), @"\s+", " "))
            .ToList();

        // Convert paragraphs into sentence units.
        var units = new List<string>();

        foreach (var paragraph in paragraphs)
        {
            var sentences = Regex.Split(
                paragraph,
                @"(?<=[.!?])\s+")
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim());

            foreach (var sentence in sentences)
            {
                // Keep normal sentences together.
                if (sentence.Length <= targetChunkCharacters)
                {
                    units.Add(sentence);
                    continue;
                }

                // Split unusually large sentences at word boundaries.
                var words = sentence.Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries);

                var part = new System.Text.StringBuilder();

                foreach (var word in words)
                {
                    if (part.Length > 0 &&
                        part.Length + word.Length + 1 > targetChunkCharacters)
                    {
                        units.Add(part.ToString());
                        part.Clear();
                    }

                    if (part.Length > 0)
                        part.Append(' ');

                    part.Append(word);
                }

                if (part.Length > 0)
                    units.Add(part.ToString());
            }
        }

        var chunks = new List<DocumentChunk>();

        int start = 0;

        while (start < units.Count)
        {
            var currentUnits = new List<string>();
            int currentLength = 0;
            int end = start;

            // Add units until the target chunk size is reached.
            while (end < units.Count)
            {
                int additionalLength =
                    units[end].Length +
                    (currentUnits.Count > 0 ? 1 : 0);

                if (currentLength + additionalLength >
                    targetChunkCharacters &&
                    currentUnits.Count > 0)
                {
                    break;
                }

                currentUnits.Add(units[end]);
                currentLength += additionalLength;
                end++;
            }

            chunks.Add(new DocumentChunk
            {
                DocumentId = documentId,
                ChunkIndex = chunks.Count,
                PageNumber = pageNumber,

                // These fields now represent unit indexes.
                // See the note below about their legacy names.
                StartSentenceIndex = start,
                EndSentenceIndex = end - 1,

                Content = string.Join(" ", currentUnits)
            });

            if (end >= units.Count)
                break;

            // Move backwards to include overlapping content.
            int nextStart = end;
            int overlapLength = 0;

            while (nextStart > start)
            {
                int previousLength = units[nextStart - 1].Length;

                if (overlapLength + previousLength >
                    overlapCharacters)
                {
                    break;
                }

                overlapLength += previousLength;
                nextStart--;
            }

            // Guarantee forward progress.
            if (nextStart <= start)
                nextStart = end;

            start = nextStart;
        }

        return chunks;
    }
}
