using KafkaFlow;

namespace Concurrent.Kafka.Core.Options;

public class ConsumerConfig
{
    public string[] Topics { get; set; }

    public string Name { get; set; }

    public string GroupId { get; set; }

    public AutoOffsetReset AutoOffsetReset { get; set; } = AutoOffsetReset.Latest;

    public int BufferSize { get; set; } = 12;

    public int MaxPollIntervalMs { get; set; } = 5_000_000;

    public KafkaRetryPolicy RetryPolicy { get; set; } = KafkaRetryPolicy.Forever;

    public TimeSpan[] RetryIntervals { get; set; } = new[] { TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(1000), };

    public SerailizationFormat SerializationFormat { get; set; } = SerailizationFormat.ProtoBuf;

    public ConcurrentConfig ConcurrentConfig { get; set; } = new();

}

public enum DistributionStrategy
{
    BytesSum, // ByteSumDistributionStrategy 
    ParitionKey, // PartitionKeyDistributionStrategy
    FreeWorker
}

public enum KafkaRetryPolicy
{
    Forever,
    Simple
}

public enum SerailizationFormat
{
    ProtoBuf,
    Avro,
}
