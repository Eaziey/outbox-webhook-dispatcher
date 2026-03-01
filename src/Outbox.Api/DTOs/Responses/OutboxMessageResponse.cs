namespace Outbox.Api.DTOs.Responses;

public record OutboxMessageResponse(
    Guid Id,
    string EventType,
    DateTime CreatedAtUtc,
    string? TenantId,
    string? SubjectKey
    // Optional computed aggregate for UI:
    // int SentCount, int FailedCount, int PendingCount, int DeadCount
);