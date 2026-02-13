using SKAgent.Agents.Tools.Abstractions;

namespace SKAgent.Host.Boostrap
{
    /// <summary>
    ///  Tool Bootstrapper 接口
    /// </summary>
    public interface IToolBootstrapper
    {
        void RegisterAll(IToolRegistry registry);
    }
}
