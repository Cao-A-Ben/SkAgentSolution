using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Agents.Profile
{
    public sealed class InMemoryUserProfileStore : IUserProfileStore
    {
        private readonly ConcurrentDictionary<string, Dictionary<string, string>> _store = new();


        public Task<Dictionary<string, string>> GetAsync(string conversationId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (_store.TryGetValue(conversationId, out var dict))
            {
                return Task.FromResult(new Dictionary<string, string>(dict, StringComparer.OrdinalIgnoreCase));
            }
            return Task.FromResult(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        }

        public Task UpsertAsync(string conversationId, Dictionary<string, string> patch, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            _store.AddOrUpdate(conversationId,
                _ => new Dictionary<string, string>(patch, StringComparer.OrdinalIgnoreCase),
                (_, existing) =>
                {
                    foreach (var kv in patch) existing[kv.Key] = kv.Value;
                    return existing;
                });


            return Task.CompletedTask;
        }
    }
}
