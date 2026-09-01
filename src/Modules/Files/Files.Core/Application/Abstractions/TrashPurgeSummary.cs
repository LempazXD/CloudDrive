namespace Files.Core.Application.Abstractions;

public sealed record TrashPurgeSummary(int PurgedCount, long ReclaimedBytes, int FailedCount);
