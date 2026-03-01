using Microsoft.EntityFrameworkCore;
using Outbox.Api.Data;
using Outbox.Api.Interfaces.IRepositories;
using Outbox.Api.Interfaces.IServices;
using Outbox.Api.Repositories;
using Outbox.Api.Services;
using Outbox.Api.Tenancy;
using Outbox.Api.Swagger;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=outbox.db");
});

builder.Services.AddHttpClient<IWebhookSender, WebhookSender>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.Configure<Outbox.Api.Options.OutboxDispatcherOptions>(
    builder.Configuration.GetSection("OutboxDispatcher"));
    
// Repositories & UoW
builder.Services.AddScoped<IOutboxMessageRepository, OutboxMessageRepository>();
builder.Services.AddScoped<IOutboxDeliveryRepository, OutboxDeliveryRepository>();
builder.Services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
builder.Services.AddScoped<IDeliveryAttemptRepository, DeliveryAttemptRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Services
builder.Services.AddSingleton<IHmacSigner, HmacSigner>();

// Background worker
builder.Services.AddHostedService<OutboxDispatcherService>();

builder.Services.AddControllers().AddJsonOptions(opt =>
{
    opt.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    opt.JsonSerializerOptions.WriteIndented = true;
});

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.OperationFilter<TenantHeaderOperationFilter>();
});
builder.Services.AddHealthChecks();

//Cors
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

//Tenant
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<ITenantContext>(sp =>
{
    var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
    var httpContext = httpContextAccessor.HttpContext;

    if (httpContext != null)
    {
        // We are in an HTTP request → use header-based tenant
        return new HttpTenantContext(httpContextAccessor);
    }

    // No HTTP context → Background service or other
    return new WorkerTenantContext(tenantId: null);
});



var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();
app.Use(async (ctx, next) =>
{
    var path = ctx.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;

    if (path.StartsWith("/swagger") || path.StartsWith("/health") || path.StartsWith("/favicon") || path.StartsWith("/assets"))
    {
        await next();
        return;
    }

    var tenantId = ctx.Request.Headers["X-Tenant-Id"].FirstOrDefault();

    if (string.IsNullOrWhiteSpace(tenantId))
    {
        ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
        await ctx.Response.WriteAsJsonAsync(new
        {
            error = "tenant_required",
            message = "Provide X-Tenant-Id header."
        });
        return;
    }

    await next();
});
app.MapHealthChecks("/health");
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

await app.RunAsync();
