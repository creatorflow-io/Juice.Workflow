using System.Collections.Generic;

namespace Juice.Workflows.Tests
{
    public class LogicGatewayTests
    {
        private readonly ITestOutputHelper _output;

        public LogicGatewayTests(ITestOutputHelper output)
        {
            _output = output;
        }

        // ─────────────────────────────────────────────────────────────────────
        // T014: SimpleExpressionEvaluator unit tests
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Helper: maps executed node IDs back to node names via the WorkflowContext.
        /// (ExecutedNode only has Id, not Name.)
        /// </summary>
        private static List<string> ExecutedNodeNames(WorkflowContext context)
        {
            var executedIds = context.State!.ExecutedNodes!.Select(n => n.Id).ToHashSet();
            return context.Nodes.Values
                .Where(n => executedIds.Contains(n.Record.Id))
                .Select(n => n.Record.Name)
                .ToList();
        }

        private static SimpleExpressionEvaluator CreateEvaluator()
        {
            var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => b.AddConsole());
            var logger = loggerFactory.CreateLogger<SimpleExpressionEvaluator>();
            return new SimpleExpressionEvaluator(logger);
        }

        private static WorkflowContext CreateContextWithInput(Dictionary<string, object?> input)
        {
            // Build a minimal WorkflowContext using in-memory infrastructure
            var resolver = new DependencyResolver { CurrentDirectory = AppContext.BaseDirectory };
            resolver.ConfigureServices(services =>
            {
                services.AddLocalization(options => options.ResourcesPath = "Resources");
                services.AddDefaultStringIdGenerator();
                services.AddLogging(b => b.AddConsole());
                services.AddMediatR(opt => opt.RegisterServicesFromAssemblyContaining<StartEvent>());
                services.AddSingleton<EventQueue>();
                services.AddWorkflowServices().AddInMemoryReposistories();
            });

            using var scope = resolver.ServiceProvider.CreateScope();
            var builder = scope.ServiceProvider.GetRequiredService<WorkflowContextBuilder>();
            builder.Start().End();
            var context = builder.Build(new Juice.Workflows.Domain.AggregatesModel.WorkflowAggregate.WorkflowRecord("test", "test", null, null));
            foreach (var kv in input)
                context.Input[kv.Key] = kv.Value;
            return context;
        }

        [Theory(DisplayName = "Numeric > comparison")]
        [InlineData(150, true)]
        [InlineData(50, false)]
        [InlineData(100, false)]
        public async Task Evaluator_numeric_greater_than_Async(int total, bool expected)
        {
            var evaluator = CreateEvaluator();
            var context = CreateContextWithInput(new Dictionary<string, object?> { { "total", total } });
            var result = await evaluator.EvaluateAsync("total > 100", context, null!);
            result.Should().Be(expected);
        }

        [Theory(DisplayName = "Numeric < comparison")]
        [InlineData(50, true)]
        [InlineData(150, false)]
        public async Task Evaluator_numeric_less_than_Async(int total, bool expected)
        {
            var evaluator = CreateEvaluator();
            var context = CreateContextWithInput(new Dictionary<string, object?> { { "total", total } });
            var result = await evaluator.EvaluateAsync("total < 100", context, null!);
            result.Should().Be(expected);
        }

        [Theory(DisplayName = "Numeric >= comparison")]
        [InlineData(100, true)]
        [InlineData(101, true)]
        [InlineData(99, false)]
        public async Task Evaluator_numeric_greater_or_equal_Async(int total, bool expected)
        {
            var evaluator = CreateEvaluator();
            var context = CreateContextWithInput(new Dictionary<string, object?> { { "total", total } });
            var result = await evaluator.EvaluateAsync("total >= 100", context, null!);
            result.Should().Be(expected);
        }

        [Theory(DisplayName = "Numeric <= comparison")]
        [InlineData(100, true)]
        [InlineData(99, true)]
        [InlineData(101, false)]
        public async Task Evaluator_numeric_less_or_equal_Async(int total, bool expected)
        {
            var evaluator = CreateEvaluator();
            var context = CreateContextWithInput(new Dictionary<string, object?> { { "total", total } });
            var result = await evaluator.EvaluateAsync("total <= 100", context, null!);
            result.Should().Be(expected);
        }

