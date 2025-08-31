using System;
using System.Collections.Generic;
using System.Text;
using UD_Modding_Toolbox;
using XRL.World.Tinkering;

namespace UD_Tinkering_Bytes
{
    public static class Extensions
    {
        public static string Color(this object String, string Color)
        {
            string output = null;
            if (String != null && !String.ToString().IsNullOrEmpty())
            {
                output = "{{" + Color + "|" + String + "}}";
            }
            return output;
        }
        public static StringBuilder AppendLines(this StringBuilder SB, string BeforeLines, int Lines, string AfterLines)
        {
            if (!BeforeLines.IsNullOrEmpty())
            {
                SB.Append(BeforeLines);
            }
            for (int i = 0; i < Lines; i++)
            {
                SB.AppendLine();
            }
            if (!AfterLines.IsNullOrEmpty())
            {
                SB.Append(AfterLines);
            }
            return SB;
        }
        public static StringBuilder AppendLines(this StringBuilder SB, int Lines, string AfterLines)
        {
            return SB.AppendLines(null, Lines, AfterLines);
        }
        public static StringBuilder AppendLines(this StringBuilder SB, string BeforeLines, int Lines)
        {
            return SB.AppendLines(BeforeLines, Lines, null);
        }
        public static StringBuilder AppendLines(this StringBuilder SB, int Lines)
        {
            return SB.AppendLines(null, Lines, null);
        }

        public static string ToLiteral(this string String, bool Quotes = false)
        {
            if (String.IsNullOrEmpty())
            {
                return null;
            }
            string output = Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(String, false);
            if (Quotes)
            {
                output = output.Quote();
            }
            return output;
        }
    }
}
