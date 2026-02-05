using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;

namespace SKAgent.SemanticKernel
{
    public static class KernelFactory
    {

        public static Kernel Create(IConfiguration config)
        {
            var builder = Kernel.CreateBuilder();

            var baseUrl = config["OpenAI:BaseUrl"];

            var httpClient = string.IsNullOrWhiteSpace(baseUrl)
                ? new HttpClient()
                : new HttpClient { BaseAddress = new Uri(baseUrl) };

            builder.AddOpenAIChatCompletion(
                modelId: config["OpenAI:ChatModelId"] ?? "gpt-4o",
                apiKey: config["OpenAI:ApiKey"]!,
                httpClient: httpClient);

            return builder.Build();
        }
    }
}
