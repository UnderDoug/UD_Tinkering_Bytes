using HarmonyLib;

using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using XRL;
using XRL.World;
using XRL.World.Capabilities;
using XRL.World.Parts;
using XRL.World.Parts.Skill;

using UD_Modding_Toolbox;

using static UD_Tinkering_Bytes.Options;
using static UD_Tinkering_Bytes.Utils;
using System.Reflection;

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
        public static IEnumerable<CodeInstruction> HandleEvent_ExcludeDescriptionIfNotTinker_Transpile(IEnumerable<CodeInstruction> Instructions, ILGenerator Generator)
        {
            string patchMethodName = $"{nameof(DataDisk_Patches)}.{nameof(DataDisk.HandleEvent)}({nameof(GetShortDescriptionEvent)})";

            CodeMatcher codeMatcher = new(Instructions, Generator);

            codeMatcher.End();

            codeMatcher.End().MatchStartBackwards(
                new CodeMatch[1]
                {
                    new(OpCodes.Ldarg_0),
                });

            if (codeMatcher.IsInvalid)
            {
                MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}: (1) {nameof(CodeMatcher.MatchStartBackwards)} failed to find instruction {OpCodes.Ldarg_0} {"Mod"}");
                return Instructions;
            }
            codeMatcher.CreateLabel(out Label returnFalseLocation);

            // find
            // gameObject.Obliterate();
            codeMatcher.MatchStartBackwards(
                new CodeMatch[1]
                {
                    new(instruction => instruction.Calls(AccessTools.Method(typeof(GameObject), nameof(GameObject.Obliterate), new Type[] { typeof(string), typeof(bool), typeof(string) }))),
                });
            if (codeMatcher.IsInvalid)
            {
                MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}: (2) {nameof(CodeMatcher.MatchStartBackwards)} failed to find instruction {OpCodes.Call} {nameof(GameObject.Obliterate)}");
                return Instructions;
            }

            // find start of
            // E.Postfix.Append("\n\n{{rules|Requires:}} ").Append(GetRequiredSkillHumanReadable());
            codeMatcher.MatchStartForward(
                new CodeMatch[1]
                {
                    new(OpCodes.Ldarg_1),
                });
            if (codeMatcher.IsInvalid)
            {
                MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}: (3) {nameof(CodeMatcher.MatchStartForward)} failed to find instruction {OpCodes.Ldarg_1}");
                return Instructions;
            }
            int startRequires = codeMatcher.Pos;

            // find end of 
            // E.Postfix.Append("\n\n{{rules|Requires:}} ").Append(GetRequiredSkillHumanReadable());
            codeMatcher.MatchStartForward(
                new CodeMatch[1]
                {
                    new(OpCodes.Pop),
                });
            if (codeMatcher.IsInvalid)
            {
                MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}: (4) {nameof(CodeMatcher.MatchStartForward)} failed to find instruction {OpCodes.Pop}");
                return Instructions;
            }
            int endRequires = codeMatcher.Pos;

            // clone
            // E.Postfix.Append("\n\n{{rules|Requires:}} ").Append(GetRequiredSkillHumanReadable());
            CodeInstruction[] appendRequires = new CodeInstruction[endRequires + 1 - startRequires];
            MetricsManager.LogModInfo(ModManager.GetMod("UD_Tinkering_Bytes"), $"{nameof(endRequires)}: {endRequires}, {nameof(startRequires)}: {startRequires} ({endRequires + 1 - startRequires})");
            for (int i = 0; i < endRequires + 1 - startRequires; i++)
            {
                appendRequires[i] = codeMatcher.InstructionAt(i).Clone();
            }

            // find start of 
            // if (TinkerData.RecipeKnown(Data))
            // {
            //     E.Postfix.Append("\n\n{{rules|You already know this recipe.}}");
            // }
            codeMatcher.MatchStartForward(
                new CodeMatch[1]
                {
                    new(OpCodes.Ldarg_1),
                });
            if (codeMatcher.IsInvalid)
            {
                MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}: (5) {nameof(CodeMatcher.MatchStartForward)} failed to find instruction {OpCodes.Ldarg_1}");
                return Instructions;
            }
            int startAlreadyKnow = codeMatcher.Pos;

            // find end of 
            // if (TinkerData.RecipeKnown(Data))
            // {
            //     E.Postfix.Append("\n\n{{rules|You already know this recipe.}}");
            // }
            codeMatcher.MatchStartForward(
                new CodeMatch[1]
                {
                    new(OpCodes.Pop),
                });
            if (codeMatcher.IsInvalid)
            {
                MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}: (6) {nameof(CodeMatcher.MatchStartForward)} failed to find instruction {OpCodes.Pop}");
                return Instructions;
            }
            int endAlreadyKnow = codeMatcher.Pos;

            // clone 
            // if (TinkerData.RecipeKnown(Data))
            // {
            //     E.Postfix.Append("\n\n{{rules|You already know this recipe.}}");
            // }
            CodeInstruction[] appendAlreadyKnow = new CodeInstruction[endAlreadyKnow + 1 - startAlreadyKnow];
            MetricsManager.LogModInfo(ModManager.GetMod("UD_Tinkering_Bytes"), $"{nameof(endAlreadyKnow)}: {endAlreadyKnow}, {nameof(startAlreadyKnow)}: {startAlreadyKnow} ({endAlreadyKnow + 1 - startAlreadyKnow})");
            for (int i = 0; i < endAlreadyKnow + 1 - startAlreadyKnow; i++)
            {
                appendAlreadyKnow[i] = codeMatcher.InstructionAt(i).Clone();
            }

            // remove
            // E.Postfix.Append("\n\n{{rules|Requires:}} ").Append(GetRequiredSkillHumanReadable());
            // if (TinkerData.RecipeKnown(Data))
            // {
            //     E.Postfix.Append("\n\n{{rules|You already know this recipe.}}");
            // }
            codeMatcher.RemoveInstructionsInRange(startRequires, endAlreadyKnow);

            codeMatcher.Start();

            // find roughly
            // if (Data.Type == "Mod")
            codeMatcher.MatchStartForward(
                new CodeMatch[1]
                {
                    new(OpCodes.Ldstr, "Mod"),
                });
            if (codeMatcher.IsInvalid)
            {
                MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}: (7) {nameof(CodeMatcher.MatchStartForward)} failed to find instruction {OpCodes.Ldstr} {"Mod"}");
                return Instructions;
            }

            // find end of
            // E.Postfix.Append("\nAdds item modification: ").Append(ItemModding.GetModificationDescription(Data.Blueprint, 0));
            codeMatcher.MatchStartForward(
                new CodeMatch[1]
                {
                    new(OpCodes.Pop),
                });
            if (codeMatcher.IsInvalid)
            {
                MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}: (8) {nameof(CodeMatcher.MatchStartForward)} failed to find instruction {OpCodes.Pop}");
                return Instructions;
            }

            // insert
            // E.Postfix.Append("\n\n{{rules|Requires:}} ").Append(GetRequiredSkillHumanReadable());
            // if (TinkerData.RecipeKnown(Data))
            // {
            //     E.Postfix.Append("\n\n{{rules|You already know this recipe.}}");
            // }
            codeMatcher.Advance(-1)
                .Insert(appendRequires)
                .Insert(appendAlreadyKnow);

            // find
            // gameObject.Obliterate();
            codeMatcher.MatchStartForward(
                new CodeMatch[1]
                {
                    new(instruction => instruction.Calls(AccessTools.Method(typeof(GameObject), nameof(GameObject.Obliterate), new Type[] { typeof(string), typeof(bool), typeof(string) }))),
                });
            if (codeMatcher.IsInvalid)
            {
                MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}: (9) {nameof(CodeMatcher.MatchStartForward)} failed to find instruction {OpCodes.Call} {nameof(GameObject.Obliterate)}");
                return Instructions;
            }

            // insert
            // E.Postfix.Append("\n\n{{rules|Requires:}} ").Append(GetRequiredSkillHumanReadable());
            // if (TinkerData.RecipeKnown(Data))
            // {
            //     E.Postfix.Append("\n\n{{rules|You already know this recipe.}}");
            // }
            // before pop
            codeMatcher.Advance(-1)
                .Insert(appendRequires)
                .Insert(appendAlreadyKnow);

            codeMatcher.Start();

            // Add this condition:
            //      E.Understood() && The.Player != null && (The.Player.HasSkill("Tinkering") || Scanning.HasScanningFor(The.Player, Scanning.Scan.Tech))
            // to the below
            // if (gameObject != null)
            // IL_005f: ldloc.0
            // IL_0060: brfalse IL_010f
            codeMatcher.MatchStartForward(
                new CodeMatch[1]
                {
                    new(OpCodes.Ldloc_0),
                });
            if (codeMatcher.IsInvalid)
            {
                MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}: (10) {nameof(CodeMatcher.MatchStartForward)} failed to find instruction {OpCodes.Ldloc_0}");
                return Instructions;
            }

            codeMatcher.MatchStartForward(
                new CodeMatch[1]
                {
                    new(OpCodes.Brfalse),
                });
            if (codeMatcher.IsInvalid)
            {
                MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}: (11) {nameof(CodeMatcher.MatchStartForward)} failed to find instruction {OpCodes.Brfalse}");
                return Instructions;
            }
            codeMatcher.Instruction.operand = returnFalseLocation;
            int position = codeMatcher.Pos;

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

            // IL_01e9: call class XRL.World.GameObject XRL.The::get_Player()
            // IL_01ee: ldc.i4.1
            // IL_01ef: call bool XRL.World.Capabilities.Scanning::HasScanningFor(class XRL.World.GameObject, valuetype XRL.World.Capabilities.Scanning/Scan)
            // IL_01f4: brfalse.s IL_0211

            codeMatcher.Advance(1).CreateLabel(out Label returnTrueLocation).Advance(-2)
                .Insert(
                    new CodeInstruction[]
                    {
                        new(OpCodes.Ldloc_0), // can be CodeInstruction.LoadLocal(0) in the future
                        CodeInstruction.Call(typeof(GameObject), nameof(GameObject.Understood)),
                        new(OpCodes.Brfalse, returnFalseLocation),

                        CodeInstruction.Call(typeof(The), $"get_{nameof(The.Player)}"),
                        new(OpCodes.Brfalse, returnFalseLocation),

                        CodeInstruction.Call(typeof(The), $"get_{nameof(The.Player)}"),
                        new(OpCodes.Ldstr, "Tinkering"),
                        CodeInstruction.Call(typeof(GameObject), nameof(GameObject.HasSkill)),
                        new(OpCodes.Brtrue_S, returnTrueLocation),

                        CodeInstruction.Call(typeof(The), $"get_{nameof(The.Player)}"),
                        new(OpCodes.Ldc_I4_1),
                        CodeInstruction.Call(typeof(Scanning), nameof(Scanning.HasScanningFor), new Type[] { typeof(GameObject), typeof(Scanning.Scan) }),
                        new(OpCodes.Brfalse_S, returnFalseLocation),
                    }
                );

            MetricsManager.LogModInfo(ModManager.GetMod("UD_Tinkering_Bytes"), $"Successfully transpiled {patchMethodName}");
            int counter = 0;
            foreach (CodeInstruction ci in codeMatcher.InstructionEnumeration())
            {
                if (counter++ > position - 8 && counter < position + 21)
                {
                    MetricsManager.LogModInfo(ModManager.GetMod("UD_Tinkering_Bytes"), $"{ci.opcode} {ci.operand}");
                }
            }

            codeMatcher.Start();

            return codeMatcher.InstructionEnumeration();
        }
    }
}