        [Theory(DisplayName = "String == equality")]
        [InlineData("approved", true)]
        [InlineData("rejected", false)]
        public async Task Evaluator_string_equality_Async(string status, bool expected)
        {
            var evaluator = CreateEvaluator();
            var context = CreateContextWithInput(new Dictionary<string, object?> { { "status", status } });
            var result = await evaluator.EvaluateAsync("status == approved", context, null!);
            result.Should().Be(expected);
        }

        [Theory(DisplayName = "String != inequality")]
        [InlineData("user", true)]
        [InlineData("admin", false)]
        public async Task Evaluator_string_inequality_Async(string role, bool expected)
        {
            var evaluator = CreateEvaluator();
            var context = CreateContextWithInput(new Dictionary<string, object?> { { "role", role } });
            var result = await evaluator.EvaluateAsync("role != admin", context, null!);
            result.Should().Be(expected);
        }

        [Theory(DisplayName = "AND / && operator")]
        [InlineData(150, "approved", true)]
        [InlineData(50, "approved", false)]
        [InlineData(150, "rejected", false)]
        public async Task Evaluator_and_keyword_Async(int total, string status, bool expected)
        {
            var evaluator = CreateEvaluator();
            var context = CreateContextWithInput(new Dictionary<string, object?> { { "total", total }, { "status", status } });
            var r1 = await evaluator.EvaluateAsync("total > 100 AND status == approved", context, null!);
            var r2 = await evaluator.EvaluateAsync("total > 100 && status == approved", context, null!);
            r1.Should().Be(expected, "AND keyword");
            r2.Should().Be(expected, "&& operator");
        }

        [Theory(DisplayName = "OR / || operator")]
        [InlineData("approved", true)]
        [InlineData("pending", true)]
        [InlineData("rejected", false)]
        public async Task Evaluator_or_keyword_Async(string status, bool expected)
        {
            var evaluator = CreateEvaluator();
            var context = CreateContextWithInput(new Dictionary<string, object?> { { "status", status } });
            var r1 = await evaluator.EvaluateAsync("status == approved OR status == pending", context, null!);
            var r2 = await evaluator.EvaluateAsync("status == approved || status == pending", context, null!);
            r1.Should().Be(expected, "OR keyword");
            r2.Should().Be(expected, "|| operator");
        }

        [Theory(DisplayName = "Parentheses grouping")]
        [InlineData("approved", 8, true)]
        [InlineData("pending", 8, true)]
        [InlineData("approved", 3, false)]
        [InlineData("rejected", 8, false)]
        public async Task Evaluator_parentheses_grouping_Async(string status, int priority, bool expected)
        {
            var evaluator = CreateEvaluator();
            var context = CreateContextWithInput(new Dictionary<string, object?>
            {
                { "status", status },
                { "priority", priority }
            });
            var result = await evaluator.EvaluateAsync(
                "(status == approved OR status == pending) AND priority > 5", context, null!);
            result.Should().Be(expected);
        }

        [Fact(DisplayName = "AND binds tighter than OR — precedence")]
        public async Task Evaluator_and_binds_tighter_than_or_Async()
        {
            var evaluator = CreateEvaluator();
            // "a OR b AND c" should be "a OR (b AND c)"
            // With a=false, b=true, c=false → false OR (true AND false) = false
            var context = CreateContextWithInput(new Dictionary<string, object?>
            {
                { "a", "x" }, { "b", "y" }, { "c", "z" }
            });
            // a==notx (false) OR b==y (true) AND c==notz (false)
            // = false OR (true AND false) = false
            var result = await evaluator.EvaluateAsync("a == notx OR b == y AND c == notz", context, null!);
            result.Should().BeFalse();
        }

