using Concurrent.Kafka.Core.DependencyInjection;
using Concurrent.Kafka.Core.Handles;
using Concurrent.Kafka.Core.Options;
using Concurrent.Kafka.Minimal;
using KafkaFlow;
using KafkaFlow.Admin.Dashboard;


// NOTE:  this .NET benchmark missing out MongoDB Service for Database Read & Write
// Please implement it yourself.

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;


var config = new KafkaOptions();
configuration.Bind("ConsumerSetting", config);

builder.Services.AddKafkaBus(
    config,
    cluster =>
        ConsumerInjectionHandle.Handle<TestHandler>(cluster, config.Consumers["KafkaFlowConsumer"], config.SchemaRegistryConfig)
);

var app = builder.Build();

app.UseKafkaFlowDashboard();

var kafkaBus = app.Services.CreateKafkaBus();
await kafkaBus.StartAsync();

await app.RunAsync();
