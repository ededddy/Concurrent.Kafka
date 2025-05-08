using Concurrent.Kafka.Core.Options;

namespace Concurrent.Kafka.Core;

public class ConcurrentConfig
{
    public DistributionStrategy DistributionStrategy { get; set; } = DistributionStrategy.BytesSum;

    public int WorkersCount { get; set; } = 12;

    public bool WithDynamicWorkersBalancer { get; set; } = false;

    public int TotalWorkers { get; set; } = 50;

    public int MinInstanceWorkers { get; set; } = 1;

    public int MaxInstanceWorkers { get; set; } = 12;

}
