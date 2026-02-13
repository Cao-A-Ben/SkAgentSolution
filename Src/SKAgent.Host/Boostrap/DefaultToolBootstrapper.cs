using System.Text.Json;
using SKAgent.Agents.Tools.Abstractions;
using SKAgent.Agents.Tools.Adapters;

namespace SKAgent.Host.Boostrap
{
    public class DefaultToolBootstrapper : IToolBootstrapper
    {
        public void RegisterAll(IToolRegistry registry)
        {
            var descriptor = new ToolDescriptor(
                Name: "string.upper",
                Description: "Convert text to uppercase",
                InputSchema: new ToolParameterSchema
                 (

                     Type: "object",
                     Properties: new Dictionary<string, ToolFieldSchema>
                     {
                         ["text"] = new ToolFieldSchema("string", "Input text")
                     },
                     Required: new[] { "text" }
                 )
            );
            registry.Register(new FunctionTool(descriptor, (args, ct) =>
            {
                var text = args.GetProperty("text").GetString() ?? string.Empty;
                var output = JsonDocument.Parse(JsonSerializer.Serialize(new { result = text.ToUpperInvariant() }))
                                     .RootElement.Clone();
                return Task.FromResult(output);
            }));
        }
    }
}
