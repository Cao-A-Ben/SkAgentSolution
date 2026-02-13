using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using SKAgent.Agents.Tools.Abstractions;

namespace SKAgent.Agents.Tools.Adapters
{
    /// <summary>
    /// 【Tools 适配器层 - HTTP 调用工具】
    /// 将 HTTP 请求包装为 ITool 实现，支持 GET/POST 方法。
    /// 约定输入格式: { "method": "GET|POST", "url": "...", "body": {...} }。
    /// 输出格式: { "status": 200, "text": "..." }。
    /// </summary>
    public sealed class HttpTool : ITool
    {
        /// <summary>HTTP 客户端实例。</summary>
        private readonly HttpClient _http;

        /// <inheritdoc />
        public ToolDescriptor Descriptor { get; }

        /// <summary>
        /// 初始化 HTTP 工具。
        /// </summary>
        /// <param name="http">HTTP 客户端实例。</param>
        /// <param name="descriptor">工具元数据描述。</param>
        public HttpTool(HttpClient http, ToolDescriptor descriptor)
        {
            _http = http;
            Descriptor = descriptor;
        }

        /// <inheritdoc />
        public async Task<ToolResult> InvokeAsync(JsonElement args, CancellationToken ct)
        {
            // 约定输入：{ "method": "GET|POST", "url": "...", "body": {...} }
            var method = args.GetProperty("method").GetString() ?? "GET";
            var url = args.GetProperty("url").GetString() ?? throw new ArgumentException("url is required");

            HttpResponseMessage resp;
            if (string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase))
            {
                JsonElement body = args.TryGetProperty("body", out var b) ? b : JsonDocument.Parse("{}").RootElement;
                resp = await _http.PostAsJsonAsync(url, body, ct);
            }
            else
            {
                resp = await _http.GetAsync(url, ct);
            }

            var text = await resp.Content.ReadAsStringAsync(ct);
            var output = JsonDocument.Parse(JsonSerializer.Serialize(new { status = (int)resp.StatusCode, text })).RootElement;
            return new ToolResult(resp.IsSuccessStatusCode, output,
                resp.IsSuccessStatusCode ? null : new ToolError("http_error", $"HTTP {(int)resp.StatusCode}")
            );
        }
    }
}
