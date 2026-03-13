using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Core.Profile
{
    public interface IProfileExtractor
    {
        /// <summary>
        /// 从用户输入中提取画像字段增量 patch。未命中返回空字典。
        /// </summary>
        Dictionary<string, string> ExtractPatch(string userInput);
    }
}
