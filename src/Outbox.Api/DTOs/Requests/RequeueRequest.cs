namespace Outbox.Api.DTOs.Requests;

public record RequeueRequest(
    DateTime? NextAttemptUtc = null
);