namespace Concurrent.Kafka.Core.Options;

public class ProducerConfig
{
    public string Name { get; set; }

    public string Destination { get; set; } = string.Empty;

    public SerailizationFormat SerailizationFormat { get; set; } = SerailizationFormat.ProtoBuf;
}