namespace Outbox.Api.Options
{
    public class OutboxDispatcherOptions
    {
        public int LeaseBatchSize { get; set; } = 50;
        public int LeaseDurationSeconds { get; set; } = 300;
        public int LoopDelayMilliseconds { get; set; } = 2000;
        public int DefaultMaxAttempts { get; set; } = 5;
        public int MaxBackoffSeconds { get; set; } = 600;
        public int BackoffBaseSeconds { get; set; } = 5;
    }
}