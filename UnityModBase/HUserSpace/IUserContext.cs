using System;

namespace UnityModBase.HUserSpace
{
    /// <summary>
    /// 可由 <see cref="UserContext"/> 持有并随父上下文统一释放的子上下文契约。
    /// 实现负责定义自身资源，父上下文只保证调用 <see cref="IDisposable.Dispose"/>。
    /// </summary>
    public interface IUserContext : IDisposable
    {
    }
}
