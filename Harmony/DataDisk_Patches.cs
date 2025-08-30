using HarmonyLib;

using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using XRL;
using XRL.World;
using XRL.World.Capabilities;
using XRL.World.Parts;
using XRL.World.Parts.Skill;

using static UD_Tinkering_Bytes.Options;
using static UD_Tinkering_Bytes.Utils;

namespace UD_Tinkering_Bytes.Harmony
{
    [HarmonyPatch]
    public static class DataDisk_Patches
    {
        private static bool doDebug => getClassDoDebug(nameof(DataDisk_Patches));

        [HarmonyPatch(
            declaringType: typeof(DataDisk),
            methodName: nameof(DataDisk.GetRequiredSkill),
            argumentTypes: new Type[] { typeof(int) },
            argumentVariations: new ArgumentType[] { ArgumentType.Normal })]
        [HarmonyPrefix]
        public static bool GetRequiredSkill_AllowTier0_Prefix(ref int Tier, ref string __result)
        {
            if (Tier < 1)
            {
                __result = nameof(UD_Basics);
                return false;
            }
            return true;
        }

        [HarmonyPatch(
            declaringType: typeof(DataDisk),
            methodName: nameof(DataDisk.GetRequiredSkillHumanReadable),
            argumentTypes: new Type[] { typeof(int) },
            argumentVariations: new ArgumentType[] { ArgumentType.Normal })]
        [HarmonyPrefix]
        public static bool GetRequiredSkillHumanReadable_AllowTier0_Prefix(ref int Tier, ref string __result)
        {
            if (Tier < 1)
            {
                __result = "Basic Life Skills";
                return false;
            }
            return true;
        }

        [HarmonyPatch(
            declaringType: typeof(DataDisk),
            methodName: nameof(DataDisk.HandleEvent),
            argumentTypes: new Type[] { typeof(GetShortDescriptionEvent) },
            argumentVariations: new ArgumentType[] { ArgumentType.Normal })]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> HandleEvent_ExcludeDescriptionIfNotTinker_Transpile(IEnumerable<CodeInstruction> Instructions)
        {
            string patchMethodName = $"{nameof(DataDisk_Patches)}.{nameof(DataDisk.HandleEvent)}({nameof(GetShortDescriptionEvent)})";

            // Add this condition:
            //      E.Understood() && The.Player != null && (The.Player.HasSkill("Tinkering") || Scanning.HasScanningFor(The.Player, Scanning.Scan.Tech))
            // to the below
            // if (gameObject != null)
            // IL_005f: ldloc.0
            // IL_0060: brfalse IL_010f

            CodeMatcher codeMatcher = new(Instructions);

            codeMatcher.MatchStartForward(
                new CodeMatch[1]
                {
                    new(OpCodes.Ldloc_0),
                });

            if (codeMatcher.IsInvalid)
            {
                MetricsManager.LogModError(ModManager.GetMod(), $"{patchMethodName}: {nameof(CodeMatcher.MatchStartForward)} failed to find instruction");
                return Instructions;
            }

            codeMatcher.MatchStartForward(
                new CodeMatch[1]
                {
                    new(OpCodes.Brfalse),
                });

            if (codeMatcher.IsInvalid)
            {
                MetricsManager.LogModError(ModManager.GetMod(), $"{patchMethodName}: {nameof(CodeMatcher.MatchStartForward)} failed to find instruction");
                return Instructions;
            }
            CodeInstruction brFalseClone = codeMatcher.Instruction.Clone();

            // Use this from HandleEvent(GetDisplayNameEvent)
            // if (E.AsIfKnown || (E.Understood() && The.Player != null && (The.Player.HasSkill("Tinkering") || Scanning.HasScanningFor(The.Player, Scanning.Scan.Tech))))
            // x IL_00cd: ldarg.1
            // x IL_00ce: ldfld bool XRL.World.GetDisplayNameEvent::AsIfKnown
            // x IL_00d3: brtrue.s IL_010b

            // Understood() can be called by a GameObject, too. Same Method Sig.
            // IL_00d5: ldarg.1
            // IL_00d6: callvirt instance bool XRL.World.GetDisplayNameEvent::Understood()
            // IL_00db: brfalse IL_0211

            // (no C# code)
            // IL_00e0: call class XRL.World.GameObject XRL.The::get_Player()
            // IL_00e5: brfalse IL_0211

            // IL_00ea: call class XRL.World.GameObject XRL.The::get_Player()
            // IL_00ef: ldstr "Tinkering"
            // IL_00f4: callvirt instance bool XRL.World.GameObject::HasSkill(string)
            // IL_00f9: brtrue.s IL_010b

            // 	IL_01e9: call class XRL.World.GameObject XRL.The::get_Player()
            // IL_01ee: ldc.i4.1
            // IL_01ef: call bool XRL.World.Capabilities.Scanning::HasScanningFor(class XRL.World.GameObject, valuetype XRL.World.Capabilities.Scanning/Scan)
            // IL_01f4: brfalse.s IL_0211

            codeMatcher.Advance(1).CreateLabel(out Label returnTrueLocation).Advance(-1)
                .Insert(
                    new CodeInstruction[]
                    {
                        new(OpCodes.Ldloc_0), // can be CodeInstruction.LoadLocal(0) in the future
                        CodeInstruction.Call(typeof(GameObject), nameof(GameObject.Understood)),
                        brFalseClone.Clone(),

                        CodeInstruction.LoadField(typeof(The), nameof(The.Player)),
                        brFalseClone.Clone(),

                        CodeInstruction.LoadField(typeof(The), nameof(The.Player)),
                        new(OpCodes.Ldstr, "Tinkering"),
                        CodeInstruction.Call(typeof(GameObject), nameof(GameObject.HasSkill)),
                        new(OpCodes.Brtrue_S, returnTrueLocation),

                        CodeInstruction.LoadField(typeof(The), nameof(The.Player)),
                        new(OpCodes.Ldc_I4_1),
                        CodeInstruction.Call(typeof(Scanning), nameof(Scanning.HasScanningFor)),
                        new(OpCodes.Brfalse_S, brFalseClone.Clone().operand),
                    }
                );

            MetricsManager.LogModInfo(ModManager.GetMod(), $"Successfully transpiled {patchMethodName}");
            return codeMatcher.InstructionEnumeration();

        }
    }
}
