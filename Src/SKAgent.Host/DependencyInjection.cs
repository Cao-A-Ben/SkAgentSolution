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
using SKAgent.Core.Agent;
using SKAgent.Core.Protocols.MCP;
using SKAgent.Infrastructure.Mcp;
using SKAgent.SemanticKernel;

namespace SKAgent.Host
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddSkAgentServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton(KernelFactory.Create(configuration));

            services.AddSingleton<IMcpClient, McpClient>();



            services.AddSingleton<SKChatAgent>();
            services.AddSingleton<McpAgent>();

            services.AddSingleton<IAgent>(sp => sp.GetRequiredService<SKChatAgent>());
            services.AddSingleton<IAgent>(sp => sp.GetRequiredService<McpAgent>());

            services.AddSingleton<RouterAgent>();
            services.AddSingleton<PlannerAgent>();
            //services.AddSingleton<OrchestratorAgent>();
            services.AddSingleton<PlanExecutor>();

            services.AddSingleton<IShortTermMemory>(new InMemoryShortTermMemory(maxPerConversation: 20));
            services.AddScoped<AgentRuntimeService>();
            services.AddSingleton<PersonaOptions>(PersonaCatalog.EngineerTCM);
            services.AddSingleton<IChatContextComposer, DefaultChatContextComposer>();


            services.AddSingleton<IUserProfileStore, InMemoryUserProfileStore>();

            return services;
        }
    }
}
