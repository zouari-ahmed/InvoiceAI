namespace InvoiceAI.Application.DTOs;

public record UploadedFileDto(int InvoiceId, int SourceFileId, string FileName, long SizeBytes);

public record RejectedFileDto(string FileName, string Reason);

public record UploadResultDto(IReadOnlyList<UploadedFileDto> Accepted, IReadOnlyList<RejectedFileDto> Rejected);