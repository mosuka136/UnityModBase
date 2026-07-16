using System;
using System.Runtime.CompilerServices;

namespace UnityModBase.BSpace
{
    /// <summary>
    /// UnityModBase 内部日志入口，将调用点信息和日志等级转发到当前基础服务的日志数据库。
    /// 基础服务尚未建立或已经释放时，各方法均为安全的空操作。
    /// </summary>
    internal static class BLog
    {
        internal static void Debug(string msg,
            [CallerMemberName] string member = "",
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0) => BService.LogDatabase?.Debug(msg, member, file, line);

        internal static void Info(string msg,
            [CallerMemberName] string member = "",
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0) => BService.LogDatabase?.Info(msg, member, file, line);

        internal static void Notice(string msg,
            [CallerMemberName] string member = "",
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0) => BService.LogDatabase?.Notice(msg, member, file, line);

        internal static void Warn(string msg,
            [CallerMemberName] string member = "",
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0) => BService.LogDatabase?.Warn(msg, member, file, line);

        internal static void Error(string msg, Exception ex = null,
            [CallerMemberName] string member = "",
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0) => BService.LogDatabase?.Error(msg, ex, member, file, line);
    }
}
