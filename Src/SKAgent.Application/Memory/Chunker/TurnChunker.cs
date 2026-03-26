using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Application.Memory.Chunker
{
    public sealed class TurnChunker
    {
        public IEnumerable<string> Chunk(string text, int size = 500)
        {
            if (string.IsNullOrWhiteSpace(text))
                yield break;

            size = Math.Max(64, size);
            for (int i = 0; i < text.Length; i += size)
            {
                yield return text.Substring(i, Math.Min(size, text.Length - i));
            }
        }
    }
}
