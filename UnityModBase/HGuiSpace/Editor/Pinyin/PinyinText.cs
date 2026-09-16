using System;
using System.Text;

namespace UnityModBase.HGuiSpace.Editor
{
    /// <summary>
    /// 把汉字转成无声调拼音，供搜索按全拼或首字母匹配。
    /// 音节表与压缩编码来自 MIT 许可的 TinyPinyin。
    /// 只覆盖 CJK 统一表意文字基本区和“〇”，多音字只取数据表中的单一读音；
    /// 所有方法均为纯函数，不修改共享状态，可在任意线程调用。
    /// </summary>
    internal static class PinyinText
    {
        /// <summary>
        /// 判断字符是否能映射到拼音表中的汉字（含“〇”）。
        /// </summary>
        public static bool IsChinese(char c)
        {
            return GetPinyinCode(c) > 0 || c == PinyinData.CHAR_12295;
        }

        /// <summary>
        /// 返回汉字的小写无声调拼音；非汉字（含表外字符）返回 <c>null</c>。
        /// 多音字只返回表中的单一读音。
        /// </summary>
        public static string GetPinyin(char c)
        {
            if (c == PinyinData.CHAR_12295)
                return "ling";

            var code = GetPinyinCode(c);
            if (code <= 0 || code >= PinyinData.PINYIN_TABLE.Length)
                return null;

            var syllable = PinyinData.PINYIN_TABLE[code];
            if (string.IsNullOrEmpty(syllable))
                return null;

            return syllable.ToLowerInvariant();
        }

        /// <summary>
        /// 连续汉字的全拼（无分隔符）。不含汉字时为空字符串。
        /// </summary>
        public static string GetFull(string text)
        {
            Build(text, out var full, out _);
            return full;
        }

        /// <summary>
        /// 连续汉字的拼音首字母。不含汉字时为空字符串。
        /// </summary>
        public static string GetInitials(string text)
        {
            Build(text, out _, out var initials);
            return initials;
        }

        /// <summary>
        /// 判断文本的汉字全拼或首字母是否包含查询（不区分大小写）。
        /// 查询中的空白会再去掉后比较一次，便于输入 <c>yin liang</c> 匹配“音量”。
        /// 文本或查询为空、或文本不含汉字时返回 <c>false</c>。
        /// </summary>
        public static bool Contains(string text, string query)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(query))
                return false;

            Build(text, out var full, out var initials);
            if (full.Length == 0)
                return false;

            if (full.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (initials.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            var compact = Compact(query);
            // 查询全为空白（compact 为空）或不含空白（与原查询等长，前面的比较已覆盖）时无需重比。
            if (compact.Length == 0 || compact.Length == query.Length)
                return false;

            return full.IndexOf(compact, StringComparison.OrdinalIgnoreCase) >= 0
                || initials.IndexOf(compact, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void Build(string text, out string full, out string initials)
        {
            if (string.IsNullOrEmpty(text))
            {
                full = string.Empty;
                initials = string.Empty;
                return;
            }

            // 容量按最长音节 6 个字母（如 zhuang）预估；非汉字直接跳过，
            // 因此全拼串和首字母串只由汉字贡献，混合文本不会拼出跨语言的伪匹配。
            var fullBuilder = new StringBuilder(text.Length * 6);
            var initialBuilder = new StringBuilder(text.Length);
            for (var i = 0; i < text.Length; i++)
            {
                var pinyin = GetPinyin(text[i]);
                if (pinyin == null)
                    continue;

                fullBuilder.Append(pinyin);
                initialBuilder.Append(pinyin[0]);
            }

            full = fullBuilder.ToString();
            initials = initialBuilder.ToString();
        }

        private static string Compact(string query)
        {
            var builder = new StringBuilder(query.Length);
            for (var i = 0; i < query.Length; i++)
            {
                if (!char.IsWhiteSpace(query[i]))
                    builder.Append(query[i]);
            }

            return builder.ToString();
        }

        // 汉字区按 7000/7000/其余 分成三段，各段有独立的索引数组和位图，查表前先减去段起点偏移。
        // 偏移越界返回 0，与“表中无映射”共用同一哨兵值（PINYIN_TABLE[0] 为空串）。
        private static int GetPinyinCode(char c)
        {
            var offset = c - PinyinData.MIN_VALUE;
            if (offset < 0 || c > PinyinData.MAX_VALUE)
                return 0;

            if (offset < PinyinData.PINYIN_CODE_1_OFFSET)
                return DecodeIndex(PinyinCode1.PINYIN_CODE_PADDING, PinyinCode1.PINYIN_CODE, offset);

            if (offset < PinyinData.PINYIN_CODE_2_OFFSET)
            {
                return DecodeIndex(
                    PinyinCode2.PINYIN_CODE_PADDING,
                    PinyinCode2.PINYIN_CODE,
                    offset - PinyinData.PINYIN_CODE_1_OFFSET);
            }

            var code3Offset = offset - PinyinData.PINYIN_CODE_2_OFFSET;
            if (code3Offset >= PinyinCode3.PINYIN_CODE.Length)
                return 0;

            return DecodeIndex(PinyinCode3.PINYIN_CODE_PADDING, PinyinCode3.PINYIN_CODE, code3Offset);
        }

        // 音节索引共 9 位：低 8 位取自 indexes[offset]，第 9 位由 padding 位图中 offset 对应的位决定，
        // 因此 8 个汉字共享 1 个 padding 字节。
        private static int DecodeIndex(byte[] paddings, byte[] indexes, int offset)
        {
            if (offset < 0 || offset >= indexes.Length)
                return 0;

            var realIndex = indexes[offset] & 0xff;
            var paddingIndex = offset / 8;
            if (paddingIndex < paddings.Length
                && (paddings[paddingIndex] & PinyinData.BIT_MASKS[offset % 8]) != 0)
            {
                realIndex |= PinyinData.PADDING_MASK & 0x1FF;
            }

            return realIndex;
        }
    }
}
