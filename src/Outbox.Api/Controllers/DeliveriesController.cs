using Microsoft.AspNetCore.Mvc;
using Outbox.Api.DTOs.Requests;
using Outbox.Api.Entities;
using Outbox.Api.Interfaces.IRepositories;
using Outbox.Api.Tenancy;

namespace Outbox.Api.Controllers;

[ApiController]
[Route("api/deliveries")]
public class DeliveriesController : ControllerBase
{
    
    private readonly IOutboxDeliveryRepository _deliveriesRepo;
    private readonly IUnitOfWork _uowRepo;
    private readonly ITenantContext _tenant;

    public DeliveriesController(IOutboxDeliveryRepository deliveries, IUnitOfWork uow, ITenantContext tenant)
    {
        _deliveriesRepo = deliveries;
        _uowRepo = uow;
        _tenant = tenant;
    }

    // GET: /api/deliveries/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<object>> GetDeliveriesById(Guid id, CancellationToken ct)
    {
        var delivery = await _deliveriesRepo.GetByIdAsync(id, ct);
        if (delivery is null) return NotFound();

        return Ok(new
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
            delivery.TenantId,
            delivery.SubjectKey
        });
    }

    // POST: /api/deliveries/{id}/requeue
    [HttpPost("{id:guid}/requeue")]
    public async Task<ActionResult> Requeue(Guid id, [FromBody] RequeueRequest request, CancellationToken ct){
        
        var existing = await _deliveriesRepo.GetByIdAsync(id, ct);
        if (existing is null) return NotFound();

        await _deliveriesRepo.RequeueAsync(id, request.NextAttemptUtc ?? DateTime.UtcNow, ct);
        await _uowRepo.SaveChangesAsync(ct);
        
        return NoContent();
    }


}

