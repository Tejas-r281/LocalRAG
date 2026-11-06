
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Rag.Application.Interfaces;
using Rag.Application.Models;
using Rag.Domain.Entities;

namespace Rag.Infrastructure.VectorStore;

/// <summary>
/// Implements vector storage and similarity search using Qdrant REST API.
/// </summary>
public class QdrantVectorStore : IVectorStore
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Name of the Qdrant collection used by this application.
    /// </summary>
    private const string CollectionName = "rag_documents";

    /// <summary>
    /// Initializes the Qdrant vector store with an HTTP client.
    /// </summary>
    public QdrantVectorStore(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Stores a document chunk and its embedding in Qdrant.
    /// </summary>
    /// <param name="chunk">Document chunk with its metadata.</param>
    /// <param name="embedding">Numerical embedding vector.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task UpsertAsync(
        DocumentChunk chunk,
        float[] embedding,
        CancellationToken cancellationToken = default)
    {
        // Validate the embedding dimensions.
        if (embedding.Length != 768)
        {
            throw new ArgumentException(
                "Embedding must contain 768 dimensions.",
                nameof(embedding));
        }

        // Create the Qdrant point with its vector and metadata.
        var point = new QdrantPoint
        {
            Id = chunk.Id,
            Vector = embedding,
            Payload = new Dictionary<string, object>
            {
                ["documentId"] = chunk.DocumentId.ToString(),
                ["content"] = chunk.Content,
                ["chunkIndex"] = chunk.ChunkIndex,
                ["pageNumber"] = chunk.PageNumber,
                ["startSentenceIndex"] = chunk.StartSentenceIndex,
                ["endSentenceIndex"] = chunk.EndSentenceIndex
            }
        };

        // Wrap the point inside the Qdrant upsert request.
        var request = new QdrantUpsertRequest
        {
            Points = [point]
        };

        // Send the point to Qdrant.
        var response = await _httpClient.PutAsJsonAsync(
            $"collections/{CollectionName}/points?wait=true",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Searches Qdrant for chunks similar to the supplied embedding.
    /// Optionally restricts the search to a specific document.
    /// </summary>
    /// <param name="queryEmbedding">Embedding of the user's question.</param>
    /// <param name="limit">Maximum number of results.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="documentId">Optional document ID filter.</param>
    /// <returns>Relevant document chunks with similarity scores.</returns>
    public async Task<List<RetrievedChunk>> SearchAsync(
        float[] queryEmbedding,
        int limit = 3,
        CancellationToken cancellationToken = default,
        Guid? documentId = null)
    {
        // Validate the query embedding dimensions.
        if (queryEmbedding == null || queryEmbedding.Length != 768)
        {
            throw new ArgumentException(
                "Query embedding must contain 768 dimensions.",
                nameof(queryEmbedding));
        }

        // Validate the search result limit.
        if (limit <= 0)
        {
            throw new ArgumentException(
                "Search limit must be positive.",
                nameof(limit));
        }

        // Build the Qdrant search request.
        var request = new QdrantSearchRequest
        {
            Query = queryEmbedding,
            Limit = limit,
            WithPayload = true,

            // Apply document filtering only when a document ID is supplied.
            Filter = documentId.HasValue
                ? new QdrantFilter
                {
                    Must =
                    [
                        new QdrantCondition
                        {
                            Key = "documentId",
                            Match = new QdrantMatch
                            {
                                Value = documentId.Value.ToString()
                            }
                        }
                    ]
                }
                : null
        };

        // Execute the vector similarity search.
        var response = await _httpClient.PostAsJsonAsync(
            $"collections/{CollectionName}/points/query",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        // Deserialize the Qdrant response.
        var result = await response.Content
            .ReadFromJsonAsync<QdrantSearchResponse>(
                cancellationToken: cancellationToken);

        // Convert Qdrant points into application models.
        return result?.Result?.Points?
            .Select(point => new RetrievedChunk
            {
                // Convert the Qdrant point ID into a Guid.
                Id = Guid.TryParse(point.Id, out var id)
                    ? id
                    : Guid.Empty,

                // Read the document ID from the payload.
                DocumentId = Guid.TryParse(
                    GetPayloadValue(point.Payload, "documentId"),
                    out var retrievedDocumentId)
                    ? retrievedDocumentId
                    : Guid.Empty,

                // Read the original chunk content.
                Content = GetPayloadValue(
                    point.Payload, "content"),

                // Read the chunk index.
                ChunkIndex = GetIntPayloadValue(
                    point.Payload, "chunkIndex"),

                // Read the original PDF page number.
                PageNumber = GetIntPayloadValue(
                    point.Payload, "pageNumber"),

                // Read the similarity score.
                Score = point.Score
            })
            .ToList() ?? [];
    }



    /// <summary>
    /// Deletes all vector points belonging to a specific document from Qdrant.
    /// Uses the documentId stored in each point's payload.
    /// </summary>
    /// <param name="documentId">Unique ID of the document.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task DeleteByDocumentIdAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        // Validate the document ID.
        if (documentId == Guid.Empty)
        {
            throw new ArgumentException(
                "Document ID cannot be empty.",
                nameof(documentId));
        }

        // Build a filter to match only this document's chunks.
        var request = new QdrantDeleteRequest
        {
            Filter = new QdrantFilter
            {
                Must =
                [
                    new QdrantCondition
                    {
                        Key = "documentId",
                        Match = new QdrantMatch
                        {
                            Value = documentId.ToString()
                        }
                    }
                ]
            }
        };

        // Ask Qdrant to delete all matching points.
        var response = await _httpClient.PostAsJsonAsync(
            $"collections/{CollectionName}/points/delete?wait=true",
            request,
            cancellationToken);

        // Throw an exception if Qdrant reports a failure.
        response.EnsureSuccessStatusCode();
    }


    /// <summary>
    /// Gets a string value from the Qdrant payload.
    /// </summary>
    private static string GetPayloadValue(
        Dictionary<string, JsonElement>? payload,
        string key)
    {
        if (payload is null ||
            !payload.TryGetValue(key, out var value))
        {
            return string.Empty;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : value.ToString();
    }

    /// <summary>
    /// Gets an integer value from the Qdrant payload.
    /// </summary>
    private static int GetIntPayloadValue(
        Dictionary<string, JsonElement>? payload,
        string key)
    {
        if (payload is null ||
            !payload.TryGetValue(key, out var value))
        {
            return 0;
        }

        return value.TryGetInt32(out var number)
            ? number
            : 0;
    }
}

/// <summary>
/// Represents a point stored in Qdrant.
/// </summary>
public class QdrantPoint
{
    /// <summary>
    /// Gets or sets the unique point ID.
    /// </summary>
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the embedding vector.
    /// </summary>
    [JsonPropertyName("vector")]
    public float[] Vector { get; set; } = [];

    /// <summary>
    /// Gets or sets the document metadata.
    /// </summary>
    [JsonPropertyName("payload")]
    public Dictionary<string, object> Payload { get; set; } = [];
}

/// <summary>
/// Represents a Qdrant upsert request.
/// </summary>
public class QdrantUpsertRequest
{
    /// <summary>
    /// Gets or sets the points to insert or update.
    /// </summary>
    [JsonPropertyName("points")]
    public List<QdrantPoint> Points { get; set; } = [];
}

/// <summary>
/// Represents a Qdrant vector search request.
/// </summary>
public class QdrantSearchRequest
{
    /// <summary>
    /// Gets or sets the query embedding.
    /// </summary>
    [JsonPropertyName("query")]
    public float[] Query { get; set; } = [];

    /// <summary>
    /// Gets or sets the maximum number of results.
    /// </summary>
    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    /// <summary>
    /// Indicates whether Qdrant should return payload metadata.
    /// </summary>
    [JsonPropertyName("with_payload")]
    public bool WithPayload { get; set; }

    /// <summary>
    /// Gets or sets the optional document filter.
    /// </summary>
    [JsonPropertyName("filter")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public QdrantFilter? Filter { get; set; }
}

/// <summary>
/// Represents a Qdrant payload filter.
/// </summary>
public class QdrantFilter
{
    /// <summary>
    /// Gets or sets the conditions that must all match.
    /// </summary>
    [JsonPropertyName("must")]
    public List<QdrantCondition> Must { get; set; } = [];
}

/// <summary>
/// Represents one condition in a Qdrant filter.
/// </summary>
public class QdrantCondition
{
    /// <summary>
    /// Gets or sets the payload field to filter.
    /// </summary>
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the exact-match condition.
    /// </summary>
    [JsonPropertyName("match")]
    public QdrantMatch Match { get; set; } = new();
}

/// <summary>
/// Represents an exact-value match condition.
/// </summary>
public class QdrantMatch
{
    /// <summary>
    /// Gets or sets the value to match.
    /// </summary>
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// Represents the Qdrant search response.
/// </summary>
public class QdrantSearchResponse
{
    /// <summary>
    /// Gets or sets the search result.
    /// </summary>
    [JsonPropertyName("result")]
    public QdrantSearchResult? Result { get; set; }
}

/// <summary>
/// Contains the points returned by Qdrant.
/// </summary>
public class QdrantSearchResult
{
    /// <summary>
    /// Gets or sets the returned points.
    /// </summary>
    [JsonPropertyName("points")]
    public List<QdrantSearchPoint> Points { get; set; } = [];
}

/// <summary>
/// Represents an individual point returned by Qdrant.
/// </summary>
public class QdrantSearchPoint
{
    /// <summary>
    /// Gets or sets the point ID.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the similarity score.
    /// </summary>
    [JsonPropertyName("score")]
    public float Score { get; set; }

    /// <summary>
    /// Gets or sets the returned payload.
    /// </summary>
    [JsonPropertyName("payload")]
    public Dictionary<string, JsonElement>? Payload { get; set; }
}



/// <summary>
/// Represents a Qdrant request to delete points using a payload filter.
/// </summary>
public class QdrantDeleteRequest
{
    /// <summary>
    /// Gets or sets the filter identifying points to delete.
    /// </summary>
    [JsonPropertyName("filter")]
    public QdrantFilter Filter { get; set; } = new();
}

