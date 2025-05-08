using Concurrent.Kafka.Core.Options;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using KafkaFlow;
using KafkaFlow.Configuration;
using KafkaFlow.Serializer;

namespace Concurrent.Kafka.Handles;
public static class ProducerInjectionHandle
{
    /// <summary>
    /// Adds a producer to the Kafka cluster configuration builder through cluster action call.
    /// </summary>
    /// <typeparam name="T">The type of the message to be produced.</typeparam>
    /// <param name="cluster">The Kafka cluster configuration builder.</param>
    /// <param name="options">The producer configuration options.</param>
    /// <returns>The updated Kafka cluster configuration builder.</returns>
    public static IClusterConfigurationBuilder Handle<T>
    (
        IClusterConfigurationBuilder cluster,
        ProducerConfig options,
        SchemaRegistryConfig? schemaRegistryConfig = null
    ) where T : class
    {
        cluster.AddProducer<T>(producer =>
        {
            producer.DefaultTopic(options.Destination)
                    .AddMiddlewares(m =>
                    {
                        switch (options.SerailizationFormat)
                        {
                            case SerailizationFormat.ProtoBuf:
                                if (schemaRegistryConfig != null)
                                {
                                    m.AddSchemaRegistryProtobufSerializer(
                                       new ProtobufSerializerConfig()
                                       {
                                           SubjectNameStrategy = Confluent.SchemaRegistry.SubjectNameStrategy.Topic
                                       }
                                    );
                                }
                                else
                                    m.AddSerializer<ProtobufNetSerializer>();
                                break;
                            case SerailizationFormat.Avro:
                                if (schemaRegistryConfig != null)
                                {
                                    m.AddSchemaRegistryAvroSerializer(
                                       new AvroSerializerConfig()
                                       {
                                           SubjectNameStrategy = Confluent.SchemaRegistry.SubjectNameStrategy.Topic
                                       }
                                    );
                                }
                                else
                                    throw new Exception("No support of avro without Confluent");
                                break;
                        }
                    });

        });
        return cluster;
    }
}