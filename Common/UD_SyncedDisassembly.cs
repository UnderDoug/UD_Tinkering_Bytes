using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using XRL;
using XRL.UI;
using XRL.Language;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;
using XRL.World.Parts.Skill;
using XRL.World.Tinkering;
using XRL.World.Text;

using UD_Modding_Toolbox;
using UD_Vendor_Actions;

using static UD_Tinkering_Bytes.Options;
using XRL.Rules;

namespace UD_Tinkering_Bytes
{
    public class UD_SyncedDisassembly : OngoingAction 
    {
        public Disassembly Disassembly => Disassembler?.GetPart<UD_VendorDisassembly>()?.Disassembly;

        public GameObject Disassembler;

        public List<TinkerData> KnownRecipes => Disassembler?.GetPart<UD_VendorTinkering>()?.KnownRecipes;

        public TinkerData ReverseEngineeredBuildRecipe;

        public List<TinkerData> ReverseEngineeredModRecipes;

        public int DramsCostPer;

        public int EnergyCostPer;

        private static int CurrentDisassembly;

        public UD_SyncedDisassembly(GameObject Disassembler, int DramsCostPer = 0, int EnergyCostPer = 1000)
        {
            this.Disassembler = Disassembler;
            this.DramsCostPer = DramsCostPer;
            this.EnergyCostPer = EnergyCostPer;
            if (Disassembly != null)
            {
                Disassembly.EnergyCostPer = EnergyCostPer;
            }
            ReverseEngineeredBuildRecipe = null;
            ReverseEngineeredModRecipes = new();
            CurrentDisassembly = 0;
        }

        public override string GetDescription()
        {
            return "waiting for disassembling";
        }

        public override bool ShouldHostilesInterrupt()
        {
            return true;
        }

        public override bool Continue()
        {
            Disassembler?.Brain?.Goals?.Clear();

            if (EnergyCostPer > 0 && Disassembly != null)
            {
                Disassembly.EnergyCostPer = 0;
            }

            bool vendorDisassemblyContinue = SyncedDisassemblyContinue();
            if (vendorDisassemblyContinue)
            {
                if (GameObject.Validate(ref Disassembler))
                {
                    The.Player?.UseDrams(DramsCostPer);
                    Disassembler?.GiveDrams(DramsCostPer);

                    Disassembler?.UseEnergy(EnergyCostPer, "Skill Tinkering Disassemble");
                }
                The.Player.UseEnergy(EnergyCostPer, "Vendor Tinkering Disassemble");
            }
            return vendorDisassemblyContinue;
        }

