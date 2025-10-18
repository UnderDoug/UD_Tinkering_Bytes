using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UD_Modding_Toolbox;
using XRL;
using XRL.World;
using XRL.World.Parts;
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

        public static string ThemIt(this GameObject Object, bool ForcePlural = false)
        {
            bool multipleItems = ForcePlural || Object.IsPlural || Object.Count > 1;
            return multipleItems ? "Them" : Object.Them;
        }

        public static string themIt(this GameObject Object, bool ForcePlural = false)
        {
            bool multipleItems = ForcePlural || Object.IsPlural || Object.Count > 1;
            return multipleItems ? "them" : Object.them;
        }

        public static Raffle<char> FillBitRaffle(this Raffle<char> BitRaffle, int Weight, int? Tier = null, int? TierCap = null)
        {
            UD_VendorTinkering.FillBitRaffle(ref BitRaffle, Weight, Tier, TierCap);
            return BitRaffle;
        }

        public static BitLocker SortBits(this BitLocker BitLocker)
        {
            if (BitLocker == null)
            {
                return null;
            }
            BitLocker.BitStorage ??= new();
            Dictionary<char, int> sortedBitStorage = new();
            foreach (char bit in BitType.BitOrder)
            {
                int count = 0;
                if (BitLocker.BitStorage.ContainsKey(bit))
                {
                    count = BitLocker.BitStorage[bit];
                }
                sortedBitStorage.Add(bit, count);
            }
            BitLocker.BitStorage = sortedBitStorage;
            return BitLocker;
        }

        public static string GetBitsDisplayString(this BitLocker BitLocker)
        {
            if (BitLocker == null)
            {
                return null;
            }
            string bits = "";

            // A(23)B(36)D(37)1(6)2(24)4(18)5(12)788 (this is base game BitType.GetDisplayString(bits))

            foreach ((char bit, int count) in BitLocker.BitStorage)
            {
                for (int i = 0; i < count; i++)
                {
                    bits += bit;
                }
            }
            return BitType.GetDisplayString(bits);
        }

        public static string GetSpacedXDisplayString(this BitLocker BitLocker, string CountColor = "C")
        {
            if (BitLocker == null)
            {
                return null;
            }
            string bits = "";

            // Ax23 Bx36 Dx37 1x6 2>99 4x18 5x12 7x1 8x2

            foreach ((char bit, int count) in BitLocker.BitStorage)
            {
                if (count == 0)
                {
                    continue;
                }
                if (!bits.IsNullOrEmpty())
                {
                    bits += " ";
                }
                bits += BitType.TranslateBit(bit).Color($"{bit}");
                string countString = Math.Min(count, 99).ToString();
                if (!CountColor.IsNullOrEmpty())
                {
                    countString = countString.Color(CountColor);
                }
                string op = "x";
                if (count > 99)
                {
                    op = ">";
                }
                bits += $"{op}{countString}";
            }
            return bits;
        }

        public static string GetBitPPDisplayString(this BitLocker BitLocker, string PlusColor = "y")
        {
            if (BitLocker == null)
            {
                return null;
            }
            string bits = "";

            // A++ B++ D++ 1+ 2++ 4++ 5++ 7 8

            foreach ((char bit, int count) in BitLocker.BitStorage)
            {
                if (count == 0)
                {
                    continue;
                }
                if (!bits.IsNullOrEmpty())
                {
                    bits += " ";
                }
                bits += BitType.TranslateBit(bit).Color($"{bit}");

                string pluses = "";
                if (count > 4)
                {
                    pluses += "+";
                }
                if (count > 8)
                {
                    pluses += "+";
                }
                if (!pluses.IsNullOrEmpty())
                {
                    if (!PlusColor.IsNullOrEmpty())
                    {
                        pluses = pluses.Color(PlusColor);
                    }
                    bits += pluses;
                }
            }
            return bits;
        }

        public static string GetDullMissingDisplayString(this BitLocker BitLocker, string DullColor = "k")
        {
            if (BitLocker == null)
            {
                return null;
            }
            string bits = "";

            // <ABCD12345678> (these are colored or not based of having any or not)

            foreach (BitType bit in BitType.BitTypes)
            {
                char c = bit.Color;
                Dictionary<char,int> bitStorage = BitLocker.BitStorage;
                string bitColor = !bitStorage.ContainsKey(c) || bitStorage[c] == 0 ? DullColor : $"{c}";
                bits += BitType.TranslateBit(c).Color(bitColor);
            }
            return bits;
        }

        public static string GetReplaceDullMissingDisplayString(this BitLocker BitLocker, string Replacement = "\u2022", string DullColor = "k")
        {
            if (BitLocker == null)
            {
                return null;
            }
            string bits = "";

            // <AB•D12•45•78> (these are colored or not based of having any or not)

            foreach (BitType bit in BitType.BitTypes)
            {
                char c = bit.Color;
                Dictionary<char,int> bitStorage = BitLocker.BitStorage;
                bool missingBit = !bitStorage.ContainsKey(c) || bitStorage[c] == 0;
                string bitColor = missingBit ? DullColor : $"{c}";
                string bitString = missingBit ? Replacement : BitType.TranslateBit(c);
                bits += bitString.Color(bitColor);
            }
            return bits;
        }

        public static string GetBitDigitDisplayString(this BitLocker BitLocker)
        {
            if (BitLocker == null)
            {
                return null;
            }
            string bits = "";

            // AA BB CC DD 1 22 33 44 55 66 7 8 (replaces the digits in the count with the appropriate bit)

            foreach ((char bit, int count) in BitLocker.BitStorage)
            {
                if (count == 0)
                {
                    continue;
                }
                if (!bits.IsNullOrEmpty())
                {
                    bits += " ";
                }
                for (int i = 0; i < count.ToString().Length; i++)
                {
                    bits += BitType.TranslateBit(bit).Color($"{bit}");
                }
            }
            return bits;
        }
        public static string GetBitDebugString(this BitLocker BitLocker)
        {
            if (BitLocker == null)
            {
                return null;
            }
            string bitsCount = "";

            // outputs csv to Player.log
            //  A, B,C, D,1,  2,3, 4, 5,6,7,8   <- this is generated elsewhere
            // 23,36,0,37,6,119,0,18,12,0,1,2   <- this is the actual output

            foreach (char bit in BitType.BitOrder)
            {
                if (!bitsCount.IsNullOrEmpty())
                {
                    bitsCount += ",";
                }
                if (BitLocker.BitStorage.Keys.Contains(bit))
                {
                    bitsCount += BitLocker.BitStorage[bit];
                }
                else
                {
                    bitsCount += 0;
                }
            }
            return bitsCount;
        }

        public static bool IsSameDatumAs(this TinkerData ContainedDatum, TinkerData QueryingDatum)
        {
            bool doDebug = false;
            Debug.Entry(4, $"{ContainedDatum.DisplayName.Strip()}:", Toggle: doDebug);
            Debug.LoopItem(4, nameof(ContainedDatum.Blueprint), $"{ContainedDatum.Blueprint}, {QueryingDatum.Blueprint}", Toggle: doDebug);
            Debug.LoopItem(4, nameof(ContainedDatum.PartName), $"{ContainedDatum.PartName}, {QueryingDatum.PartName}", Toggle: doDebug);
            Debug.LoopItem(4, nameof(ContainedDatum.Cost), $"{ContainedDatum.Cost}, {QueryingDatum.Cost}", Toggle: doDebug);
            return (ContainedDatum.Blueprint == QueryingDatum.Blueprint || ContainedDatum.PartName == QueryingDatum.PartName)
                && ContainedDatum.Cost == QueryingDatum.Cost;
        }

        public static bool RemoveFromInventory(this GameObject Actor, GameObject Item)
        {
            return Actor != null
                && Actor.Inventory != null
                && Item != null
                && Actor.Inventory.FireEvent(Event.New("CommandRemoveObject", "Object", Item));
        }

        public static string ThisTheseN(this string Noun, int Count, bool ForceMultiple = false)
        {
            return (ForceMultiple || Count > 1) ? ("these " + Count.Things(Noun)) : ("this " + Noun);
        }
    }
}
