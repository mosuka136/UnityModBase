using System;

namespace UnityModBase.HGuiSpace.Editor
{
    /// <summary>
    /// TinyPinyin 汉字拼音数据：无声调音节表（<see cref="PINYIN_TABLE"/>）与压缩索引编码的解码常量。
    /// 数据来自 MIT 许可的 TinyPinyin
    /// （https://github.com/promeG/TinyPinyin，.NET 移植 https://github.com/hstarorg/TinyPinyin.Net），
    /// 解码逻辑见 <see cref="PinyinText"/>。
    /// </summary>
    internal static class PinyinData
    {
        /// <summary>
        /// CJK 统一表意文字区起点 U+4E00（“一”）。压缩编码表按 <c>c - MIN_VALUE</c> 的偏移索引。
        /// </summary>
        public static char MIN_VALUE = (char)19968;

        /// <summary>
        /// CJK 统一表意文字区终点 U+9FA5（“龥”）。区间外的字符不查表。
        /// </summary>
        public static char MAX_VALUE = (char)40869;

        /// <summary>
        /// 汉字“〇”（U+3007，码点 12295）的读音。该字符在 CJK 统一表意文字区之外，
        /// 需在查表前单独特判；解码路径直接返回小写 "ling"（见 <see cref="PinyinText.GetPinyin"/>），
        /// 此常量保留自上游数据，当前未被引用。
        /// </summary>
        public static String PINYIN_12295 = "LING";

        /// <summary>汉字“〇”的字符字面量，供特判比较使用。</summary>
        public static char CHAR_12295 = (char)12295;

        /// <summary>
        /// 汉字区按每 7000 个一段拆成三个编码表；此为第二段（PinyinCode2）相对
        /// <see cref="MIN_VALUE"/> 的起始偏移，也是第一段（PinyinCode1）的长度。
        /// </summary>
        public static int PINYIN_CODE_1_OFFSET = 7000;

        /// <summary>第三段（PinyinCode3）相对 <see cref="MIN_VALUE"/> 的起始偏移。</summary>
        public static int PINYIN_CODE_2_OFFSET = 7000 * 2;

        /// <summary>padding 位图按每 8 个汉字 1 字节打包，此数组为字节内第 0～7 位的掩码。</summary>
        public static int[] BIT_MASKS = new int[] { 1, 2, 4, 8, 16, 32, 64, 128 };

        /// <summary>音节索引第 9 位的标志值（0x100），与低 8 位拼合成 <see cref="PINYIN_TABLE"/> 的完整下标。</summary>
        public static short PADDING_MASK = 256;

