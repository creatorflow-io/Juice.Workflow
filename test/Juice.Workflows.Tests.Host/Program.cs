using System.Threading.Tasks;
using Juice.EF.Extensions;
using Juice.Services;
using Juice.Workflows;
using Juice.Workflows.Designer.Commands;
using Juice.Workflows.Api;
using Juice.Workflows.Api.Contracts.IntegrationEvents.Events;
using Juice.Workflows.Domain.AggregatesModel.WorkflowStateAggregate;
using Juice.Workflows.Domain.Commands;
using Juice.Workflows.EF;
using Juice.Workflows.Helpers;
using Juice.Workflows.Nodes.Activities;
using Juice.Workflows.Nodes.Events;
using Juice.Workflows.Services;
using Juice.Workflows.Tests.Host.IntegrationEvents.Handlers;
using Juice.MediatR;
using Newtonsoft.Json;
using Juice.Messaging;

var builder = WebApplication.CreateBuilder(args);

var workflowId = "incodeWf";
Console.WriteLine("******** WorkflowId: " + workflowId);

ConfigureCommons(builder.Services);
ConfigureWorkflow(builder.Services, builder.Configuration);
ConfigureMediator(builder.Services);
ConfigureIntegrations(builder.Services, builder.Configuration);
RegisterWorkflow(builder.Services, workflowId);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Workflow Designer API", Version = "v1" });
});

builder.Services.AddWorkflowDesigner();
builder.Services.AddGrpc(o => o.EnableDetailedErrors = true);

var app = builder.Build();

var configuration = app.Configuration;

await MigrateDbAsync(app);

await StartWorkflowAsync(app, workflowId);

app.MapGet("/", async (context) =>
{
    if (!string.IsNullOrEmpty(WorkflowAccessor.WorkflowId))
    {
        context.Response.Redirect("/visualize?id=" + WorkflowAccessor.WorkflowId);
    }
    else
    {
        await context.Response.WriteAsync("Hello world!");
    }
});

app.MapGet("/visualize", async (context) =>
{
    var id = context.Request.Query["id"];
    var contextResolver = context.RequestServices.GetRequiredService<IWorkflowContextResolver>();
    var wfContext = await contextResolver.StateResolveAsync(id, default, default);
    var visual = ContextPrintHelper.Visualize(wfContext);
    await context.Response.WriteAsync(visual);
});

app.MapGet("/state", async (context) =>
{
    var id = context.Request.Query["id"];
    var stateRepo = context.RequestServices.GetRequiredService<IWorkflowStateRepository>();
    var wfState = await stateRepo.GetAsync(id, default);
    await context.Response.WriteAsync(JsonConvert.SerializeObject(wfState));
});

app.MapGet("/resume", async (context) =>
{
    var id = context.Request.Query["id"];
    var nodeId = context.Request.Query["nodeId"];
    var mediator = context.RequestServices.GetRequiredService<IMediator>();
    var rs = await mediator.Send(new ResumeWorkflowCommand(id, nodeId));
    await context.Response.WriteAsync(JsonConvert.SerializeObject(rs));
});

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Workflow Designer API v1"));

app.MapControllers();
app.MapWorkflowGrpcServices();

app.Run();

static void ConfigureCommons(IServiceCollection services)
{
    services.AddLocalization(options => options.ResourcesPath = "Resources");

    services.AddDefaultStringIdGenerator();
}

static void ConfigureWorkflow(IServiceCollection services, IConfiguration configuration,
    string provider = "PostgreSQL")
{
    services.AddWorkflowServices()
    .StoreWorkflowToEFRepo(configuration, options =>
    {
        options.Schema = "Workflows";
        options.DatabaseProvider = provider;
    })
    .PersistStateToEFRepo(configuration, options =>
    {
        options.Schema = "Workflows";
        options.DatabaseProvider = provider;
    });

    services.RegisterDbWorkflows();

}

static void ConfigureMediator(IServiceCollection services)
{
    services.AddMediatR(options =>
    {
        options.RegisterServicesFromAssemblyContaining<StartEvent>();
        options.AddIdempotencyRequestBehavior();
        options.AddWorkflowApiServices();
    });
}

static void ConfigureIntegrations(IServiceCollection services, IConfiguration configuration, string provider = "PostgreSQL")
{
    services
            .AddMessaging()
            .AddIdempotencyRedis(redis =>
            {
                redis.ConnectionString = configuration.GetConnectionString("Redis");
            })
            .AddWorkflowOutbox()
            .AddPublishingPolicies(configuration.GetSection("EventBus:PublishingPolicies"))
            .AddDelivery(delivery =>
            {
                delivery.AddDeliveryPolicies(configuration.GetSection("EventBus:DeliveryPolicies"));
                delivery.AddWorkflowDelivery("rabbitmq");
            })
            .AddEventBus()
                .SubscribeWorkflowIntegrationEvents()
                .AddRabbitMQ(cfg =>
                {
                    cfg.AddConnection(name: "rabbitmq", configuration.GetSection("EventBus:Connections:RabbitMQ"))
                        .AddProducer("rabbitmq", "rabbitmq")
                        .AddConsumer("testhost_consumer", "testhost_workflow_queue", "rabbitmq", ccfg =>
                        {
                            ccfg.Subscribe<MessageThrowIntegrationEvent, MessageThrowIntegrationEventHandler>("wfthrow.*.*");
                        });
                    ;
                });
}

static void RegisterWorkflow(IServiceCollection services, string workflowId)
{
    services.RegisterWorkflow(workflowId, builder =>
    {
        builder
            .Start()
            .Wait<TimerIntermediateCatchEvent>("Wait").SetProperties(new Dictionary<string, object> { { "After", "00:00:15" } })
            .Parallel("p1")
                .Fork().SubProcess("P-KB", subBuilder =>
                {
                    subBuilder.Start().Then<UserTask>("KB").Then<ServiceTask>("Convert KB").End();
                }, default).Then<UserTask>("Approve Grph")
                .Seek("P-KB")
                    .Attach<BoundaryTimerEvent>("Timeout")
                        .SetProperties(new Dictionary<string, object> { { "After", "00:01:15" } })
                    .Then<SendTask>("Author inform").Terminate()
                .Seek("p1")
                .Fork().Then<UserTask>("Editing").SetProperties(new Dictionary<string, object> { { "CatchEvent", "uploaded.media.final" }, { "$Shared", "shared value" } })
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
}

static async Task MigrateDbAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();

    {
        try
        {
            var persistContext = scope.ServiceProvider.GetRequiredService<WorkflowPersistDbContext>();
            await persistContext.MigrateAsync();

            var wfContext = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
            await wfContext.MigrateAsync();
        }
        catch (Exception ex)
        {

        }
    }
}

static async Task StartWorkflowAsync(WebApplication app, string workflowId)
{
    MessageContext.Initialize(
        correlationId: StringIdGenerator.Instance.GenerateUniqueId(),
        causationId: workflowId,
        executionId: StringIdGenerator.Instance.GenerateUniqueId(),
        source: "wfhost"
        );
    using var scope = app.Services.CreateScope();
    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
    var correlationId = StringIdGenerator.Instance.GenerateUniqueId();
    var rs = await mediator.Send(new StartWorkflowCommand(workflowId, correlationId, "wf name"));
    Console.WriteLine(rs.ToString());
    if (rs.Succeeded)
    {
        var accessor = scope.ServiceProvider.GetRequiredService<IWorkflowContextAccessor>();
        WorkflowAccessor.WorkflowId = accessor.WorkflowId;
    }
}

public class WorkflowAccessor
{
    public static string? WorkflowId { get; set; }
}
