namespace PAFA.Domain.Entities;

/// <summary>
/// Base class for all business entities.
/// Provides systematic audit fields and soft delete support.
/// </summary>
public abstract class BaseEntity
{
    public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;
    public string   CreatedBy  { get; set; } = "SYSTEM";
    public DateTime? UpdatedAt { get; set; }
    public string?   UpdatedBy { get; set; }
    public bool      IsDeleted { get; set; } = false;

    /// <summary>Optimistic concurrency token — managed automatically by EF Core.</summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}