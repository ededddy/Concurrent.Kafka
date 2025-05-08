using Confluent.SchemaRegistry;
using KafkaFlow.Configuration;

namespace Concurrent.Kafka.Core.Options;

public class KafkaOptions
{
    public string[] ServerUrls { get; set; } = new string[0];

    public bool EnableTracing { get; set; } = false;

    public bool EnableHealthChecks { get; set; } = false;

    public string HealthCheckTopic { get; set; } = "HealthChecks";

    public bool EnableAdmin { get; set; } = false;

    public string AdminMessageTopic { get; set; } = "kafka-flow.admin";

    public string TelemetryTopic { get; set; } = "kafka-flow.admin";

    public SecurityInformation? AuthenticationConfig { get; set; } = null!;

    public Dictionary<string, ConsumerConfig> Consumers { get; set; } = new();

    public Dictionary<string, ProducerConfig> Producers { get; set; } = new();

    public SchemaRegistryConfig? SchemaRegistryConfig { get; set; } = null!;

}