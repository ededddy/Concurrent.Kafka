using Concurrent.Kafka.Core.Options;
using Confluent.SchemaRegistry;
using KafkaFlow;
using KafkaFlow.Clusters;
using KafkaFlow.Configuration;
using KafkaFlow.Consumers;
using KafkaFlow.Consumers.DistributionStrategies;
using KafkaFlow.Retry;
using KafkaFlow.Serializer;

namespace Concurrent.Kafka.Core.Handles;

public static class ConsumerInjectionHandle
{
    /// <summary>
    /// Adds a consumer to the Kafka cluster configuration builder through cluster action call.
    /// </summary>
    /// <typeparam name="T">The message handler type that implements <see cref="IMessageHandler"/>.</typeparam>
    /// <param name="cluster">The cluster configuration builder.</param>
    /// <param name="options">The consumer configuration options.</param>
    /// <param name="calculator">A function to calculate the number of workers dynamically.</param>
    /// <param name="workerBalanceInterval">The interval for worker balancing.</param>
    /// <returns>The updated cluster configuration builder.</returns>
    /// <exception cref="Exception">Thrown if no consumer topic or group is configured.</exception>
    public static IClusterConfigurationBuilder Handle<T>(
        IClusterConfigurationBuilder cluster,
        ConsumerConfig options,
        SchemaRegistryConfig? schemaRegistryConfig = null,
        Func<WorkersCountContext, IDependencyResolver, Task<int>>? calculator = null,
        TimeSpan? workerBalanceInterval = null
    )
        where T : class, IMessageHandler
    {
        var concurrentConfig = options.ConcurrentConfig;
        cluster.AddConsumer(consumer =>
        {
            if (options.Topics.Length == 0)
                throw new Exception("No Consumer Topic configured.");

            if (options.GroupId == null)
                throw new Exception("No Consumer Group configured.");

            #region Worker and Balancer
            if (!concurrentConfig.WithDynamicWorkersBalancer)
                consumer.WithWorkersCount(concurrentConfig.WorkersCount);
            else
            {
                if (calculator != null)
                    consumer.WithWorkersCount(
                        calculator,
                        workerBalanceInterval != null
                            ? (TimeSpan)workerBalanceInterval
                            : TimeSpan.FromMinutes(1)
                    );
                else
                    //  consumer.WithWorkersCount(
                    //      (WorkersCountContext context, IDependencyResolver resolver) =>
                    //          new HybridWorkerBalancer(
                    //              resolver.Resolve<ILogHandler>(),
                    //              resolver.Resolve<IClusterManager>(),
                    //              resolver.Resolve<IConsumerAccessor>(),
                    //              totalWorkers: concurrentConfig.TotalWorkers,
                    //              minInstanceWorkers: concurrentConfig.MinInstanceWorkers,
                    //              maxInstanceWorkers: concurrentConfig.MaxInstanceWorkers
                    //          ).GetWorkersCountAsync(context),
                    //      evaluationInterval: workerBalanceInterval != null
                    //          ? (TimeSpan)workerBalanceInterval
                    //          : TimeSpan.FromSeconds(30)
                    //  );
                    consumer.WithConsumerLagWorkerBalancer(
                        totalWorkers: concurrentConfig.TotalWorkers,
                        minInstanceWorkers: concurrentConfig.MinInstanceWorkers,
                        maxInstanceWorkers: concurrentConfig.MaxInstanceWorkers,
                        workerBalanceInterval != null
                            ? (TimeSpan)workerBalanceInterval
                            : TimeSpan.FromMinutes(1));
            }
            #endregion

            #region DistributionStrategy
            switch (concurrentConfig.DistributionStrategy)
            {
                case DistributionStrategy.BytesSum:
                    consumer.WithWorkerDistributionStrategy<BytesSumDistributionStrategy>();
                    break;
                case DistributionStrategy.ParitionKey:
                    consumer.WithWorkerDistributionStrategy<PartitionKeyDistributionStrategy>();
                    break;
                case DistributionStrategy.FreeWorker:
                    consumer.WithWorkerDistributionStrategy<FreeWorkerDistributionStrategy>();
                    break;
            }
            #endregion

            #region Basic
            if (options.Topics.Length == 1)
                consumer.Topic(options.Topics.First());
            else
                consumer.Topics(options.Topics);

            consumer
                .WithName(options.Name)
                .WithGroupId(options.GroupId)
                .WithAutoOffsetReset(options.AutoOffsetReset)
                .WithBufferSize(options.BufferSize)
                .WithMaxPollIntervalMs(options.MaxPollIntervalMs);
            #endregion

            #region MiddleWare
            consumer.AddMiddlewares(m =>
            {
                #region Retry
                // TODO: Add Durable Retry
                switch (options.RetryPolicy)
                {
                    case KafkaRetryPolicy.Forever:
                        m.RetryForever(cfg =>
                        {
                            cfg.HandleAnyException()
                                .WithTimeBetweenTriesPlan(options.RetryIntervals);
                        });
                        break;
                    case KafkaRetryPolicy.Simple:
                        m.RetrySimple(cfg =>
                        {
                            cfg.HandleAnyException()
                                .TryTimes(9999999)
                                .WithTimeBetweenTriesPlan(options.RetryIntervals);
                        });
                        break;
                }
                #endregion

                #region SerializationConfig
                switch (options.SerializationFormat)
                {
                    case SerailizationFormat.ProtoBuf:
                        if (schemaRegistryConfig != null)
                            m.AddSchemaRegistryProtobufDeserializer();
                        else
                            m.AddDeserializer<ProtobufNetDeserializer>();

                        m.AddTypedHandlers(h =>
                        {
                            h.WithHandlerLifetime(InstanceLifetime.Scoped)
                                .AddHandler<T>()
                                .WhenNoHandlerFound(context =>
                                {
                                    Console.WriteLine(
                                        "Message not handled > Partition: {0} | Offset: {1}",
                                        context.ConsumerContext.Partition,
                                        context.ConsumerContext.Offset
                                    );
                                });
                        });
                        break;
                    case SerailizationFormat.Avro:

                        if (schemaRegistryConfig != null)
                            m.AddSchemaRegistryAvroDeserializer();
                        else
                            m.AddDeserializer<NewtonsoftJsonDeserializer>();

                        m.AddTypedHandlers(h =>
                        {
                            h.WithHandlerLifetime(InstanceLifetime.Scoped)
                                .AddHandler<T>()
                                .WhenNoHandlerFound(context =>
                                {
                                    Console.WriteLine(
                                        "Message not handled > Partition: {0} | Offset: {1}",
                                        context.ConsumerContext.Partition,
                                        context.ConsumerContext.Offset
                                    );
                                });
                        });
                        break;
                }
                #endregion
            });
            #endregion
        });
        return cluster;
    }
}

