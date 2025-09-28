using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using XRL.Language;
using XRL.Messages;
using XRL.Rules;
using XRL.UI;
using XRL.World.Capabilities;
using XRL.World.Parts.Skill;
using XRL.World.Tinkering;

using UD_Modding_Toolbox;
using UD_Vendor_Actions;

using UD_Tinkering_Bytes;

using static UD_Tinkering_Bytes.Options;
using XRL.World.Text;

namespace XRL.World.Parts
{
    [AlwaysHandlesVendor_UD_VendorActions]
    [Serializable]
    public class UD_VendorDisassembly : IScribedPart, I_UD_VendorActionEventHandler
    {
        private static bool doDebug = true;

        public const string COMMAND_DISASSEMBLE = "CmdVendorDisassemble";
        public const string COMMAND_DISASSEMBLE_ALL = "CmdVendorDisassembleAll";

        public bool WantVendorActions => (bool)ParentObject?.HasSkill(nameof(Tinkering_Disassemble)) && (bool)!ParentObject?.IsPlayer();

        public List<TinkerData> KnownRecipes => ParentObject?.GetPart<UD_VendorTinkering>()?.KnownRecipes;

        // Disassembly 
        public Disassembly Disassembly;

        private static int CurrentDisassembly;

        public UD_VendorDisassembly()
        {
            ResetDisassembly();
        }

        public virtual void ResetDisassembly()
        {
            Disassembly = null;
            CurrentDisassembly = 0;
        }

        public static void VendorDisassemblyProcessDisassemblingWhat(Disassembly Disassembly)
        {
            if (!Disassembly.DisassemblingWhat.IsNullOrEmpty())
            {
                string disassemblingWhat = Disassembly.DisassemblingWhat;
                if (Disassembly.NumberDone > 1 || Disassembly.NumberWanted > 1)
                {
                    disassemblingWhat = disassemblingWhat + " x" + Disassembly.NumberDone;
                }
                if (Disassembly.DisassembledWhat == null)
                {
                    Disassembly.DisassembledWhat = disassemblingWhat;
                    Disassembly.DisassembledWhere = Disassembly.DisassemblingWhere;
                }
                else
                {
                    if (Disassembly.DisassembledWhats.IsNullOrEmpty())
                    {
                        Disassembly.DisassembledWhats ??= new();
                        Disassembly.DisassembledWhatsWhere ??= new();

                        Disassembly.DisassembledWhats.Add(Disassembly.DisassembledWhat);
                        Disassembly.DisassembledWhatsWhere[Disassembly.DisassembledWhat] = Disassembly.DisassembledWhere;
                    }
                    Disassembly.DisassembledWhats.Add(disassemblingWhat);
                    Disassembly.DisassembledWhatsWhere[disassemblingWhat] = Disassembly.DisassemblingWhere;
                }
                Disassembly.DisassemblingWhat = null;
                Disassembly.DisassemblingWhere = null;
            }
        }

