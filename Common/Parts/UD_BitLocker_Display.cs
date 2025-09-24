using System;
using System.Collections.Generic;
using System.Text;
using XRL.Language;
using XRL.UI;
using XRL.World.Capabilities;
using XRL.World.Effects;
using XRL.World.Parts.Mutation;
using XRL.World.Tinkering;

using static XRL.World.Parts.Skill.Tinkering;


using UD_Vendor_Actions;
using UD_Modding_Toolbox;

using static UD_Modding_Toolbox.Const;

using UD_Tinkering_Bytes;
using static UD_Tinkering_Bytes.Options;
using Qud.UI;

namespace XRL.World.Parts
{
    [Serializable]
    public class UD_BitLocker_Display
        : IScribedPart
        , I_UD_VendorActionEventHandler
    {
        private static bool doDebug => false;

        private static int SelectedDisplayNameStyle => SelectTinkerBitLockerInlineDisplay;

        public int DisplayNameStyle;

        public UD_BitLocker_Display()
        {
            DisplayNameStyle = SelectedDisplayNameStyle;
        }

        public override bool CanGenerateStacked()
        {
            return false;
        }

        public override bool AllowStaticRegistration()
        {
            return true;
        }

        public override bool WantEvent(int ID, int Cascade)
        {
            return base.WantEvent(ID, Cascade)
                || ID == GetDisplayNameEvent.ID
                || ID == GetShortDescriptionEvent.ID;
        }
        public override bool HandleEvent(GetDisplayNameEvent E)
        {
            if (ParentObject.InInventory is GameObject vendor
                && vendor.TryGetPart(out BitLocker bitLocker)
                && E.Context == nameof(TradeLine))
            {
                // style 0: bit locker
                // style 1: bit locker <A(23)B(36)C(119)1(6)2(24)4(18)5(12)788> (this is base game BitType.GetDisplayString(bits))
                // style 2: bit locker - Ax23 Bx36 Cx119 1x6 2x24 4x18 5x12 7x1 8x2
                // style 3: bit locker - A++ B++ C++ 1+ 2++ 4++ 5++ 7 8
                // style 4: bit locker <ABCD12345678> (these are colored or not based of having any or not)
                // style 5: bit locker <ABC•12•45•78> (these are also colored or not based of having any or not)
                // style 6: bit locker - AA BB CCC 1 22 44 55 7 8 (replaces the digits in the count with the appropriate bit)

                string bits = DisplayNameStyle switch
                {
                    1 => bitLocker.GetBitsDisplayString(),
                    2 => bitLocker.GetSpacedXDisplayString(),
                    3 => bitLocker.GetBitPPDisplayString(),
                    4 => bitLocker.GetDullMissingDisplayString(),
                    5 => bitLocker.GetReplaceDullMissingDisplayString(),
                    6 => bitLocker.GetBitDigitDisplayString(),
                    _ => "",
                };
                if (bits.IsNullOrEmpty())
                {
                    bits = "empty".Color("K");
                }
                string bitLockerSummary = DisplayNameStyle > 0 ? $"{HONLY} {bits}".Color("y") : "";
                if (DisplayNameStyle == 1
                    || DisplayNameStyle == 4
                    || DisplayNameStyle == 5)
                {
                    bitLockerSummary = $"<{bits}>".Color("y");
                }

                E.AddTag(bitLockerSummary);

                if (DebugShowAllTinkerBitLockerInlineDisplay)
                {
                    E.AddAdjective(DisplayNameStyle.Color("K"));
                }
            }
            return base.HandleEvent(E);
        }
        public override bool HandleEvent(GetShortDescriptionEvent E)
        {
            if (ParentObject.InInventory is GameObject vendor
                && vendor.TryGetPart(out BitLocker bitLocker))
            {
                E.Postfix.AppendLine()
                    .Append(GameText.VariableReplace("=subject.T's= bit locker contains:", vendor))
                    .AppendLine().AppendLine()
                    .Append(bitLocker.GetBitsString());
            }
            return base.HandleEvent(E);
        }
    }
}
