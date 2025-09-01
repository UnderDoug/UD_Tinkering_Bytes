using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;
using UD_Modding_Toolbox;
using XRL;
using XRL.World;
using XRL.World.Capabilities;
using XRL.World.Parts;
using XRL.World.Parts.Skill;
using XRL.World.Tinkering;
using static UD_Tinkering_Bytes.Options;
using static UD_Tinkering_Bytes.Utils;

namespace UD_Tinkering_Bytes.Harmony
{
    [HarmonyPatch]
    public static class UD_DataDisk_Patches
    {
        private static bool doDebug => getClassDoDebug(nameof(UD_DataDisk_Patches));

        [HarmonyPatch(
            declaringType: typeof(UD_DataDisk),
            methodName: nameof(UD_DataDisk.HandleEvent),
            argumentTypes: new Type[] { typeof(GetShortDescriptionEvent) },
            argumentVariations: new ArgumentType[] { ArgumentType.Normal })]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> HandleEvent_SpitOutInstr_Transpile(IEnumerable<CodeInstruction> Instructions, ILGenerator Generator)
        {
            bool doVomit = false;
            string patchMethodName = $"{nameof(UD_DataDisk_Patches)}.{nameof(UD_DataDisk.HandleEvent)}({nameof(GetShortDescriptionEvent)})";

            CodeMatcher codeMatcher = new(Instructions, Generator);

            MetricsManager.LogModInfo(ModManager.GetMod("UD_Tinkering_Bytes"), $"Successfully transpiled {patchMethodName}");
            return codeMatcher.Vomit(doVomit).InstructionEnumeration();
        }
    }
}
