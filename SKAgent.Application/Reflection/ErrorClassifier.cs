using System;
using System.Collections.Generic;
using System.Text;
using SKAgent.Core.Reflection;

namespace SKAgent.Application.Reflection
{
   

    public static class ErrorClassifier
    {
        public static ErrorClassification Classify(ErrorInfo err)
        {
            var msg = err.Message ?? string.Empty;
            var code = err.Code ?? string.Empty;

            // 1) 明确不可恢复：余额/配额耗尽
            if (ContainsAny(msg, "资源包余量已用尽", "余量已用尽", "insufficient quota", "quota exceeded"))
            {
                return new ErrorClassification(
                    Retryability.NonRetryable,
                    Category: "quota_exhausted",
                    Reason: "Quota/credits exhausted (non-retryable)."
                );
            }

            // 2) HTTP 400 一般是参数/请求不可恢复（可按需细分）
            if (err.HttpStatus == 400)
            {
                return new ErrorClassification(
                    Retryability.NonRetryable,
                    Category: "bad_request",
                    Reason: "HTTP 400 indicates invalid request/arguments (non-retryable)."
                );
            }

            // 3) ToolInvoker 的标准错误码
            if (string.Equals(code, "tool_not_found", StringComparison.OrdinalIgnoreCase))
            {
                return new ErrorClassification(
                    Retryability.NonRetryable,
                    Category: "tool_not_found",
                    Reason: "Tool not registered (non-retryable)."
                );
            }

            if (string.Equals(code, "tool_timeout", StringComparison.OrdinalIgnoreCase))
            {
                return new ErrorClassification(
                    Retryability.TransientRetryable,
                    Category: "timeout",
                    Reason: "Timeout is usually transient (retryable)."
                );
            }

            // 4) 429 / rate limit（如果 HttpTool 把 status 带上）
            if (err.HttpStatus == 429 || ContainsAny(msg, "rate limit", "throttle", "too many requests"))
            {
                return new ErrorClassification(
                    Retryability.TransientRetryable,
                    Category: "rate_limited",
                    Reason: "Rate limited (retryable)."
                );
            }

            // 5) 网络/临时错误关键词（兜底）
            if (ContainsAny(msg, "timeout", "temporar", "network", "connection", "reset", "unavailable"))
            {
                return new ErrorClassification(
                    Retryability.TransientRetryable,
                    Category: "transient_network",
                    Reason: "Likely transient network issue (retryable)."
                );
            }

            // 默认：Unknown（保守起见通常不重试，或交给策略）
            return new ErrorClassification(
                Retryability.Unknown,
                Category: "unknown",
                Reason: "Unknown error type."
            );
        }


        private static bool ContainsAny(string s, params string[] tokens)
  => tokens.Any(t => s.Contains(t, StringComparison.OrdinalIgnoreCase));
    }



}
