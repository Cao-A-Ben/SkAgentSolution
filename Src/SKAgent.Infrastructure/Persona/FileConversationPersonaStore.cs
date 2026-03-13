using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using SKAgent.Core.Personas;

namespace SKAgent.Application.Persona
{
    /// <summary>
    /// 基于本地 JSON 文件的会话人格绑定存储实现。
    /// 用于持久化 conversationId 与 personaName 的映射。
    /// </summary>
    public sealed class FileConversationPersonaStore : IConversationPersonaStore
    {
        private readonly string _filePath;
        private readonly object _lock = new();

        private static readonly JsonSerializerOptions ReadOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static readonly JsonSerializerOptions WriteOptions = new()
        {
            WriteIndented = true
        };

        public FileConversationPersonaStore(string filePath)
        {
            _filePath = filePath;
        }

        /// <summary>
        /// 读取会话绑定的人格名。
        /// </summary>
        public string? Get(string conversationId)
        {
            if (string.IsNullOrWhiteSpace(conversationId)) return null;

            lock (_lock)
            {
                var map = ReadMap();
                return map.TryGetValue(conversationId, out var personaName) ? personaName : null;
            }
        }

        /// <summary>
        /// 写入会话绑定的人格名。
        /// </summary>
        public void Set(string conversationId, string personaName)
        {
            if (string.IsNullOrWhiteSpace(conversationId))
                throw new ArgumentException("conversationId is required.", nameof(conversationId));
            if (string.IsNullOrWhiteSpace(personaName))
                throw new ArgumentException("personaName is required.", nameof(personaName));

            lock (_lock)
            {
                var map = ReadMap();
                map[conversationId] = personaName;
                WriteMap(map);
            }
        }

        private Dictionary<string, string> ReadMap()
        {
            if (!File.Exists(_filePath))
                return new Dictionary<string, string>(StringComparer.Ordinal);

            var json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json))
                return new Dictionary<string, string>(StringComparer.Ordinal);

            return JsonSerializer.Deserialize<Dictionary<string, string>>(json, ReadOptions)
                   ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }

        private void WriteMap(Dictionary<string, string> map)
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            var tmp = _filePath + ".tmp";
            var json = JsonSerializer.Serialize(map, WriteOptions);

            File.WriteAllText(tmp, json);

            // 原子替换（Windows/Linux 都可用）
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
            File.Move(tmp, _filePath);
        }
    }

}
