namespace Files.Endpoints.Contracts;

public sealed record CompleteUploadRequest(IReadOnlyList<CompletedPartRequest>? Parts);
