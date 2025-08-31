using HarmonyLib;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

using XRL;
using XRL.World;
using XRL.World.Capabilities;
using XRL.World.Parts;
using XRL.World.Parts.Skill;
using XRL.World.Tinkering;

using UD_Modding_Toolbox;

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
        public static IEnumerable<CodeInstruction> HandleEvent_ExcludeDescriptionIfNotTinker_Transpile(IEnumerable<CodeInstruction> Instructions, ILGenerator Generator)
        {
            string patchMethodName = $"{nameof(DataDisk_Patches)}.{nameof(DataDisk.HandleEvent)}({nameof(GetShortDescriptionEvent)})";
            int metricsCheckSteps = 0;
            bool doTranspile = true;

            CodeMatcher codeMatcher = new(Instructions, Generator);
            if (doTranspile)
            {
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
                    MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}: ({metricsCheckSteps++}) {nameof(CodeMatcher.MatchStartForward)} failed to find instruction {OpCodes.Ldloc_0}");
                    return Instructions;
                }

                codeMatcher.MatchStartForward(
                    new CodeMatch[1]
                    {
                    new(OpCodes.Brfalse),
                    });
                if (codeMatcher.IsInvalid)
                {
                    MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}: ({metricsCheckSteps++}) {nameof(CodeMatcher.MatchStartForward)} failed to find instruction {OpCodes.Brfalse}");
                    return Instructions;
                }
                Label oldReturnFalseLocation = (System.Reflection.Emit.Label)codeMatcher.Instruction.operand;

                foreach (CodeInstruction ci in codeMatcher.Instructions())
                {
                    if (ci.operand == null || ci.operand.GetType() != typeof(Label) || ci.operand is not Label ciOperand || ciOperand != oldReturnFalseLocation)
                    {
                        continue;
                    }
                    // ci.operand = returnFalseLocation;
                }

                // codeMatcher.Instruction.operand = returnFalseLocation;
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
                        new(OpCodes.Brfalse, oldReturnFalseLocation),

                        CodeInstruction.Call(typeof(The), $"get_{nameof(The.Player)}"),
                        new(OpCodes.Brfalse, oldReturnFalseLocation),

                        CodeInstruction.Call(typeof(The), $"get_{nameof(The.Player)}"),
                        new(OpCodes.Ldstr, "Tinkering"),
                        CodeInstruction.Call(typeof(GameObject), nameof(GameObject.HasSkill)),
                        new(OpCodes.Brtrue_S, returnTrueLocation),

                        CodeInstruction.Call(typeof(The), $"get_{nameof(The.Player)}"),
                        new(OpCodes.Ldc_I4_1),
                        CodeInstruction.Call(typeof(Scanning), nameof(Scanning.HasScanningFor), new Type[] { typeof(GameObject), typeof(Scanning.Scan) }),
                        new(OpCodes.Brfalse_S, oldReturnFalseLocation),
                        }
                    );

                MetricsManager.LogModInfo(ModManager.GetMod("UD_Tinkering_Bytes"), $"Successfully transpiled {patchMethodName}");
                int counter = 0;
                int counterPadding = (codeMatcher.Length + 1).ToString().Length;
                foreach (CodeInstruction ci in codeMatcher.InstructionEnumeration())
                {
                    if (counter > position - 8 && counter < position + 21)
                    {
                        MetricsManager.LogModInfo(ModManager.GetMod("UD_Tinkering_Bytes"), $"[{counter.ToString().PadLeft(counterPadding, '0')}] {ci.opcode} {ci.operand}");
                    }
                    counter++;
                }

                codeMatcher.Start();
            }
            return codeMatcher.InstructionEnumeration();
        }

        [HarmonyPatch(
            declaringType: typeof(DataDisk),
            methodName: nameof(DataDisk.HandleEvent),
            argumentTypes: new Type[] { typeof(GetShortDescriptionEvent) },
            argumentVariations: new ArgumentType[] { ArgumentType.Normal })]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> HandleEvent_MoveRequiresAlreadyKnown_Transpile(IEnumerable<CodeInstruction> Instructions, ILGenerator Generator)
        {
            bool doVomit = true;
            string patchMethodName = $"{nameof(DataDisk_Patches)}.{nameof(DataDisk.HandleEvent)}({nameof(GetShortDescriptionEvent)})";
            int metricsCheckSteps = 0;
            bool doTranspile = false;
            bool doOtherTranspile = !doTranspile;

            CodeMatcher codeMatcher = new(Instructions, Generator);

            if (doOtherTranspile)
            {
                int counter = 0;
                int counterPadding = (codeMatcher.Length + 1).ToString().Length;
                foreach (CodeInstruction ci in codeMatcher.InstructionEnumeration())
                {
                    MetricsManager.LogModInfo(ModManager.GetMod("UD_Tinkering_Bytes"), $"[{counter.ToString().PadLeft(counterPadding, '0')}] {ci.opcode} {ci.operand}");
                    counter++;
                }
                // E.Postfix.Append("\n\n{{rules|Requires:}} ").Append(GetRequiredSkillHumanReadable());
                CodeMatch[] match_Return_BaseHandleEvent_E = new CodeMatch[]
                {
                    new(OpCodes.Ldarg_0),
                    new(OpCodes.Ldarg_1),
                    new(ins => ins.Calls(AccessTools.Method(typeof(IComponent<GameObject>), nameof(IComponent<GameObject>.HandleEvent), new Type[] { typeof(GetShortDescriptionEvent) }))),
                    new(OpCodes.Ret),
                };

                // instance DataDisk.GetRequiredSkillHumanReadable();
                if (AccessTools.FirstMethod(typeof(DataDisk), mi => mi.Name == nameof(DataDisk.GetRequiredSkillHumanReadable) && !mi.IsStatic && mi.GetParameters().IsNullOrEmpty())
                    is not MethodInfo dataDisk_GetRequiredSkillHumanReadable_Instance)
                {
                    MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}: ({metricsCheckSteps}) {nameof(AccessTools.GetDeclaredMethods)} failed to find method {nameof(DataDisk)}.{nameof(DataDisk.GetRequiredSkillHumanReadable)}");
                    return Instructions;
                }
                metricsCheckSteps++;

                // E.Postfix.Append("\n\n{{rules|Requires:}} ").Append(GetRequiredSkillHumanReadable());
                CodeMatch[] match_RequiresSkill_PostfixAppend = new CodeMatch[]
                {
                    new(OpCodes.Ldarg_1),
                    new(ins => ins.LoadsField(AccessTools.Field(typeof(IShortDescriptionEvent), nameof(IShortDescriptionEvent.Postfix)))),
                    new(OpCodes.Ldstr, "\n\n{{rules|Requires:}} "),
                    new(ins => ins.Calls(AccessTools.Method(typeof(StringBuilder), nameof(StringBuilder.Append), new Type[] { typeof(string) }))),
                    new(OpCodes.Ldarg_0),
                    new(ins => ins.Calls(dataDisk_GetRequiredSkillHumanReadable_Instance)),
                    new(ins => ins.Calls(AccessTools.Method(typeof(StringBuilder), nameof(StringBuilder.Append), new Type[] { typeof(string) }))),
                    new(OpCodes.Pop),
                };

                // if (TinkerData.RecipeKnown(Data))
                // {
                //     E.Postfix.Append("\n\n{{rules|You already know this recipe.}}");
                // }
                CodeMatch[] match_IfKnown = new CodeMatch[]
                {
                    // if (TinkerData.RecipeKnown(Data))
                    new(OpCodes.Ldarg_0),
                    new(ins => ins.LoadsField(AccessTools.Field(typeof(DataDisk), nameof(DataDisk.Data)))),
                    new(ins => ins.Calls(AccessTools.Method(typeof(TinkerData), nameof(TinkerData.RecipeKnown), new Type[] { typeof(TinkerData) }))),
                    new(OpCodes.Brfalse_S),
                    // {
                };
                CodeMatch[] match_AlreadyKnown_PostfixAppend = new CodeMatch[]
                {
                    // E.Postfix.Append("\n\n{{rules|You already know this recipe.}}");
                    new(OpCodes.Ldarg_1),
                    new(ins => ins.LoadsField(AccessTools.Field(typeof(IShortDescriptionEvent), nameof(IShortDescriptionEvent.Postfix)))),
                    new(OpCodes.Ldstr, "\n\n{{rules|You already know this recipe.}}"),
                    new(ins => ins.Calls(AccessTools.Method(typeof(StringBuilder), nameof(StringBuilder.Append), new Type[] { typeof(string) }))),
                    // }
                    new(OpCodes.Pop),
                };

                CodeMatch[] match_IfKnown_AlreadyKnown_PostfixAppend = match_IfKnown.Concat(match_AlreadyKnown_PostfixAppend).ToArray();

                // E.Postfix.Append("\nAdds item modification: ").Append(ItemModding.GetModificationDescription(Data.Blueprint, 0));
                CodeMatch[] match_AddsItemMod_PostfixAppend = new CodeMatch[]
                {
                    new(OpCodes.Ldarg_1),
                    new(ins => ins.LoadsField(AccessTools.Field(typeof(IShortDescriptionEvent), nameof(IShortDescriptionEvent.Postfix)))),
                    new(OpCodes.Ldstr, "\nAdds item modification: "),
                    new(ins => ins.Calls(AccessTools.Method(typeof(StringBuilder), nameof(StringBuilder.Append), new Type[] { typeof(string) }))),
                    new(OpCodes.Ldarg_0),
                    new(ins => ins.LoadsField(AccessTools.Field(typeof(DataDisk), nameof(DataDisk.Data)))),
                    new(ins => ins.LoadsField(AccessTools.Field(typeof(TinkerData), nameof(TinkerData.Blueprint)))),
                    new(OpCodes.Ldc_I4_0),
                    new(ins => ins.Calls(AccessTools.Method(typeof(ItemModding), nameof(ItemModding.GetModificationDescription), new Type[] { typeof(string), typeof(int) }))),
                    new(ins => ins.Calls(AccessTools.Method(typeof(StringBuilder), nameof(StringBuilder.Append), new Type[] { typeof(string) }))),
                    new(OpCodes.Pop),
                };

                // gameObject.Obliterate();
                CodeMatch[] match_GameObject_Obliterate = new CodeMatch[]
                {
                    new(OpCodes.Ldloc_0),
                    new(OpCodes.Ldnull),
                    new(OpCodes.Ldc_I4_0),
                    new(OpCodes.Ldnull),
                    new(ins => ins.Calls(AccessTools.Method(typeof(GameObject), nameof(GameObject.Obliterate), new Type[] { typeof(string), typeof(bool), typeof(string) }))),
                    new(OpCodes.Pop),
                };

                // find start of:
                // return base.HandleEvent(E);
                // from the start
                if (codeMatcher.Start().MatchStartForward(match_Return_BaseHandleEvent_E).IsInvalid)
                {
                    MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}: ({metricsCheckSteps}) {nameof(CodeMatcher.MatchStartForward)} failed to find instructions {nameof(match_Return_BaseHandleEvent_E)}");
                    foreach (CodeMatch match in match_Return_BaseHandleEvent_E)
                    {
                        MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}:     {match.opcode} {match.operand}");
                    }
                    codeMatcher.Vomit(doVomit);
                    return Instructions;
                }
                metricsCheckSteps++;
                codeMatcher.CreateLabel(out Label label_Return_BaseHandleEvent_E);

                // find end of:
                // E.Postfix.Append("\nAdds item modification: ").Append(ItemModding.GetModificationDescription(Data.Blueprint, 0));
                // from the start
                if (codeMatcher.Start().MatchEndForward(match_AddsItemMod_PostfixAppend).IsInvalid)
                {
                    MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}: ({metricsCheckSteps}) {nameof(CodeMatcher.MatchStartForward)} failed to find instructions {nameof(match_AddsItemMod_PostfixAppend)}");
                    foreach (CodeMatch match in match_AddsItemMod_PostfixAppend)
                    {
                        MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}:     {match.opcode} {match.operand}");
                    }
                    codeMatcher.Vomit(doVomit);
                    return Instructions;
                }
                metricsCheckSteps++;
                codeMatcher.Advance(1).CreateLabel(out Label label_Return_AfterAddsMods);

                // find end of:
                // gameObject.Obliterate();
                // from the start
                if (codeMatcher.Start().MatchEndForward(match_GameObject_Obliterate).IsInvalid)
                {
                    MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}: ({metricsCheckSteps}) {nameof(CodeMatcher.MatchStartForward)} failed to find instructions {nameof(match_GameObject_Obliterate)}");
                    MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{AccessTools.Method(typeof(GameObject), nameof(GameObject.Obliterate), new Type[] { typeof(string), typeof(bool), typeof(string) })?.ToString()}");
                    foreach (CodeMatch match in match_GameObject_Obliterate)
                    {
                        MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}:     {match.opcode} {match.operand}");
                    }
                    codeMatcher.Vomit(doVomit);
                    return Instructions;
                }
                metricsCheckSteps++;
                codeMatcher.Advance(1).CreateLabel(out Label label_Return_AfterObliterate);

                // find start of:
                // E.Postfix.Append("\n\n{{rules|Requires:}} ").Append(GetRequiredSkillHumanReadable());
                // from the start
                if (codeMatcher.Start().MatchStartForward(match_RequiresSkill_PostfixAppend).IsInvalid)
                {
                    MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}: ({metricsCheckSteps}) {nameof(CodeMatcher.MatchStartForward)} failed to find instructions {nameof(match_RequiresSkill_PostfixAppend)}");
                    foreach (CodeMatch match in match_RequiresSkill_PostfixAppend)
                    {
                        MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"    {match.name} {match.opcode}");
                    }
                    codeMatcher.Vomit(doVomit);
                    return Instructions;
                }
                metricsCheckSteps++;
                codeMatcher
                    .Advance(-1)
                    .Insert(
                        new CodeInstruction[] 
                        { 
                            new(OpCodes.Br_S, label_Return_BaseHandleEvent_E) 
                        })
                    .Advance(1);

                CodeInstruction[] clone_RequiresSkill_PostfixAppend_ForAddsMods = new CodeInstruction[match_RequiresSkill_PostfixAppend.Length];
                CodeInstruction[] clone_RequiresSkill_PostfixAppend_ForBeforeObliterate = new CodeInstruction[match_RequiresSkill_PostfixAppend.Length];
                for (int i = 0; i < match_RequiresSkill_PostfixAppend.Length; i++)
                {
                    clone_RequiresSkill_PostfixAppend_ForAddsMods[i] = codeMatcher.InstructionAt(i).Clone();
                    clone_RequiresSkill_PostfixAppend_ForBeforeObliterate[i] = codeMatcher.InstructionAt(i).Clone();
                }

                // find start of:
                // if (TinkerData.RecipeKnown(Data))
                // {
                //     E.Postfix.Append("\n\n{{rules|You already know this recipe.}}");
                // }
                // from the start
                if (codeMatcher.Start().MatchStartForward(match_IfKnown_AlreadyKnown_PostfixAppend).IsInvalid)
                {
                    MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}: ({metricsCheckSteps}) {nameof(CodeMatcher.MatchStartForward)} failed to find instructions {nameof(match_IfKnown_AlreadyKnown_PostfixAppend)}");
                    foreach (CodeMatch match in match_IfKnown_AlreadyKnown_PostfixAppend)
                    {
                        MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}:     {match.opcode} {match.operand}");
                    }
                    codeMatcher.Vomit(doVomit);
                    return Instructions;
                }
                metricsCheckSteps++;

                CodeInstruction[] clone_IfKnown_AlreadyKnown_PostfixAppend_ForAddsMods = new CodeInstruction[match_IfKnown_AlreadyKnown_PostfixAppend.Length];
                CodeInstruction[] clone_IfKnown_AlreadyKnown_PostfixAppend_ForBeforeObliterate = new CodeInstruction[match_IfKnown_AlreadyKnown_PostfixAppend.Length];
                for (int i = 0; i < match_IfKnown_AlreadyKnown_PostfixAppend.Length; i++)
                {
                    clone_IfKnown_AlreadyKnown_PostfixAppend_ForAddsMods[i] = codeMatcher.InstructionAt(i).Clone();
                    if (clone_IfKnown_AlreadyKnown_PostfixAppend_ForAddsMods[i].opcode == OpCodes.Brfalse_S)
                    {
                        clone_IfKnown_AlreadyKnown_PostfixAppend_ForAddsMods[i].operand = label_Return_AfterAddsMods;
                    }

                    clone_IfKnown_AlreadyKnown_PostfixAppend_ForBeforeObliterate[i] = codeMatcher.InstructionAt(i).Clone();
                    if (clone_IfKnown_AlreadyKnown_PostfixAppend_ForBeforeObliterate[i].opcode == OpCodes.Brfalse_S)
                    {
                        clone_IfKnown_AlreadyKnown_PostfixAppend_ForBeforeObliterate[i].operand = label_Return_AfterObliterate;
                    }
                }

                // find end of:
                // E.Postfix.Append("\nAdds item modification: ").Append(ItemModding.GetModificationDescription(Data.Blueprint, 0));
                // from the start
                if (codeMatcher.Start().MatchEndForward(match_AddsItemMod_PostfixAppend).IsInvalid)
                {
                    MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}: ({metricsCheckSteps}) {nameof(CodeMatcher.MatchStartForward)} failed to find instructions {nameof(match_AddsItemMod_PostfixAppend)}");
                    foreach (CodeMatch match in match_AddsItemMod_PostfixAppend)
                    {
                        MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}:     {match.opcode} {match.operand}");
                    }
                    codeMatcher.Vomit(doVomit);
                    return Instructions;
                }
                metricsCheckSteps++;

                // insert
                // E.Postfix.Append("\n\n{{rules|Requires:}} ").Append(GetRequiredSkillHumanReadable());
                // if (TinkerData.RecipeKnown(Data))
                // {
                //     E.Postfix.Append("\n\n{{rules|You already know this recipe.}}");
                // }
                codeMatcher
                    .Insert(clone_RequiresSkill_PostfixAppend_ForAddsMods)
                    .Insert(clone_IfKnown_AlreadyKnown_PostfixAppend_ForAddsMods);

                // find end of:
                // E.Postfix.Append("\nAdds item modification: ").Append(ItemModding.GetModificationDescription(Data.Blueprint, 0));
                // from the start
                if (codeMatcher.Start().MatchStartForward(match_GameObject_Obliterate).IsInvalid)
                {
                    MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}: ({metricsCheckSteps}) {nameof(CodeMatcher.MatchStartForward)} failed to find instructions {nameof(match_GameObject_Obliterate)}");
                    foreach (CodeMatch match in match_GameObject_Obliterate)
                    {
                        MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}:     {match.opcode} {match.operand}");
                    }
                    codeMatcher.Vomit(doVomit);
                    return Instructions;
                }
                metricsCheckSteps++;
                codeMatcher
                    .Advance(-1)
                    .Insert(clone_RequiresSkill_PostfixAppend_ForBeforeObliterate)
                    .Insert(clone_IfKnown_AlreadyKnown_PostfixAppend_ForBeforeObliterate);

                MetricsManager.LogModInfo(ModManager.GetMod("UD_Tinkering_Bytes"), $"Successfully transpiled {patchMethodName}");
                counter = 0;
                counterPadding = (codeMatcher.Length + 1).ToString().Length;
                foreach (CodeInstruction ci in codeMatcher.InstructionEnumeration())
                {
                    MetricsManager.LogModInfo(ModManager.GetMod("UD_Tinkering_Bytes"), $"[{counter.ToString().PadLeft(counterPadding, '0')}] {ci.opcode} {ci.operand}");
                    counter++;
                }

            }
            if (doTranspile)
            {
                codeMatcher.End().MatchStartBackwards(
                    new CodeMatch[1]
                    {
                    new(OpCodes.Ldarg_0),
                    });

                if (codeMatcher.IsInvalid)
                {
                    MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}: ({metricsCheckSteps++}) {nameof(CodeMatcher.MatchStartBackwards)} failed to find instruction {OpCodes.Ldarg_0} {"Mod"}");
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
                    MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}: ({metricsCheckSteps++}) {nameof(CodeMatcher.MatchStartBackwards)} failed to find instruction {OpCodes.Call} {nameof(GameObject.Obliterate)}");
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
                    MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}: ({metricsCheckSteps++}) {nameof(CodeMatcher.MatchStartForward)} failed to find instruction {OpCodes.Ldarg_1}");
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
                    MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}: ({metricsCheckSteps++}) {nameof(CodeMatcher.MatchStartForward)} failed to find instruction {OpCodes.Pop}");
                    return Instructions;
                }
                int endRequires = codeMatcher.Advance(1).Pos;

                codeMatcher.Start().Advance(startRequires);
                // clone
                // E.Postfix.Append("\n\n{{rules|Requires:}} ").Append(GetRequiredSkillHumanReadable());
                CodeInstruction[] appendRequires = new CodeInstruction[endRequires - startRequires];
                MetricsManager.LogModInfo(ModManager.GetMod("UD_Tinkering_Bytes"), $"{nameof(endRequires)}: {endRequires}, {nameof(startRequires)}: {startRequires} ({endRequires + 1 - startRequires})");
                for (int i = 0; i < endRequires - startRequires; i++)
                {
                    appendRequires[i] = codeMatcher.InstructionAt(i).Clone();
                    MetricsManager.LogModInfo(ModManager.GetMod("UD_Tinkering_Bytes"), $"    {appendRequires[i]?.opcode} {appendRequires[i]?.operand}");
                }
                codeMatcher.Start().Advance(endRequires);

                // find start of 
                // if (TinkerData.RecipeKnown(Data))
                // {
                //     E.Postfix.Append("\n\n{{rules|You already know this recipe.}}");
                // }
                codeMatcher.MatchStartForward(
                    new CodeMatch[1]
                    {
                    new(OpCodes.Ldarg_0),
                    });
                if (codeMatcher.IsInvalid)
                {
                    MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}: ({metricsCheckSteps++}) {nameof(CodeMatcher.MatchStartForward)} failed to find instruction {OpCodes.Ldarg_0}");
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
                    MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}: ({metricsCheckSteps++}) {nameof(CodeMatcher.MatchStartForward)} failed to find instruction {OpCodes.Pop}");
                    return Instructions;
                }
                int endAlreadyKnow = codeMatcher.Advance(1).Pos;

                codeMatcher.Start().Advance(startAlreadyKnow);
                // clone 
                // if (TinkerData.RecipeKnown(Data))
                // {
                //     E.Postfix.Append("\n\n{{rules|You already know this recipe.}}");
                // }
                CodeInstruction[] appendAlreadyKnow = new CodeInstruction[endAlreadyKnow - startAlreadyKnow];
                MetricsManager.LogModInfo(ModManager.GetMod("UD_Tinkering_Bytes"), $"{nameof(endAlreadyKnow)}: {endAlreadyKnow}, {nameof(startAlreadyKnow)}: {startAlreadyKnow} ({endAlreadyKnow + 1 - startAlreadyKnow})");
                for (int i = 0; i < endAlreadyKnow - startAlreadyKnow; i++)
                {
                    appendAlreadyKnow[i] = codeMatcher.InstructionAt(i).Clone();
                    MetricsManager.LogModInfo(ModManager.GetMod("UD_Tinkering_Bytes"), $"    {appendAlreadyKnow[i]?.opcode} {appendAlreadyKnow[i]?.operand}");
                }
                MetricsManager.LogModInfo(ModManager.GetMod("UD_Tinkering_Bytes"), $"{nameof(codeMatcher.Advance)}: {endAlreadyKnow}");
                codeMatcher.Start().Advance(endAlreadyKnow);

                // remove
                // E.Postfix.Append("\n\n{{rules|Requires:}} ").Append(GetRequiredSkillHumanReadable());
                // if (TinkerData.RecipeKnown(Data))
                // {
                //     E.Postfix.Append("\n\n{{rules|You already know this recipe.}}");
                // }
                // codeMatcher.RemoveInstructionsInRange(startRequires, endAlreadyKnow);

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
                    MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}: ({metricsCheckSteps++}) {nameof(CodeMatcher.MatchStartForward)} failed to find instruction {OpCodes.Ldstr} {"Mod"}");
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
                    MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}: ({metricsCheckSteps++}) {nameof(CodeMatcher.MatchStartForward)} failed to find instruction {OpCodes.Pop}");
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

                codeMatcher.MatchStartForward(
                    new CodeMatch[1]
                    {
                    new(OpCodes.Pop),
                    });
                if (codeMatcher.IsInvalid)
                {
                    MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}: ({metricsCheckSteps++}) {nameof(CodeMatcher.MatchStartForward)} failed to find instruction {OpCodes.Pop}");
                    return Instructions;
                }

                codeMatcher.MatchStartForward(
                    new CodeMatch[1]
                    {
                    new(OpCodes.Pop),
                    });
                if (codeMatcher.IsInvalid)
                {
                    MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}: ({metricsCheckSteps++}) {nameof(CodeMatcher.MatchStartForward)} failed to find instruction {OpCodes.Pop}");
                    return Instructions;
                }
                // codeMatcher.Advance(1);
                codeMatcher.CreateLabel(out Label addsModInsertReturnsFalse);

                codeMatcher.MatchStartBackwards(
                    new CodeMatch[1]
                    {
                    new(OpCodes.Brfalse_S),
                    });
                if (codeMatcher.IsInvalid)
                {
                    MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}: ({metricsCheckSteps++}) {nameof(CodeMatcher.MatchStartBackwards)} failed to find instruction {OpCodes.Brfalse_S}");
                    return Instructions;
                }
                codeMatcher.Instruction.operand = addsModInsertReturnsFalse;

                codeMatcher.Start();

                // find
                // gameObject.Obliterate();
                codeMatcher.MatchStartForward(
                    new CodeMatch[1]
                    {
                    new(instruction => instruction != null && instruction.Calls(AccessTools.Method(typeof(GameObject), nameof(GameObject.Obliterate), new Type[] { typeof(string), typeof(bool), typeof(string) }))),
                    });
                if (codeMatcher.IsInvalid)
                {
                    MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}: ({metricsCheckSteps++}) {nameof(CodeMatcher.MatchStartForward)} failed to find instruction {OpCodes.Call} {nameof(GameObject.Obliterate)}");
                    return Instructions;
                }

                // find
                // gameObject.Obliterate();
                codeMatcher.MatchStartBackwards(
                    new CodeMatch[1]
                    {
                    new(OpCodes.Ldloc_0),
                    });
                if (codeMatcher.IsInvalid)
                {
                    MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}: ({metricsCheckSteps++}) {nameof(CodeMatcher.MatchStartBackwards)} failed to find instruction {OpCodes.Ldloc_0}");
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

                codeMatcher.MatchStartForward(
                    new CodeMatch[1]
                    {
                    new(OpCodes.Pop),
                    });
                if (codeMatcher.IsInvalid)
                {
                    MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}: ({metricsCheckSteps++}) {nameof(CodeMatcher.MatchStartForward)} failed to find instruction {OpCodes.Pop}");
                    return Instructions;
                }
                codeMatcher.MatchStartForward(
                    new CodeMatch[1]
                    {
                    new(OpCodes.Pop),
                    });
                if (codeMatcher.IsInvalid)
                {
                    MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}: ({metricsCheckSteps++}) {nameof(CodeMatcher.MatchStartForward)} failed to find instruction {OpCodes.Pop}");
                    return Instructions;
                }
                // codeMatcher.Advance(1);
                codeMatcher.CreateLabel(out Label obliterateInsertReturnsFalse);

                codeMatcher.MatchStartBackwards(
                    new CodeMatch[1]
                    {
                    new(OpCodes.Brfalse_S),
                    });
                if (codeMatcher.IsInvalid)
                {
                    MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}: ({metricsCheckSteps++}) {nameof(CodeMatcher.MatchStartBackwards)} failed to find instruction {OpCodes.Brfalse_S}");
                    return Instructions;
                }
                codeMatcher.Instruction.operand = obliterateInsertReturnsFalse;

                codeMatcher.Start();

                foreach (CodeInstruction ci in codeMatcher.InstructionEnumeration())
                {
                    //MetricsManager.LogModInfo(ModManager.GetMod("UD_Tinkering_Bytes"), $"{ci?.opcode} {ci?.operand}");
                    if (ci?.opcode == OpCodes.Ldloc_0)
                    {
                        break;
                    }
                }
            }
            return codeMatcher.InstructionEnumeration();
        }

        public static IEnumerable<CodeInstruction> Vomit(this IEnumerable<CodeInstruction> Instructions, bool Do = false)
        {
            if (Do)
            {
                List<CodeInstruction> instructions = new(Instructions);
                int counter = 0;
                int counterPadding = (instructions.Count + 1).ToString().Length;
                foreach (CodeInstruction ci in instructions)
                {
                    MetricsManager.LogModInfo(ModManager.GetMod("UD_Tinkering_Bytes"), $"[{counter.ToString().PadLeft(counterPadding, '0')}] {ci.opcode} {ci.operand}");
                    counter++;
                }
            }
            return Instructions;
        }
        public static CodeMatcher Vomit(this CodeMatcher CodeMatcher, bool Do = false)
        {
            if (Do)
            {
                CodeMatcher.Instructions().Vomit(Do);
            }
            return CodeMatcher;
        }
    }
}
