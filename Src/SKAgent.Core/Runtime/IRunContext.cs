using System;
using System.Collections.Generic;
using System.Text;
using SKAgent.Core.Observability;

namespace SKAgent.Core.Runtime
{
    /// <summary>
    /// 运行期上下文的最小抽象（Core Port）。
    /// Application 的编排服务应只依赖该接口，而不是依赖具体 Runtime 实现。
    /// </summary>
    public interface IRunContext
    {
        string RunId { get; }
        string ConversationId { get; }
        string UserInput { get; }
        /// <summary>
        /// 会话级共享状态（跨 Step 传递 persona/profile/memoryBundle 等）。
        /// </summary>
        Dictionary<string, object> ConversationState { get; }

        //IRunEventSink EventSink { get; }
        CancellationToken CancellationToken { get; }

        /// <summary>
        /// 记录运行期事件（结构化 payload，顺序由 runtime 生成 seq）。
        /// </summary>
        ValueTask EmitAsync(string type, object payload, CancellationToken ct = default);
    }

}
