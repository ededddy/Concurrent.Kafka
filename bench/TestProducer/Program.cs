using Concurrent.Kafka.Core.DependencyInjection;
using Concurrent.Kafka.Core.Options;
using Concurrent.Kafka.Handles;
using KafkaFlow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchemaRegistry;

var services = new ServiceCollection();

var builder = new ConfigurationBuilder();

services.AddLogging();
builder.AddJsonFile("./appsettings.json");
var configuration = builder.Build();

var config = new KafkaOptions();
configuration.Bind("ConsumerSetting", config);

services.AddKafkaBus(
    config,
    cluster =>
        ProducerInjectionHandle.Handle<KafkaFlowMessage>(cluster, config.Producers["KafkaflowProducer"], config.SchemaRegistryConfig)
);

var provider = services.BuildServiceProvider();

var bus = provider.CreateKafkaBus();
await bus.StartAsync();

var producer = provider.GetRequiredService<IMessageProducer<KafkaFlowMessage>>();

while (true)
{
    Console.WriteLine("Number of messages to produce or exit");
    var input = Console.ReadLine()!.ToLower();
    switch (input)
    {
        case var _ when int.TryParse(input, out var count):
            for (var i = 0; i < count; i++)
            {
                await producer.ProduceAsync(Guid.NewGuid().ToString(),
                new KafkaFlowMessage()
                {
                    Message = Guid.NewGuid().ToString(),
                    Code = 64
                }
                );
            }
            break;
        case "exit":
            await bus.StopAsync();
            return;
    }

}

