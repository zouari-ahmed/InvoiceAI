namespace InvoiceAI.Domain.Entities;

public enum InvoiceStatus
{
    Uploaded,
    Processing,
    ExtractionFailed,
    PendingReview,
    Reviewed,
    Exported
}