        //CHECKSTYLE:OFF
        /// <summary>
        /// 无声调音节表，下标为 9 位解码索引；下标 0 为空串，表示该汉字无拼音映射。
        /// </summary>
        public static string[] PINYIN_TABLE = new string[]{"", "A", "AI", "AN", "ANG", "AO", "BA", "BAI",
            "BAN", "BANG", "BAO", "BEI", "BEN", "BENG", "BI", "BIAN", "BIAO", "BIE", "BIN", "BING",
            "BO", "BU", "CA", "CAI", "CAN", "CANG", "CAO", "CE", "CEN", "CENG", "CHA", "CHAI",
            "CHAN", "CHANG", "CHAO", "CHE", "CHEN", "CHENG", "CHI", "CHONG", "CHOU", "CHU", "CHUAI",
            "CHUAN", "CHUANG", "CHUI", "CHUN", "CHUO", "CI", "CONG", "COU", "CU", "CUAN", "CUI",
            "CUN", "CUO", "DA", "DAI", "DAN", "DANG", "DAO", "DE", "DENG", "DI", "DIA", "DIAN",
            "DIAO", "DIE", "DING", "DIU", "DONG", "DOU", "DU", "DUAN", "DUI", "DUN", "DUO", "E",
            "EI", "EN", "ER", "E^", "FA", "FAN", "FANG", "FEI", "FEN", "FENG", "FO", "FOU", "FU",
            "GA", "GAI", "GAN", "GANG", "GAO", "GE", "GEI", "GEN", "GENG", "GONG", "GOU", "GU",
            "GUA", "GUAI", "GUAN", "GUANG", "GUI", "GUN", "GUO", "HA", "HAI", "HAN", "HANG", "HAO",
            "HE", "HEI", "HEN", "HENG", "HONG", "HOU", "HU", "HUA", "HUAI", "HUAN", "HUANG", "HUI",
            "HUN", "HUO", "JI", "JIA", "JIAN", "JIANG", "JIAO", "JIE", "JIN", "JING", "JIONG",
            "JIU", "JU", "JUAN", "JUE", "JUN", "KA", "KAI", "KAN", "KANG", "KAO", "KE", "KEN",
            "KENG", "KONG", "KOU", "KU", "KUA", "KUAI", "KUAN", "KUANG", "KUI", "KUN", "KUO", "LA",
            "LAI", "LAN", "LANG", "LAO", "LE", "LEI", "LENG", "LI", "LIA", "LIAN", "LIANG", "LIAO",
            "LIE", "LIN", "LING", "LIU", "LONG", "LOU", "LU", "LUAN", "LUN", "LUO", "LV", "LVE",
            "M", "MA", "MAI", "MAN", "MANG", "MAO", "ME", "MEI", "MEN", "MENG", "MI", "MIAN",
            "MIAO", "MIE", "MIN", "MING", "MIU", "MO", "MOU", "MU", "NA", "NAI", "NAN", "NANG",
            "NAO", "NE", "NEI", "NEN", "NENG", "NG", "NI", "NIAN", "NIANG", "NIAO", "NIE", "NIN",
            "NING", "NIU", "NONG", "NOU", "NU", "NUAN", "NUO", "NV", "NVE", "O", "OU", "PA", "PAI",
            "PAN", "PANG", "PAO", "PEI", "PEN", "PENG", "PI", "PIAN", "PIAO", "PIE", "PIN", "PING",
            "PO", "POU", "PU", "QI", "QIA", "QIAN", "QIANG", "QIAO", "QIE", "QIN", "QING", "QIONG",
            "QIU", "QU", "QUAN", "QUE", "QUN", "RAN", "RANG", "RAO", "RE", "REN", "RENG", "RI",
            "RONG", "ROU", "RU", "RUAN", "RUI", "RUN", "RUO", "SA", "SAI", "SAN", "SANG", "SAO",
            "SE", "SEN", "SENG", "SHA", "SHAI", "SHAN", "SHANG", "SHAO", "SHE", "SHEI", "SHEN",
            "SHENG", "SHI", "SHOU", "SHU", "SHUA", "SHUAI", "SHUAN", "SHUANG", "SHUI", "SHUN",
            "SHUO", "SI", "SONG", "SOU", "SU", "SUAN", "SUI", "SUN", "SUO", "TA", "TAI", "TAN",
            "TANG", "TAO", "TE", "TENG", "TI", "TIAN", "TIAO", "TIE", "TING", "TONG", "TOU", "TU",
            "TUAN", "TUI", "TUN", "TUO", "WA", "WAI", "WAN", "WANG", "WEI", "WEN", "WENG", "WO",
            "WU", "XI", "XIA", "XIAN", "XIANG", "XIAO", "XIE", "XIN", "XING", "XIONG", "XIU", "XU",
            "XUAN", "XUE", "XUN", "YA", "YAN", "YANG", "YAO", "YE", "YI", "YIAO", "YIN", "YING",
            "YO", "YONG", "YOU", "YU", "YUAN", "YUE", "YUN", "ZA", "ZAI", "ZAN", "ZANG", "ZAO",
            "ZE", "ZEI", "ZEN", "ZENG", "ZHA", "ZHAI", "ZHAN", "ZHANG", "ZHAO", "ZHE", "ZHEI",
            "ZHEN", "ZHENG", "ZHI", "ZHONG", "ZHOU", "ZHU", "ZHUA", "ZHUAI", "ZHUAN", "ZHUANG",
            "ZHUI", "ZHUN", "ZHUO", "ZI", "ZONG", "ZOU", "ZU", "ZUAN", "ZUI", "ZUN", "ZUO"};
        //CHECKSTYLE:ON
    }
}
