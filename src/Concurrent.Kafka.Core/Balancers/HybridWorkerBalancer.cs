using KafkaFlow;
using KafkaFlow.Clusters;
using KafkaFlow.Configuration;
using KafkaFlow.Consumers;

namespace Concurrent.Kafka.Core;

/// <summary>
///     Calculate the workers base on the Consumer Lag and System Utilization rate
/// </summary>
/// <remarks>
///     ConsumerLagWorkerBalancer is not exposed so we copied the official implentation
/// </remarks>
public class HybridWorkerBalancer
{
    private readonly int DefaultWorkersCount = 1;
    private readonly ILogHandler _logHandler;
    private readonly IClusterManager _clusterManager;
    private readonly IConsumerAccessor _consumerAccessor;
    private readonly int _totalWorkers;
    private readonly int _minInstanceWorkers;
    private readonly int _maxInstanceWorkers;

    public HybridWorkerBalancer(
        ILogHandler logHandler,
        IClusterManager clusterManager,
        IConsumerAccessor consumerAccessor,
        int totalWorkers,
        int minInstanceWorkers,
        int maxInstanceWorkers)
    {
        _logHandler = logHandler;
        _clusterManager = clusterManager;
        _consumerAccessor = consumerAccessor;
        _totalWorkers = totalWorkers;
        _minInstanceWorkers = minInstanceWorkers;
        _maxInstanceWorkers = maxInstanceWorkers;
    }

    #region From ConsumerLagWorkerBalancer
    private static long CalculateMyPartitionsLag(
        WorkersCountContext context,
        IEnumerable<(string Topic, int Partition, long Lag)> partiionsLag)
    {
        return partiionsLag
        .Where(
            partitionLag => context.AssignedTopicsPartitions.Any(
                topic => topic.Name == partitionLag.Topic
                        && topic.Partitions.Any(p => p == partitionLag.Partition)
            )
        ).Sum(partitionLag => partitionLag.Lag);
    }

    private static IReadOnlyList<(string Topic, int Partition, long Lag)> CalculatePartitionsLag(
        IEnumerable<(string Topic, int Partition, long Offset)> lastOffsets,
        IEnumerable<TopicPartitionOffset> currentPartitionsOffset)
    {
        return lastOffsets
            .Select(last =>
            {
                var currentOffset = currentPartitionsOffset
                    .Where(current => current.Topic == last.Topic && current.Partition == last.Partition)
                    .Select(current => current.Offset)
                    .FirstOrDefault();

                var lastOffset = Math.Max(0, last.Offset);
                currentOffset = Math.Max(0, currentOffset);

                return (last.Topic, last.Partition, lastOffset - currentOffset);
            })
            .ToList();
    }

    private IEnumerable<(string TopicName, int Partition, long Offset)> GetPartitionsLastOffset(
        string consumerName,
        IEnumerable<(string Name, TopicMetadata? Metadata)> topicsMetadata)
    {
        var consumer = _consumerAccessor[consumerName];

        return topicsMetadata
        .SelectMany(
            topic => topic.Metadata.Partitions.Select(
                partition => (
                    topic.Name,
                    partition.Id,
                    consumer
                        .QueryWatermarkOffsets(new(topic.Name, new(partition.Id)), TimeSpan.FromSeconds(30))
                        .High
                        .Value
                    )));
    }

    private async Task<IReadOnlyList<(string Name, TopicMetadata Metadata)>> GetTopicsMetadataAsync(
        WorkersCountContext context)
    {
        var topicsMetadata = new List<(string Name, TopicMetadata Metadata)>(context.AssignedTopicsPartitions.Count);

        foreach (var topic in context.AssignedTopicsPartitions)
            topicsMetadata.Add((topic.Name, await _clusterManager.GetTopicMetadataAsync(topic.Name).ConfigureAwait(false)));

        return topicsMetadata;
    }
    #endregion

    private async Task<int> CalculateAsync(WorkersCountContext context)
    {
        try
        {

            if (!context.AssignedTopicsPartitions.Any())
            {
                return DefaultWorkersCount;
            }

            // TODO: Add system utilization calculation logic
            var gcMemoryInfo = GC.GetGCMemoryInfo();
            var installedMemoryKb = gcMemoryInfo.TotalAvailableMemoryBytes / 1024;
            var usedMemoryKb = GC.GetTotalMemory(true) / 1024;
            var availableMemoryKb = installedMemoryKb - usedMemoryKb;

            // using var currentProcess = Process.GetCurrentProcess();

            // var memory = currentProcess.WorkingSet64;
            // var maxMemory = currentProcess.MaxWorkingSet;

            decimal memRatio = Convert.ToDecimal((double)availableMemoryKb / (double)installedMemoryKb);
            _logHandler.Info($"mem : {availableMemoryKb} kbytes | max mem : {installedMemoryKb} kbytes", null);


            var topicsMetadata = await GetTopicsMetadataAsync(context);

            var lastOffsets = this.GetPartitionsLastOffset(context.ConsumerName, topicsMetadata);

            var partionsOffset = await _clusterManager.GetConsumerGroupOffsetsAsync(
                context.ConsumerGroupId,
                context.AssignedTopicsPartitions.Select(tp => tp.Name)
            );

            var partitionLag = CalculatePartitionsLag(lastOffsets, partionsOffset);
            var instanceLag = CalculateMyPartitionsLag(context, partitionLag);

            decimal totalConsumerLag = partitionLag.Sum(p => p.Lag);

            var consumerLagRatio = instanceLag / Math.Max(1, totalConsumerLag);

            // TODO this is experimental
            // averages out consumer lag and memory usage.
            // var ratio = (consumerLagRatio + memRatio) / 2;

            var ratio = consumerLagRatio >= memRatio
                        ? consumerLagRatio
                        : memRatio;

            _logHandler.Info($"mem ratio: {memRatio} | lag ratio {consumerLagRatio} | final : {ratio}", null);

            var workers = (int)Math.Round(_totalWorkers * ratio);

            workers = Math.Min(workers, _maxInstanceWorkers);
            workers = Math.Max(workers, _minInstanceWorkers);

            return workers;

        }
        catch (Exception e)
        {
            _logHandler.Error("Error calculating new workers count, using 1 as fallback.",
            e,
            new
            {
                context.ConsumerName
            }
            );
        }
        return DefaultWorkersCount;
    }

    public async Task<int> GetWorkersCountAsync(WorkersCountContext context)
    {
        var workers = await this.CalculateAsync(context).ConfigureAwait(false);

        _logHandler.Info("New workers count calculated", new
        {
            Workers = workers,
            Consumer = context.ConsumerName
        });

        return workers;
    }

}
