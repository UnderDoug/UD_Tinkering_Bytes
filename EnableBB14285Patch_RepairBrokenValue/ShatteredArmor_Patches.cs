using HarmonyLib;

using System;
using System.Collections.Generic;
using System.Reflection.Emit;

using XRL;
using XRL.World;
using XRL.World.Effects;

using UD_Modding_Toolbox;
using UD_Vendor_Actions;

using static UD_Tinkering_Bytes.Options;
using static UD_Tinkering_Bytes.Utils;

namespace UD_Tinkering_Bytes.Harmony
{
    [HarmonyPatch]
    public static class ShatteredArmor_Patches
    {
        [HarmonyPatch(
            declaringType: typeof(ShatteredArmor),
            methodName: nameof(ShatteredArmor.HandleEvent),
            argumentTypes: new Type[] { typeof(AdjustValueEvent) },
            argumentVariations: new ArgumentType[] { ArgumentType.Normal })]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> HandleEvent_AdjustValueEvent_SkipIfRepair_Transpile(IEnumerable<CodeInstruction> Instructions, ILGenerator Generator)
        {
            bool doVomit = true;
            string patchMethodName = $"{nameof(ShatteredArmor_Patches)}.{nameof(ShatteredArmor.HandleEvent)}({nameof(AdjustValueEvent)})";
            int metricsCheckSteps = 0;

            CodeMatcher codeMatcher = new(Instructions, Generator);

            // return base.HandleEvent(E);
            CodeMatch[] match_Return_BaseHandleEventE = new CodeMatch[]
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldarg_1),
                new(ins => ins.Calls(AccessTools.Method(typeof(IComponent<GameObject>), nameof(IComponent<GameObject>.HandleEvent), new Type[]{ typeof(AdjustValueEvent) }))),
                new(OpCodes.Ret),
            };

            // if (Amount > 1)
            codeMatcher.Start().CreateLabel(out Label label_If_Amount_GT_1);

            // find start of:
            // return base.HandleEvent(E);
            // from the start
            if (codeMatcher.Start().MatchStartForward(match_Return_BaseHandleEventE).IsInvalid)
            {
                MetricsManager.LogModError(ThisMod, $"{patchMethodName}: ({metricsCheckSteps}) {nameof(CodeMatcher.MatchStartForward)} failed to find instructions {nameof(match_Return_BaseHandleEventE)}");
                foreach (CodeMatch match in match_Return_BaseHandleEventE)
                {
                    MetricsManager.LogModError(ThisMod, $"    {match.name} {match.opcode}");
                }
                codeMatcher.Vomit(Generator, doVomit);
                return Instructions;
            }
            metricsCheckSteps++;

            codeMatcher.CreateLabel(out Label label_Return_BaseHandleEventE).Start();

            codeMatcher.Insert(
                new CodeInstruction[]
                {
                    new(OpCodes.Ldsfld, AccessTools.Field(typeof(UD_VendorAction), nameof(UD_VendorAction.CurrentAction))),
                    new(OpCodes.Brfalse, label_If_Amount_GT_1),
                    new(OpCodes.Ldsfld, AccessTools.Field(typeof(UD_VendorAction), nameof(UD_VendorAction.CurrentAction))),
                    new(OpCodes.Ldfld, AccessTools.Field(typeof(UD_VendorAction), nameof(UD_VendorAction.Name))),
                    new(OpCodes.Ldstr, "Repair"),
                    new(OpCodes.Call, AccessTools.Method(typeof(string), nameof(string.Equals), new Type[] { typeof(object) })),
                    new(OpCodes.Brtrue, label_Return_BaseHandleEventE),
                });

            MetricsManager.LogModInfo(ThisMod, $"Successfully transpiled {patchMethodName}");
            return codeMatcher.Vomit(Generator, doVomit).InstructionEnumeration();
        }
    }
}
