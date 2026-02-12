using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;

namespace SKAgent.SemanticKernel
{
    /// <summary>
    /// 【SemanticKernel 集成层 - Kernel 工厂】
    /// 负责创建和配置 Semantic Kernel 实例。
    /// 从 IConfiguration 中读取 OpenAI 配置（BaseUrl、ApiKey、ChatModelId），
    /// 构建带有 OpenAI Chat Completion 能力的 Kernel。
    /// 通过 DependencyInjection 以单例注册到 DI 容器。
    /// </summary>
    public static class KernelFactory
    {
        /// <summary>
        /// 创建并配置 Semantic Kernel 实例。
        /// </summary>
        /// <param name="config">应用配置，需包含 OpenAI 节点（BaseUrl、ApiKey、ChatModelId）。</param>
        /// <returns>配置完成的 Kernel 实例。</returns>
        public static Kernel Create(IConfiguration config)
        {
            var builder = Kernel.CreateBuilder();

            // 1. 读取自定义 BaseUrl（支持兼容 OpenAI API 的第三方服务，如腾讯混元、DeepSeek 等）
            var baseUrl = config["OpenAI:BaseUrl"];

            // 2. 如果有自定义 BaseUrl，构建带 BaseAddress 的 HttpClient
            var httpClient = string.IsNullOrWhiteSpace(baseUrl)
                ? new HttpClient()
                : new HttpClient { BaseAddress = new Uri(baseUrl) };

            // 3. 添加 OpenAI Chat Completion 服务
            builder.AddOpenAIChatCompletion(
                modelId: config["OpenAI:ChatModelId"] ?? "gpt-4o",
                apiKey: config["OpenAI:ApiKey"]!,
                httpClient: httpClient);

            return builder.Build();
        }
    }
}
