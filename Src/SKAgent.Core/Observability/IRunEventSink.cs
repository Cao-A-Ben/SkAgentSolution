using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Core.Observability
{
    /// <summary>
    /// RunEvent 的消费端接口，用于将事件投影到日志、SSE、落盘等输出。
    /// </summary>
    public interface IRunEventSink
    {
        /// <summary>
        /// 发送单条 RunEvent。
        /// </summary>
        /// <param name="evt">事件实体。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>异步任务。</returns>
        ValueTask EmitAsync(RunEvent evt, CancellationToken ct);
    }
}
