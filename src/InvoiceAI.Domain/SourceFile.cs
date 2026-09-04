namespace InvoiceAI.Domain.Entities;

public class SourceFile
{
    public int Id { get; set; }

    // The GUID-based name the file is actually stored under
    public string StorageKey { get; set; } = string.Empty;

    // What the user called it — shown in the UI, never used as a path
    public string OriginalFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public int? PageCount { get; set; }

    public string? UploadedByUserId { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
