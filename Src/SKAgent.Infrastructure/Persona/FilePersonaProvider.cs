using System;
using System.Collections.Generic;
using System.Text;
using SKAgent.Core.Personas;

namespace SKAgent.Application.Persona
{
    using System.Text.Json;

    /// <summary>
    /// 基于文件系统的人格提供器。
    /// 启动时从 personas 目录加载全部 JSON 人格定义并缓存到内存。
    /// </summary>
    public sealed class FilePersonaProvider : IPersonaProvider
    {
        private readonly IReadOnlyList<PersonaOptions> _all;
        private readonly Dictionary<string, PersonaOptions> _byName;

        public FilePersonaProvider(string personasDir)
        {
            if (!Directory.Exists(personasDir))
                throw new DirectoryNotFoundException($"Personas dir not found: {personasDir}");

            var opt = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            var list = new List<PersonaOptions>();
            foreach (var file in Directory.GetFiles(personasDir, "*.json", SearchOption.TopDirectoryOnly))
            {
                var json = File.ReadAllText(file);
                var persona = JsonSerializer.Deserialize<PersonaOptions>(json, opt)
                             ?? throw new InvalidOperationException($"Invalid persona json: {file}");
                list.Add(persona);
            }

            if (list.Count == 0)
                throw new InvalidOperationException($"No persona json found in {personasDir}");

            _all = list;
            _byName = list.ToDictionary(
                x => x.Name,                   // 如果你的 PersonaOptions 属性叫 Name
                x => x,
                StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 获取所有可用人格定义。
        /// </summary>
        public IReadOnlyList<PersonaOptions> GetAll() => _all;

        /// <summary>
        /// 按名称查找人格定义（不区分大小写）。
        /// </summary>
        public PersonaOptions? GetByName(string name)
            => _byName.TryGetValue(name, out var p) ? p : null;
    }


}
