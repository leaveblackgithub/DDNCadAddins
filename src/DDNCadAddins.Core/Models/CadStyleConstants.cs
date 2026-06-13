namespace DDNCadAddins.Core.Models
{
    /// <summary>
    ///     CAD 样式相关常量（与 AutoCAD ACI / 线型命名约定对齐）
    /// </summary>
    public static class CadStyleConstants
    {
        /// <summary>
        ///     颜色索引常量
        /// </summary>
        public static class Colors
        {
            public const short ByBlock = 0;
            public const short ByLayer = 256;
            public const short Red = 1;
            public const short Yellow = 2;
            public const short Green = 3;
            public const short Cyan = 4;
            public const short Blue = 5;
            public const short Magenta = 6;
            public const short White = 7;
            public const short MinAciIndex = 0;
            public const short MaxAciIndex = 255;
        }

        /// <summary>
        ///     线型名称常量
        /// </summary>
        public static class Linetypes
        {
            public const string ByBlock = "BYBLOCK";
            public const string ByLayer = "BYLAYER";
            public const string Continuous = "Continuous";
        }
    }
}
