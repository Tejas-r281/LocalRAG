
using Microsoft.EntityFrameworkCore;
using Rag.Domain.Entities;

namespace Rag.Infrastructure.Persistence;

/// <summary>
/// Represents the SQLite database context for the RAG application.
/// </summary>
public class RagDbContext : DbContext
{
    /// <summary>
    /// Initializes the database context with configured options.
    /// </summary>
    public RagDbContext(DbContextOptions<RagDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Represents the Documents table in the SQLite database.
    /// </summary>
    public DbSet<Document> Documents => Set<Document>();

    /// <summary>
    /// Configures the document entity and its database constraints.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Document>(entity =>
        {
            // Configure the primary key.
            entity.HasKey(document => document.Id);

            // Configure filename as a required field.
            entity.Property(document => document.FileName)
                .IsRequired()
                .HasMaxLength(255);

            // Configure upload date.
            entity.Property(document => document.UploadedAt)
                .IsRequired();

            // Configure total chunks.
            entity.Property(document => document.TotalChunks)
                .IsRequired();

            // Configure document processing status.
            entity.Property(document => document.Status)
                .IsRequired()
                .HasMaxLength(50);

            // Create an index for filename searches.
            entity.HasIndex(document => document.FileName);
        });
    }
}
