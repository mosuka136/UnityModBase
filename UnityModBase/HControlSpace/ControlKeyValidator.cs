namespace UnityModBase.HControlSpace
{
    /// <summary>
    /// 定义实时控制模型自己的表键和条目键规则。
    /// </summary>
    internal static class ControlKeyValidator
    {
        internal static bool IsValidTableKey(string key)
        {
            return IsValidKey(key);
        }

        internal static bool IsValidEntryKey(string key)
        {
            return IsValidKey(key);
        }

        private static bool IsValidKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;

            foreach (var character in key)
            {
                if (!char.IsLetterOrDigit(character) && character != '_')
                    return false;
            }

            return true;
        }
    }
}
