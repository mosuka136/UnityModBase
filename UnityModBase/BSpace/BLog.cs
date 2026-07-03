using System;
using System.Runtime.CompilerServices;

namespace UnityModBase.BSpace
{
    internal class BLog
    {
        public static void Debug(string msg,
            [CallerMemberName] string member = "",
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0) => BService.LogDatabase?.Debug(msg, member, file, line);

        public static void Info(string msg,
            [CallerMemberName] string member = "",
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0) => BService.LogDatabase?.Info(msg, member, file, line);

        public static void Notice(string msg,
            [CallerMemberName] string member = "",
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0) => BService.LogDatabase?.Notice(msg, member, file, line);

        public static void Warn(string msg,
            [CallerMemberName] string member = "",
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0) => BService.LogDatabase?.Warn(msg, member, file, line);

        public static void Error(string msg, Exception ex = null,
            [CallerMemberName] string member = "",
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0) => BService.LogDatabase?.Error(msg, ex, member, file, line);
    }
}
