using System;

namespace IdleGymBro.UI
{
    public static class NumberFormatter
    {
        private static readonly string[] _suffixes = { "", "K", "M", "B", "T", "aa", "ab", "ac", "ad", "ae" };

        public static string Format(double value)
        {
            if (value <= 0)
            {
                return "0";
            }

            if (value < 1000)
            {
                return ((long)value).ToString();
            }

            int tier = (int)(Math.Log10(value) / 3);
            tier = Math.Clamp(tier, 1, _suffixes.Length - 1);

            double scaled = value / Math.Pow(1000, tier);
            return scaled.ToString("0.##") + _suffixes[tier];
        }
    }
}
