
using Microsoft.EntityFrameworkCore;
using Rag.Application.Interfaces;
using Rag.Application.Services;
using Rag.Infrastructure.Chat;
using Rag.Infrastructure.Embeddings;
using Rag.Infrastructure.Pdf;
using Rag.Infrastructure.Persistence;
using Rag.Infrastructure.VectorStore;
using Rag.Infrastructure.OCR;

var builder = WebApplication.CreateBuilder(args);

// Register services required by the API.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Register Ollama embedding service through dependency injection.
builder.Services.AddHttpClient<IEmbeddingService, OllamaEmbeddingService>(
    client =>
    {
        client.BaseAddress = new Uri("http://localhost:11434/");
        client.Timeout = TimeSpan.FromMinutes(5);
    });


// Register Qdrant vector store.
builder.Services.AddHttpClient<IVectorStore, QdrantVectorStore>(
    client =>
    {
        client.BaseAddress = new Uri("http://localhost:6333/");
        client.Timeout = TimeSpan.FromMinutes(2);
    });


builder.Services.AddScoped<TextChunkingService>();


// Register Qwen chat service.
builder.Services.AddHttpClient<IChatService, OllamaChatService>(
    client =>
    {
        client.BaseAddress = new Uri("http://localhost:11434/");
        client.Timeout = TimeSpan.FromMinutes(5);
    });

// Register RAG question-answering service.
builder.Services.AddScoped<RagQuestionAnsweringService>();

// Register PDF text extraction service.
builder.Services.AddScoped<PdfPigTextExtractor>();

builder.Services.AddScoped<
    IPdfTextExtractor,
    HybridPdfTextExtractor>();

builder.Services.AddScoped<PdfIngestionService>();

builder.Services.AddScoped<IOcrService, TesseractOcrService>();


var databasePath = Path.Combine(
    builder.Environment.ContentRootPath,
    "rag.db");

Console.WriteLine($"SQLite Database Path: {databasePath}");

builder.Services.AddDbContext<RagDbContext>(options =>
    options.UseSqlite($"Data Source={databasePath}"));

builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();

// Build the application.
var app = builder.Build();

// Configure HTTP request pipeline.

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
