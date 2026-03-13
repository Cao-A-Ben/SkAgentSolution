using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Core.Personas
{
    /// <summary>
    /// 会话人格绑定存储契约。
    /// </summary>
    public interface IConversationPersonaStore
    {
        /// <summary>
        /// 获取某个会话当前绑定的人格名（persona name）。
        /// </summary>
        string? Get(string conversationId);

        /// <summary>
        /// 设置某个会话当前绑定的人格名（persona name）。
        /// </summary>
        void Set(string conversationId, string personaName);
    }

}
