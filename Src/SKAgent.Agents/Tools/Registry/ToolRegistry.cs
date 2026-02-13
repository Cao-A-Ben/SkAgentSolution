using System;
using System.Collections.Generic;
using System.Text;
using SKAgent.Agents.Tools.Abstractions;

namespace SKAgent.Agents.Tools.Registry
{
    /// <summary>
    /// 【Tools 注册表层 - 工具注册表实现】
    /// IToolRegistry 的默认实现，使用内存字典存储工具实例（名称忽略大小写）。
    /// 通过 DI 注册为 Singleton，在应用启动时注册工具，运行时只读查询。
    /// </summary>
    public sealed class ToolRegistry : IToolRegistry
    {
        /// <summary>工具实例字典，key 为工具名称（忽略大小写）。</summary>
        private readonly Dictionary<string, ITool> _tools = new(StringComparer.OrdinalIgnoreCase);

        /// <inheritdoc />
        public IReadOnlyList<ToolDescriptor> List()
        {
            return [.. _tools.Values.Select(d => d.Descriptor)];
        }

        /// <inheritdoc />
        public void Register(ITool tool)
        {
            var name = tool.Descriptor.Name.Trim();

            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Tool name cannot be null or empty.", nameof(tool));
            }

            if (_tools.ContainsKey(name))
            {
                throw new InvalidOperationException($"A tool with the name '{name}' is already registered.");
            }

            _tools[name] = tool;
        }

        /// <inheritdoc />
        public bool TryGet(string name, out ITool tool) => _tools.TryGetValue(name, out tool!);
    }
}
