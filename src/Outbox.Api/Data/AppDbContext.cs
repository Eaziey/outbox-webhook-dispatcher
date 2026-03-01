using Microsoft.EntityFrameworkCore;
using Outbox.Api.Entities;

namespace Outbox.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<DeliveryAttempt> DeliveryAttempts => Set<DeliveryAttempt>();
    public DbSet<OutboxDelivery> OutboxDeliveries => Set<OutboxDelivery>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // OutboxMessage
        modelBuilder.Entity<OutboxMessage>().HasKey(m => m.Id);
        modelBuilder.Entity<OutboxMessage>().HasIndex(x => x.CreatedAtUtc);
        modelBuilder.Entity<OutboxMessage>().HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique();
        modelBuilder.Entity<OutboxMessage>().HasIndex(x => new { x.TenantId, x.SubjectKey });
        modelBuilder.Entity<OutboxMessage>().HasIndex(x => new { x.TenantId, x.CreatedAtUtc });

        // OutboxDelivery
        modelBuilder.Entity<OutboxDelivery>().HasIndex(x => new { x.Status, x.NextAttemptUtc });
        modelBuilder.Entity<OutboxDelivery>().HasIndex(x => new { x.OutboxMessageId, x.SubscriptionId }).IsUnique();
        modelBuilder.Entity<OutboxDelivery>().HasIndex(x => new { x.TenantId, x.Status, x.NextAttemptUtc });
        modelBuilder.Entity<OutboxDelivery>().HasIndex(x => new { x.TenantId, x.SubscriptionId, x.Status });
        
        modelBuilder.Entity<OutboxDelivery>()
            .HasOne(d => d.OutboxMessage)
            .WithMany(m => m.Deliveries)
            .HasForeignKey(d => d.OutboxMessageId)
            .HasPrincipalKey(m => m.Id)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<OutboxDelivery>()
            .HasOne(d => d.Subscription)
            .WithMany()
            .HasForeignKey(d => d.SubscriptionId)
            .OnDelete(DeleteBehavior.Restrict);


        // DeliveryAttempt
        modelBuilder.Entity<DeliveryAttempt>().HasIndex(x => new { x.OutboxDeliveryId, x.AttemptedAtUtc });
        modelBuilder.Entity<DeliveryAttempt>().HasIndex(x => x.TenantId);

        modelBuilder.Entity<DeliveryAttempt>()
            .HasOne(a => a.OutboxDelivery)
            .WithMany(d => d.Attempts)
            .HasForeignKey(a => a.OutboxDeliveryId)
            .OnDelete(DeleteBehavior.Cascade);

        // Subscription
         modelBuilder.Entity<Subscription>().HasIndex(x => new { x.TenantId, x.IsActive });
    }
}