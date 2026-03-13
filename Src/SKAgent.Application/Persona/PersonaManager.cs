using System;
using System.Collections.Generic;
using System.Text;
using SKAgent.Core.Personas;

namespace SKAgent.Application.Persona
{
    /// <summary>
    /// Week6 人格选择管理器。
    /// 负责按 request -> store -> default 的优先级选择人格，并遵守 persona policy。
    /// </summary>
    public sealed class PersonaManager
    {
        private readonly IPersonaProvider _provider;
        private readonly IConversationPersonaStore _store;

        public PersonaManager(IPersonaProvider provider, IConversationPersonaStore store)
        {
            _provider = provider;
            _store = store;
        }

        /// <summary>
        /// 获取当前运行应使用的人格，并在需要时持久化会话绑定。
        /// </summary>
        public PersonaSelectionResult GetOrSelect(
            string runId,
            string conversationId,
            string? requestedPersonaName)
        {
            // persisted
            var persistedName = _store.Get(conversationId); // 你 store 若是 async，就这里 await；否则同步
            var persisted = !string.IsNullOrWhiteSpace(persistedName)
                ? _provider.GetByName(persistedName!)
                : null;

            // request override
            if (!string.IsNullOrWhiteSpace(requestedPersonaName))
            {
                var requested = _provider.GetByName(requestedPersonaName!)
                    ?? throw new InvalidOperationException($"Persona not found: {requestedPersonaName}");

                var allowSwitch = persisted?.Policy.AllowSwitch ?? true;

                if (persisted is not null && !allowSwitch &&
                    !string.Equals(persisted.Name, requested.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return new PersonaSelectionResult(persisted, "store", "policy_allowSwitch=false; request_ignored");
                }

                var persist = requested.Policy.PersistSelection;
                if (persist) _store.Set(conversationId, requested.Name);

                return new PersonaSelectionResult(requested, "request", $"requestedPersona={requestedPersonaName}");
            }

            // store hit
            if (persisted is not null)
                return new PersonaSelectionResult(persisted, "store", $"conversationId={conversationId}");

            // default
            var all = _provider.GetAll();
            var defName = all.FirstOrDefault()?.Policy?.DefaultPersonaName;

            var def = _provider.GetByName("default")
                   ?? (defName is not null ? _provider.GetByName(defName) : null)
                   ?? all.First();

            if (def.Policy?.PersistSelection ?? true)
                _store.Set(conversationId, def.Name);

            return new PersonaSelectionResult(def, "default", "fallback");
        }
    }

    /// <summary>
    /// 人格选择结果。
    /// </summary>
    /// <param name="Persona">最终选中的人格配置。</param>
    /// <param name="Source">来源（request/store/default）。</param>
    /// <param name="Reason">选择原因，便于日志与可观测性。</param>
    public sealed record PersonaSelectionResult(PersonaOptions Persona, string Source, string Reason);


}