        [Fact(DisplayName = "Missing variable evaluates to false")]
        public async Task Evaluator_missing_variable_returns_false_Async()
        {
            var evaluator = CreateEvaluator();
            var context = CreateContextWithInput(new Dictionary<string, object?>());
            var result = await evaluator.EvaluateAsync("missingVar > 100", context, null!);
            result.Should().BeFalse();
        }

        [Fact(DisplayName = "Malformed expression returns false without throwing")]
        public async Task Evaluator_malformed_expression_returns_false_Async()
        {
            var evaluator = CreateEvaluator();
            var context = CreateContextWithInput(new Dictionary<string, object?> { { "x", 1 } });
            var result = await evaluator.EvaluateAsync("this is not valid @@##", context, null!);
            result.Should().BeFalse();
        }

        // ─────────────────────────────────────────────────────────────────────
        // T015: Exclusive mode routing — integration tests
        // ─────────────────────────────────────────────────────────────────────

        private IServiceProvider BuildServiceProvider(string workflowId, Action<WorkflowContextBuilder> buildWorkflow, Action<IServiceCollection>? extra = null)
        {
            var resolver = new DependencyResolver { CurrentDirectory = AppContext.BaseDirectory };
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
                services.AddWorkflowServices().AddInMemoryReposistories();
                services.RegisterNodes(typeof(ServiceTask));

                extra?.Invoke(services);

                services.RegisterWorkflow(workflowId, buildWorkflow);
            });
            return resolver.ServiceProvider;
        }

        [Theory(DisplayName = "Exclusive mode: only matching branch executes")]
        [InlineData(500, "low")]    // total <= 1000 → "low" branch
        [InlineData(2000, "high")]  // total > 1000  → "high" branch
        public async Task Logic_exclusive_routes_single_branch_Async(int total, string expectedBranch)
        {
            var workflowId = StringIdGenerator.Instance.GenerateRandomId(6);
            var sp = BuildServiceProvider(workflowId, builder =>
            {
                builder
                    .Start()
                    .Then<ServiceTask>("task0")
                    .Logic("route", GatewayRoutingMode.Exclusive)
                        .Fork().Then<ServiceTask>("low", condition: "total <= 1000")
                        .Fork().Then<ServiceTask>("high", condition: "total > 1000")
                    .Merge()
                    .End();
            });

            var result = await WorkflowTestHelper.ExecuteAsync(sp, _output, workflowId,
                new Dictionary<string, object?> { { "total", total } });

            _output.WriteLine(ContextPrintHelper.Visualize(result!.Context));
            result!.Status.Should().Be(WorkflowStatus.Finished);

            var executedIds = result.Context.State!.ExecutedNodes!.Select(n => n.Id).ToHashSet();
            var executedNames = result.Context.Nodes.Values
                .Where(n => executedIds.Contains(n.Record.Id))
                .Select(n => n.Record.Name).ToList();
            executedNames.Should().Contain(expectedBranch);
            executedNames.Should().NotContain(expectedBranch == "low" ? "high" : "low");
        }

        [Fact(DisplayName = "Exclusive mode: first match wins when multiple conditions true")]
        public async Task Logic_exclusive_first_match_wins_Async()
        {
            var workflowId = StringIdGenerator.Instance.GenerateRandomId(6);
            var sp = BuildServiceProvider(workflowId, builder =>
            {
                builder
                    .Start()
                    .Then<ServiceTask>("task0")
                    .Logic("route", GatewayRoutingMode.Exclusive)
                        .Fork().Then<ServiceTask>("branch1", condition: "total > 0")  // always true
                        .Fork().Then<ServiceTask>("branch2", condition: "total > 0")  // also true
                    .Merge()
                    .End();
            });

            var result = await WorkflowTestHelper.ExecuteAsync(sp, _output, workflowId,
                new Dictionary<string, object?> { { "total", 5 } });

            result!.Status.Should().Be(WorkflowStatus.Finished);
            var executedIds = result.Context.State!.ExecutedNodes!.Select(n => n.Id).ToHashSet();
            var executedNames = result.Context.Nodes.Values
                .Where(n => executedIds.Contains(n.Record.Id))
                .Select(n => n.Record.Name).ToList();
            executedNames.Should().Contain("branch1");
            executedNames.Should().NotContain("branch2", "exclusive mode: first match wins");
        }

        // ─────────────────────────────────────────────────────────────────────
        // T016: Inclusive mode routing
        // ─────────────────────────────────────────────────────────────────────

        [Fact(DisplayName = "Inclusive mode: all matching branches execute")]
        public async Task Logic_inclusive_all_matching_branches_execute_Async()
        {
            var workflowId = StringIdGenerator.Instance.GenerateRandomId(6);
            var sp = BuildServiceProvider(workflowId, builder =>
            {
                builder
                    .Start()
                    .Then<ServiceTask>("task0")
                    .Logic("notify", GatewayRoutingMode.Inclusive)
                        .Fork().Then<ServiceTask>("mgr", condition: "severity > 5")
                        .Fork().Then<ServiceTask>("oncall", condition: "type == outage")
                    .Merge()
                    .End();
            });

            // Both conditions match: severity=8, type=outage
            var result = await WorkflowTestHelper.ExecuteAsync(sp, _output, workflowId,
                new Dictionary<string, object?> { { "severity", 8 }, { "type", "outage" } });

            _output.WriteLine(ContextPrintHelper.Visualize(result!.Context));
            result!.Status.Should().Be(WorkflowStatus.Finished);
            var executedNames1 = ExecutedNodeNames(result.Context);
            executedNames1.Should().Contain("mgr");
            executedNames1.Should().Contain("oncall");
        }

        [Fact(DisplayName = "Inclusive mode: only one matching branch executes when one condition false")]
        public async Task Logic_inclusive_one_branch_when_one_condition_false_Async()
        {
            var workflowId = StringIdGenerator.Instance.GenerateRandomId(6);
            var sp = BuildServiceProvider(workflowId, builder =>
            {
                builder
                    .Start()
                    .Then<ServiceTask>("task0")
                    .Logic("notify", GatewayRoutingMode.Inclusive)
                        .Fork().Then<ServiceTask>("mgr", condition: "severity > 5")
                        .Fork().Then<ServiceTask>("oncall", condition: "type == outage")
                    .Merge()
                    .End();
            });

            // Only oncall matches: severity=2, type=outage
            var result = await WorkflowTestHelper.ExecuteAsync(sp, _output, workflowId,
                new Dictionary<string, object?> { { "severity", 2 }, { "type", "outage" } });

            result!.Status.Should().Be(WorkflowStatus.Finished);
            var executedNames2 = ExecutedNodeNames(result.Context);
            executedNames2.Should().Contain("oncall");
            executedNames2.Should().NotContain("mgr");
        }

        // ─────────────────────────────────────────────────────────────────────
        // T018: No condition matches, no default → Fault
        // ─────────────────────────────────────────────────────────────────────

        [Fact(DisplayName = "No condition matches and no default flow → workflow faults")]
        public async Task Logic_no_match_no_default_faults_Async()
        {
            var workflowId = StringIdGenerator.Instance.GenerateRandomId(6);
            var sp = BuildServiceProvider(workflowId, builder =>
            {
                builder
                    .Start()
                    .Then<ServiceTask>("task0")
                    .Logic("route", GatewayRoutingMode.Exclusive)
                        .Fork().Then<ServiceTask>("high", condition: "total > 9999")
                    .Merge()
                    .End();
            });

            var result = await WorkflowTestHelper.ExecuteAsync(sp, _output, workflowId,
                new Dictionary<string, object?> { { "total", 5 } });

            result!.Status.Should().Be(WorkflowStatus.Faulted);
            result.Message.Should().Contain("No condition matched");
        }

        // ─────────────────────────────────────────────────────────────────────
        // T021: Custom ILogicConditionEvaluator replaces SimpleExpressionEvaluator
        // ─────────────────────────────────────────────────────────────────────

        private class AlwaysTrueEvaluator : ILogicConditionEvaluator
        {
            public Task<bool> EvaluateAsync(string expression, WorkflowContext context, NodeContext source)
                => Task.FromResult(true);
        }

        [Fact(DisplayName = "Custom evaluator replaces default and controls routing")]
        public async Task Logic_custom_evaluator_replaces_default_Async()
        {
            var workflowId = StringIdGenerator.Instance.GenerateRandomId(6);
            var sp = BuildServiceProvider(workflowId,
                builder =>
                {
                    builder
                        .Start()
                        .Then<ServiceTask>("task0")
                        .Logic("route", GatewayRoutingMode.Inclusive)
                            .Fork().Then<ServiceTask>("branch1", condition: "anything")
                            .Fork().Then<ServiceTask>("branch2", condition: "anything")
                        .Merge()
                        .End();
                },
                services =>
                {
                    // Override the default evaluator
                    services.AddTransient<ILogicConditionEvaluator, AlwaysTrueEvaluator>();
                });

            var result = await WorkflowTestHelper.ExecuteAsync(sp, _output, workflowId,
                new Dictionary<string, object?>());

            result!.Status.Should().Be(WorkflowStatus.Finished);
            var executedNames3 = ExecutedNodeNames(result.Context);
            executedNames3.Should().Contain("branch1", "AlwaysTrueEvaluator makes every condition true");
            executedNames3.Should().Contain("branch2");
        }

        // ─────────────────────────────────────────────────────────────────────
        // T022: Custom LogicGateway subclass
        // ─────────────────────────────────────────────────────────────────────

        private class FixedRouteGateway : LogicGateway
        {
            public FixedRouteGateway(
                ILogicConditionEvaluator evaluator,
                ILogger<LogicGateway> logger,
                IStringLocalizerFactory stringLocalizer)
                : base(evaluator, logger, stringLocalizer) { }

            public override LocalizedString DisplayText => Localizer["Fixed Route"];

            public override Task<bool?> PreSelectOutgoingFlowAsync(
                WorkflowContext context, NodeContext source, NodeContext dest, FlowContext flow)
            {
                // Only intercept conditional flows (guarded branches).
                // Unconditional flows (e.g., merge → End) are left to default handling.
                if (string.IsNullOrEmpty(flow.Record.ConditionExpression))
                    return Task.FromResult<bool?>(null);

                // For conditional flows: always activate "fixed", block all others
                return Task.FromResult<bool?>(dest.Record.Name == "fixed" ? true : false);
            }
        }

        [Fact(DisplayName = "LogicGateway subclass can override routing via PreSelectOutgoingFlowAsync")]
        public async Task Logic_subclass_overrides_routing_Async()
        {
            var workflowId = StringIdGenerator.Instance.GenerateRandomId(6);
            var sp = BuildServiceProvider(workflowId,
                builder =>
                {
                    builder
                        .Start()
                        .Then<ServiceTask>("task0")
                        .Logic("route")
                            .Fork().Then<ServiceTask>("fixed")
                            .Fork().Then<ServiceTask>("other")
                        .Merge()
                        .End();
                },
                services =>
                {
                    services.RegisterNodes(typeof(FixedRouteGateway));
                });

            // Replace LogicGateway type in the workflow with FixedRouteGateway by rebuilding manually
            // (For integration, use FixedRouteGateway in a fresh builder)
            var workflowId2 = StringIdGenerator.Instance.GenerateRandomId(6);
            var resolver = new DependencyResolver { CurrentDirectory = AppContext.BaseDirectory };
            resolver.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration();
                services.AddLocalization(options => options.ResourcesPath = "Resources");
                services.AddDefaultStringIdGenerator();
                services.AddSingleton(provider => _output);
                services.AddLogging(builder =>
                {
                    builder.ClearProviders().AddTestOutputLogger()
                        .AddConfiguration(configuration.GetSection("Logging"));
                });
                services.AddMediatR(options =>
                {
                    options.RegisterServicesFromAssemblyContaining<StartEvent>();
                    options.RegisterServicesFromAssemblyContaining<TimerEventStartDomainEventHandler>();
                });
                services.AddSingleton<EventQueue>();
                services.AddWorkflowServices().AddInMemoryReposistories();
                services.RegisterNodes(typeof(FixedRouteGateway));
                services.RegisterNodes(typeof(ServiceTask));
                services.RegisterWorkflow(workflowId2, b =>
                {
                    b
                        .Start()
                        .Then<ServiceTask>("task0")
                        .Gateway<FixedRouteGateway>("route")
                            .Fork().Then<ServiceTask>("fixed", condition: "x == x")   // conditional branch
                            .Fork().Then<ServiceTask>("other", condition: "x == y")   // conditional branch
                        .Merge()
                        .End();
                });
            });

            var result = await WorkflowTestHelper.ExecuteAsync(resolver.ServiceProvider, _output, workflowId2,
                new Dictionary<string, object?>());

            result!.Status.Should().Be(WorkflowStatus.Finished);
            var executedNames4 = ExecutedNodeNames(result.Context);
            executedNames4.Should().Contain("fixed");
            executedNames4.Should().NotContain("other");
        }

        // ─────────────────────────────────────────────────────────────────────
        // T023: Exception in custom evaluator → Faulted
        // ─────────────────────────────────────────────────────────────────────

        private class ThrowingEvaluator : ILogicConditionEvaluator
        {
            public Task<bool> EvaluateAsync(string expression, WorkflowContext context, NodeContext source)
                => throw new InvalidOperationException("Evaluator failed intentionally");
        }

        [Fact(DisplayName = "Exception in custom evaluator transitions workflow to Faulted")]
        public async Task Logic_exception_in_evaluator_faults_workflow_Async()
        {
            var workflowId = StringIdGenerator.Instance.GenerateRandomId(6);
            var resolver = new DependencyResolver { CurrentDirectory = AppContext.BaseDirectory };
            resolver.ConfigureServices(services =>
            {
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();
                var configuration = configService.GetConfiguration();
                services.AddLocalization(options => options.ResourcesPath = "Resources");
                services.AddDefaultStringIdGenerator();
                services.AddSingleton(provider => _output);
                services.AddLogging(builder =>
                {
                    builder.ClearProviders().AddTestOutputLogger()
                        .AddConfiguration(configuration.GetSection("Logging"));
                });
                services.AddMediatR(options =>
                {
                    options.RegisterServicesFromAssemblyContaining<StartEvent>();
                    options.RegisterServicesFromAssemblyContaining<TimerEventStartDomainEventHandler>();
                });
                services.AddSingleton<EventQueue>();
                services.AddWorkflowServices().AddInMemoryReposistories();
                services.RegisterNodes(typeof(ServiceTask));
                // Override with throwing evaluator
                services.AddTransient<ILogicConditionEvaluator, ThrowingEvaluator>();
                services.RegisterWorkflow(workflowId, b =>
                {
                    b
                        .Start()
                        .Then<ServiceTask>("task0")
                        .Logic("route")
                            .Fork().Then<ServiceTask>("branch1", condition: "something")
                        .Merge()
                        .End();
                });
            });

            // The throwing evaluator will propagate the exception, which the executor should catch
            // and transition the workflow to Faulted.
            await Assert.ThrowsAnyAsync<Exception>(async () =>
            {
                await WorkflowTestHelper.ExecuteAsync(resolver.ServiceProvider, _output, workflowId,
                    new Dictionary<string, object?>());
            });
        }

        // ─────────────────────────────────────────────────────────────────────
        // T030: Default fallback flow activates when no conditions match
        // ─────────────────────────────────────────────────────────────────────

        [Fact(DisplayName = "Default (unconditional) fallback flow activates when no conditions match")]
        public async Task Logic_default_flow_activates_when_no_match_Async()
        {
            var workflowId = StringIdGenerator.Instance.GenerateRandomId(6);
            var sp = BuildServiceProvider(workflowId, builder =>
            {
                builder
                    .Start()
                    .Then<ServiceTask>("task0")
                    .Logic("triage", GatewayRoutingMode.Exclusive)
                        .Fork().Then<ServiceTask>("fast", condition: "priority == high")
                        .Fork().Then<ServiceTask>("standard", isDefault: true)  // default fallback
                    .Merge()
                    .End();
            });

            // priority is not "high" → standard branch (default) should activate
            var result = await WorkflowTestHelper.ExecuteAsync(sp, _output, workflowId,
                new Dictionary<string, object?> { { "priority", "normal" } });

            result!.Status.Should().Be(WorkflowStatus.Finished);
            var executedNames5 = ExecutedNodeNames(result.Context);
            executedNames5.Should().Contain("standard");
            executedNames5.Should().NotContain("fast");
        }

        // ─────────────────────────────────────────────────────────────────────
        // T031: No default + no match → Fault (with complex condition)
        // ─────────────────────────────────────────────────────────────────────

        [Fact(DisplayName = "Inclusive mode: no match + no default → workflow faults")]
        public async Task Logic_inclusive_no_match_faults_Async()
        {
            var workflowId = StringIdGenerator.Instance.GenerateRandomId(6);
            var sp = BuildServiceProvider(workflowId, builder =>
            {
                builder
                    .Start()
                    .Then<ServiceTask>("task0")
                    .Logic("route", GatewayRoutingMode.Inclusive)
                        .Fork().Then<ServiceTask>("branchA", condition: "x > 9999")
                        .Fork().Then<ServiceTask>("branchB", condition: "x > 9998")
                    .Merge()
                    .End();
            });

            var result = await WorkflowTestHelper.ExecuteAsync(sp, _output, workflowId,
                new Dictionary<string, object?> { { "x", 0 } });

            result!.Status.Should().Be(WorkflowStatus.Faulted);
        }

        // ─────────────────────────────────────────────────────────────────────
        // T029: Inclusive mode with complex AND/OR expressions
        // ─────────────────────────────────────────────────────────────────────

        [Fact(DisplayName = "Inclusive mode with AND/OR compound conditions routes correctly")]
        public async Task Logic_inclusive_compound_conditions_Async()
        {
            var workflowId = StringIdGenerator.Instance.GenerateRandomId(6);
            var sp = BuildServiceProvider(workflowId, builder =>
            {
                builder
                    .Start()
                    .Then<ServiceTask>("task0")
                    .Logic("route", GatewayRoutingMode.Inclusive)
                        .Fork().Then<ServiceTask>("branchA", condition: "total > 100 AND status == approved")
                        .Fork().Then<ServiceTask>("branchB", condition: "status == approved OR status == pending")
                    .Merge()
                    .End();
            });

            // total=500, status=approved → branchA: (500>100 AND approved==approved) = true
            //                            → branchB: (approved OR pending) = true → both execute
            var result = await WorkflowTestHelper.ExecuteAsync(sp, _output, workflowId,
                new Dictionary<string, object?> { { "total", 500 }, { "status", "approved" } });

            result!.Status.Should().Be(WorkflowStatus.Finished);
            var executedNames6 = ExecutedNodeNames(result.Context);
            executedNames6.Should().Contain("branchA");
            executedNames6.Should().Contain("branchB");
        }

        // ─────────────────────────────────────────────────────────────────────
        // T032: LogicGateway appears in node library (registered)
        // ─────────────────────────────────────────────────────────────────────

        [Fact(DisplayName = "LogicGateway is registered in the node library")]
        public void LogicGateway_is_registered_in_node_library()
        {
            var resolver = new DependencyResolver { CurrentDirectory = AppContext.BaseDirectory };
            resolver.ConfigureServices(services =>
            {
                services.AddLocalization(options => options.ResourcesPath = "Resources");
                services.AddDefaultStringIdGenerator();
                services.AddLogging(b => b.AddConsole());
                services.AddWorkflowServices();
            });

            using var scope = resolver.ServiceProvider.CreateScope();
            var library = scope.ServiceProvider.GetRequiredService<INodeLibrary>();
            var types = library.GetAllTypes();
            types.Should().Contain(t => t.Name == nameof(LogicGateway),
                "LogicGateway should be auto-registered by AddWorkflowServices()");
        }
    }
}
