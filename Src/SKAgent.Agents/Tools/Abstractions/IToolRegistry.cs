using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Agents.Tools.Abstractions
{
    /// <summary>
    /// 【Tools 抽象层 - 工具注册表接口】
    /// 管理所有已注册工具的集合，支持按名称查找和枚举。
    /// PlannerAgent 通过 List() 获取可用工具目录注入 prompt；
    /// ToolInvoker 通过 TryGet() 按名称定位具体工具实例。
    /// </summary>
    public interface IToolRegistry
    {
        /// <summary>注册一个工具实例，名称不可重复。</summary>
        void Register(ITool tool);

        /// <summary>按名称（忽略大小写）查找已注册的工具。</summary>
        bool TryGet(string name, out ITool tool);

        /// <summary>返回所有已注册工具的描述符列表，用于 Planner prompt 注入。</summary>
        IReadOnlyList<ToolDescriptor> List();
    }
}
