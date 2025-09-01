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
        public static IEnumerable<CodeInstruction> HandleEvent_ObfuscateUnknown_Transpile(IEnumerable<CodeInstruction> Instructions, ILGenerator Generator)
        {
            bool doVomit = true;
            string patchMethodName = $"{nameof(DataDisk_Patches)}.{nameof(DataDisk.HandleEvent)}({nameof(GetShortDescriptionEvent)})";
            int metricsCheckSteps = 0;

            CodeMatcher codeMatcher = new(Instructions, Generator);

            // return base.HandleEvent(E);
            CodeMatch[] match_Return_BaseHandleEvent_E = new CodeMatch[]
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldarg_1),
                new(ins => ins.Calls(AccessTools.Method(typeof(IComponent<GameObject>), nameof(IComponent<GameObject>.HandleEvent), new Type[] { typeof(GetShortDescriptionEvent) }))),
                new(OpCodes.Ret),
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
            codeMatcher.Advance(-1).CreateLabel(out Label label_Pop_Return_BaseHandleEvent_E);
            codeMatcher.Advance(1).CreateLabel(out Label label_Return_BaseHandleEvent_E);

            static bool IsMethodGetRequiredSkillHumanReadable(MethodInfo Method)
            {
                return Method.Name == nameof(DataDisk.GetRequiredSkillHumanReadable) 
                    && !Method.IsStatic 
                    && Method.GetParameters().IsNullOrEmpty();
            }
            // instance DataDisk.GetRequiredSkillHumanReadable();
            if (AccessTools.FirstMethod(typeof(DataDisk), mi => IsMethodGetRequiredSkillHumanReadable(mi))
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
            CodeInstruction[] instr_RequiresSkill_PostfixAppend = new CodeInstruction[]
            {
                new(OpCodes.Ldarg_1),
                new(OpCodes.Ldfld, AccessTools.Field(typeof(IShortDescriptionEvent), nameof(IShortDescriptionEvent.Postfix))),
                new(OpCodes.Ldstr, "\n\n{{rules|Requires:}} "),
                new(OpCodes.Call, AccessTools.Method(typeof(StringBuilder), nameof(StringBuilder.Append), new Type[] { typeof(string) })),
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, dataDisk_GetRequiredSkillHumanReadable_Instance),
                new(OpCodes.Call, AccessTools.Method(typeof(StringBuilder), nameof(StringBuilder.Append), new Type[] { typeof(string) })),
                new(OpCodes.Pop),
            };

            // find start of:
            // return base.HandleEvent(E);
            // from the start
            if (codeMatcher.Start().MatchStartForward(match_RequiresSkill_PostfixAppend).IsInvalid)
            {
                MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}: ({metricsCheckSteps}) {nameof(CodeMatcher.MatchStartForward)} failed to find instructions {nameof(match_RequiresSkill_PostfixAppend)}");
                foreach (CodeMatch match in match_RequiresSkill_PostfixAppend)
                {
                    MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}:     {match.opcode} {match.operand}");
                }
                codeMatcher.Vomit(doVomit);
                return Instructions;
            }
            metricsCheckSteps++;

            // create label at top
            codeMatcher.CreateLabel(out Label label_RequiresSkill_PostfixAppend);

            // if (Data.Type == "Mod")
            CodeInstruction[] instr_If_DataTypeMod = new CodeInstruction[]
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldfld, AccessTools.Field(typeof(DataDisk), nameof(DataDisk.Data))),
                new(OpCodes.Ldfld, AccessTools.Field(typeof(TinkerData), nameof(TinkerData.Type))),
                new(OpCodes.Ldstr, "Mod"),
                new(OpCodes.Call, AccessTools.Method(typeof(string), "op_Equality", new Type[] { typeof(string), typeof(string) })),
                new(OpCodes.Brfalse_S, label_Return_BaseHandleEvent_E), // label_Pop_Return_BaseHandleEvent_E
            };
            codeMatcher.Insert(instr_If_DataTypeMod)
                .CreateLabel(out Label label_If_DataTypeMod);

            // if (gameObject != null)
            CodeMatch[] match_If_GameObjectNull = new CodeMatch[]
            {
                    new(OpCodes.Ldloc_0),
                    new(OpCodes.Brfalse),
            };

            // find end of:
            // if (gameObject != null)
            // from start
            if (codeMatcher.Start().MatchEndForward(match_If_GameObjectNull).IsInvalid)
            {
                MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}: ({metricsCheckSteps}) {nameof(CodeMatcher.MatchStartForward)} failed to find instructions {nameof(match_If_GameObjectNull)}");
                foreach (CodeMatch match in match_If_GameObjectNull)
                {
                    MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}:     {match.opcode} {match.operand}");
                }
                codeMatcher.Vomit(doVomit);
                return Instructions;
            }
            if (codeMatcher.Instruction.operand is Label label_If_GameObjectNull && label_If_GameObjectNull == label_If_DataTypeMod)
            {
                MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{nameof(label_If_GameObjectNull)} is {nameof(label_RequiresSkill_PostfixAppend)}");
            }
            else
            {
                codeMatcher.Instruction.operand = label_If_DataTypeMod;
                MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{nameof(label_If_GameObjectNull)} not {nameof(label_RequiresSkill_PostfixAppend)}");
            }

            // create label afterwards
            codeMatcher
                .Advance(1)
                .CreateLabel(out Label label_TinkeringHelpers_StripForTinkering);

            CodeInstruction[] instr_If_Understood_PlayerNotNull_HasSkillOrScanning = new CodeInstruction[]
            {
                // if (
                //     gameObject.Understood()
                new(OpCodes.Ldloc_0),
                CodeInstruction.Call(typeof(GameObject), nameof(GameObject.Understood)),
                new(OpCodes.Brfalse, label_If_DataTypeMod), // label_RequiresSkill_PostfixAppend

                //     && The.Player != null
                CodeInstruction.Call(typeof(The), $"get_{nameof(The.Player)}"),
                new(OpCodes.Brfalse, label_If_DataTypeMod), // label_RequiresSkill_PostfixAppend

                //     && (
                //         The.Player.HasSkill("Tinkering")
                CodeInstruction.Call(typeof(The), $"get_{nameof(The.Player)}"),
                new(OpCodes.Ldstr, "Tinkering"),
                CodeInstruction.Call(typeof(GameObject), nameof(GameObject.HasSkill)),
                new(OpCodes.Brtrue_S, label_TinkeringHelpers_StripForTinkering),

                //         || Scanning.HasScanningFor(The.Player, Scanning.Scan.Tech)
                //     )
                // )
                CodeInstruction.Call(typeof(The), $"get_{nameof(The.Player)}"),
                new(OpCodes.Ldc_I4_1),
                CodeInstruction.Call(typeof(Scanning), nameof(Scanning.HasScanningFor), new Type[] { typeof(GameObject), typeof(Scanning.Scan) }),
                new(OpCodes.Brfalse, label_If_DataTypeMod), // label_RequiresSkill_PostfixAppend
            };

            // insert condition:
            // if (gameObject.Understood() && The.Player != null && (The.Player.HasSkill("Tinkering") || Scanning.HasScanningFor(The.Player, Scanning.Scan.Tech)))
            codeMatcher.Insert(instr_If_Understood_PlayerNotNull_HasSkillOrScanning);

            // if (Data != null)
            CodeMatch[] match_If_DataNull = new CodeMatch[]
            {
                new(OpCodes.Ldarg_0),
                new(ins => ins.LoadsField(AccessTools.Field(typeof(DataDisk), nameof(DataDisk.Data)))),
                new(OpCodes.Brfalse),
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
                new(OpCodes.Pop),
                // }
            };
            CodeMatch[] match_IfKnown_AlreadyKnown_PostfixAppend = match_IfKnown.Concat(match_AlreadyKnown_PostfixAppend).ToArray();

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
            // gameObject.Obliterate();
            // from the start
            if (codeMatcher.Start().MatchStartForward(match_GameObject_Obliterate).IsInvalid)
            {
                MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}: ({metricsCheckSteps}) {nameof(CodeMatcher.MatchStartForward)} failed to find instructions {nameof(match_GameObject_Obliterate)}");
                foreach (CodeMatch match in match_GameObject_Obliterate)
                {
                    MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"    {match.name} {match.opcode}");
                }
                codeMatcher.Vomit(doVomit);
                return Instructions;
            }
            metricsCheckSteps++;
            codeMatcher.Insert(instr_RequiresSkill_PostfixAppend)
                .CreateLabel(out Label label_RequiresSkill_PostFixAppend_BeforeObliterate)
                .Advance(instr_RequiresSkill_PostfixAppend.Length);

            // insert:
            // if (TinkerData.RecipeKnown(Data))
            // {
            //     E.Postfix.Append("\n\n{{rules|You already know this recipe.}}");
            // }
            CodeInstruction[] instr_AlreadyKnown_PostfixAppend = new CodeInstruction[]
            {
                new(OpCodes.Ldarg_1),
                new(OpCodes.Ldfld, AccessTools.Field(typeof(IShortDescriptionEvent), nameof(IShortDescriptionEvent.Postfix))),
                new(OpCodes.Ldstr, "\n\n{{rules|You already know this recipe.}}"),
                new(OpCodes.Call, AccessTools.Method(typeof(StringBuilder), nameof(StringBuilder.Append), new Type[] { typeof(string) })),
                new(OpCodes.Pop),
            };
            codeMatcher.InsertAndAdvance(instr_AlreadyKnown_PostfixAppend)
                .CreateLabel(out Label label_GameObject_Obliterate)
                .Advance(-1)
                .CreateLabel(out Label label_Pop_GameObject_Obliterate)
                .Advance(1);

            CodeInstruction[] instr_IfKnown = new CodeInstruction[]
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldfld, AccessTools.Field(typeof(DataDisk), nameof(DataDisk.Data))),
                new(OpCodes.Call, AccessTools.Method(typeof(TinkerData), nameof(TinkerData.RecipeKnown), new Type[] { typeof(TinkerData) })),
                new(OpCodes.Brfalse_S, label_GameObject_Obliterate), // was label_Pop_GameObject_Obliterate
            };
            codeMatcher.Advance(-instr_AlreadyKnown_PostfixAppend.Length);
            codeMatcher.Insert(instr_IfKnown);

            // unused
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
                new(OpCodes.Br),
            };

            // find end of:
            // E.Postfix.Append("\nAdds item modification: ").Append(ItemModding.GetModificationDescription(Data.Blueprint, 0));
            // from the start
            if (codeMatcher.Start().MatchEndForward(match_AddsItemMod_PostfixAppend).IsInvalid)
            {
                MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}: ({metricsCheckSteps}) {nameof(CodeMatcher.MatchStartForward)} failed to find instructions {nameof(match_AddsItemMod_PostfixAppend)}");
                foreach (CodeMatch match in match_AddsItemMod_PostfixAppend)
                {
                    MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"    {match.name} {match.opcode}");
                }
                codeMatcher.Vomit(doVomit);
                return Instructions;
            }
            codeMatcher.Instruction.operand = label_If_DataTypeMod;

            // unused
            // find start of:
            // if (Data != null)
            // from the start
            if (codeMatcher.Start().MatchEndForward(match_If_DataNull).IsInvalid)
            {
                MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}: ({metricsCheckSteps}) {nameof(CodeMatcher.MatchStartForward)} failed to find instructions {nameof(match_If_DataNull)}");
                foreach (CodeMatch match in match_If_DataNull)
                {
                    MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"    {match.name} {match.opcode}");
                }
                codeMatcher.Vomit(doVomit);
                return Instructions;
            }
            metricsCheckSteps++;
            // codeMatcher.Instruction.operand = label_Return_BaseHandleEvent_E;

            // if (part2 != null)
            CodeMatch[] match_If_DescriptionNull = new CodeMatch[]
            {
                new(OpCodes.Ldloc_2),
                new(OpCodes.Brfalse_S),
            };
            // find end of:
            // if (part2 != null)
            // from the start
            if (codeMatcher.Start().MatchEndForward(match_If_DescriptionNull).IsInvalid)
            {
                MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}: ({metricsCheckSteps}) {nameof(CodeMatcher.MatchStartForward)} failed to find instructions {nameof(match_If_DescriptionNull)}");
                foreach (CodeMatch match in match_If_DescriptionNull)
                {
                    MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"    {match.name} {match.opcode}");
                }
                codeMatcher.Vomit(doVomit);
                return Instructions;
            }
            metricsCheckSteps++;
            codeMatcher.Instruction.operand = label_RequiresSkill_PostFixAppend_BeforeObliterate;

            MetricsManager.LogModInfo(ModManager.GetMod("UD_Tinkering_Bytes"), $"Successfully transpiled {patchMethodName}");
            return codeMatcher.Vomit(doVomit).InstructionEnumeration();
        }

        [HarmonyPatch(
            declaringType: typeof(DataDisk),
            methodName: nameof(DataDisk.HandleEvent),
            argumentTypes: new Type[] { typeof(GetDisplayNameEvent) },
            argumentVariations: new ArgumentType[] { ArgumentType.Normal })]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> HandleEvent_ShowMods_Transpile(IEnumerable<CodeInstruction> Instructions, ILGenerator Generator)
        {
            bool doVomit = true;
            string patchMethodName = $"{nameof(DataDisk_Patches)}.{nameof(DataDisk.HandleEvent)}({nameof(GetDisplayNameEvent)})";
            int metricsCheckSteps = 0;

            CodeMatcher codeMatcher = new(Instructions, Generator);

            // if (E.AsIfKnown
            //     || (E.Understood()
            //         && The.Player != null
            //         && (The.Player.HasSkill("Tinkering") || Scanning.HasScanningFor(The.Player, Scanning.Scan.Tech))))
            CodeMatch[] match_If_KnownUnderstoodSkilledScanning = new CodeMatch[]
            {
                // E.AsIfKnown
                new(OpCodes.Ldarg_1),
                new(ins => ins.LoadsField(AccessTools.Field(typeof(GetDisplayNameEvent), nameof(GetDisplayNameEvent.AsIfKnown)))),
                new(OpCodes.Brtrue_S),

                // E.Understood
                new(OpCodes.Ldarg_1),
                new(ins => ins.Calls(AccessTools.Method(typeof(GetDisplayNameEvent), nameof(GetDisplayNameEvent.Understood)))),
                new(OpCodes.Brfalse_S),

                // The.Player != null
                new(ins => ins.Calls(AccessTools.Method(typeof(The), $"get_{nameof(The.Player)}"))),
                new(OpCodes.Brfalse_S),

                // The.Player.HasSkill("Tinkering")
                new(ins => ins.Calls(AccessTools.Method(typeof(The), $"get_{nameof(The.Player)}"))),
                new(OpCodes.Ldstr, "Tinkering"),
                new(ins => ins.Calls(AccessTools.Method(typeof(GameObject), nameof(GameObject.HasSkill), new Type[] { typeof(string) }))),
                new(OpCodes.Brtrue_S),

                // Scanning.HasScanningFor(The.Player, Scanning.Scan.Tech)
                new(ins => ins.Calls(AccessTools.Method(typeof(The), $"get_{nameof(The.Player)}"))),
                new(OpCodes.Ldc_I4_1),
                new(ins => ins.Calls(AccessTools.Method(typeof(Scanning), nameof(Scanning.HasScanningFor), new Type[] { typeof(GameObject), typeof(Scanning.Scan) }))),
                new(OpCodes.Brfalse_S),
            };

            // find start of:
            // if (E.AsIfKnown
            //     || (E.Understood()
            //         && The.Player != null
            //         && (The.Player.HasSkill("Tinkering") || Scanning.HasScanningFor(The.Player, Scanning.Scan.Tech))))
            // from the end
            if (codeMatcher.End().MatchStartBackwards(match_If_KnownUnderstoodSkilledScanning).IsInvalid)
            {
                MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"{patchMethodName}: ({metricsCheckSteps}) {nameof(CodeMatcher.MatchEndBackwards)} failed to find instructions {nameof(match_If_KnownUnderstoodSkilledScanning)}");
                foreach (CodeMatch match in match_If_KnownUnderstoodSkilledScanning)
                {
                    MetricsManager.LogModError(ModManager.GetMod("UD_Tinkering_Bytes"), $"    {match.name} {match.opcode}");
                }
                codeMatcher.Vomit(doVomit);
                return Instructions;
            }
            metricsCheckSteps++;
            codeMatcher.RemoveInstructions(match_If_KnownUnderstoodSkilledScanning.Length);

            MetricsManager.LogModInfo(ModManager.GetMod("UD_Tinkering_Bytes"), $"Successfully transpiled {patchMethodName}");
            return codeMatcher.Vomit(doVomit).InstructionEnumeration();
        }

        public static IEnumerable<CodeInstruction> Vomit(this IEnumerable<CodeInstruction> Instructions, bool Do = false)
        {
            if (Do)
            {
                int counter = 0;
                int counterPadding = Math.Max(4, (Instructions.Count() + 1).ToString().Length);
                foreach (CodeInstruction ci in Instructions)
                {
                    string ciOperand = ci?.operand?.ToString();
                    if (ci?.operand?.GetType() == typeof(string))
                    {
                        ciOperand = ci.operand?.ToString()?.ToLiteral(Quotes: true);
                    }
                    UnityEngine.Debug.Log($"[{counter.ToString().PadLeft(counterPadding, '0')}] {ci.opcode,-10} {ciOperand}");
                    counter++;

                    string ciOpcode = ci.opcode.ToString();
                    if (ciOpcode.StartsWith("pop")
                        || ciOpcode.StartsWith("br")
                        || ciOpcode.StartsWith("bl")
                        || ciOpcode.StartsWith("ret")
                        || ciOpcode.StartsWith("stloc"))
                    {
                        UnityEngine.Debug.Log("");
                    }
                }
            }
            return Instructions;
        }
        public static CodeMatcher Vomit(this CodeMatcher CodeMatcher, bool Do = false)
        {
            if (Do)
            {
                Dictionary<Label, int> labelInstructions = new();
                CodeMatcher.Start();
                while (CodeMatcher.Advance(1).IsValid)
                {
                    CodeInstruction ci = CodeMatcher.Instruction;
                    if (ci.labels.IsNullOrEmpty())
                    {
                        continue;
                    }
                    foreach(Label label in ci.labels)
                    {
                        if (!labelInstructions.ContainsKey(label))
                        {
                            labelInstructions.Add(label, CodeMatcher.Pos);
                        }
                        else
                        {
                            labelInstructions[label] = CodeMatcher.Pos;
                        }
                    }
                }
                int counter = 0;
                int counterPadding = Math.Max(4, (CodeMatcher.Instructions().Count + 1).ToString().Length);

                foreach (CodeInstruction ci in CodeMatcher.InstructionEnumeration())
                {
                    string ciOperand = ci?.operand?.ToString();
                    if (ci?.operand?.GetType() == typeof(string))
                    {
                        ciOperand = ci.operand?.ToString()?.ToLiteral(Quotes: true);
                    }
                    else
                    if (ci.operand is Label ciLabel)
                    {
                        string ciLabelString = "????";
                        if (labelInstructions.ContainsKey(ciLabel))
                        {
                            ciLabelString = labelInstructions[ciLabel].ToString().PadLeft(counterPadding, '0');
                        }
                        ciOperand = $"[{ciLabelString}]";
                    }
                    UnityEngine.Debug.Log($"[{counter.ToString().PadLeft(counterPadding, '0')}] {ci.opcode,-10} {ciOperand}");
                    counter++;

                    string ciOpcode = ci.opcode.ToString();
                    if (ciOpcode.StartsWith("pop")
                        || ciOpcode.StartsWith("br")
                        || ciOpcode.StartsWith("bl")
                        || ciOpcode.StartsWith("ret")
                        || ciOpcode.StartsWith("stloc"))
                    {
                        UnityEngine.Debug.Log("");
                    }
                }
            }
            return CodeMatcher;
        }
        public static CodeMatcher VomitInstruction(this CodeMatcher CodeMatcher, string Context = null)
        {
            int counter = CodeMatcher.Pos;
            int counterPadding = Math.Max(4, (CodeMatcher.Length + 1).ToString().Length);

            CodeInstruction ci = CodeMatcher.Instruction;
            string ciOperand = ci?.operand?.ToString();
            if (ci?.operand?.GetType() == typeof(string))
            {
                ciOperand = ci.operand?.ToString()?.ToLiteral(Quotes: true);
            }
            UnityEngine.Debug.Log($"[{counter.ToString().PadLeft(counterPadding, '0')}] {ci.opcode,-10} {ciOperand} {Context}");
            return CodeMatcher;
        }
    }
}
