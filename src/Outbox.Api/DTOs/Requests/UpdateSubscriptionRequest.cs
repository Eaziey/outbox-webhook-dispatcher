namespace Outbox.Api.DTOs.Requests;

public record UpdateSubscriptionRequest(
    string? Endpoint,
    bool? IsActive,
    string? Secret = null,
    int? MaxConcurrency = null
);