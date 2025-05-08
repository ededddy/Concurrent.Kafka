using System.Text;
using KafkaFlow;
using SchemaRegistry;

namespace Concurrent.Kafka.Minimal;

public class TestHandler : IMessageHandler<KafkaFlowMessage>
{
    private readonly ILogger _logger;
    // mongodb related
    // private readonly IUnitOfWork _uow;
    // private readonly ITestKafkaFlowRepository _kafkaflowRepo;
    public TestHandler(
        ILogger<TestHandler> logger
    // mongodb related
    // ,IUnitOfWork uow,
    // ITestKafkaFlowRepository kafkaFlowRepository
    )
    {
        _logger = logger;
        // mongodb related
        //_uow = uow;
        //_kafkaflowRepo = kafkaFlowRepository;
    }

    public async Task Handle(IMessageContext context, KafkaFlowMessage message)
    {
        _logger.LogInformation("Message handled > Partition: {0} | Offset: {1} | Key: {2} | Message: {3}",
                            context.ConsumerContext.Partition,
                            context.ConsumerContext.Offset,
                           // Assume the key is string from another .NET program
                           Encoding.UTF8.GetString(context.Message.Key as byte[]),
                            message
                        );

        try
        {
            // mongoDB related
            // please implement the related UnitOfWork and mongod db entity & repository
            // var payload = new KafkaFlowMsg()
            // {
            //     Id = Guid.NewGuid().ToString(),
            //     CreatedTime = DateTime.UtcNow
            // };
            // await _kafkaflowRepo.AddAsync(payload);
            // await _uow.CommitAsync();
            return;

        }
        catch (Exception e)
        {
            _logger.LogError(e.Message);
            throw;
        }


    }
}