        public virtual bool SyncedDisassemblyContinue()
        {
            if (Disassembly == null)
            {
                MetricsManager.LogPotentialModError(Utils.ThisMod,
                    $"{nameof(UD_VendorDisassembly)}.{nameof(SyncedDisassemblyContinue)}, " +
                    $"{nameof(UD_VendorDisassembly)}.{nameof(Disassembly)} is null for " +
                    $"{Disassembler?.DebugName ?? Const.NULL}");
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
                $"{paddedCounter}/{Disassembly?.TotalNumberWanted}] " +
                $"{nameof(SyncedDisassemblyContinue)}, " +
                $"{nameof(Item)}: {Item?.ShortDisplayNameSingleStripped ?? Const.NULL}",
                Indent: indent + 1, Toggle: doDebug);

            int currentDisassembly = CurrentDisassembly;

            var SB = Event.NewStringBuilder();
            var RB = GameText.StartReplace(SB);
            RB.AddObject(Disassembler, "vendor");
            RB.AddObject(Item, "item");
            RB.AddObject(The.Player, "player");

            var tinkerItem = Item.GetPart<TinkerItem>();
            bool interrupt = false;
            if (!interrupt && (!GameObject.Validate(ref Disassembly.Object) || Item.IsNowhere()))
            {
                SB.Append("the item =vendor.subjective= =verb:were:afterpronoun= disassembling disappeared");
                interrupt = true;
            }
            if (!interrupt && Item.IsInGraveyard())
            {
                SB.Append($"the item =vendor.subjective= =verb:were:afterpronoun= disassembling was destroyed");
                interrupt = true;
            }
            if (!interrupt && Item.IsInStasis())
            {
                SB.Append($"=vendor.subjective= can no longer interact with =item.t=");
                interrupt = true;
            }
            if (!interrupt && !Disassembler.HasSkill(nameof(Tinkering_Disassemble)))
            {
                SB.Append($"=vendor.subjective= no longer =verb:know:afterpronoun= how to disassemble things");
                interrupt = true;
            }
            if (!interrupt && (tinkerItem == null || !tinkerItem.CanBeDisassembled(Disassembler)))
            {
                SB.Append($"=item.t= can no longer be disassembled");
                interrupt = true;
            }
            if (!interrupt && !Disassembler.CanMoveExtremities("Disassemble", AllowTelekinetic: true))
            {
                SB.Append($"=vendor.subjective= can no longer move =vendor.possessive= extremities");
                interrupt = true;
            }
            if (!interrupt && Disassembler.ArePerceptibleHostilesNearby(logSpot: true, popSpot: true, Action: this))
            {
                SB.Append($"=vendor.subjective= can no longer disassemble safely");
                interrupt = true;
            }
            if (!interrupt)
            {
                ReplaceBuilder.Return(RB);
            }
            else
            {
                Disassembly.InterruptBecause = RB.ToString();
                return false;
            }
            UD_VendorTinkering vendorTinkering = null;
            bool useEnergy = false;
            try
            {
                if (Disassembly.BitChance == int.MinValue)
                {
                    if (tinkerItem.Bits.Length == 1)
                    {
                        Disassembly.BitChance = 0;
                    }
                    else
                    {
                        int disassembleBonus = Disassembler.GetIntProperty("DisassembleBonus");
                        Disassembly.BitChance = 50;
                        disassembleBonus = GetTinkeringBonusEvent.GetFor(Disassembler, Item, "Disassemble", Disassembly.BitChance, disassembleBonus, ref interrupt);
                        if (!interrupt)
                        {
                            disassembleBonus = GetVendorTinkeringBonusEvent.GetFor(Disassembler, Item, "Disassemble", disassembleBonus, disassembleBonus, ref interrupt);
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
                if (Disassembler.HasSkill(nameof(Tinkering_ReverseEngineer))
                    && TinkerData.TinkerRecipes.Any(datum =>
                        (Item.HasPart(datum.PartName) && !knownRecipes.Contains(datum))
                        || (datum.Blueprint == Item.Blueprint && !knownRecipes.Contains(datum))))
                {
                    Debug.Entry(4,
                        $"Processing {Skills.GetGenericSkill(nameof(Tinkering_ReverseEngineer))?.DisplayName ?? nameof(Tinkering_ReverseEngineer)}",
                        Indent: indent + 2, Toggle: doDebug);

                    vendorTinkering = Disassembler.RequirePart<UD_VendorTinkering>();

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
                    reverseEngineerBonus = GetTinkeringBonusEvent.GetFor(Disassembler, Item, "ReverseEngineer", reverseEngineerChance, reverseEngineerBonus, ref interrupt);
                    if (!interrupt)
                    {
                        reverseEngineerBonus = GetVendorTinkeringBonusEvent.GetFor(Disassembler, Item, "ReverseEngineer", reverseEngineerBonus, reverseEngineerBonus, ref interrupt);
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
                    InventoryActionEvent.Check(Item, Disassembler, Item, "EmptyForDisassemble");
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
                        RB = GameText.StartReplace($"=subject.T= =verb:start:afterpronoun= disassembling =object.t=.");
                        RB.AddObject(Disassembler);
                        RB.AddObject(Item);

                        MessageQueue.AddPlayerMessage(RB.ToString());
                    }
                }
                if (Disassembly.DisassemblingWhere == null && Item.CurrentCell != null)
                {
                    Disassembly.DisassemblingWhere = Disassembler.DescribeDirectionToward(Item);
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
                                vendorTinkering = Disassembler.RequirePart<UD_VendorTinkering>();
                                ReverseEngineeredBuildRecipe ??= learnableBuildRecipe;
                                if (!learnableModRecipes.IsNullOrEmpty())
                                {
                                    foreach (TinkerData learnableModRecipe in learnableModRecipes)
                                    {
                                        ReverseEngineeredModRecipes.TryAdd(learnableModRecipe);
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
                        if (Disassembler.HasRegisteredEvent("ModifyBitsReceived"))
                        {
                            Event @event = Event.New("ModifyBitsReceived", "Item", Item, "Bits", bitsToAward);
                            Disassembler.FireEvent(@event);
                            bitsToAward = @event.GetStringParameter("Bits", "");
                        }
                        Disassembly.BitsDone += bitsToAward;
                    }
                    useEnergy = true;
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
                if (useEnergy)
                {
                    Disassembler.UseEnergy(Disassembly.EnergyCostPer, "Skill Tinkering Disassemble");
                    The.Player.UseEnergy(Disassembly.EnergyCostPer, "Vendor Tinkering Disassemble");
                }
            }
            if (Debug.LastIndent > indent + 1)
            {
                Debug.Divider(4, Const.HONLY, Indent: indent + 1, Toggle: doDebug);
            }

            if (DebugSpawnSnapjawWhileVendorDisassembles && currentDisassembly % 25 == 0)
            {
                (The.Player.CurrentCell?.GetEmptyAdjacentCells()?.GetRandomElementCosmetic() ?? Disassembler.CurrentCell?.GetEmptyAdjacentCells()?.GetRandomElementCosmetic())?.AddObject("Snapjaw Scavenger");
            }

            Debug.LastIndent = indent;
            return true;
        }

        public override bool CanComplete()
        {
            return Disassembly.CanComplete();
        }

        public override void Interrupt()
        {
            base.Interrupt();
            Disassembly.InterruptBecause ??= GameText.VariableReplace("=object.t= interrupted =pronouns.objective=", Disassembler, The.Player);
            Disassembly.Interrupt();
            MessageQueue.AddPlayerMessage(Event.NewStringBuilder()
                .Append(Disassembler.T())
                .Append(Disassembler.GetVerb("stop"))
                .Append(" ")
                .Append(Disassembly.GetDescription())
                .Append(" because ")
                .Append(Disassembly.GetInterruptBecause())
                .Append(".")
                .ToString());
            Loading.SetLoadingStatus($"Interrupted!");

            if (Disassembler.TryGetPart(out UD_VendorDisassembly vendorDisassembly))
            {
                CurrentDisassembly = 0;
                vendorDisassembly.ResetDisassembly();
            }
        }

        public override void Complete()
        {
            base.Complete();
            Disassembly.Complete();
        }

        public override void End()
        {
            base.End();
            VendorDisassemblyEnd(Disassembler, Disassembly, this);
            if (Disassembler.TryGetPart(out UD_VendorDisassembly vendorDisassembly))
            {
                CurrentDisassembly = 0;
                vendorDisassembly.ResetDisassembly();
            }
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
                var SB = Event.NewStringBuilder();
                SB.Append("=subject.T= disassembled ");
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
                            finishedMessage = $"=subject.Subjective= ";
                        }
                        else
                        {
                            finishedMessage = $"=subject.T= ";
                        }
                        string bits = BitType.GetDisplayString(Disassembly.BitsDone);
                        finishedMessage += $"=verb:give:afterpronoun= =object.t= tinkering bits <{bits}>.";
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
                Popup.Show(GameText.StartReplace(SB).AddObject(Vendor).AddObject(The.Player).ToString());
            }
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
    }
}
