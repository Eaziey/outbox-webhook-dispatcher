namespace Outbox.Api.DTOs.Requests;

public record CreateSubscriptionRequest(
    string Endpoint,
    string Secret,
    int? MaxConcurrency = null
);