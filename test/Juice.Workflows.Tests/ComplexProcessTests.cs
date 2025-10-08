using Juice.Workflows.Api;
using Juice.Workflows.Api.Domain.CommandHandlers;
using Juice.Workflows.Bpmn;
using Juice.Workflows.Domain.AggregatesModel.DefinitionAggregate;
using Juice.Workflows.Extensions;
using Juice.Workflows.Yaml;

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
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };
            var workflowId = new DefaultStringIdGenerator().GenerateRandomId(6);
            resolver.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration();

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
                });

                services.RegisterRabbitMQEventBus<IWorkflowEventBus>(configuration.GetSection("RabbitMQ"), options =>
                {
                    options.BrokerName = "workflow_exchange";
                    options.SubscriptionClientName = "juice_wf_xunit_1";
                });

                services.AddSingleton<EventQueue>();

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
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };
            var workflowId = new DefaultStringIdGenerator().GenerateRandomId(6);
            resolver.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration();

                services.AddLocalization(options => options.ResourcesPath = "Resources");

                services.AddDefaultStringIdGenerator();

                services.AddSingleton(provider => _output);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

                services.AddMediatR(options =>
                {
                    options.RegisterServicesFromAssemblyContaining<StartEvent>();
                    options.RegisterServicesFromAssemblyContaining<TimerEventStartDomainEventHandler>();
                });
                services.AddSingleton<EventQueue>();

                services.AddWorkflowServices()
                    .AddInMemoryReposistories();
                services.RegisterNodes(typeof(FailureTask));

                services.RegisterRabbitMQEventBus<IWorkflowEventBus>(configuration.GetSection("RabbitMQ"), options =>
                {
                    options.BrokerName = "workflow_exchange";
                    options.SubscriptionClientName = "juice_wf_xunit_2";
                });

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
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };
            var workflowId = new DefaultStringIdGenerator().GenerateRandomId(6);
            resolver.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration();

                services.AddLocalization(options => options.ResourcesPath = "Resources");

                services.AddDefaultStringIdGenerator();

                services.AddSingleton(provider => _output);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

                services.AddMediatR(options =>
                {
                    options.RegisterServicesFromAssemblyContaining<StartEvent>();
                    options.RegisterServicesFromAssemblyContaining<TimerEventStartDomainEventHandler>();
                });
                services.AddSingleton<EventQueue>();

                services.AddWorkflowServices()
                    .AddInMemoryReposistories();
                services.RegisterNodes(typeof(FailureTask));

                services.RegisterRabbitMQEventBus<IWorkflowEventBus>(configuration.GetSection("RabbitMQ"), options =>
                {
                    options.BrokerName = "workflow_exchange";
                    options.SubscriptionClientName = "juice_wf_xunit_3";
                });

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
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };
            var workflowId = new DefaultStringIdGenerator().GenerateRandomId(6);
            resolver.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration();

                services.AddLocalization(options => options.ResourcesPath = "Resources");

                services.AddDefaultStringIdGenerator();

                services.AddSingleton(provider => _output);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

                services.AddMediatR(options =>
                {
                    options.RegisterServicesFromAssemblyContaining<StartEvent>();
                    options.RegisterServicesFromAssemblyContaining<TimerEventStartDomainEventHandler>();
                    options.RegisterServicesFromAssemblyContaining<StartBoundaryTimerEventCommandHandler>();
                });
                services.AddSingleton<EventQueue>();

                services.RegisterRabbitMQEventBus<IWorkflowEventBus>(configuration.GetSection("RabbitMQ"), options =>
                {
                    options.BrokerName = "workflow_exchange";
                    options.SubscriptionClientName = "juice_wf_xunit_4";
                });

                services.AddWorkflowServices()
                    .AddInMemoryReposistories();
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
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };
            var workflowId = new DefaultStringIdGenerator().GenerateRandomId(6);
            resolver.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration();

                services.AddLocalization(options => options.ResourcesPath = "Resources");

                services.AddDefaultStringIdGenerator();

                services.AddSingleton(provider => _output);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

                services.AddMediatR(options =>
                {
                    options.RegisterServicesFromAssemblyContaining<StartEvent>();
                    options.RegisterServicesFromAssemblyContaining<TimerEventStartDomainEventHandler>();
                });
                services.AddSingleton<EventQueue>();

                services.AddWorkflowServices()
                    .AddInMemoryReposistories();
                services.RegisterNodes(typeof(FailureTask));

                services.RegisterRabbitMQEventBus<IWorkflowEventBus>(configuration.GetSection("RabbitMQ"), options =>
                {
                    options.BrokerName = "workflow_exchange";
                    options.SubscriptionClientName = "juice_wf_xunit_5";
                });

                services.RegisterYamlWorkflows();

                services.RegisterWorkflow("incodewf", builder =>
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
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };
            var workflowId = new DefaultStringIdGenerator().GenerateRandomId(6);
            resolver.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration();

                services.AddLocalization(options => options.ResourcesPath = "Resources");

                services.AddDefaultStringIdGenerator();

                services.AddSingleton(provider => _output);

                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                    .AddTestOutputLogger()
                    .AddConfiguration(configuration.GetSection("Logging"));
                });

                services.AddMediatR(options =>
                {
                    options.RegisterServicesFromAssemblyContaining<StartEvent>();
                    options.RegisterServicesFromAssemblyContaining<TimerEventStartDomainEventHandler>();
                });
                services.AddSingleton<EventQueue>();

                services.AddWorkflowServices()
                    .RegisterDbWorkflows()
                    .AddInMemoryReposistories();

                services.RegisterRabbitMQEventBus<IWorkflowEventBus>(configuration.GetSection("RabbitMQ"), options =>
                {
                    options.BrokerName = "workflow_exchange";
                    options.SubscriptionClientName = "juice_wf_xunit_6";
                });

                services.RegisterNodes(typeof(FailureTask));

                services.RegisterBpmnWorkflows();
            });

            var definitionRepo = resolver.ServiceProvider.GetRequiredService<IDefinitionRepository>();

            {
                WorkflowExecutionResult? result = default;
                try
                {
                    result = await WorkflowTestHelper.ExecuteAsync(resolver.ServiceProvider, _output, "diagram",
                        new System.Collections.Generic.Dictionary<string, object?> { { "TaskStatus", WorkflowStatus.Faulted } });

                    result.Should().NotBeNull();

                    result.Status.Should().Be(WorkflowStatus.Aborted);

                    var context = result.Context;
                    context.ResolvedBy.Should().Be(typeof(Bpmn.Builder.WorkflowContextBuilder).FullName);

                    var createResult = await definitionRepo.SaveWorkflowContextAsync(context, "diagram", context.Name!, true, default);
                    _output.WriteLine(createResult.ToString());
                    createResult.Succeeded.Should().BeTrue();

                }
                finally
                {
                    if (result != null)
                    {
                        _output.WriteLine(ContextPrintHelper.Visualize(result.Context));
                    }
                }
            }
            {
                WorkflowExecutionResult? result = default;
                try
                {
                    result = await WorkflowTestHelper.ExecuteAsync(resolver.ServiceProvider, _output, "diagram",
                        new System.Collections.Generic.Dictionary<string, object?> { { "TaskStatus", WorkflowStatus.Faulted } });

                    result.Should().NotBeNull();

                    result.Status.Should().Be(WorkflowStatus.Aborted);

                    var context = result.Context;
                    context.ResolvedBy.Should().Be(typeof(Builder.DbWorkflowContextBuilder).FullName);

                }
                finally
                {
                    if (result != null)
                    {
                        _output.WriteLine(ContextPrintHelper.Visualize(result.Context));
                    }
                }
            }
        }

    }
}
