using System.Collections.Concurrent;
using System.Text.Json;
using SKAgent.Agents.Tools.Abstractions;
using SKAgent.Agents.Tools.Adapters;

namespace SKAgent.Host.Boostrap
{
    public class DefaultToolBootstrapper : IToolBootstrapper
    {

        // 注意：这里需要一个进程内状态。测试用可以，生产建议换成 run scoped。
        // 最简单做法：用静态 ConcurrentDictionary 记录 key 是否已失败过。
        static ConcurrentDictionary<string, int> _failOnceState = new();

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


            // time.now
            registry.Register(new FunctionTool(
                new ToolDescriptor(
                    Name: "time.now",
                    Description: "Get current UTC time in ISO8601.",
                    InputSchema: new ToolParameterSchema("object", new Dictionary<string, ToolFieldSchema>(), Array.Empty<string>())
                ),
                (args, ct) =>
                {
                    var output = JsonDocument.Parse(JsonSerializer.Serialize(new { utc = DateTimeOffset.UtcNow.ToString("O") }))
                                             .RootElement.Clone();
                    return Task.FromResult(output);
                }
            ));

            // math.add
            registry.Register(new FunctionTool(
                new ToolDescriptor(
                    Name: "math.add",
                    Description: "Add two numbers a and b.",
                    InputSchema: new ToolParameterSchema(
                        "object",
                        new Dictionary<string, ToolFieldSchema>
                        {
                            ["a"] = new ToolFieldSchema("number", "First number"),
                            ["b"] = new ToolFieldSchema("number", "Second number"),
                        },
                        new[] { "a", "b" }
                    )
                ),
                (args, ct) =>
                {
                    var a = args.GetProperty("a").GetDouble();
                    var b = args.GetProperty("b").GetDouble();
                    var output = JsonDocument.Parse(JsonSerializer.Serialize(new { result = a + b }))
                                             .RootElement.Clone();
                    return Task.FromResult(output);
                }
            ));
            var failOnceDesc = new ToolDescriptor(
    Name: "debug.fail_once",
    Description: "Fail on first call per run, succeed on second call. For testing retry.",
    InputSchema: new ToolParameterSchema(
        "object",
        new Dictionary<string, ToolFieldSchema>
        {
            ["key"] = new ToolFieldSchema("string", "A key to identify the failure bucket (e.g. 'default').")
        },
        new[] { "key" }
    ),
    Idempotent: false
);


            registry.Register(new FunctionTool(failOnceDesc, (args, ct) =>
            {
                var key = args.GetProperty("key").GetString() ?? "default";
                var count = _failOnceState.AddOrUpdate(key, 1, (_, old) => old + 1);

                if (count == 1)
                {
                    // 让 ToolInvoker 捕获异常并转成 ToolResult.error（或你直接返回 ToolResult.Success=false 的工具也行）
                    throw new InvalidOperationException("fail_once: simulated failure");
                }

                var output = JsonDocument.Parse(JsonSerializer.Serialize(new { ok = true, attempt = count }))
                                         .RootElement.Clone();
                return Task.FromResult(output);
            }));

        }
    }
}
