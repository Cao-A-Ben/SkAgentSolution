using System;
using System.Collections.Generic;
using System.Text;
using SKAgent.Application.Observability;
using SKAgent.Application.Runtime;
using SKAgent.Core.Memory;
using SKAgent.Core.Memory.LongTerm;
using SKAgent.Core.Memory.ShortTerm;
using SKAgent.Core.Memory.Working;
using SKAgent.Core.Personas;
using SKAgent.Core.Runtime;

namespace SKAgent.Application.Memory
{
    /// <summary>
    /// Week6 记忆编排器。
    /// 聚合 short-term / working / long-term 三层记忆并执行统一预算裁剪。
    /// </summary>
    public sealed class MemoryOrchestrator
    {
        private readonly IShortTermMemory _short;
        private readonly IWorkingMemoryStore _working;
        private readonly ILongTermMemory _long;
        private readonly MemoryBudgeter _budgeter;

        public MemoryOrchestrator(
            IShortTermMemory shortTerm,
            IWorkingMemoryStore working,
            ILongTermMemory longTerm,
            MemoryBudgeter budgeter)
        {
            _short = shortTerm;
            _working = working;
            _long = longTerm;
            _budgeter = budgeter;
        }

        /// <summary>
        /// 构建本次运行可注入 Prompt 的分层记忆包。
        /// 并发出 memory_layer_included 事件用于可观测性追踪。
        /// </summary>
        public async Task<MemoryBundle> BuildAsync(
            IRunContext run,
            PersonaOptions persona,
            string userInput,
            CancellationToken ct)
        {
            // W6-2 先写死预算；后续可从 persona.Policy 或配置读取
            const int shortBudget = 4000;
            const int workingBudget = 4000;
            const int longBudget = 4000;

            var conversationId = run.ConversationId;

            // -------- Short-term --------
            var turns = await _short.GetRecentAsync(conversationId, take: 20, ct);
            // 你的 InMemoryShortTermMemory 返回是“最新在前”，这很好
            var shortRaw = turns.Select(t => TurnToShortTermItem(conversationId, t)).ToList();

            // Fallback：短期存储为空时，尝试使用run.ConversationState["recent_turns"]构造
            if(shortRaw.Count==0&&run.ConversationState.TryGetValue("recent_turns",out var rtObj)
                && rtObj is IReadOnlyList<TurnRecord> recentTurns
                && recentTurns.Count > 0)
            {
                // recent_turns 你的 run 里通常是“时间顺序/或某种顺序”，这里我们统一转成“最新在前”
                // 确保与 short-term 的“最新在前”语义一致，Budgeter 才能按顺序裁剪
                var normalized = recentTurns.Reverse().ToList(); // 反转后：最新在前（假设 recentTurns 是旧→新）
                shortRaw = normalized.Select(t => TurnToShortTermItem(conversationId, t)).ToList();
            }

            var shortItems = _budgeter.ClipByChars(shortRaw, shortBudget, out var shortReason);
            if (turns.Count == 0 && shortRaw.Count > 0)
                shortReason = shortReason == "none" ? "fallback_recent_turns" : "fallback_recent_turns+" + shortReason;
            await run.EmitAsync("memory_layer_included", new
            {
                layer = "short-term",
                countBefore = shortRaw.Count,
                countAfter = shortItems.Count,
                budgetChars = shortBudget,
                truncateReason = shortReason
            }, ct);

            // -------- Working --------
            var workingRaw = await _working.ListAsync(conversationId, ct); // 若接口名不同，改这里
            var workingItems = _budgeter.ClipByChars(workingRaw, workingBudget, out var workingReason);

            await run.EmitAsync("memory_layer_included", new
            {
                layer = "working",
                countBefore = workingRaw.Count,
                countAfter = workingItems.Count,
                budgetChars = workingBudget,
                truncateReason = workingReason
            }, ct);

            // -------- Long-term (NoOp for now) --------
            var query = new MemoryQuery(conversationId, userInput, TopK: 8, BudgetChars: longBudget);
            var longRaw = await _long.QueryAsync(query, ct);
            var longItems = _budgeter.ClipByChars(longRaw, longBudget, out var longReason);

            await run.EmitAsync("memory_layer_included", new
            {
                layer = "long-term",
                countBefore = longRaw.Count,
                countAfter = longItems.Count,
                budgetChars = longBudget,
                truncateReason = longReason
            }, ct);

            return new MemoryBundle(shortItems, workingItems, longItems);
        }

        /// <summary>
        /// 将短期记忆回合模型转换为统一 MemoryItem 结构。
        /// </summary>
        private static MemoryItem TurnToShortTermItem(string conversationId, TurnRecord t)
        {
            // 统一成“可注入 prompt”的文本块（W6-3 会直接使用）
            // 只要稳定即可，先不要过度格式化
            var content =
                $"[At] {t.At:O}\n" +
                $"[Goal] {t.Goal}\n" +
                $"[User] {t.UserInput}\n" +
                $"[Assistant] {t.AssistantOutput}";

            return new MemoryItem(
                Id: $"st:{conversationId}:{t.At.ToUnixTimeMilliseconds()}",
                Layer: MemoryLayer.ShortTerm,
                Content: content,
                At: t.At
            );
        }
    }
}