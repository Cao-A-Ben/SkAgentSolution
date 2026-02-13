using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SKAgent.Agents;
using SKAgent.Agents.Chat;
using SKAgent.Agents.Execution;
using SKAgent.Agents.Memory;
using SKAgent.Agents.Persona;
using SKAgent.Agents.Planning;
using SKAgent.Agents.Profile;
using SKAgent.Agents.Runtime;
using SKAgent.Agents.Tools.Abstractions;
using SKAgent.Agents.Tools.Invoker;
using SKAgent.Agents.Tools.Registry;
using SKAgent.Core.Agent;
using SKAgent.Core.Protocols.MCP;
using SKAgent.Host.Boostrap;
using SKAgent.Infrastructure.Mcp;
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
            services.AddSingleton<PlannerAgent>();
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




            return services;
        }
    }
}
