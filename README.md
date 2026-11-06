# Local RAG Application

A locally hosted Retrieval-Augmented Generation (RAG) application built
with **.NET 10, Clean Architecture, Ollama, Qdrant, SQLite, PdfPig,
PDFtoImage, and Tesseract OCR**.

The application ingests PDF documents, extracts text (including scanned
PDFs through OCR), splits the text into chunks, creates embeddings
locally, stores vectors in Qdrant, tracks document metadata in SQLite,
and answers questions using a local language model.

> **Project goal:** Build a practical, extensible RAG system without
> depending on paid AI APIs. Document processing, embeddings, vector
> search, and answer generation currently run locally.

------------------------------------------------------------------------

## Table of contents

1.  [Project overview](#project-overview)
2.  [Implemented capabilities](#implemented-capabilities)
3.  [Technology stack](#technology-stack)
4.  [Architecture](#architecture)
5.  [Prerequisites](#prerequisites)
6.  [Local services and
    configuration](#local-services-and-configuration)
7.  [Project structure](#project-structure)
8.  [How document ingestion works](#how-document-ingestion-works)
9.  [Hybrid PDF text extraction and
    OCR](#hybrid-pdf-text-extraction-and-ocr)
10. [Chunking strategy](#chunking-strategy)
11. [Embedding generation](#embedding-generation)
12. [Qdrant vector storage](#qdrant-vector-storage)
13. [SQLite document metadata](#sqlite-document-metadata)
14. [Question answering flow](#question-answering-flow)
15. [API reference](#api-reference)
16. [Running the application](#running-the-application)
17. [Testing with Postman](#testing-with-postman)
18. [Operational notes and
    troubleshooting](#operational-notes-and-troubleshooting)
19. [Known limitations and technical
    debt](#known-limitations-and-technical-debt)
20. [Development roadmap](#development-roadmap)

------------------------------------------------------------------------

## Project overview

This solution implements a local RAG workflow:

``` text
                       DOCUMENT INGESTION
                               |
                               v
                         PDF upload API
                               |
                               v
                    Hybrid PDF text extraction
                       /               \
                      /                 \
             PdfPig native text     Scanned page
                                      |
                                      v
                               PDFtoImage
                               PDF -> PNG
                                      |
                                      v
                               Tesseract OCR
                                English + Hindi
                      \                 /
                       \               /
                               |
                               v
                         Extracted pages
                               |
                               v
                         Text chunking
                               |
                               v
                    Ollama embedding model
                               |
                               v
                       Qdrant vector store
                               |
                               v
                    SQLite document metadata


                        QUESTION ANSWERING
                               |
                               v
                         User question
                               |
                               v
                    Ollama embedding model
                               |
                               v
                     Qdrant similarity search
                               |
                               v
                      Retrieved text chunks
                               |
                               v
                      Ollama chat model
                               |
                               v
                      Answer + source chunks
```

RAG combines information retrieval with language generation. Instead of
asking the language model to answer only from its general training, the
application first retrieves relevant chunks from uploaded documents and
supplies them as context to the model.

### Important local-service boundaries

  ------------------------------------------------------------------------------------------------
  Component               Responsibility          Local endpoint / location
  ----------------------- ----------------------- ------------------------------------------------
  ASP.NET Core API        HTTP entry point and    `http://localhost:5081` in the development setup
                          dependency injection    
                          composition root        

  Ollama                  Embeddings and chat     `http://localhost:11434`
                          generation              

  Qdrant                  Vector storage and      `http://localhost:6333`
                          similarity search       

  Qdrant dashboard        Inspect collections and `http://localhost:6333/dashboard`
                          points                  

  SQLite                  Document metadata and   `Rag.API/rag.db` in the current setup
                          processing status       

  Tesseract               Local OCR executable    `C:\Program Files\Tesseract-OCR\tesseract.exe`
  ------------------------------------------------------------------------------------------------

The API port can change depending on the ASP.NET Core launch profile.
Check the application startup output if `5081` is not available.

------------------------------------------------------------------------

## Implemented capabilities

The following capabilities have been implemented and exercised during
development:

-   Clean Architecture solution with four projects.
-   Paragraph-aware, sentence-aware character-based chunking with configurable overlap (default target: 1,200 characters; overlap: 200 characters).
-   Local embeddings through Ollama using `nomic-embed-text`.
-   Qdrant collection creation/use, upsert, similarity search, and metadata-filtered retrieval by document ID and page number.
-   Local chat generation through Ollama using `qwen2.5:7b`.
-   RAG question answering with retrieved source chunks returned
    alongside the answer.
-   PDF text extraction using PdfPig.
-   Scanned PDF page rendering using PDFtoImage and PDFium.
-   Local Tesseract OCR with English and Hindi language data.
-   Hybrid page-by-page extraction: use native PDF text when present;
    use OCR when the page has no extracted text.
-   SQLite document metadata using EF Core.
-   Document upload and ingestion status tracking.
-   Get all documents and get a document by ID.
-   Delete document vectors from Qdrant by document ID and delete the
    corresponding SQLite metadata.
-   Optional document-specific question answering using a document ID
    filter.
-   Optional page-level retrieval filtering, including page-only searches
    and combined document ID + page number filters.
-   Postman-based endpoint testing.

A successful scanned-PDF upload was verified in Postman with HTTP
`200 OK`, `status: Completed`, and a non-zero chunk count. OCR quality
is not yet perfect; some words and lines can be missed in scanned
documents.

------------------------------------------------------------------------

## Technology stack

  Technology                        Use
  --------------------------------- -------------------------------------------------
  .NET 10 / ASP.NET Core            Web API
  C#                                Application implementation
  Clean Architecture                Separation of concerns and dependency direction
  Ollama                            Local model runtime
  `nomic-embed-text`                Text embedding model
  `qwen2.5:7b`                      Chat / answer generation model
  Qdrant                            Vector database
  SQLite                            Document metadata
  Entity Framework Core 10          SQLite data access and migrations
  PdfPig (`PdfPig` NuGet package)   Native PDF text extraction
  PDFtoImage 5.4.0                  Render PDF pages to images
  PDFium                            PDF rendering engine used by PDFtoImage
  Tesseract OCR 5                   Local OCR
  `eng` and `hin` trained data      English and Hindi OCR
  Postman                           API testing
  Docker Desktop + WSL2             Run Qdrant locally

### Models

The current local Ollama models are:

``` text
nomic-embed-text
qwen2.5:7b
```

The embedding model currently returns vectors with **768 dimensions**.
The Qdrant collection must use the same vector size.

Check installed models:

``` powershell
ollama list
```

Pull the models if they are not already installed:

``` powershell
ollama pull nomic-embed-text
ollama pull qwen2.5:7b
```

------------------------------------------------------------------------

## Architecture

The solution uses four projects:

``` text
RagApplication/
|
+-- Rag.API/
|   +-- Controllers/
|   +-- Program.cs
|   +-- appsettings.json
|   +-- appsettings.Development.json
|   +-- rag.db
|
+-- Rag.Application/
|   +-- Interfaces/
|   +-- Models/
|   +-- Services/
|
+-- Rag.Domain/
|   +-- Entities/
|
+-- Rag.Infrastructure/
    +-- Chat/
    +-- Embeddings/
    +-- OCR/
    +-- Pdf/
    +-- Persistence/
    +-- VectorStore/
```

### Dependency direction

``` text
Rag.API -----------> Rag.Application
   |                         |
   |                         v
   +--------------> Rag.Infrastructure
                             |
                             v
                         Rag.Domain

Rag.Application ----------> Rag.Domain

Rag.Infrastructure -------> Rag.Application
Rag.Infrastructure -------> Rag.Domain

Rag.Domain has no project dependencies.
```

The key Clean Architecture rule is that the Domain project does not
depend on infrastructure or web frameworks. Application owns use cases
and abstractions. Infrastructure implements those abstractions. API is
the HTTP entry point and composition root.

### Project responsibilities

#### `Rag.Domain`

Contains core entities such as:

-   `Document`
-   `DocumentChunk`

It should remain independent of EF Core, Qdrant, Ollama, ASP.NET Core,
and PDF libraries.

#### `Rag.Application`

Contains application contracts, models, and orchestration:

-   Interfaces such as `IEmbeddingService`, `IVectorStore`,
    `IChatService`, `IPdfTextExtractor`, `IOcrService`, and
    `IDocumentRepository`.
-   `TextChunkingService`
-   `PdfIngestionService`
-   `RagQuestionAnsweringService`
-   Retrieval and answer response models.

Application coordinates the workflow but does not directly implement
external integrations.

#### `Rag.Infrastructure`

Contains implementations of external dependencies:

-   `OllamaEmbeddingService`
-   `OllamaChatService`
-   `QdrantVectorStore`
-   `PdfPigTextExtractor`
-   `HybridPdfTextExtractor`
-   `TesseractOcrService`
-   `RagDbContext`
-   `DocumentRepository`

#### `Rag.API`

Contains controllers, application startup, configuration, and dependency
injection registration.

`Program.cs` is the composition root for the API host. If a separate
Worker service is introduced later, it will need its own registrations.

------------------------------------------------------------------------

## Prerequisites

Install the following on the development machine:

1.  .NET 10 SDK.
2.  Visual Studio or Visual Studio Code.
3.  Docker Desktop with WSL2 backend.
4.  Ollama for Windows.
5.  Tesseract OCR for Windows.
6.  Postman or another HTTP client.

Verify .NET:

``` powershell
dotnet --version
```

Verify Docker:

``` powershell
docker version
```

Verify Ollama:

``` powershell
ollama --version
ollama list
```

Verify Tesseract:

``` powershell
tesseract --version
tesseract --list-langs
```

The language list should include:

``` text
eng
hin
```

The current Windows Tesseract installation is located at:

``` text
C:\Program Files\Tesseract-OCR\tesseract.exe
```

If the `tesseract` command is not recognised, use the full executable
path or add the installation directory to Windows PATH.

------------------------------------------------------------------------

## Local services and configuration

### Ollama

Ollama is used for both embedding generation and chat completion.

Base URL:

``` text
http://localhost:11434
```

The embedding integration uses the Ollama embedding endpoint with model
`nomic-embed-text`. The chat integration uses the Ollama chat endpoint
with model `qwen2.5:7b`.

Check that Ollama is running and that both models are available before
testing ingestion or question answering.

### Qdrant with Docker

The Qdrant container was created with persistent Docker volume storage:

``` powershell
docker run -d `
  --name qdrant `
  -p 6333:6333 `
  -p 6334:6334 `
  -v qdrant_storage:/qdrant/storage `
  qdrant/qdrant
```

Start an existing container:

``` powershell
docker start qdrant
```

Check its status:

``` powershell
docker ps
```

Check the Qdrant API:

``` powershell
Invoke-RestMethod http://localhost:6333/collections
```

Open the dashboard:

``` text
http://localhost:6333/dashboard
```

**Do not create another container with the same host ports if the
existing `qdrant` container already exists.** Reuse the existing
container and volume so previously stored vectors are not lost.

The collection used by the application is:

``` text
rag_documents
```

Its vector configuration is:

-   Vector size: `768`
-   Distance: `Cosine`

The embedding model and Qdrant vector size must remain compatible. If
the embedding model is changed to one with a different dimension, the
collection configuration and stored vectors must be migrated or
recreated.

### Tesseract configuration

The API configuration contains a Tesseract section similar to:

``` json
{
  "Tesseract": {
    "ExecutablePath": "C:\\Program Files\\Tesseract-OCR\\tesseract.exe",
    "Language": "eng+hin"
  }
}
```

Merge this section into the existing `appsettings.json`; do not replace
other configuration sections.

The `eng+hin` setting asks Tesseract to use English and Hindi trained
data.

### SQLite configuration

The API currently registers EF Core SQLite using a connection string
similar to:

``` csharp
builder.Services.AddDbContext<RagDbContext>(options =>
    options.UseSqlite("Data Source=rag.db"));
```

With this relative path, the database is created relative to the API
host's content root/current working directory. In the current
development setup, the database is:

``` text
Rag.API/rag.db
```

A more explicit path can be configured using the API content root:

``` csharp
var databasePath = Path.Combine(
    builder.Environment.ContentRootPath,
    "rag.db");

builder.Services.AddDbContext<RagDbContext>(options =>
    options.UseSqlite($"Data Source={databasePath}"));
```

Use one consistent database path for migrations, runtime, and
inspection.

------------------------------------------------------------------------

## Project structure

The following is the logical structure of the implemented solution.
Exact file placement may evolve as the project grows.

``` text
RagApplication/
|
+-- Rag.API/
|   +-- Controllers/
|   |   +-- EmbeddingController.cs
|   |   +-- DocumentController.cs
|   |   +-- RagController.cs
|   |   +-- OcrController.cs              # Development OCR test endpoint
|   |
|   +-- Program.cs
|   +-- appsettings.json
|   +-- appsettings.Development.json
|   +-- rag.db
|
+-- Rag.Application/
|   +-- Interfaces/
|   |   +-- IEmbeddingService.cs
|   |   +-- IVectorStore.cs
|   |   +-- IChatService.cs
|   |   +-- IPdfTextExtractor.cs
|   |   +-- IOcrService.cs
|   |   +-- IDocumentRepository.cs
|   |
|   +-- Models/
|   |   +-- RetrievedChunk.cs
|   |   +-- RagAnswerResponse.cs
|   |
|   +-- Services/
|       +-- TextChunkingService.cs
|       +-- PdfIngestionService.cs
|       +-- RagQuestionAnsweringService.cs
|
+-- Rag.Domain/
|   +-- Entities/
|       +-- Document.cs
|       +-- DocumentChunk.cs
|
+-- Rag.Infrastructure/
    +-- Embeddings/
    |   +-- OllamaEmbeddingService.cs
    |
    +-- Chat/
    |   +-- OllamaChatService.cs
    |
    +-- VectorStore/
    |   +-- QdrantVectorStore.cs
    |
    +-- Pdf/
    |   +-- PdfPigTextExtractor.cs
    |   +-- HybridPdfTextExtractor.cs
    |
    +-- OCR/
    |   +-- TesseractOcrService.cs
    |
    +-- Persistence/
        +-- RagDbContext.cs
        +-- DocumentRepository.cs
        +-- Migrations/
```

------------------------------------------------------------------------

## How document ingestion works

The main upload endpoint accepts a PDF file and calls
`PdfIngestionService`.

The implemented workflow is:

1.  Receive the uploaded PDF through the API.
2.  Create a document ID.
3.  Create a document metadata record in SQLite with a processing
    status.
4.  Extract text page by page.
5.  Split extracted text into chunks.
6.  Generate an embedding for each chunk using Ollama.
7.  Store each chunk, vector, and metadata in Qdrant.
8.  Update the SQLite document record with total chunk count and final
    status.
9.  Return the document ID, filename, chunk count, and status.

The same document ID is used in SQLite and Qdrant so that chunks can be
filtered and deleted by document.

### Ingestion status

The document entity currently has a status string. The ingestion service
uses statuses such as:

-   `Processing`
-   `Completed`
-   `Failed`

If ingestion throws an exception, the service attempts to mark the
document as `Failed` and sets its chunk count to zero before rethrowing
the exception.

A zero-chunk validation is present. If no readable text is extracted,
ingestion throws an exception rather than silently marking an empty
document as successfully processed.

**Current limitation:** If an error happens after some vectors have
already been inserted into Qdrant, the failure path does not yet
guarantee cleanup of all partially inserted vectors. This should be
addressed with transactional-style compensation or a background
ingestion design.

------------------------------------------------------------------------

## Hybrid PDF text extraction and OCR

PDFs can contain:

1.  Selectable/native text.
2.  Scanned pages where text is stored as images.
3.  A mixture of native text and scanned pages.

PdfPig can extract a PDF's text layer, but it cannot recognise text that
exists only inside a scanned image. Tesseract performs OCR on images, so
the PDF page must first be rendered.

### Hybrid page-by-page workflow

``` text
PDF
 |
 v
PdfPig: inspect/extract each page
 |
 +-- Page has extracted text
 |       |
 |       +--> Keep PdfPig text
 |
 +-- Page has no extracted text
         |
         v
      PDFtoImage
      Render page to PNG
         |
         v
      Tesseract OCR
      Language: eng+hin
         |
         v
      Keep recognised text
```

The current hybrid implementation uses a simple empty-text check. If
PdfPig returns non-empty text for a page, OCR is skipped for that page.
If PdfPig returns empty text, the page is rendered and sent to
Tesseract.

### Why page-by-page fallback matters

A single PDF can contain both selectable and scanned pages. Running OCR
only when the entire PDF is empty would fail to recover scanned pages in
a mixed PDF. The page-by-page approach avoids that limitation.

### `IPdfTextExtractor`

The Application contract represents page-by-page extraction and supports
asynchronous operation:

``` csharp
public interface IPdfTextExtractor
{
    Task<IReadOnlyList<ExtractedPage>> ExtractPagesAsync(
        Stream pdfStream,
        CancellationToken cancellationToken = default);
}
```

Each `ExtractedPage` contains:

-   `PageNumber`
-   `Text`

### `IOcrService`

The OCR contract is independent of the OCR engine:

``` csharp
public interface IOcrService
{
    Task<string> RecognizeTextAsync(
        byte[] imageBytes,
        CancellationToken cancellationToken = default);
}
```

This allows the OCR implementation to be replaced later without changing
the application use case.

### `TesseractOcrService`

The Infrastructure implementation:

1.  Receives PNG/JPEG image bytes.
2.  Writes the bytes to a temporary image file.
3.  Starts the local Tesseract executable.
4.  Uses the configured language combination, currently `eng+hin`.
5.  Captures recognised text from standard output.
6.  Removes the temporary image file.

The service uses `ProcessStartInfo.ArgumentList` to pass arguments
safely without manually constructing a shell command.

### OCR limitations

OCR quality depends on image quality, font size, scan quality,
orientation, contrast, layout, and language. The current implementation
is functional but does not yet include:

-   Image deskewing.
-   Noise removal.
-   Adaptive thresholding.
-   Contrast enhancement.
-   Text-quality scoring.
-   Partial-text fallback.
-   Table structure reconstruction.
-   OCR confidence scoring.

A page containing a small amount of native text is currently treated as
a successful PdfPig extraction, even if some content is missing.
Quality-based fallback is planned.

The OCR test endpoint is intended for development/testing and should be
restricted or removed before production.

------------------------------------------------------------------------

## Chunking strategy

`TextChunkingService` splits extracted text into smaller overlapping
chunks. The current implementation is paragraph-aware and sentence-aware,
with character-based chunk sizing.

Current defaults:

-   Target chunk size: `1200` characters.
-   Overlap: `200` characters.
-   Paragraphs are split first, then sentences are grouped into chunks.
-   An unusually long sentence is split at word boundaries.
-   Consecutive chunks reuse a trailing portion of the previous chunk to
    preserve some context.

The values can be overridden through the `CreateChunks` method parameters:
`targetChunkCharacters` and `overlapCharacters`.

The chunk size is a character target, not a token limit. Actual chunk
lengths can vary because sentence units are kept together where possible.

Each chunk has metadata such as:

-   Document ID.
-   Chunk ID.
-   Chunk index.
-   Page number.
-   Start unit index.
-   End unit index.

**Metadata note:** The existing entity property names
`StartSentenceIndex` and `EndSentenceIndex` are retained for compatibility,
but with the current paragraph/sentence-unit chunker they represent
indexes into the generated text units; they should not be treated as
verified original sentence positions.
-   Content.

### Current chunking limitations

The current regex-based sentence splitting is still a learning-stage
implementation. It may not correctly handle abbreviations, decimal values,
bulleted lists, tables, OCR line breaks, and mixed Hindi/English
punctuation. The current implementation uses character-based sizing;
token-aware chunking remains future work.

------------------------------------------------------------------------

## Embedding generation

The embedding service converts text into a numerical vector.

The current model is:

``` text
nomic-embed-text
```

The Application contract:

``` csharp
public interface IEmbeddingService
{
    Task<float[]> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default);
}
```

The Infrastructure implementation calls Ollama locally and returns a
`float[]`.

The verified output dimension is:

``` text
768
```

The embedding is generated for each chunk during ingestion. The same
embedding model is used for the user's question before retrieval.

**Important:** Use the same compatible embedding model for document
chunks and question embeddings. Changing the model can change vector
dimensions and semantic space; existing vectors should not be mixed with
vectors from an incompatible model.

------------------------------------------------------------------------

## Qdrant vector storage

Qdrant stores the vector representation and chunk payload.

Current collection:

``` text
rag_documents
```

Current vector configuration:

``` text
Vector size: 768
Distance: Cosine
```

The payload includes fields such as:

``` json
{
  "documentId": "document-guid",
  "content": "Text of the chunk",
  "chunkIndex": 0,
  "pageNumber": 1,
  "startSentenceIndex": 0,
  "endSentenceIndex": 1
}
```

The actual point ID is also stored by Qdrant.

### Main vector operations

The vector store abstraction supports the operations used by the
application:

-   Upsert a document chunk and embedding.
-   Search using a query embedding.
-   Optionally filter search by document ID.
-   Optionally filter search by page number.
-   Combine document ID and page number conditions in one filter.
-   Delete all points belonging to a particular document ID.

Document-specific filtering uses the payload field `documentId`.
Page-level filtering uses the integer payload field `pageNumber`.

The current `SearchAsync` contract accepts optional `documentId` and
`pageNumber` parameters. When both are supplied, Qdrant applies both
conditions using its `must` filter, so a point must match both values.
When neither is supplied, retrieval remains global.

Supported filter combinations:

| `documentId` | `pageNumber` | Retrieval scope |
|---|---|---|
| Omitted | Omitted | All indexed documents |
| Supplied | Omitted | One document |
| Omitted | Supplied | That page number across documents |
| Supplied | Supplied | That page in that document |

Page numbers are one-based PDF page numbers, matching the metadata stored
during ingestion.

### Inspect Qdrant points

To inspect points belonging to one document, use the Qdrant scroll
endpoint:

``` http
POST http://localhost:6333/collections/rag_documents/points/scroll
Content-Type: application/json
```

Example body:

``` json
{
  "filter": {
    "must": [
      {
        "key": "documentId",
        "match": {
          "value": "YOUR-DOCUMENT-GUID"
        }
      }
    ]
  },
  "limit": 10,
  "with_payload": true,
  "with_vector": false
}
```

Inspect the `payload.content` field to see the extracted chunk text
stored for retrieval.

Count points:

``` http
POST http://localhost:6333/collections/rag_documents/points/count
Content-Type: application/json
```

``` json
{
  "exact": true
}
```

------------------------------------------------------------------------

## SQLite document metadata

SQLite stores document-level metadata. Qdrant stores chunk-level content
and vectors.

The `Document` entity currently contains:

``` csharp
public class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FileName { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public int TotalChunks { get; set; }
    public string Status { get; set; } = "Uploaded";
}
```

The repository contract supports:

``` csharp
Task AddAsync(
    Document document,
    CancellationToken cancellationToken = default);

Task<Document?> GetByIdAsync(
    Guid documentId,
    CancellationToken cancellationToken = default);

Task<List<Document>> GetAllAsync(
    CancellationToken cancellationToken = default);

Task<bool> DeleteAsync(
    Guid documentId,
    CancellationToken cancellationToken = default);

Task UpdateAsync(
    Document document,
    CancellationToken cancellationToken = default);
```

The EF Core context is `RagDbContext`, and the implementation is
`DocumentRepository`.

### Migrations

The initial migration was created in the Infrastructure project, using
the API as the startup project:

``` powershell
dotnet ef migrations add InitialCreate `
  --project .\Rag.Infrastructure\Rag.Infrastructure.csproj `
  --startup-project .\Rag.API\Rag.API.csproj `
  --output-dir Persistence\Migrations
```

Apply migrations:

``` powershell
dotnet ef database update `
  --project .\Rag.Infrastructure\Rag.Infrastructure.csproj `
  --startup-project .\Rag.API\Rag.API.csproj
```

The API startup project has the EF Core Design package installed for
design-time tooling.

------------------------------------------------------------------------

## Question answering flow

The question answering service coordinates retrieval and generation.

Current flow:

1.  Receive a question and optional `documentId` and `pageNumber` filters.
2.  Generate an embedding for the question using `nomic-embed-text`.
3.  Search Qdrant for similar chunks.
4.  Apply the supplied metadata filters: document ID, page number, or both.
5.  Build a prompt containing retrieved chunks and source metadata.
6.  Send the prompt to the local chat model `qwen2.5:7b`.
7.  Return the answer and retrieved source chunks.

The service is designed to answer from retrieved document context rather
than inventing facts not present in the supplied context.

### Document-specific question answering

The question request model supports:

``` csharp
public class RagQuestionRequest
{
    public string Question { get; set; } = string.Empty;
    public Guid? DocumentId { get; set; }
    public int? PageNumber { get; set; }
}
```

Both metadata filters are optional:

-   If both are omitted, search all indexed documents.
-   If only `DocumentId` is supplied, search within that document.
-   If only `PageNumber` is supplied, search that page number across all
    indexed documents.
-     If both are supplied, search that page in the specified document.

`PageNumber` must be a positive, one-based page number.

The response model includes the generated answer and retrieved source
chunks. The current source metadata includes document/chunk information
such as page and chunk indexes.

------------------------------------------------------------------------

## API reference

The following routes are based on the implemented controllers and
development tests. Confirm exact route attributes and HTTP methods in
the current controller source if they have since changed.

Base URL in the current development setup:

``` text
http://localhost:5081
```

### Document APIs

  ------------------------------------------------------------------------------
  Method                  Endpoint                       Purpose
  ----------------------- ------------------------------ -----------------------
  `POST`                  `/api/document/upload`         Upload and ingest a PDF

  `GET`                   `/api/document`                Get all document
                                                         metadata

  `GET`                   `/api/document/{documentId}`   Get document metadata
                                                         by ID

  `DELETE`                `/api/document/{documentId}`   Delete document vectors
                                                         and SQLite metadata

  `POST`                  `/api/document/ingest-test`    Development endpoint
                                                         for direct text/chunk
                                                         ingestion

  `POST`                  `/api/document/search-test`    Development endpoint
                                                         for vector search
  ------------------------------------------------------------------------------

#### Upload a PDF

``` http
POST /api/document/upload
```

Use `multipart/form-data` with the field:

``` text
file: <select a PDF file>
```

A successful response has the following shape:

``` json
{
  "message": "PDF processed successfully.",
  "documentId": "YOUR-DOCUMENT-GUID",
  "fileName": "example.pdf",
  "totalChunks": 4,
  "status": "Completed"
}
```

The chunk count depends on the document content and chunking
configuration.

#### Get all documents

``` http
GET /api/document
```

Returns document metadata records from SQLite.

#### Get one document

``` http
GET /api/document/YOUR-DOCUMENT-GUID
```

Returns the document record for the supplied ID, if found.

#### Delete a document

``` http
DELETE /api/document/YOUR-DOCUMENT-GUID
```

The intended operation deletes the Qdrant points filtered by
`documentId` and then deletes the SQLite metadata record. The endpoint
checks that the document exists before attempting deletion.

Deletion is not yet a distributed database transaction. Failure handling
between Qdrant and SQLite should be strengthened before production.

### RAG API

  -----------------------------------------------------------------------
  Method                  Endpoint                Purpose
  ----------------------- ----------------------- -----------------------
  `POST`                  `/api/rag/ask`          Ask a question using
                                                  retrieved document
                                                  context

  -----------------------------------------------------------------------

Example request across all documents:

``` json
{
  "question": "What information is available in the uploaded documents?"
}
```

Example request for one document:

``` json
{
  "question": "Summarise the key information in this document.",
  "documentId": "YOUR-DOCUMENT-GUID"
}
```

Example request for page 3 across all documents:

``` json
{
  "question": "What information is available on this page?",
  "pageNumber": 3
}
```

Example request for page 3 of one document:

``` json
{
  "question": "What information is available on this page?",
  "documentId": "YOUR-DOCUMENT-GUID",
  "pageNumber": 3
}
```

The response includes an answer and the source chunks used by the
application.

### Embedding API

  Method   Endpoint                Purpose
  -------- ----------------------- ---------------------------------
  `POST`   `/api/embedding/test`   Test local embedding generation

A previously verified test returned a 768-dimensional embedding.

### OCR test API

  -----------------------------------------------------------------------
  Method                  Endpoint                Purpose
  ----------------------- ----------------------- -----------------------
  `POST`                  `/api/ocr/test`         Development test of
                                                  Tesseract OCR on an
                                                  uploaded image

  -----------------------------------------------------------------------

Use `multipart/form-data` with:

``` text
image: <select PNG or JPEG>
```

The response includes the filename, extracted text, and character count.

This is a development test endpoint, not the normal PDF ingestion route.

------------------------------------------------------------------------

## Running the application

### 1. Start Ollama

Ensure Ollama is running and the required models are installed:

``` powershell
ollama list
```

Required models:

``` text
nomic-embed-text
qwen2.5:7b
```

### 2. Start Docker Desktop and Qdrant

Start Docker Desktop and wait for its engine to become available.

``` powershell
docker start qdrant
```

If the container does not exist, create it once using the Docker command
in the Qdrant section.

Verify:

``` powershell
docker ps
Invoke-RestMethod http://localhost:6333/collections
```

### 3. Verify Tesseract

``` powershell
tesseract --version
tesseract --list-langs
```

Expected languages include `eng` and `hin`.

### 4. Apply database migrations

From the solution root:

``` powershell
dotnet ef database update `
  --project .\Rag.Infrastructure\Rag.Infrastructure.csproj `
  --startup-project .\Rag.API\Rag.API.csproj
```

### 5. Run the API

``` powershell
dotnet run --project .\Rag.API\Rag.API.csproj
```

Use the actual URL printed by ASP.NET Core. The development setup
previously used:

``` text
http://localhost:5081
```

### 6. Test document upload

In Postman:

-   Method: `POST`
-   URL: `http://localhost:5081/api/document/upload`
-   Body: `form-data`
-   Key: `file`
-   Type: `File`
-   Value: choose a PDF

A successful response should contain a document ID, a non-zero chunk
count for a readable document, and `Completed` status.

### 7. Ask a question

Use:

``` http
POST http://localhost:5081/api/rag/ask
Content-Type: application/json
```

``` json
{
  "question": "What are the main points in the uploaded document?"
}
```

For document-specific retrieval, include the document ID.

------------------------------------------------------------------------

## Testing with Postman

The application has been tested incrementally with Postman.

Recommended end-to-end test sequence:

1.  Test the embedding endpoint and confirm 768 dimensions.
2.  Confirm the Qdrant collection exists.
3.  Upload a text-based PDF.
4.  Inspect its SQLite document record.
5.  Inspect its Qdrant points and payload content.
6.  Ask a question across all documents.
7.  Ask a question filtered to a specific document.
8.  Ask a question filtered by page number across documents.
9.  Ask a question filtered by both document ID and page number.
10. Upload a scanned PDF.
9.  Confirm that the scanned PDF is processed with a non-zero chunk
    count.
10. Inspect the extracted OCR chunks in Qdrant.
11. Retrieve all documents and retrieve one by ID.
12. Delete a test document and verify both Qdrant and SQLite.

### Verify extracted text in Qdrant

Use the scroll request from the Qdrant section and inspect
`payload.content`. This is the chunk text actually stored for retrieval.

### Verify document metadata in SQLite

Open `Rag.API/rag.db` with a SQLite viewer, or query the `Documents`
table. Refresh the database viewer after an upload if it does not
immediately show the latest row.

------------------------------------------------------------------------

## Operational notes and troubleshooting

### Qdrant connection refused on port 6333

Typical error:

``` text
No connection could be made because the target machine actively refused it.
(localhost:6333)
```

Check Docker:

``` powershell
docker ps -a
```

If the `qdrant` container is stopped:

``` powershell
docker start qdrant
```

Verify:

``` powershell
Invoke-RestMethod http://localhost:6333/collections
```

A stopped Docker Desktop Linux engine can also cause Docker commands to
fail. Start Docker Desktop and wait for the engine to be ready.

Do not delete the Qdrant container or its persistent volume just to
restart the service.

### Tesseract command is not recognised

Use the full path:

``` powershell
& "C:\Program Files\Tesseract-OCR\tesseract.exe" --version
```

Check the language files:

``` powershell
& "C:\Program Files\Tesseract-OCR\tesseract.exe" --list-langs
```

Add the installation directory to Windows PATH if desired, then open a
new terminal.

### Scanned PDF produces no text

Check that:

-   Tesseract is installed.
-   `eng` and `hin` trained data are available.
-   The configured executable path is correct.
-   PDFtoImage can render the PDF.
-   The PDF page is not blank or corrupted.
-   The hybrid extractor is registered in API dependency injection.

The current hybrid fallback triggers when PdfPig returns an empty page
text. It does not yet detect partial native text extraction.

### Upload returns zero chunks

The ingestion service rejects a document when no readable text is
extracted. Inspect the extraction stage before investigating embeddings
or Qdrant.

For scanned PDFs, verify that the OCR fallback is being invoked and that
the OCR output is non-empty.

### SQLite viewer does not show a new row

Refresh the SQLite extension/viewer. Verify that the viewer is opening
the same database file used by the API. The current development database
is expected under `Rag.API/rag.db`.

### Qdrant collection/vector dimension mismatch

The collection is configured for 768-dimensional vectors. Verify that
the embedding model still returns 768 values. Do not change embedding
models without planning a vector migration or collection rebuild.

### NuGet vulnerability warnings

A development restore reported a high-severity advisory for
`SQLitePCLRaw.lib.e_sqlite3` version `2.1.11`. This is tracked technical
debt. Review the advisory and compatible dependency updates before
production deployment; do not blindly upgrade transitive packages
without testing EF Core SQLite functionality.

------------------------------------------------------------------------

## Known limitations and technical debt

The current application is a working local learning/prototype system,
not yet production-hardened.

1.  **OCR completeness:** scanned-page OCR can miss words and lines.
2.  **Partial native extraction:** non-empty PdfPig text currently skips
    OCR even if the extracted text is incomplete.
3.  **OCR preprocessing:** no deskewing, denoising, contrast
    enhancement, or adaptive thresholding yet.
4.  **OCR confidence:** OCR confidence scores are not stored.
5.  **Table extraction:** text may be recognised, but row/column
    relationships are not reliably reconstructed.
6.  **Chunking:** paragraph-aware and sentence-aware character-based
    chunking is implemented, but regex splitting can still mishandle
    abbreviations, lists, tables, decimals, and OCR line breaks.
7.  **Chunk sizing:** the current 1,200-character target and 200-character
    overlap are not token-aware; token-based limits have not yet been
    implemented.
9.  **Chunk index metadata:** the legacy `StartSentenceIndex` and
    `EndSentenceIndex` property names do not precisely describe the
    generated text-unit indexes used by the current chunker.
9.  **Retrieval:** current retrieval is vector similarity search; hybrid
    keyword/vector retrieval is planned.
10.  **Reranking:** retrieved chunks are not yet reranked.
10. **Query rewriting:** query expansion or rewriting is not yet
    implemented.
11. **Context compression:** retrieved context is not yet compressed.
12. **Retrieval evaluation:** no repeatable benchmark dataset or
    retrieval metrics have been implemented.
13. **No-context handling:** answer behaviour when no relevant chunks
    exist needs further work.
14. **Source citations:** source chunk metadata is returned, but
    citation quality and document/page navigation need improvement.
15. **Ingestion recovery:** partial Qdrant inserts are not guaranteed to
    be rolled back if ingestion fails.
16. **Background processing:** PDF ingestion currently runs in the API
    request rather than a dedicated Worker.
17. **Authentication:** no authentication/authorization layer has been
    implemented.
18. **Monitoring:** structured logging, metrics, tracing, and health
    checks are not yet complete.
19. **File support:** DOCX, TXT, and other formats are not yet
    integrated.
20. **Multimodal support:** image understanding and richer document
    interpretation are future work.
21. **Production deployment:** Dockerized API deployment, secrets
    management, resource limits, and operational hardening remain future
    work.
22. **Testing:** automated unit, integration, and performance test
    coverage needs to be added.

------------------------------------------------------------------------
## Development roadmap

The following is the planned project roadmap. It distinguishes
implemented work from future work.

### Phase 1 --- Foundation and core RAG

**Implemented**

-   Clean Architecture solution.
-   Domain entities and Application interfaces.
-   Text chunking.
-   Ollama embeddings.
-   Qdrant vector storage.
-   Ollama chat integration.
-   Basic RAG question answering.
-   PDF text extraction.
-   SQLite document metadata and EF Core migrations.

### Phase 2 --- Document Management

**Implemented**

1.  Get all uploaded documents.
2.  Get document details by ID.
3.  Delete documents from both SQLite and Qdrant.
4.  Document processing status and basic error handling.

### Phase 3 --- Improve RAG

**Implemented / available**

5.  Document-specific question answering.
6.  Improved paragraph-aware, sentence-aware character-based chunking.
7.  Metadata filtering by document ID and page number, including combined
    filters.

**Planned**

8.  Improve source citations.
9.  Handle questions when no relevant context exists.

### Phase 4 --- Advanced Retrieval

**Planned**

10. Hybrid search (keyword + vector).
11. Reranking retrieved chunks.
12. Query rewriting.
13. Context compression.
14. Retrieval evaluation.

### Phase 5 --- Document Intelligence

**Partially implemented**

15. OCR for scanned PDFs --- local hybrid PdfPig + PDFtoImage +
    Tesseract pipeline is implemented. OCR quality improvements remain
    planned.

**Planned**

16. Tables and structured data extraction.
17. Support DOCX, TXT, and other formats.
18. Image and multimodal document understanding.

### Phase 6 --- Frontend

**Planned**

19. React document dashboard.
20. PDF upload interface.
21. Chat interface.
22. Source document preview.
23. Document-specific chat.

### Phase 7 --- Production Readiness

**Planned**

24. Authentication and authorization.
25. Background ingestion using a .NET Worker.
26. Logging and monitoring.
27. Docker deployment.
28. Testing and performance optimization.

------------------------------------------------------------------------

## Design principles for future development

Keep these rules as the application grows:

-   Domain must not depend on Infrastructure or API.
-   Application owns abstractions and use-case orchestration.
-   Infrastructure implements external integrations.
-   API remains the composition root for the API host.
-   Keep model and service endpoints in configuration, not scattered
    through code.
-   Use the same embedding model and vector dimensions for ingestion and
    retrieval.
-   Store document-level metadata in SQLite and chunk-level
    vectors/payloads in Qdrant.
-   Use one stable document ID across SQLite and Qdrant.
-   Preserve page numbers and chunk metadata so answers can cite their
    sources.
-   Keep all local processing local unless a deliberate architecture
    decision changes that requirement.
-   Add automated tests before changing core retrieval or ingestion
    behaviour.
-   Treat uploaded documents and extracted text as untrusted input.

------------------------------------------------------------------------

## Current project status

The project has reached a working local RAG prototype with document
management and hybrid scanned-PDF OCR.

**Verified end-to-end capabilities:**

-   Upload PDF.
-   Extract native or OCR text.
-   Chunk text.
-   Generate local embeddings.
-   Store vectors in Qdrant.
-   Track document metadata and status in SQLite.
-   Retrieve relevant chunks.
-   Generate an answer with the local chat model.
-   Filter retrieval to a selected document.
-   Filter retrieval by page number, either across documents or within a
    selected document.

The next planned work is to complete the remaining RAG improvements,
advanced retrieval, document intelligence, React frontend, and
production readiness phases.
