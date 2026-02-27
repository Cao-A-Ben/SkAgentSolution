using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Core.Reflection
{
    public enum Retryability
    {
        Unknown = 0,
        TransientRetryable = 1,
        NonRetryable = 2,
    }

    public sealed record ErrorInfo(
        string? Code,
        string Message,
        int? HttpStatus = null);

    public sealed record ErrorClassification(
        Retryability Retryability,
        string Category,
        string Reason);

}
