using System.Text.Json;

namespace Outbox.Api.DTOs.Requests;

public record EnqueueEventRequest(
    string EventType,
    JsonElement Payload,
    string? SubjectKey = null,
    string? ExtraHeadersJson = null 
);