using Juice.EventBus;
using Juice.Workflows.Api.Contracts.IntegrationEvents.Events;
using Juice.Workflows.EF;
using Juice.XUnit;
using Microsoft.Extensions.Configuration;

namespace Juice.Workflows.Tests
{
    public class IntegrationTests
    {
        private ITestOutputHelper _output;

        public IntegrationTests(ITestOutputHelper output)
        {
            _output = output;
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        }
        private DependencyResolver CreateResolver(Action<IServiceCollection, IConfiguration> configure)
        {
            var resolver = DependencyResolver.Create((services, configuration) =>
            {
                services.AddLocalization(options => options.ResourcesPath = "Resources");

                services.AddDefaultStringIdGenerator();

                services.AddSingleton(provider => _output);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

                services.AddWorkflowServices()
                    .AddInMemoryReposistories();
                services.RegisterNodes(typeof(OutcomeBranchUserTask));

                services.AddMediatR(options =>
                {
                    options.RegisterServicesFromAssemblyContaining<StartEvent>();
                    options.RegisterServicesFromAssemblyContaining<TimerEventStartDomainEventHandler>();
                    options.AddIdempotencyRequestBehavior();
                    options.AddWorkflowApiServices();
                });


                services
                    .AddMessaging()
                    .AddIdempotencyRedis(redis =>
                    {
                        redis.ConnectionString = configuration.GetConnectionString("Redis");
                    })
                    .AddOutbox()
                    .AddPublishingPolicies(configuration.GetSection("EventBus:PublishingPolicies"))
                    .AddEventBus()
                        .AddRabbitMQ(cfg =>
                        {
                            cfg.AddConnection(name: "rabbitmq", configuration.GetSection("EventBus:Connections:RabbitMQ"))
                                .AddProducer("rabbitmq", "rabbitmq");
                            ;
                        });


                services.AddSingleton<EventQueue>();

                configure(services, configuration);

            }, default);
            return resolver;
        }
        [IgnoreOnCIFact(DisplayName = "Send topic event"), TestPriority(800)]
        [InitializeMessageContext]
        public async Task Send_topic_event_Async()
        {
            _output.WriteLine("THIS TEST RUN WITH Juice.Workflows.Tests.Host TOGETHER");
            var resolver = CreateResolver((services, configuration) =>
            {
                services.AddMessaging()
                    .AddEventBus().AddPublishingServices();
            });

            using var scope = resolver.ServiceProvider.CreateScope();
            var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();

            await eventBus.PublishAsync(new MessageCatchIntegrationEvent("wfcatch.uploaded.media.final", default, "89czp0dd01r4b72zr19fc61jkr",
                true, new System.Collections.Generic.Dictionary<string, object?> { { "Transfered", "Success" } }), "Workflows");

            await Task.Delay(TimeSpan.FromSeconds(1));
        }
    }
}