        public static bool VendorDisassemblyContinue(
            GameObject Vendor, 
            Disassembly Disassembly, 
            UD_SyncedDisassembly SyncedDisassembly, 
            ref List<TinkerData> KnownRecipes)
        {
            if (Disassembly == null)
            {
                MetricsManager.LogPotentialModError(UD_Tinkering_Bytes.Utils.ThisMod,
                    $"{nameof(UD_VendorDisassembly)}.{nameof(VendorDisassemblyContinue)} passed null {nameof(Disassembly)}");
                return false;
            }

            int indent = Debug.LastIndent;
            GameObject Item = Disassembly.Object;

            int counter;
            if (Disassembly != null)
            {
                counter = CurrentDisassembly = Disassembly.TotalNumberDone + 1;
            }
            else
            {
                counter = ++CurrentDisassembly;
            }
            int counterPadding = (int)Disassembly?.TotalNumberWanted.ToString().Length;
            string paddedCounter = (counter).ToString().PadLeft((int)Disassembly?.TotalNumberWanted.ToString().Length);
            Debug.LoopItem(4, 
                $"{paddedCounter}/{Disassembly?.TotalNumberWanted}] {nameof(VendorDisassemblyContinue)}, " +
                $"{nameof(Item)}: {Item?.ShortDisplayNameSingleStripped ?? Const.NULL}",
                Indent: indent + 1, Toggle: doDebug);

            int currentDisassembly = CurrentDisassembly;

            ReplaceBuilder RB = ReplaceBuilder.Get();
            RB.AddObject(Vendor, nameof(Vendor));
            RB.AddObject(Item, nameof(Item));
            RB.AddObject(The.Player, nameof(The.Player));
            if (!GameObject.Validate(ref Disassembly.Object))
            {
                Disassembly.InterruptBecause = RB.Start($"the item ={nameof(Vendor)}.subjective ==verb:were:afterpronoun= working on disappeared").ToString();
                Debug.LastIndent = indent;
                return false;
            }
            if (Item.IsInGraveyard())
            {
                Disassembly.InterruptBecause = RB.Start($"the item ={nameof(Vendor)}.subjective ==verb:were:afterpronoun= working on was destroyed").ToString();
                Debug.LastIndent = indent;
                return false;
            }
            if (Item.IsNowhere())
            {
                Disassembly.InterruptBecause = RB.Start($"the item ={nameof(Vendor)}.subjective ==verb:were:afterpronoun= working on disappeared").ToString();
                Debug.LastIndent = indent;
                return false;
            }
            if (Item.IsInStasis())
            {
                Disassembly.InterruptBecause = RB.Start($"={nameof(Vendor)}.subjective= can no longer interact with ={nameof(Item)}.t=").ToString();
                Debug.LastIndent = indent;
                return false;
            }
            if (!Item.TryGetPart<TinkerItem>(out var tinkerItem))
            {
                Disassembly.InterruptBecause = RB.Start($"={nameof(Item)}.subjective= can no longer be disassembled").ToString();
                Debug.LastIndent = indent;
                return false;
            }
            if (!Vendor.HasSkill(nameof(Tinkering_Disassemble)))
            {
                Disassembly.InterruptBecause = RB.Start($"={nameof(Vendor)}.subjective= no longer =verb:know:afterpronoun= how to disassemble things").ToString();
                Debug.LastIndent = indent;
                return false;
            }
            if (!tinkerItem.CanBeDisassembled(Vendor))
            {
                Disassembly.InterruptBecause = RB.Start($"={nameof(Item)}.t= can no longer be disassembled").ToString();
                Debug.LastIndent = indent;
                return false;
            }
            if (!Vendor.CanMoveExtremities("Disassemble", ShowMessage: false, Involuntary: false, AllowTelekinetic: true))
            {
                Disassembly.InterruptBecause = RB.Start($"={nameof(Vendor)}.subjective= can no longer move ={nameof(Vendor)}.possessive= extremities").ToString();
                Debug.LastIndent = indent;
                return false;
            }
            if (Vendor.ArePerceptibleHostilesNearby(true, true, Action: SyncedDisassembly))
            {
                Disassembly.InterruptBecause = RB.Start($"={nameof(Vendor)}.subjective= can no longer disassemble safely").ToString();
                Debug.LastIndent = indent;
                return false;
            }
            ReplaceBuilder.Return(RB);
            UD_VendorTinkering vendorTinkering = null;
            int totalEnergyCost = 0;
            try
            {
                bool interrupt = false;
                if (Disassembly.BitChance == int.MinValue)
                {
                    if (tinkerItem.Bits.Length == 1)
                    {
                        Disassembly.BitChance = 0;
                    }
                    else
                    {
                        int disassembleBonus = Vendor.GetIntProperty("DisassembleBonus");
                        Disassembly.BitChance = 50;
                        disassembleBonus = GetTinkeringBonusEvent.GetFor(Vendor, Item, "Disassemble", Disassembly.BitChance, disassembleBonus, ref interrupt);
                        if (!interrupt)
                        {
                            disassembleBonus = GetVendorTinkeringBonusEvent.GetFor(Vendor, Item, "Disassemble", disassembleBonus, disassembleBonus, ref interrupt);
                        }
                        if (interrupt)
                        {
                            Debug.LastIndent = indent;
                            return false;
                        }
                        Disassembly.BitChance += disassembleBonus;
                    }
                }
                string activeBlueprint = tinkerItem.ActiveBlueprint;
                TinkerData learnableBuildRecipe = null;
                List<TinkerData> learnableModRecipes = null;
                List<TinkerData> knownRecipes = new(KnownRecipes);
                if (Vendor.HasSkill(nameof(Tinkering_ReverseEngineer))
                    && TinkerData.TinkerRecipes.Any(datum => 
                        (Item.HasPart(datum.PartName) && !knownRecipes.Contains(datum)) 
                        || (datum.Blueprint == Item.Blueprint && !knownRecipes.Contains(datum))))
                {
                    Debug.Entry(4, 
                        $"Processing {Skills.GetGenericSkill(nameof(Tinkering_ReverseEngineer))?.DisplayName ?? nameof(Tinkering_ReverseEngineer)}",
                        Indent: indent + 2, Toggle: doDebug);

                    vendorTinkering = Vendor.RequirePart<UD_VendorTinkering>();

                    learnableBuildRecipe = TinkerData.TinkerRecipes.FirstOrDefault(t => t.Type == "Build" && t.Blueprint == activeBlueprint && !knownRecipes.Any(r => r.IsSameDatumAs(t)));

                    List<TinkerData> attachedMods = 
                        (from mod in Item.GetPartsDescendedFrom<IModification>()
                         where !knownRecipes.Any(r => r.PartName == mod.Name)
                         select TinkerData.TinkerRecipes.FirstOrDefault(r => r.PartName == mod.Name)
                        ).ToList();

                    learnableModRecipes =
                        (from recipe in TinkerData.TinkerRecipes
                         where attachedMods.Any(mod => mod.IsSameDatumAs(recipe))
                         select recipe
                        ).ToList();

                    Debug.Divider(4, Const.HONLY, Count: 56, Indent: indent + 3, Toggle: doDebug);
                    if (learnableBuildRecipe != null)
                    {
                        Debug.CheckYeh(4, $"{activeBlueprint} recipe loaded up", Indent: indent + 3, Toggle: doDebug);
                    }
                    else
                    {
                        Debug.CheckNah(4, $"{activeBlueprint} recipe \"Already Know\"", Indent: indent + 3, Toggle: doDebug);
                    }

                    foreach (TinkerData attachedMod in attachedMods)
                    {
                        Debug.Divider(4, Const.HONLY, Count: 56, Indent: indent + 3, Toggle: doDebug);
                        Debug.LoopItem(4, $"{attachedMod.PartName ?? "[mod]" + attachedMod.Blueprint}", Indent: indent + 3, Toggle: doDebug);
                        string attachedModName = attachedMod.Blueprint ?? attachedMod.PartName;
                        if (learnableModRecipes.Contains(attachedMod))
                        {
                            Debug.CheckYeh(4, $"{attachedModName} recipe loaded up", Indent: indent + 3, Toggle: doDebug);
                        }
                        else
                        {
                            Debug.CheckNah(4, $"{attachedModName} recipe \"Already Know\"", Indent: indent + 3, Toggle: doDebug);
                        }
                    }
                    Debug.Divider(4, Const.HONLY, Count: 56, Indent: indent + 3, Toggle: doDebug);
                }

                int reverseEngineerChance = 0;
                int reverseEngineerBonus = 0;
                if (learnableBuildRecipe != null || (learnableModRecipes != null && learnableModRecipes.Count > 0))
                {
                    reverseEngineerChance = 15;
                    reverseEngineerBonus = GetTinkeringBonusEvent.GetFor(Vendor, Item, "ReverseEngineer", reverseEngineerChance, reverseEngineerBonus, ref interrupt);
                    if (!interrupt)
                    {
                        reverseEngineerBonus = GetVendorTinkeringBonusEvent.GetFor(Vendor, Item, "ReverseEngineer", reverseEngineerBonus, reverseEngineerBonus, ref interrupt);
                    }
                    if (interrupt)
                    {
                        Debug.LastIndent = indent;
                        return false;
                    }
                    reverseEngineerChance += reverseEngineerBonus;
                    Debug.Entry(4, $"{nameof(reverseEngineerChance)}", reverseEngineerChance.ToString(), Indent: indent + 3, Toggle: doDebug);
                }
                try
                {
                    InventoryActionEvent.Check(Item, Vendor, Item, "EmptyForDisassemble");
                }
                catch (Exception x)
                {
                    MetricsManager.LogError("EmptyForDisassemble", x);
                }
                bool multipleObjects = Disassembly.NumberWanted > 1 && Disassembly.OriginalCount > 1;
                if (!Item.IsTemporary)
                {
                    Disassembly.WasTemporary = false;
                }
                if (Disassembly.DisassemblingWhat == null)
                {
                    Disassembly.DisassemblingWhat = Item.t(Single: true);
                    if (multipleObjects || Disassembly.TotalNumberWanted > 1)
                    {
                        RB = GameText.StartReplace($"={nameof(Vendor)}.T= =verb:start:afterpronoun= disassembling ={nameof(Item)}.t=.");
                        RB.AddObject(Vendor, nameof(Vendor));
                        RB.AddObject(Item, nameof(Item));

                        MessageQueue.AddPlayerMessage(RB.ToString());
                    }
                }
                if (Disassembly.DisassemblingWhere == null && Item.CurrentCell != null)
                {
                    Disassembly.DisassemblingWhere = Vendor.DescribeDirectionToward(Item);
                }
                if (Disassembly.NumberDone < Disassembly.NumberWanted)
                {
                    string bitsToAward = "";
                    if (!Disassembly.WasTemporary)
                    {
                        bool itemsPerBuildBitChance = tinkerItem.NumberMade <= 1 || Stat.Random(1, tinkerItem.NumberMade + 1) == 1;

                        if (tinkerItem.Bits.Length == 1)
                        {
                            if (itemsPerBuildBitChance)
                            {
                                bitsToAward += tinkerItem.Bits;
                            }
                        }
                        else
                        {
                            int bestBitIndex = tinkerItem.Bits.Length - 1;
                            for (int i = 0; i < tinkerItem.Bits.Length; i++)
                            {
                                bool firstBitOrChance = bestBitIndex == i || Disassembly.BitChance.in100();
                                if (firstBitOrChance && itemsPerBuildBitChance)
                                {
                                    bitsToAward += tinkerItem.Bits[i];
                                }
                            }
                        }

                        if (learnableBuildRecipe != null || !learnableModRecipes.IsNullOrEmpty())
                        {
                            Debug.Entry(4, $"Checking for reverse engineer successes...", Indent: indent + 3, Toggle: doDebug);
                            if (learnableBuildRecipe != null)
                            {
                                if (!reverseEngineerChance.in100())
                                {
                                    Debug.CheckNah(4, $"not learning {activeBlueprint} this time...",
                                        Indent: indent + 4, Toggle: doDebug);
                                    learnableBuildRecipe = null;
                                }
                                else
                                {
                                    Debug.CheckYeh(4, $"learning {activeBlueprint}!",
                                        Indent: indent + 4, Toggle: doDebug);
                                }
                            }
                            if (!learnableModRecipes.IsNullOrEmpty())
                            {
                                List<TinkerData> removeList = new();
                                foreach (TinkerData learnableModRecipe in learnableModRecipes)
                                {
                                    string learnableModRecipeName = learnableModRecipe.PartName ?? "[mod]" + learnableModRecipe.Blueprint ?? Const.NULL;
                                    if (!reverseEngineerChance.in100())
                                    {
                                        Debug.CheckNah(4, $"not learning {learnableModRecipeName} this time...",
                                            Indent: indent + 4, Toggle: doDebug);
                                        removeList.Add(learnableModRecipe);
                                    }
                                    else
                                    {
                                        Debug.CheckYeh(4, $"learning {learnableModRecipeName}!",
                                            Indent: indent + 4, Toggle: doDebug);
                                    }
                                }
                                learnableModRecipes.RemoveAll(d => removeList.Contains(d));
                            }
                            Debug.Entry(4, $"Learning any learnable recipes...", Indent: indent + 3, Toggle: doDebug);
                            if (learnableBuildRecipe != null || !learnableModRecipes.IsNullOrEmpty())
                            {
                                vendorTinkering = Vendor.RequirePart<UD_VendorTinkering>();
                                SyncedDisassembly.ReverseEngineeredBuildRecipe ??= learnableBuildRecipe;
                                if (!learnableModRecipes.IsNullOrEmpty())
                                {
                                    foreach (TinkerData learnableModRecipe in learnableModRecipes)
                                    {
                                        SyncedDisassembly.ReverseEngineeredModRecipes.TryAdd(learnableModRecipe);
                                    }
                                }
                                
                                if (learnableBuildRecipe != null)
                                {
                                    bool shouldScribeRecipe = vendorTinkering.ScribesKnownRecipesOnRestock && vendorTinkering.RestockScribeChance.in100();
                                    if (vendorTinkering.LearnRecipe(learnableBuildRecipe, shouldScribeRecipe))
                                    {
                                        Debug.CheckYeh(4, $"{learnableBuildRecipe.DisplayName} \"Learned\"", Indent: indent + 3, Toggle: doDebug);
                                    }
                                }
                                if (!learnableModRecipes.IsNullOrEmpty())
                                {
                                    foreach (TinkerData learnableMod in learnableModRecipes)
                                    {
                                        bool shouldScribeRecipe = vendorTinkering.ScribesKnownRecipesOnRestock && vendorTinkering.RestockScribeChance.in100();
                                        if (vendorTinkering.LearnRecipe(learnableMod, shouldScribeRecipe))
                                        {
                                            Debug.CheckYeh(4, $"{learnableMod?.DisplayName?.Strip() ?? Const.NULL} \"Learned\"", Indent: indent + 3, Toggle: doDebug);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                Debug.CheckNah(4, $"none to learn... this time...", Indent: indent + 3, Toggle: doDebug);
                            }
                        }
                    }
                    Disassembly.NumberDone++;
                    Disassembly.TotalNumberDone++;
                    if (Disassembly.TotalNumberWanted > 1)
                    {
                        Loading.SetLoadingStatus($"Disassembled {Disassembly.TotalNumberDone.Things("item")} of {Disassembly.TotalNumberWanted}...");
                    }
                    if (!Disassembly.Abort)
                    {
                        if (Vendor.HasRegisteredEvent("ModifyBitsReceived"))
                        {
                            Event @event = Event.New("ModifyBitsReceived", "Item", Item, "Bits", bitsToAward);
                            Vendor.FireEvent(@event);
                            bitsToAward = @event.GetStringParameter("Bits", "");
                        }
                        Disassembly.BitsDone += bitsToAward;
                    }
                    totalEnergyCost += Disassembly.EnergyCostPer;
                    Disassembly.DoBitMessage = true;
                    Item.PlayWorldOrUISound("Sounds/Misc/sfx_interact_artifact_disassemble", null);

                    Item.Destroy();
                }
                if (Disassembly.NumberDone >= Disassembly.NumberWanted)
                {
                    VendorDisassemblyProcessDisassemblingWhat(Disassembly);
                    if (!Disassembly.Abort)
                    {
                        if (!Disassembly.Queue.IsNullOrEmpty())
                        {
                            Item = Disassembly.Queue[0];
                            Disassembly.Queue.RemoveAt(0);
                            Disassembly.NumberDone = 0;
                            Disassembly.OriginalCount = Item.Count;
                            if (Disassembly.QueueNumberWanted == null 
                                || !Disassembly.QueueNumberWanted.TryGetValue(Item, out Disassembly.NumberWanted))
                            {
                                Disassembly.NumberWanted = Disassembly.OriginalCount;
                            }
                            Disassembly.Alarms = null;
                            Disassembly.QueueAlarms?.TryGetValue(Item, out Disassembly.Alarms);
                        }
                        else
                        {
                            Disassembly.Abort = true;
                        }
                    }
                }
            }
            finally
            {
                if (totalEnergyCost > 0)
                {
                    Vendor.UseEnergy(totalEnergyCost, "Skill Tinkering Disassemble");
                    The.Player.UseEnergy(totalEnergyCost, "Vendor Tinkering Disassemble");
                }
            }
            if (Debug.LastIndent > indent + 1)
            {
                Debug.Divider(4, Const.HONLY, Indent: indent + 1, Toggle: doDebug);
            }

            if (DebugSpawnSnapjawWhileVendorDisassembles && currentDisassembly % 25 == 0)
            {
                (The.Player.CurrentCell?.GetEmptyAdjacentCells()?.GetRandomElementCosmetic() ?? Vendor.CurrentCell?.GetEmptyAdjacentCells()?.GetRandomElementCosmetic())?.AddObject("Snapjaw Scavenger");
            }

            Debug.LastIndent = indent;
            return true;
        }

        public static void VendorDisassemblyEnd(GameObject Vendor, Disassembly Disassembly, UD_SyncedDisassembly SyncedDisassembly)
        {
            if (Disassembly == null)
            {
                MetricsManager.LogPotentialModError(UD_Tinkering_Bytes.Utils.ThisMod, 
                    $"{nameof(UD_VendorDisassembly)}.{nameof(VendorDisassemblyEnd)} passed null {nameof(Disassembly)}");
                return;
            }

            GameObject sampleObject = null;
            TinkerData learnedBuildRecipe = SyncedDisassembly.ReverseEngineeredBuildRecipe;
            List<TinkerData> learnedModRecipes = SyncedDisassembly.ReverseEngineeredModRecipes;
            string reverseEngineerMessage = "";
            if (learnedBuildRecipe != null)
            {
                sampleObject = GameObject.CreateSample(learnedBuildRecipe.Blueprint);

                string objectDisplayName = sampleObject.IsPlural
                    ? sampleObject.DisplayNameOnlyDirect
                    : sampleObject.GetPluralName(AsIfKnown: true, NoConfusion: true, Stripped: false, BaseOnly: true);

                if (GameObject.Validate(ref sampleObject))
                {
                    sampleObject.Obliterate();
                }

                reverseEngineerMessage = "build " + objectDisplayName;
            }
            if (!learnedModRecipes.IsNullOrEmpty())
            {
                List<string> learnedModsDisplayNames = new();
                foreach (TinkerData learnableMod in learnedModRecipes)
                {
                    learnedModsDisplayNames.Add(learnableMod.DisplayName);
                }
                string pluralMods = learnedModsDisplayNames.Count > 1 ? "mods" : "mod";
                string learnedModsMessage = $"mod items with the {Grammar.MakeAndList(learnedModsDisplayNames)} {pluralMods}";

                if (!reverseEngineerMessage.IsNullOrEmpty())
                {
                    reverseEngineerMessage += " and ";
                }
                reverseEngineerMessage += learnedModsMessage;
            }
            if (!reverseEngineerMessage.IsNullOrEmpty())
            {
                string eurikaMessage = GameText.VariableReplace($"Eureka! =subject.Subjective= may now {reverseEngineerMessage}.".Color("G"), Vendor);

                if (Disassembly.ReverseEngineeringMessage.IsNullOrEmpty())
                {
                    Disassembly.ReverseEngineeringMessage = eurikaMessage;
                }
                else
                {
                    Disassembly.ReverseEngineeringMessage = $"{Disassembly.ReverseEngineeringMessage}\n\n{eurikaMessage}";
                }
            }

            if (Disassembly.TotalNumberDone > 0)
            {
                VendorDisassemblyProcessDisassemblingWhat(Disassembly);
                StringBuilder SB = Event.NewStringBuilder();
                SB.Append(Vendor.T()).Append(" disassembled ");
                if (Disassembly.DisassembledWhats.IsNullOrEmpty())
                {
                    SB.Append(Disassembly.DisassembledWhat ?? "something");
                    if (!Disassembly.DisassembledWhere.IsNullOrEmpty())
                    {
                        SB.Append(' ').Append(Disassembly.DisassembledWhere);
                    }
                }
                else
                {
                    List<string> disassembledWhatAndWhereLines = new();
                    List<string> disassembledWhatLines = new();
                    string lastWhatAndWhere = null;
                    foreach (string disassembledWhat in Disassembly.DisassembledWhats)
                    {
                        Disassembly.DisassembledWhatsWhere.TryGetValue(disassembledWhat, out var whatAndWhereEntry);
                        if (whatAndWhereEntry != lastWhatAndWhere && disassembledWhatLines.Count > 0)
                        {
                            string whatAndWhere = Grammar.MakeAndList(disassembledWhatLines);
                            if (!lastWhatAndWhere.IsNullOrEmpty())
                            {
                                whatAndWhere = whatAndWhere + " " + lastWhatAndWhere;
                            }
                            disassembledWhatAndWhereLines.Add(whatAndWhere);
                            disassembledWhatLines.Clear();
                        }
                        lastWhatAndWhere = whatAndWhereEntry;
                        disassembledWhatLines.Add(disassembledWhat);
                    }
                    string disassembledWhatAndWhere = Grammar.MakeAndList(disassembledWhatLines);
                    if (!lastWhatAndWhere.IsNullOrEmpty())
                    {
                        disassembledWhatAndWhere = disassembledWhatAndWhere + " " + lastWhatAndWhere;
                    }
                    disassembledWhatAndWhereLines.Add(disassembledWhatAndWhere);
                    SB.Append(Grammar.MakeAndList(disassembledWhatAndWhereLines));
                }
                SB.Append('.');
                if (!Disassembly.ReverseEngineeringMessage.IsNullOrEmpty())
                {
                    if (Disassembly.ReverseEngineeringMessage.Contains('\n'))
                    {
                        SB.AppendLines(2).Append(Disassembly.ReverseEngineeringMessage).AppendLines(2);
                    }
                    else
                    {
                        SB.Compound(Disassembly.ReverseEngineeringMessage, ' ');
                    }
                }
                string finishedMessage = null;
                if (!Disassembly.BitsDone.IsNullOrEmpty())
                {
                    The.Player.RequirePart<BitLocker>().AddBits(Disassembly.BitsDone);
                    if (Disassembly.DoBitMessage)
                    {
                        if (Disassembly.ReverseEngineeringMessage.IsNullOrEmpty())
                        {
                            finishedMessage = $"={nameof(Vendor)}.Subjective= ";
                        }
                        else
                        {
                            finishedMessage = $"={nameof(Vendor)}.T= ";
                        }
                        string bits = BitType.GetDisplayString(Disassembly.BitsDone);
                        finishedMessage += $"=verb:give:afterpronoun= ={nameof(The.Player)}.t= tinkering bits <{bits}>.";

                        ReplaceBuilder RB = GameText.StartReplace(finishedMessage)
                            .AddObject(Vendor, nameof(Vendor))
                            .AddObject(The.Player, nameof(The.Player));

                        finishedMessage = RB.ToString();
                    }
                }
                else
                if (Disassembly.WasTemporary)
                {
                    finishedMessage = "The parts crumble into dust.";
                }
                if (!finishedMessage.IsNullOrEmpty())
                {
                    SB.Compound(finishedMessage, ' ');
                }
                Popup.Show(SB.ToString());
            }
        }

        public static bool VendorDoDisassembly(GameObject Vendor, GameObject Item, TinkerItem TinkerItem, int CostPerItem, ref Disassembly Disassembly, List<TinkerData> KnownRecipes)
        {
            if (Vendor == null || Item == null || TinkerItem == null)
            {
                Popup.ShowFail($"That trader or item doesn't exist, or the item can't be disassembled (this is an error).");
                return false;
            }
            Disassembly disassembly = Disassembly;
            UD_SyncedDisassembly syncedDisassembly = new(disassembly, Vendor, ref KnownRecipes, CostPerItem);

            AutoAct.Action = syncedDisassembly;
            Vendor.ForfeitTurn(EnergyNeutral: true);
            The.Player.ForfeitTurn(EnergyNeutral: true);

            return true;
        }

        public override void Register(GameObject Object, IEventRegistrar Registrar)
        {
            Registrar.Register(AllowTradeWithNoInventoryEvent.ID, EventOrder.EARLY);
            base.Register(Object, Registrar);
        }
        public override bool WantEvent(int ID, int Cascade)
        {
            return base.WantEvent(ID, Cascade)
                || (WantVendorActions && ID == UD_GetVendorActionsEvent.ID)
                || (WantVendorActions && ID == UD_VendorActionEvent.ID);
        }
        public override bool HandleEvent(AllowTradeWithNoInventoryEvent E)
        {
            if (E.Trader != null && ParentObject == E.Trader && WantVendorActions)
            {
                return true;
            }
            return base.HandleEvent(E);
        }
        public virtual bool HandleEvent(UD_GetVendorActionsEvent E)
        {
            if (E.Vendor != null && ParentObject == E.Vendor && WantVendorActions)
            {
                if (E.Item.InInventory != ParentObject
                    && E.Item.TryGetPart(out TinkerItem tinkerItem)
                    && tinkerItem.CanBeDisassembled(E.Vendor))
                {
                    E.AddAction(
                        Name: "Disassemble",
                        Display: "disassemble", 
                        Command: COMMAND_DISASSEMBLE,
                        Key: 'd',
                        Priority: -4,
                        ProcessSecondAfterAwait: true,
                        Staggered: true,
                        CloseTradeBeforeProcessingSecond: true);
                    if (E.Item.Count > 1)
                    {
                        E.AddAction(
                            Name: "Disassemble all", 
                            Display: "disassemble all", 
                            Command: COMMAND_DISASSEMBLE_ALL, 
                            Key: 'D',
                            Priority: -5,
                            ProcessSecondAfterAwait: true,
                            Staggered: true,
                            CloseTradeBeforeProcessingSecond: true);
                    }
                }
            }
            return base.HandleEvent(E);
        }
        public virtual bool HandleEvent(UD_VendorActionEvent E)
        {
            if ((E.Command == COMMAND_DISASSEMBLE || E.Command == COMMAND_DISASSEMBLE_ALL) 
                && E.Item != null 
                && E.Item.TryGetPart(out TinkerItem tinkerItem))
            {
                GameObject Vendor = E.Vendor;
                GameObject Item = E.Item;
                GameObject player = The.Player;

                int itemCount = Item.Count;
                bool multipleItems = E.Command == COMMAND_DISASSEMBLE_ALL && itemCount > 1;

                string bits = tinkerItem.Bits;
                int numberOfBits = bits.Length;
                int bestBit = BitType.GetBitTier(BitType.ReverseCharTranslateBit(bits[^1]));
                double minCost = 1.0;
                double costPerItem;
                double bestBitCost = (bestBit + 1) * 0.3;
                double numberBitCost = numberOfBits * 0.667;
                int totalCost = 0;

                if (E.Item.GetPropertyOrTag("VendorTinker_DisassemblyBitCountOverride", null) is string bitCountOverrideString
                    && int.TryParse(bitCountOverrideString, out int bitCountOverride))
                {
                    numberOfBits = bitCountOverride;
                }
                if (numberOfBits == 1 && BitType.GetBitTier(BitType.ReverseCharTranslateBit(bits[0])) == 0)
                {
                    costPerItem = 1.0;
                }
                else
                {
                    costPerItem = Math.Max(1.0, numberBitCost + bestBitCost);
                }
                if (E.Item.GetPropertyOrTag("VendorTinker_DisassemblyValueOverride", null) is string valueOverrideString
                    && int.TryParse(valueOverrideString, out int valueOverride))
                {
                    costPerItem = valueOverride;
                }
                totalCost = (int)Math.Max(minCost, multipleItems ? itemCount * costPerItem : costPerItem);
                int RealCostPerItem = multipleItems ? totalCost / itemCount : totalCost;
                totalCost = multipleItems ? itemCount * RealCostPerItem : RealCostPerItem;

                if (Vendor.IsPlayerLed())
                {
                    costPerItem = 0;
                    RealCostPerItem = 0;
                    totalCost = 0;
                }

                if (E.Staggered && E.Second)
                {
                    if (VendorDoDisassembly(Vendor, Item, tinkerItem, RealCostPerItem, ref Disassembly, KnownRecipes))
                    {
                        ResetDisassembly();
                        return true;
                    }
                    ResetDisassembly();
                }
                if (!E.Second)
                {
                    if (player.GetFreeDrams() < totalCost)
                    {
                        Popup.ShowFail(
                            $"{player.T()}{player.GetVerb("do")} not have the required " +
                            $"{totalCost.Color("C")} {((totalCost == 1) ? "dram" : "drams")} " +
                            $"to disassemble {(multipleItems ? $"these {itemCount.Things("item")}" : "this item")}.");
                    }
                    else if (Popup.ShowYesNo(
                        $"{player.T()} may have {Vendor.T()} disassemble " +
                        $"{(multipleItems ? $"these {itemCount.Things("item")}" : "this item")} " +
                        $"for {totalCost.Things("dram").Color("C")} of fresh water.") == DialogResult.Yes)
                    {
                        List<Action<GameObject>> broadcastActions = null;

                        if (multipleItems && (AutoAct.ShouldHostilesInterrupt("o") || (Vendor.AreHostilesNearby() && Vendor.FireEvent("CombatPreventsTinkering"))))
                        {
                            Popup.ShowFail($"{Vendor.T()} cannot disassemble so many items at once with hostiles nearby.");
                            E.RequestCancelSecond();
                            return false;
                        }
                        string pluralItems = Item.IsPlural ? Item.ShortDisplayName : Grammar.Pluralize(Item.ShortDisplayName);
                        if (Item.IsImportant())
                        {
                            if (Item.ConfirmUseImportant(player, "disassemble", null, (!multipleItems) ? 1 : itemCount))
                            {
                                E.RequestCancelSecond();
                                return false;
                            }
                        }
                        else if (TinkerItem.ConfirmBeforeDisassembling(Item)
                            && Popup.ShowYesNoCancel($"Are you sure you want {Vendor.t()} to disassemble " +
                            $"{(multipleItems ? $"all the {pluralItems}" : Item.t())}?") != 0)
                        {
                            E.RequestCancelSecond();
                            return false;
                        }
                        if (!Item.Owner.IsNullOrEmpty() && !Item.HasPropertyOrTag("DontWarnOnDisassemble"))
                        {
                            string themIt = multipleItems ? "them" : Item.them;
                            if (Popup.ShowYesNoCancel(
                                $"{Item.T()} {(multipleItems ? "are" : Item.Is)} not owned by you. " +
                                $"Are you sure you want {Vendor.t()} to disassemble {themIt}?") != 0)
                            {
                                E.RequestCancelSecond();
                                return false;
                            }
                            broadcastActions ??= new();
                            broadcastActions.Add(Item.Physics.BroadcastForHelp);
                        }
                        GameObject container = Item.InInventory;
                        if (container != null && container != player && !container.Owner.IsNullOrEmpty() && container.Owner != Item.Owner && !container.HasPropertyOrTag("DontWarnOnDisassemble"))
                        {
                            if (Popup.ShowYesNoCancel(
                                $"{container.Does("are")} not owned by you. " +
                                $"Are you sure you want {Vendor.t()} to disassemble {(multipleItems ? "items" : Item.an())} inside {container.them}?") != 0)
                            {
                                E.RequestCancelSecond();
                                return false;
                            }
                            broadcastActions ??= new();
                            broadcastActions.Add(container.Physics.BroadcastForHelp);
                        }
                        if (!broadcastActions.IsNullOrEmpty())
                        {
                            foreach (Action<GameObject> broadcastAction in broadcastActions)
                            {
                                broadcastAction(player);
                            }
                        }

                        Disassembly = new(E.Item, multipleItems ? itemCount : 1, EnergyCostPer: 0);
                        return true;
                    }
                    E.RequestCancelSecond();
                }
                return false;
            }
            return base.HandleEvent(E);
        }
    }
}
