using SkAgent.Runtime.Execution;
using SkAgent.Runtime.Planning;
using SkAgent.Runtime.Runtime;
using SKAgent.Agents;
using SKAgent.Agents.Memory;
using SKAgent.Agents.Planning;
using SKAgent.Application.Chat;
using SKAgent.Application.Memory;
using SKAgent.Application.Persona;
using SKAgent.Application.Profile;
using SKAgent.Application.Prompt;
using SKAgent.Application.Reflection;
using SKAgent.Application.Runtime;
using SKAgent.Application.Tools.Invoker;
using SKAgent.Application.Tools.Registry;
using SKAgent.Core.Agent;
using SKAgent.Core.Chat;
using SKAgent.Core.Memory.LongTerm;
using SKAgent.Core.Memory.ShortTerm;
using SKAgent.Core.Memory.Working;
using SKAgent.Core.Personas;
using SKAgent.Core.Planning;
using SKAgent.Core.Profile;
using SKAgent.Core.Protocols.MCP;
using SKAgent.Core.Reflection;
using SKAgent.Core.Routing;
using SKAgent.Core.Runtime;
using SKAgent.Core.Tools.Abstractions;
using SKAgent.Host.Boostrap;
using SKAgent.Infrastructure.Mcp;
using SKAgent.Infrastructure.Memory.LongTerm;
using SKAgent.Infrastructure.Memory.Working;
using SKAgent.Infrastructure.Profile;
using SKAgent.SemanticKernel;

namespace SKAgent.Host
{
    /// <summary>
    /// 【Host 层 - 依赖注入配置】
    /// 集中配置所有服务的 DI 注册，将各层组件组装在一起。
    /// 由 Program.cs 中的 builder.Services.AddSkAgentServices 调用。
    /// </summary>
    public static class DependencyInjection
    {
        /// <summary>
        /// 注册所有 SKAgent 相关服务到 DI 容器。
        /// </summary>
        /// <param name="services">DI 服务集合。</param>
        /// <param name="configuration">应用配置，用于读取 OpenAI 等配置项。</param>
        /// <returns>服务集合（支持链式调用）。</returns>
        public static IServiceCollection AddSkAgentServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            // 1. 注册 Semantic Kernel 实例（单例，通过 KernelFactory 创建）
            services.AddSingleton(KernelFactory.Create(configuration));

            // 2. 注册基础设施层实现
            services.AddSingleton<IMcpClient, McpClient>();

            // 3. 注册具体 Agent 实例（单例）
            services.AddSingleton<SKChatAgent>();
            services.AddSingleton<McpAgent>();

            // 4. 将具体 Agent 同时注册为 IAgent 接口（供 RouterAgent 枚举所有已注册 Agent）
            services.AddSingleton<IAgent>(sp => sp.GetRequiredService<SKChatAgent>());
            services.AddSingleton<IAgent>(sp => sp.GetRequiredService<McpAgent>());

            // 5. 注册路由、规划、执行组件
            services.AddSingleton<RouterAgent>();
            //services.AddSingleton<IAgent>(sp => sp.GetRequiredService<RouterAgent>());      // RouterAgent 也作为 IAgent 暴露
            services.AddSingleton<IStepRouter>(sp => sp.GetRequiredService<RouterAgent>()); // 同一个实例暴露为 IStepRouter


            services.AddSingleton<PlannerAgent>();
            services.AddSingleton<IPlanner>(sp => sp.GetRequiredService<PlannerAgent>());
            services.AddSingleton<PlanExecutor>();

            // 6. 注册短期记忆（内存版，单例，每个会话最多 20 条）
            services.AddSingleton<IShortTermMemory>(new InMemoryShortTermMemory(maxPerConversation: 20));

            // 7. 注册运行时服务（Scoped，每次请求独立实例）
            services.AddScoped<AgentRuntimeService>();

            // 8. 注册人格配置（默认使用“工程师+中医养生”预设）
            services.AddSingleton<PersonaOptions>(PersonaCatalog.EngineerTCM);

