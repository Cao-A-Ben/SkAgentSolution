using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Agents.Profile
{
    public interface IUserProfileStore
    {

        Task<Dictionary<string, string>> GetAsync(string conversationId, CancellationToken ct = default);
        Task UpsertAsync(string conversationId, Dictionary<string, string> patch, CancellationToken ct = default);
    }
}
