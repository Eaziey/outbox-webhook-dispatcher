namespace Outbox.Api.DTOs.Responses;

public record DeliveryAttemptResponse(
    long Id,
    Guid OutboxDeliveryId,
    DateTime AttemptedAtUtc,
    int? StatusCode,
    string? Error,
    int AttemptNumber,
    int? DurationMs,
    bool? ConsideredRetryable
);