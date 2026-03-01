using Outbox.Api.Entities;

namespace Outbox.Api.DTOs.Responses;

public record DeliveryResponse(
    Guid Id,
    Guid OutboxMessageId,
    Guid SubscriptionId,
    DeliveryStatus Status,
    int AttemptCount,
    DateTime? NextAttemptUtc,
    DateTime? LastAttemptUtc,
    int? LastStatusCode,
    string? LastError,
    string? TenantId,
    string? SubjectKey
);