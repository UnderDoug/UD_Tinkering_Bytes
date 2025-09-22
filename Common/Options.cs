using System.Collections.Generic;
using XRL;

namespace UD_Tinkering_Bytes
{
    [HasModSensitiveStaticCache]
    [HasOptionFlagUpdate(Prefix = "Option_UD_Tinkering_Bytes_")]
    public static class Options
    {
        public static bool doDebug = true;
        public static Dictionary<string, bool> classDoDebug = new()
        {
            // General
            { nameof(Utils), true },
            { nameof(Extensions), true },
        };

        public static bool getClassDoDebug(string Class)
        {
            if (classDoDebug.ContainsKey(Class))
            {
                return classDoDebug[Class];
            }
            return doDebug;
        }

        // Debug Settings
        [OptionFlag] public static bool DebugBitLockerDebugDescriptions;
        [OptionFlag] public static bool DebugKnownRecipesDebugDescriptions;
        [OptionFlag] public static bool DebugTinkerSkillsDebugDescriptions;
        [OptionFlag] public static bool DebugShowAllTinkerBitLockerInlineDisplay;
        [OptionFlag] public static bool DebugSpawnSnapjawWhileVendorDisassembles;

        // Combo Options
        [OptionFlag] public static int SelectTinkerBitLockerInlineDisplay;

        // Checkbox settings
        [OptionFlag] public static bool EnableKnownRecipeCategoryMirroring;
        [OptionFlag] public static bool EnableOverrideTinkerRepair;
        [OptionFlag] public static bool DisableBB14284Patch_RepairInvertedPerformance;
        [OptionFlag] public static bool DisableBB14285Patch_RepairBrokenValue;
        [OptionFlag] public static bool EnableOverrideTinkerRecharge;
        [OptionFlag] public static bool EnableGiantsAllKnowModGiganticIfTinkerableAtAll;

    }
}
