using Juice.Timers.Api.IntegrationEvents.Events;
using Juice.Workflows.Api.Domain.CommandHandlers;
using Juice.Workflows.Bpmn;
using Juice.Workflows.Domain.AggregatesModel.DefinitionAggregate;
using Juice.Workflows.EF;
using Juice.Workflows.Extensions;
using Juice.Workflows.Yaml;
using Juice.XUnit;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;

namespace Juice.Workflows.Tests
{
    public class ComplexProcessTests
    {
        private ITestOutputHelper _output;

        public ComplexProcessTests(ITestOutputHelper output)
        {
            _output = output;
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        }
        #region Init
        [IgnoreOnCIFact(DisplayName = "Init infra"), TestPriority(999)]
        public async Task InitInfraAsync()
        {
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };
            resolver.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration(GetType().Assembly);
                services.AddSingleton(_output);
                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

                services.AddEventBus()
                    .AddRabbitMQ(cfg =>
                    {
                        cfg.AddConnection(name: "rabbitmq", configuration.GetSection("EventBus:Connections:RabbitMQ"))
                            .AddInfrastructureTopology("rabbitmq", icfg =>
                            {
                                icfg.DeclareExchange("x.workflow.integration", ExchangeType.Topic)
                                    .DeclareQueue("x_workflow_queue")
                                    .BindQueue("x_workflow_queue", "x.workflow.integration", "wfthrow.#")
                                    .BindQueue("x_workflow_queue", "x.workflow.integration", "wfcatch.#")
                                    .DeclareQueue("testhost_workflow_queue")
                                    .BindQueue("testhost_workflow_queue", "x.workflow.integration", "wfthrow.#")
                                    .BindQueue("testhost_workflow_queue", "x.workflow.integration", "wfcatch.#")
                                    .BindQueue("testhost_workflow_queue", "x.timer.integration", "timer.expired.workflow")
                                    ;

                            });
                    });
            });
            var serviceProvider = resolver.ServiceProvider;
            await serviceProvider.InitRabbitMQInfrastructureAsync();
        }

        [IgnoreOnCITheory(DisplayName = "Migrate Outbox"), TestPriority(999)]
        [InlineData("SqlServer")]
        [InlineData("PostgreSQL")]
        public async Task MigrateOutboxAsync(string provider)
        {
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };
            resolver.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration(GetType().Assembly);
                services.AddSingleton(_output);
                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

                services.AddOutboxMigrations<WorkflowDbContext>(configuration, options => {
                    options.DatabaseProvider = provider;
                    options.ConnectionName = provider switch
                    {
                        "SqlServer" => "SqlServerConnection",
                        "PostgreSQL" => "PostgreConnection",
                        _ => throw new NotSupportedException($"Unsupported provider: {provider}")
                    };
                    options.Schema = "Workflows";
                });
            });
            var serviceProvider = resolver.ServiceProvider;
            await serviceProvider.MigrateOutboxAsync<WorkflowDbContext>();
        }


        #endregion

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
                });

                services
                    .AddMessaging()
                    .AddIdempotencyRedis(redis =>
                    {
                        redis.ConnectionString = configuration.GetConnectionString("Redis");
                    })
                    .AddWorkflowOutbox()
                    .AddPublishingPolicies(configuration.GetSection("EventBus:PublishingPolicies"));

                services.AddSingleton<EventQueue>();

                configure(services, configuration);

            }, default);
            return resolver;
        }

        /*
         * Should print workflow visualization
         * 
         *                | P-KB           ---------------          ---------------              |         ---------------
( )----0----><+>----1---->|   ( )----3---->|      KB      |---4---->|  Approve KB  |---8---->()) |--16---->| Approve Grph |--17----><+>---18---->())
              |           |_               ---------------          ---------------           __ |         ---------------           ^
              |           ---------------                       ---------------          ---------------                       ------|'-------
              '-----2---->|    Editing   |---5----><+>----6---->|      WEB     |---9---->|  Approve Vid |--11----><+>---12---->|    Copy PS   |
                          ---------------           |           ---------------          ---------------           |           ------'--------
                                                    |           ---------------                                    |           -------'-------
                                                    '-----7---->|    Social    |----------------10-----------------'----13---->|    Publish   |
                                                                ---------------                                                ---------------

         */

        [Fact(DisplayName = "Should select all branches")]
        public async Task Should_select_all_paths_Async()
        {
            var workflowId = StringIdGenerator.Instance.GenerateRandomId(6);

            var resolver = CreateResolver((services, configuration) =>
            {
                services.RegisterWorkflow(workflowId, builder =>
                {
                    builder
                        .Start()
                        .Parallel("p1")
                            .Fork().SubProcess("P-KB", subBuilder =>
                            {
                                subBuilder.Start().Then<UserTask>("KB").Then<UserTask>("Approve KB").End();
                            }, default, default).Then<UserTask>("Approve Grph").Seek("p1")
                            .Fork().Then<UserTask>("Editing")
                                .Parallel("p2")
                                    .Fork().Then<ServiceTask>("WEB").Then<UserTask>("Approve Vid")
                                    .Fork().Then<ServiceTask>("Social")
                                    .Merge()
                                        .Fork().Then<ServiceTask>("Copy PS")
                                        .Fork().Then<ServiceTask>("Publish")
                                        .Merge("Copy PS", "Publish", "Approve Grph")
                        .End()
                        ;
                });
            });

            var result = await WorkflowTestHelper.ExecuteAsync(resolver.ServiceProvider, _output, workflowId);

            result.Should().NotBeNull();
            _output.WriteLine(ContextPrintHelper.Visualize(result.Context));
        }

        [Fact(DisplayName = "Should not timeout")]
        public async Task Should_not_timeout_Async()
        {
            var workflowId = StringIdGenerator.Instance.GenerateRandomId(6);

            var resolver = CreateResolver((services, configuration) =>
            {
                services.RegisterNodes(typeof(FailureTask));

                services.RegisterWorkflow(workflowId, builder =>
                {
                    builder
                        .Start()
                        .Parallel("p1")
                            .Fork().SubProcess("P-KB", subBuilder =>
                            {
                                subBuilder.Start().Then<UserTask>("KB").Then<ServiceTask>("Convert KB").End();
                            }, default).Then<UserTask>("Approve Grph")
                            .Seek("P-KB").Attach<BoundaryTimerEvent>("Error").Then<SendTask>("Author inform").Terminate()
                            .Seek("p1")
                            .Fork().Then<UserTask>("Editing")
                                   .Wait<TimerIntermediateCatchEvent>()
                                .Parallel("p2")
                                    .Fork().Then<ServiceTask>("WEB").Then<UserTask>("Approve Vid")
                                    .Fork().Then<ServiceTask>("Social")
                                    .Merge()
                                        .Fork().Then<ServiceTask>("Copy PS")
                                        .Fork().Then<ServiceTask>("Publish")
                                        .Merge("Copy PS", "Publish", "Approve Grph")
                        .End()
                        ;
                });
            });

            var result = await WorkflowTestHelper.ExecuteAsync(resolver.ServiceProvider, _output, workflowId,
                new System.Collections.Generic.Dictionary<string, object?> { });

            result.Should().NotBeNull();
            _output.WriteLine(ContextPrintHelper.Visualize(result.Context));
            result.Status.Should().Be(WorkflowStatus.Finished);
        }

        [Fact(DisplayName = "Should timeout terminate")]
        public async Task Should_timeout_Async()
        {
            var workflowId = StringIdGenerator.Instance.GenerateRandomId(6);

            var resolver = CreateResolver((services, configuration) =>
            {
                services.RegisterNodes(typeof(FailureTask));

                services.RegisterWorkflow(workflowId, builder =>
                {
                    builder
                        .Start()
                        .Parallel("p1")
                            .Fork().SubProcess("P-KB", subBuilder =>
                            {
                                subBuilder.Start().Then<UserTask>("KB").Then<ServiceTask>("Convert KB").End();
                            }, default, default).Then<UserTask>("Approve Grph")
                            .Seek("P-KB").Attach<BoundaryTimerEvent>("Error").Then<SendTask>("Author inform").Terminate()
                            .Seek("p1")
                            .Fork().Then<UserTask>("Editing")
                                .Parallel("p2")
                                    .Fork().Then<ServiceTask>("WEB").Then<UserTask>("Approve Vid")
                                    .Fork().Then<ServiceTask>("Social")
                                    .Merge()
                                        .Fork().Then<ServiceTask>("Copy PS")
                                        .Fork().Then<ServiceTask>("Publish")
                                        .Merge("Copy PS", "Publish", "Approve Grph")
                        .End()
                        ;
                });
            });

            var result = await WorkflowTestHelper.ExecuteAsync(resolver.ServiceProvider, _output, workflowId,
                new System.Collections.Generic.Dictionary<string, object?> { { "TaskStatus", WorkflowStatus.Faulted } });

            result.Should().NotBeNull();
            _output.WriteLine(ContextPrintHelper.Visualize(result.Context));
            result.Status.Should().Be(WorkflowStatus.Aborted);
        }

        [Fact(DisplayName = "Should failure terminate")]
        public async Task Should_terminate_Async()
        {
            var workflowId = StringIdGenerator.Instance.GenerateRandomId(6);

            var resolver = CreateResolver((services, configuration) =>
            {
                services.AddMediatR(options =>
                {
                    options.RegisterServicesFromAssemblyContaining<StartBoundaryTimerEventCommandHandler>();
                });

                services.RegisterNodes(typeof(FailureTask));

                services.RegisterWorkflow(workflowId, builder =>
                {
                    builder
                        .Start()
                        .Parallel("p1")
                            .Fork().SubProcess("P-KB", subBuilder =>
                            {
                                subBuilder.Start().Then<UserTask>("KB").Then<ServiceTask>("Convert KB").End();
                            }, default, default).Then<UserTask>("Approve Grph")
                            .Seek("P-KB").Attach<BoundaryErrorEvent>("Error").Then<SendTask>("Author inform").Terminate()
                            .Seek("p1")
                            .Fork().Then<UserTask>("Editing")
                                .Parallel("p2")
                                    .Fork().Then<ServiceTask>("WEB").Then<UserTask>("Approve Vid")
                                    .Fork().Then<ServiceTask>("Social")
                                    .Merge()
                                        .Fork().Then<ServiceTask>("Copy PS")
                                        .Fork().Then<ServiceTask>("Publish")
                                        .Merge("Copy PS", "Publish", "Approve Grph")
                        .End()
                        ;
                });
            });

            var result = await WorkflowTestHelper.ExecuteAsync(resolver.ServiceProvider, _output, workflowId,
                new System.Collections.Generic.Dictionary<string, object?> { { "TaskStatus", WorkflowStatus.Faulted } });

            result.Should().NotBeNull();
            _output.WriteLine(ContextPrintHelper.Visualize(result.Context));
            result.Status.Should().Be(WorkflowStatus.Aborted);
        }

        [Fact(DisplayName = "Yaml failure terminate")]
        public async Task Yaml_should_terminate_Async()
        {
            var workflowId = StringIdGenerator.Instance.GenerateRandomId(6);

            var resolver = CreateResolver((services, configuration) =>
            {
                services.RegisterNodes(typeof(FailureTask));
                services.RegisterYamlWorkflows();

                services.RegisterWorkflow(workflowId, builder =>
                {
                    builder
                        .Start()
                        .Parallel("p1")
                            .Fork().SubProcess("P-KB", subBuilder =>
                            {
                                subBuilder.Start().Then<UserTask>("KB").Then<ServiceTask>("Convert KB").End();
                            }, default, default).Then<UserTask>("Approve Grph")
                            .Seek("P-KB").Attach<BoundaryErrorEvent>("Error").Then<SendTask>("Author inform").Terminate()
                            .Seek("p1")
                            .Fork().Then<UserTask>("Editing")
                                .Parallel("p2")
                                    .Fork().Then<ServiceTask>("WEB").Then<UserTask>("Approve Vid")
                                    .Fork().Then<ServiceTask>("Social")
                                    .Merge()
                                        .Fork().Then<ServiceTask>("Copy PS")
                                        .Fork().Then<ServiceTask>("Publish")
                                        .Merge("Copy PS", "Publish", "Approve Grph")
                        .End()
                        ;
                });
            });

            var result = await WorkflowTestHelper.ExecuteAsync(resolver.ServiceProvider, _output, "test",
                new System.Collections.Generic.Dictionary<string, object?> { { "TaskStatus", WorkflowStatus.Faulted } });

            result.Should().NotBeNull();
            _output.WriteLine(ContextPrintHelper.Visualize(result.Context));
            result.Status.Should().Be(WorkflowStatus.Aborted);
        }

        [Fact(DisplayName = "Bpmn should add to db")]
        public async Task Bpmn_should_terminate_Async()
        {
            var workflowId = StringIdGenerator.Instance.GenerateRandomId(6);

            var resolver = CreateResolver((services, configuration) =>
            {
                services.AddWorkflowServices()
                    .RegisterDbWorkflows()
                    .AddInMemoryReposistories();

                services.RegisterNodes(typeof(FailureTask));
                services.RegisterBpmnWorkflows();
            });

            var definitionRepo = resolver.ServiceProvider.GetRequiredService<IDefinitionRepository>();

            {
                var result = await WorkflowTestHelper.ExecuteAsync(resolver.ServiceProvider, _output, "diagram",
                    new System.Collections.Generic.Dictionary<string, object?> { { "TaskStatus", WorkflowStatus.Faulted } });

                result.Should().NotBeNull();
                _output.WriteLine(ContextPrintHelper.Visualize(result.Context));
                result.Status.Should().Be(WorkflowStatus.Aborted);

                var context = result.Context;
                context.ResolvedBy.Should().Be(typeof(Bpmn.Builder.WorkflowContextBuilder).FullName);

                var createResult = await definitionRepo.SaveWorkflowContextAsync(context, "diagram", context.Name!, true, default);
                _output.WriteLine(createResult.ToString());
                createResult.Succeeded.Should().BeTrue();
            }

            {
                var result = await WorkflowTestHelper.ExecuteAsync(resolver.ServiceProvider, _output, "diagram",
                    new System.Collections.Generic.Dictionary<string, object?> { { "TaskStatus", WorkflowStatus.Faulted } });

                result.Should().NotBeNull();
                _output.WriteLine(ContextPrintHelper.Visualize(result.Context));
                result.Status.Should().Be(WorkflowStatus.Aborted);

                var context = result.Context;
                context.ResolvedBy.Should().Be(typeof(Builder.DbWorkflowContextBuilder).FullName);
            }
        }
    }
}
