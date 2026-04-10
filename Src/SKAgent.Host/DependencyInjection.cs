using Npgsql;
using SkAgent.Runtime.Execution;
using SkAgent.Runtime.Planning;
using SkAgent.Runtime.Runtime;
using SKAgent.Agents;
using SKAgent.Agents.Planning;
using SKAgent.Application.Chat;
using SKAgent.Application.Jobs;
using SKAgent.Application.Memory;
using SKAgent.Application.Memory.Chunker;
using SKAgent.Application.Modeling;
using SKAgent.Application.Persona;
using SKAgent.Application.Profile;
using SKAgent.Application.Prompt;
using SKAgent.Application.Reflection;
using SKAgent.Application.Retrieval;
using SKAgent.Application.Runtime;
using SKAgent.Application.Tools.Invoker;
using SKAgent.Application.Tools.Registry;
using SKAgent.Core.Agent;
using SKAgent.Core.Chat;
using SKAgent.Core.Embedding;
using SKAgent.Core.Memory.Facts;
using SKAgent.Core.Memory.LongTerm;
using SKAgent.Core.Memory.ShortTerm;
using SKAgent.Core.Memory.Vector;
using SKAgent.Core.Memory.Working;
using SKAgent.Core.Modeling;
using SKAgent.Core.Observability;
using SKAgent.Core.Personas;
using SKAgent.Core.Planning;
using SKAgent.Core.Profile;
using SKAgent.Core.Protocols.MCP;
using SKAgent.Core.Reflection;
using SKAgent.Core.Retrieval;
using SKAgent.Core.Routing;
using SKAgent.Core.Runtime;
using SKAgent.Core.Suggestions;
using SKAgent.Core.Tools.Abstractions;
using SKAgent.Host.Boostrap;
using SKAgent.Infrastructure.Mcp;
using SKAgent.Infrastructure.Memory.Embedding;
using SKAgent.Infrastructure.Memory.Facts;
using SKAgent.Infrastructure.Memory.LongTerm;
using SKAgent.Infrastructure.Memory.ShortTerm;
using SKAgent.Infrastructure.Memory.Vector;
using SKAgent.Infrastructure.Memory.Working;
using SKAgent.Infrastructure.Observability;
using SKAgent.Infrastructure.Profile;
using SKAgent.Infrastructure.Suggestions;
using SKAgent.SemanticKernel;

namespace SKAgent.Host;

public static class DependencyInjection
{
    public static IServiceCollection AddSkAgentServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddSingleton(KernelFactory.Create(configuration));
        services.AddSingleton<IMcpClient, McpClient>();

        services.Configure<DailySuggestionOptions>(configuration.GetSection(DailySuggestionOptions.SectionName));
        services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DailySuggestionOptions>>().Value);
        services.Configure<ModelRoutingOptions>(configuration.GetSection(ModelRoutingOptions.SectionName));
        services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ModelRoutingOptions>>().Value);

        services.AddSingleton<SKChatAgent>();
        services.AddSingleton<McpAgent>();
        services.AddSingleton<IAgent>(sp => sp.GetRequiredService<SKChatAgent>());
        services.AddSingleton<IAgent>(sp => sp.GetRequiredService<McpAgent>());

        services.AddSingleton<RouterAgent>();
        services.AddSingleton<IStepRouter>(sp => sp.GetRequiredService<RouterAgent>());

        services.AddSingleton<PlannerAgent>();
        services.AddSingleton<IPlanner>(sp => sp.GetRequiredService<PlannerAgent>());
        services.AddSingleton<PlanExecutor>();

        services.AddScoped<AgentRuntimeService>();

        services.AddSingleton<PersonaOptions>(PersonaCatalog.EngineerTCM);
        services.AddSingleton<IChatContextComposer, DefaultChatContextComposer>();
        services.AddSingleton<IUserProfileStore, InMemoryUserProfileStore>();
        services.AddSingleton<IProfileExtractor, ProfileExtractor>();

        services.AddSingleton<IToolRegistry, ToolRegistry>();
        services.AddSingleton<IToolInvoker, ToolInvoker>();
        services.AddSingleton<IToolBootstrapper, DefaultToolBootstrapper>();

        var storePath = Path.Combine(AppContext.BaseDirectory, "data", "conversation-persona.json");
        services.AddSingleton<IConversationPersonaStore>(_ => new FileConversationPersonaStore(storePath));

        var personasDir = Path.Combine(AppContext.BaseDirectory, "personas");
        services.AddSingleton<IPersonaProvider>(_ => new FilePersonaProvider(personasDir));
        services.AddSingleton<PersonaManager>();

        services.AddSingleton<IShortTermMemory>(_ => new InMemoryShortTermMemory(maxPerConversation: 20));
        services.AddSingleton<IWorkingMemoryStore, InMemoryWorkingMemoryStore>();
        services.AddSingleton<IFactStore, InMemoryFactStore>();

        services.AddSingleton<MemoryBudgeter>();
        services.AddSingleton<MemoryExtractor>();
        services.AddSingleton<TurnChunker>();
        services.AddSingleton<MemoryOrchestrator>();
        services.AddSingleton<LongTermMemoryService>();
        services.AddSingleton<ProfileUpdatePolicy>();

        services.AddSingleton<IIntentRouter, IntentRouter>();
        services.AddSingleton<IQueryRewriter, QueryRewriter>();
        services.AddSingleton<IRetrievalFusion, RetrievalFusion>();
        services.AddSingleton<IModelRouter, DefaultModelRouter>();
        services.AddSingleton<ITextGenerationService, SemanticKernelTextGenerationService>();
        services.AddSingleton<IRunEventLogFactory, JsonlRunEventLogFactory>();

        services.AddSingleton<PromptComposer>();
        services.AddSingleton<IPlanRequestFactory, DefaultPlanRequestFactory>();
        services.AddSingleton<IRunPreparationService, RunPreparationService>();

        services.AddSingleton<IOutputEvaluator, SimpleOutputEvaluator>();
        services.AddSingleton<IReflectionAgent, ReflectionAgent>();

        services.AddSingleton<IEmbeddingProvider>(sp => new EmbeddingProvider(
            sp.GetRequiredService<IModelRouter>(),
            dimension: 128));

        var cs = configuration.GetConnectionString("PgVector");
        if (string.IsNullOrWhiteSpace(cs))
        {
            services.AddSingleton<IRecentConversationHistory, ShortTermRecentConversationHistory>();
            services.AddSingleton<ILongTermMemory, NoOpLongTermMemory>();
            services.AddSingleton<ISuggestionStore, InMemorySuggestionStore>();
            services.AddSingleton<IConversationScopeResolver, NullConversationScopeResolver>();
        }
        else
        {
            services.AddSingleton<NpgsqlDataSource>(_ =>
            {
                var builder = new NpgsqlDataSourceBuilder(cs);
                builder.UseVector();
                return builder.Build();
            });

            services.AddSingleton<IRecentConversationHistory, PgRecentConversationHistory>();
            services.AddSingleton<IVectorStore, PgVectorStore>();
            services.AddSingleton<ILongTermMemory, PgLongTermMemory>();
            services.AddSingleton<ISuggestionStore, SqlSuggestionStore>();
            services.AddSingleton<IConversationScopeResolver, LatestConversationScopeResolver>();
        }

        services.AddSingleton<DailySuggestionService>();
        services.AddHostedService<SKAgent.Host.HostedServices.DailySuggestionJob>();

        return services;
    }
}
