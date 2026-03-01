using Microsoft.AspNetCore.Mvc;
using Outbox.Api.DTOs.Responses;
using Outbox.Api.Interfaces.IRepositories;

namespace Outbox.Api.Controllers;

[ApiController]
[Route("api")]
public class DeliveryAttemptsController : ControllerBase
{
    private readonly IDeliveryAttemptRepository _attempts;

    public DeliveryAttemptsController(IDeliveryAttemptRepository attempts)
    {
        _attempts = attempts;
    }

    // GET: /api/messages/{messageId}/attempts?skip=0&take=50
    [HttpGet("messages/{messageId:guid}/attempts")]
    public async Task<ActionResult<object>> GetAttemptByMessage(Guid messageId, [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 200);
        var attempts = await _attempts.GetByMessageAsync(messageId, skip, take, ct);
        
        return Ok(attempts.Select(attempt => new DeliveryAttemptResponse(
        
            attempt.Id,
            attempt.OutboxDeliveryId,
            attempt.AttemptedAtUtc,
            attempt.StatusCode,
            attempt.Error,
            attempt.AttemptNumber,
            attempt.DurationMs,
            attempt.ConsideredRetryable
        )));
    }

    // GET: /api/deliveries/{deliveryId}/attempts?skip=0&take=50
    [HttpGet("deliveries/{deliveryId:guid}/attempts")]
    public async Task<ActionResult<object>> GetAttemptByDelivery(Guid deliveryId,[FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 200);

        var attempts = await _attempts.GetByDeliveryAsync(deliveryId, skip, take, ct);

        return Ok(attempts.Select(attempt => new DeliveryAttemptResponse(
        
            attempt.Id,
            attempt.OutboxDeliveryId,
            attempt.AttemptedAtUtc,
            attempt.StatusCode,
            attempt.Error,
            attempt.AttemptNumber,
            attempt.DurationMs,
            attempt.ConsideredRetryable
        )));
    }
}