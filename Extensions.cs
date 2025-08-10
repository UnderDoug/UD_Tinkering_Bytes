using System;
using System.Collections.Generic;
using System.Text;
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
    }
}
