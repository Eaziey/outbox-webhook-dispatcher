using Microsoft.AspNetCore.Mvc;
using Outbox.Api.DTOs.Requests;
using Outbox.Api.DTOs.Responses;
using Outbox.Api.Entities;
using Outbox.Api.Interfaces.IRepositories;
using System.Security.Cryptography;

namespace Outbox.Api.Controllers;

[ApiController]
[Route("api/subscriptions")]
public class SubscriptionsController : ControllerBase
{
    private readonly ISubscriptionRepository _subscriptionsRepo;
    private readonly IUnitOfWork _uowRepo;
    private readonly ILogger<SubscriptionsController> _logger;

    public SubscriptionsController(ISubscriptionRepository subscriptions,IUnitOfWork uow,ILogger<SubscriptionsController> logger)
    {
        _subscriptionsRepo = subscriptions;
        _uowRepo = uow;
        _logger = logger;
    }
    private static string GenerateSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    [HttpPost]
    public async Task<ActionResult<SubscriptionResponse>> CreateSubscription([FromBody] CreateSubscriptionRequest request, CancellationToken ct)
    {
        if (!Uri.TryCreate(request.Endpoint, UriKind.Absolute, out var uri))
        {
            return ValidationProblem("Endpoint must be a valid absolute URL.");
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return ValidationProblem("Endpoint must use HTTPS.");
        }

        var secret = string.IsNullOrWhiteSpace(request.Secret) ? GenerateSecret() : request.Secret;
        
        var subscription = new Subscription
        {
            Endpoint = request.Endpoint,
            Secret = secret,
            IsActive = true,
            MaxConcurrency = request.MaxConcurrency
        };

        await _subscriptionsRepo.AddAsync(subscription, ct);
        await _uowRepo.SaveChangesAsync(ct);

        var response = new SubscriptionResponse(
                    subscription.Id, 
                    subscription.Endpoint, 
                    subscription.IsActive, 
                    subscription.CreatedAtUtc,
                    subscription.TenantId,
                    subscription.MaxConcurrency
                );
                
        return CreatedAtAction(nameof(GetSubscriptionById), new { id = subscription.Id }, response);
    }
    
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SubscriptionResponse>> GetSubscriptionById(Guid id, CancellationToken ct)
    {
        var subscription = await _subscriptionsRepo.GetByIdAsync(id, ct);
        if (subscription is null) return NotFound();

        var response = new SubscriptionResponse(
                    subscription.Id, 
                    subscription.Endpoint, 
                    subscription.IsActive, 
                    subscription.CreatedAtUtc,
                    subscription.TenantId,
                    subscription.MaxConcurrency
                );

        return Ok(response);
    }

    [HttpGet]
    public async Task<ActionResult<List<SubscriptionResponse>>> List([FromQuery] bool? isActive, CancellationToken ct)
    {
        var items = await _subscriptionsRepo.ListAsync(isActive, ct);

        var subscriptions = items.Select(sub => new SubscriptionResponse(
            sub.Id, sub.Endpoint, sub.IsActive, sub.CreatedAtUtc, sub.TenantId, sub.MaxConcurrency
        ));
        
        return Ok(subscriptions);

    }

    
// PUT: /api/subscriptions/{id}
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SubscriptionResponse>> UpdateSubscription(Guid id, [FromBody] UpdateSubscriptionRequest request, CancellationToken ct)
    {
        var subscription = await _subscriptionsRepo.GetByIdAsync(id, ct);
        if (subscription is null) return NotFound();

        if (!string.IsNullOrWhiteSpace(request.Endpoint))
        {
            if (!Uri.TryCreate(request.Endpoint, UriKind.Absolute, out var uri))
            {
                return ValidationProblem("Endpoint must be a valid absolute URL.");
            }

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)){
                return ValidationProblem("Endpoint must use HTTPS.");
            }
                
            subscription.Endpoint = request.Endpoint!;
        }

        if (request.IsActive.HasValue) subscription.IsActive = request.IsActive.Value;
        if (request.Secret is not null)
        {
            if (request.Secret.Length < 16)
            {
                return ValidationProblem("Secret must be at least 16 characters.");
            }

            subscription.Secret = request.Secret;
        }

        if (request.MaxConcurrency.HasValue) subscription.MaxConcurrency = request.MaxConcurrency.Value;

        await _subscriptionsRepo.UpdateAsync(subscription, ct);
        await _uowRepo.SaveChangesAsync(ct);

        var response = new SubscriptionResponse(
            subscription.Id, 
            subscription.Endpoint, 
            subscription.IsActive, 
            subscription.CreatedAtUtc, 
            subscription.TenantId, 
            subscription.MaxConcurrency
        );

        return Ok(response);
    }

    // POST: /api/subscriptions/{id}/rotate-secret
    [HttpPost("{id:guid}/rotate-secret")]
    public async Task<ActionResult> RotateSecret(Guid id, CancellationToken ct)
    {
        var subscription = await _subscriptionsRepo.GetByIdAsync(id, ct);
        if (subscription is null) return NotFound();

        subscription.Secret = GenerateSecret();
        await _subscriptionsRepo.UpdateAsync(subscription, ct);
        await _uowRepo.SaveChangesAsync(ct);

        return NoContent();
    }

    // DELETE: /api/subscriptions/{id}  (soft delete)
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var subscription = await _subscriptionsRepo.GetByIdAsync(id, ct);
        if (subscription is null) return NotFound();

        subscription.IsActive = false;
        await _subscriptionsRepo.UpdateAsync(subscription, ct);
        await _uowRepo.SaveChangesAsync(ct);

        return NoContent();
    }

}
