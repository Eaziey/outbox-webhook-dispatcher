namespace Outbox.Api.DTOs.Responses;

public record SubscriptionResponse(
    Guid Id,
    string Endpoint,
    bool IsActive,
    DateTime CreatedAtUtc,
    string? TenantId,
    int? MaxConcurrency
);