            // 9. 注册对话上下文组合器
            services.AddSingleton<IChatContextComposer, DefaultChatContextComposer>();

            // 10. 注册用户画像存储（内存版，单例）
            services.AddSingleton<IUserProfileStore, InMemoryUserProfileStore>();


            // 11.1 注册工具注册表和调用器
            services.AddSingleton<IToolRegistry, ToolRegistry>();
            services.AddSingleton<IToolInvoker, ToolInvoker>();

            //11.2 注册工具引导器
            services.AddSingleton<IToolBootstrapper, DefaultToolBootstrapper>();

            // 12. Week6：注册“会话人格绑定存储”（conversationId -> personaName）
            // 持久化到本地文件，保证同一会话的人格选择可跨请求复用。
            var storePath = Path.Combine(AppContext.BaseDirectory, "data", "conversation-persona.json");
            services.AddSingleton<IConversationPersonaStore>(_ => new FileConversationPersonaStore(storePath));

            // 13. Week6：注册“人格定义提供器”
            // 从 personas 目录加载 JSON 定义，供 PersonaManager 按名称查询与选择。
            var personasDir = Path.Combine(AppContext.BaseDirectory, "personas");
            services.AddSingleton<IPersonaProvider>(_ => new FilePersonaProvider(personasDir));

            // 14. Week6：人格选择编排器（request/store/default 选择链）
            services.AddSingleton<PersonaManager>();


            // 15. Week6：记忆分层与预算裁剪编排
            // MemoryBudgeter：负责去重 + 字符预算裁剪
            // MemoryOrchestrator：负责汇总 short/working/long 三层记忆
            services.AddSingleton<MemoryBudgeter>();
            services.AddSingleton<MemoryOrchestrator>();

            // 16. Week6：长期记忆先使用 NoOp 实现（占位，后续可替换向量库）
            services.AddSingleton<ILongTermMemory, NoOpLongTermMemory>();

            // 17. Week6：工作记忆存储（当前使用内存实现）
            services.AddSingleton<IWorkingMemoryStore, InMemoryWorkingMemoryStore>();

            // 兼容注册：短期记忆内存实现（保留单例）
            services.AddSingleton<IShortTermMemory>(_ => new InMemoryShortTermMemory(maxPerConversation: 20));
           
            // 18. Week6：Prompt 组合器（统一 prompt 拼装入口）
            services.AddSingleton<PromptComposer>();

            // 兼容重复注册（保持最后一次生效的配置行为）
            services.AddSingleton<PromptComposer>();
            //services.AddSingleton<IChatContextComposer, DefaultChatContextComposer>();

            services.AddSingleton<IPlanRequestFactory, DefaultPlanRequestFactory>();
            services.AddSingleton<IRunPreparationService, RunPreparationService>();
            services.AddSingleton<IPlanRequestFactory, DefaultPlanRequestFactory>();

            services.AddSingleton<IProfileExtractor, ProfileExtractor>();
            //services.AddSingleton<IRunEventSink, NullRunEventSink>();//默认使用 NullSink
            //services.AddScoped<IRunEventSink>(sp =>
            //{
            //    var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
            //    var response = httpContextAccessor.HttpContext?.Response;
            //    return new SseRunEventSink(response!); // SSE 用的事件流
            //});

            //// Create a CompositeRunEventSink that aggregates multiple sinks
            //// 修改为：注入所有实现的 IRunEventSink 实例
            //services.AddSingleton<IRunEventSink>(sp =>
            //{
            //    // 获取所有注册的 IRunEventSink 实现
            //    var sinks = sp.GetServices<IRunEventSink>().ToArray();
            //    return new CompositeRunEventSink(sinks);  // 使用数组传递多个 sinks 实例
            //});
            // 注册 Reflection 服务
            services.AddSingleton<IOutputEvaluator, SimpleOutputEvaluator>();
            services.AddSingleton<IReflectionAgent, ReflectionAgent>();
            return services;
        }
    }
}
