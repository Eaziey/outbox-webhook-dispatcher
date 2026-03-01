using Microsoft.AspNetCore.Mvc;
using Outbox.Api.DTOs.Requests;
using Outbox.Api.DTOs.Responses;
using Outbox.Api.Entities;
using Outbox.Api.Interfaces.IRepositories;
using System.Text.Json;
using Outbox.Api.Tenancy;

namespace Outbox.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MessagesController : ControllerBase
{
    
    private readonly IOutboxMessageRepository _messagesRepo;
    private readonly IOutboxDeliveryRepository _deliveriesRepo;
    private readonly IUnitOfWork _uowRepo;
    private readonly ILogger<MessagesController> _logger;
    private readonly ITenantContext _tenant;

    public MessagesController(IOutboxMessageRepository messages,IOutboxDeliveryRepository deliveries,IUnitOfWork uow,ILogger<MessagesController> logger, ITenantContext tenant)
    {
        _messagesRepo = messages;
        _deliveriesRepo = deliveries;
        _uowRepo = uow;
        _logger = logger;
        _tenant = tenant;
    }

    
    private static object MapMessageSummary(OutboxMessage message)
    {
        return new
        {
            message.Id,
            message.EventType,
            message.TenantId,
            message.SubjectKey,
            message.CreatedAtUtc,
            message.UpdatedAtUtc,
            DeliveryCount = message.Deliveries?.Count ?? 0,
            StatusBreakdown = message.Deliveries?
                .GroupBy(d => d.Status)
                .ToDictionary(g => g.Key.ToString(), g => g.Count()) ?? new Dictionary<string, int>()
        };
    }


    [HttpPost]
    public async Task<ActionResult<object>> Enqueue([FromBody] EnqueueEventRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.EventType))
        {
            return ValidationProblem("EventType is required.");
        }

        if (!string.IsNullOrWhiteSpace(request.ExtraHeadersJson))
        {
            try
            {
                JsonDocument.Parse(request.ExtraHeadersJson);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Invalid ExtraHeadersJson provided.");
                return ValidationProblem("ExtraHeadersJson is not valid JSON.");
            }
        }
        
       OutboxMessage message;
        try
        {
            message = await _messagesRepo.EnqueueAsync(
                eventType: request.EventType,
                payload: request.Payload,
                subjectKey: request.SubjectKey,
                idempotencyKey: null,
                extraHeadersJson: request.ExtraHeadersJson,
                ct: ct);

        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to enqueue message.");
            return Conflict(new { error = ex.Message });
        }

        await _deliveriesRepo.CreateDeliveriesForMessageAsync(message, ct);
        await _uowRepo.SaveChangesAsync(ct);

        var summary = MapMessageSummary(message);
        return CreatedAtAction(nameof(GetMessageById), new { id = message.Id }, summary);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OutboxMessageResponse>> GetMessageById(Guid id, CancellationToken ct)
    {
        var message = await _messagesRepo.GetByIdAsync(id, ct);
        if (message is null) return NotFound();

        return Ok(MapMessageSummary(message));
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> List([FromQuery] string? subjectKey, [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 200);

        if (!string.IsNullOrWhiteSpace(subjectKey))
        {
            var filtered = await _messagesRepo.ListBySubjectAsync(subjectKey!, skip, take, ct);
            return Ok(filtered.Select(MapMessageSummary));
        }

        var items = await _messagesRepo.ListAsync(skip, take, ct);
        return Ok(items.Select(MapMessageSummary));
    }

    [HttpGet("{id:guid}/deliveries")]
    public async Task<ActionResult<IEnumerable<object>>> ListDeliveries(Guid id, [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 200);
        var deliveries = await _deliveriesRepo.ListByMessageAsync(id, skip, take, ct);

        var result = deliveries.Select(delivery => new
        {
            delivery.Id,
            delivery.OutboxMessageId,
            delivery.SubscriptionId,
            delivery.Status,
            delivery.AttemptCount,
            delivery.NextAttemptUtc,
            delivery.LastAttemptUtc,
            delivery.LastStatusCode,
            delivery.LastError,
            delivery.CreatedAtUtc,
            delivery.UpdatedAtUtc
        });

        return Ok(result);
    }


}