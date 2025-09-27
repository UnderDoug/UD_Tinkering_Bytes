using System;
using System.Text;
using System.Collections.Generic;

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
using System.Linq;

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

        public static bool VendorDisassemblyContinue(GameObject Vendor, Disassembly Disassembly, UD_SyncedDisassembly SyncedDisassembly, ref List<TinkerData> KnownRecipes)
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
                $"{nameof(Item)}: {Item?.DebugName ?? Const.NULL}",
                Indent: indent + 1, Toggle: doDebug);

            int currentDisassembly = CurrentDisassembly;

            if (!GameObject.Validate(ref Disassembly.Object))
            {
                Disassembly.InterruptBecause = $"the item {Vendor.it} {Vendor.GetVerb("were")} working on disappeared";
                Debug.LastIndent = indent;
                return false;
            }
            if (Item.IsInGraveyard())
            {
                Disassembly.InterruptBecause = $"the item {Vendor.it}{Vendor.GetVerb("were")} working on was destroyed";
                Debug.LastIndent = indent;
                return false;
            }
            if (Item.IsNowhere())
            {
                Disassembly.InterruptBecause = $"the item {Vendor.it}{Vendor.GetVerb("were")} working on disappeared";
                Debug.LastIndent = indent;
                return false;
            }
            if (Item.IsInStasis())
            {
                Disassembly.InterruptBecause = $"{Vendor.it} can no longer interact with {Item.t()}";
                Debug.LastIndent = indent;
                return false;
            }
            if (!Item.TryGetPart<TinkerItem>(out var tinkerItem))
            {
                Disassembly.InterruptBecause = $"{Vendor.it} can no longer be disassembled";
                Debug.LastIndent = indent;
                return false;
            }
            if (!Vendor.HasSkill(nameof(Tinkering_Disassemble)))
            {
                Disassembly.InterruptBecause = $"{Vendor.it} no longer know how to disassemble things";
                Debug.LastIndent = indent;
                return false;
            }
            if (!tinkerItem.CanBeDisassembled(Vendor))
            {
                Disassembly.InterruptBecause =  $"{Vendor.it} can no longer be disassembled";
                Debug.LastIndent = indent;
                return false;
            }
            if (!Vendor.CanMoveExtremities("Disassemble", ShowMessage: false, Involuntary: false, AllowTelekinetic: true))
            {
                Disassembly.InterruptBecause = $"{Vendor.it} can no longer move {Vendor.its} extremities";
                Debug.LastIndent = indent;
                return false;
            }
            if (Vendor.ArePerceptibleHostilesNearby(true, true, Action: SyncedDisassembly))
            {
                Disassembly.InterruptBecause = $"{Vendor.it} can no longer tinker safely";
                Debug.LastIndent = indent;
                return false;
            }
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
                List<TinkerData> learnableMods = null;
                List<TinkerData> knownRecipes = new(KnownRecipes);
                if (Vendor.HasSkill(nameof(Tinkering_ReverseEngineer))
                    && TinkerData.TinkerRecipes.Any(datum => 
                        (Item.HasPart(datum.PartName) && !knownRecipes.Contains(datum)) 
                        || (datum.Blueprint == Item.Blueprint && !knownRecipes.Contains(datum))))
                {
                    Debug.Entry(4, $"{nameof(Tinkering_ReverseEngineer)}", Indent: indent + 1, Toggle: doDebug);
                    vendorTinkering = Vendor.RequirePart<UD_VendorTinkering>();
                    foreach (TinkerData tinkerRecipe in TinkerData.TinkerRecipes)
                    {
                        bool recipeTypeIsBuild = tinkerRecipe.Type == "Build";
                        bool recipeBlueprintIsThisItem = recipeTypeIsBuild && tinkerRecipe.Blueprint == activeBlueprint;

                        bool recipeTypeIsMod = tinkerRecipe.Type == "Mod";
                        bool recipeIsModThisItemHas = recipeTypeIsMod && Item.HasPart(tinkerRecipe.PartName);

                        if (!recipeBlueprintIsThisItem && !recipeIsModThisItemHas)
                        {
                            continue;
                        }

                        Debug.Divider(4, Const.HONLY, Count: 56, Indent: indent + 2, Toggle: doDebug);
                        Debug.LoopItem(4, $"{tinkerRecipe.Blueprint ?? tinkerRecipe.PartName}", Indent: indent + 2, Toggle: doDebug);

                        Debug.LoopItem(4, $"{nameof(recipeBlueprintIsThisItem)}", $"{recipeBlueprintIsThisItem}",
                            Good: recipeBlueprintIsThisItem, Indent: indent + 3, Toggle: doDebug);
                        Debug.LoopItem(4, $"{nameof(recipeIsModThisItemHas)}", $"{recipeIsModThisItemHas}",
                            Good: recipeIsModThisItemHas, Indent: indent + 3, Toggle: doDebug);

                        if (recipeBlueprintIsThisItem)
                        {
                            learnableBuildRecipe = tinkerRecipe;
                        }
                        bool alreadyKnowMod = false;
                        if (KnownRecipes.IsNullOrEmpty())
                        {
                            KnownRecipes ??= vendorTinkering.KnownRecipes ?? new();
                        }
                        if (!KnownRecipes.IsNullOrEmpty())
                        {
                            Debug.CheckYeh(4, $"{nameof(KnownRecipes)} not NullOrEmpty", Indent: indent + 2, Toggle: doDebug);
                            Debug.Entry(4, $"Looping known recipes", Indent: indent + 2, Toggle: doDebug);
                            if (recipeBlueprintIsThisItem && KnownRecipes.Any(r => r.Blueprint == tinkerRecipe.Blueprint && r.Cost == tinkerRecipe.Cost))
                            {
                                Debug.CheckNah(4, $"\"Already Know\" build recipe", Indent: indent + 3, Toggle: doDebug);
                                learnableBuildRecipe = null;
                            }
                            else
                            {
                                Debug.CheckYeh(4, $"build recipe \"Learnable\".", Indent: indent + 3, Toggle: doDebug);
                            }
                            if (recipeIsModThisItemHas && KnownRecipes.Any(r => r.PartName == tinkerRecipe.PartName && r.Cost == tinkerRecipe.Cost))
                            {
                                Debug.CheckNah(4, $"\"Already Know\" mod recipe", Indent: indent + 3, Toggle: doDebug);
                                alreadyKnowMod = true;
                            }
                            else
                            {
                                Debug.CheckYeh(4, $"mod recipe \"Learnable\".", Indent: indent + 3, Toggle: doDebug);
                            }
                            /*
                            foreach (TinkerData knownRecipe in KnownRecipes)
                            {
                                Debug.Divider(4, Const.HONLY, Count: 52, Indent: indent + 3, Toggle: doDebug);
                                Debug.LoopItem(4, $"{knownRecipe.Blueprint ?? knownRecipe.PartName ?? "No Recipe? This is an error."}", Indent: indent + 3, Toggle: doDebug);
                                if (recipeBlueprintIsThisItem)
                                {
                                    if (knownRecipe.Blueprint == tinkerRecipe.Blueprint)
                                    {
                                        Debug.CheckNah(4, $"\"Already Know\" build recipe", Indent: indent + 4, Toggle: doDebug);
                                        learnableBuildRecipe = null;
                                        break;
                                    }
                                }
                                if (recipeIsModThisItemHas)
                                {
                                    if (knownRecipe.PartName == tinkerRecipe.PartName)
                                    {
                                        Debug.CheckNah(4, $"\"Already Know\" mod recipe", Indent: indent + 4, Toggle: doDebug);
                                        alreadyKnowMod = true;
                                        break;
                                    }
                                }
                            }
                            Debug.Divider(4, Const.HONLY, Count: 52, Indent: indent + 3, Toggle: doDebug);
                            Debug.CheckYeh(4, $"Build recipe \"Learnable\"", Indent: indent + 2, Toggle: doDebug);
                            */
                        }
                        if (!alreadyKnowMod && recipeIsModThisItemHas)
                        {
                            learnableMods ??= new();
                            learnableMods.Add(tinkerRecipe);
                            Debug.CheckYeh(4, $"mod recipe loaded up.", Indent: indent + 3, Toggle: doDebug);
                        }
                        if (learnableBuildRecipe != null)
                        {
                            Debug.CheckYeh(4, $"build recipe loaded up", Indent: indent + 3, Toggle: doDebug);
                        }
                    }
                    Debug.Divider(4, Const.HONLY, Count: 56, Indent: indent + 2, Toggle: doDebug);
                }

                int reverseEngineerChance = 0;
                int reverseEngineerBonus = 0;
                if (learnableBuildRecipe != null || (learnableMods != null && learnableMods.Count > 0))
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
                    Debug.Entry(4, $"{nameof(reverseEngineerChance)}", reverseEngineerChance.ToString(), Indent: indent + 2, Toggle: doDebug);
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
                        MessageQueue.AddPlayerMessage($"{Vendor.T()}{Vendor.GetVerb("start")} disassembling {Item.t()}.");
                    }
                }
                if (Disassembly.DisassemblingWhere == null && Item.CurrentCell != null)
                {
                    Disassembly.DisassemblingWhere = Vendor.DescribeDirectionToward(Item);
                }
                if (Disassembly.NumberDone < Disassembly.NumberWanted)
                {
                    string bitsToAward = "";
                    GameObject gameObject = null;
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

                        Debug.Entry(4, $"Checking for reverse engineer successes...", Indent: indent + 2, Toggle: doDebug);
                        if (!reverseEngineerChance.in100() && learnableBuildRecipe != null)
                        {
                            Debug.CheckNah(4, $"not learning {learnableBuildRecipe.DisplayName} this time", Indent: indent + 3, Toggle: doDebug);
                            learnableBuildRecipe = null;
                        }
                        else
                        {
                            Debug.CheckYeh(4, $"learning {learnableBuildRecipe.DisplayName}!", Indent: indent + 3, Toggle: doDebug);
                        }
                        if (!learnableMods.IsNullOrEmpty())
                        {
                            List<TinkerData> removeList = new();
                            foreach (TinkerData learnableMod in learnableMods)
                            {
                                if (!reverseEngineerChance.in100())
                                {
                                    Debug.CheckNah(4, $"not learning {learnableMod.DisplayName} this time", Indent: indent + 3, Toggle: doDebug);
                                    removeList.Add(learnableMod);
                                }
                                else
                                {
                                    Debug.CheckYeh(4, $"learning {learnableMod.DisplayName}!", Indent: indent + 3, Toggle: doDebug);
                                }
                            }
                            learnableMods.RemoveAll(d => removeList.Contains(d));
                        }
                        Debug.Entry(4, $"Learning any learnable recipes...", Indent: indent + 2, Toggle: doDebug);
                        if (learnableBuildRecipe != null || !learnableMods.IsNullOrEmpty())
                        {
                            vendorTinkering = Vendor.RequirePart<UD_VendorTinkering>();
                            string reverseEngineerMessage = "";
                            if (learnableBuildRecipe != null)
                            {
                                gameObject = GameObject.CreateSample(learnableBuildRecipe.Blueprint);
                                learnableBuildRecipe.DisplayName = gameObject.DisplayNameOnlyDirect;

                                string objectDisplayName = gameObject.IsPlural 
                                    ? gameObject.DisplayNameOnlyDirect 
                                    : gameObject.GetPluralName(AsIfKnown: true, NoConfusion: true, Stripped: false, BaseOnly: true);

                                reverseEngineerMessage = "build " + objectDisplayName;
                            }
                            if (!learnableMods.IsNullOrEmpty())
                            {
                                List<string> learnedModsDisplayNames = new();
                                foreach (TinkerData learnableMod in learnableMods)
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
                                string eurikaMessage = $"Eureka! {Vendor.it} may now {reverseEngineerMessage}.".Color("G");

                                if (Disassembly.ReverseEngineeringMessage.IsNullOrEmpty())
                                {
                                    Disassembly.ReverseEngineeringMessage = eurikaMessage;
                                }
                                else
                                {
                                    Disassembly.ReverseEngineeringMessage = $"{Disassembly.ReverseEngineeringMessage}\n{eurikaMessage}";
                                }
                            }
                            if (learnableBuildRecipe != null)
                            {
                                bool shouldScribeRecipe = vendorTinkering.ScribesKnownRecipesOnRestock && vendorTinkering.RestockScribeChance.in100();
                                if (vendorTinkering.LearnRecipe(learnableBuildRecipe, shouldScribeRecipe))
                                {
                                    Debug.CheckYeh(4, $"{learnableBuildRecipe.DisplayName} \"Learned\"", Indent: indent + 2, Toggle: doDebug);
                                }
                            }
                            if (!learnableMods.IsNullOrEmpty())
                            {
                                foreach (TinkerData learnableMod in learnableMods)
                                {
                                    bool shouldScribeRecipe = vendorTinkering.ScribesKnownRecipesOnRestock && vendorTinkering.RestockScribeChance.in100();
                                    if (vendorTinkering.LearnRecipe(learnableMod, shouldScribeRecipe))
                                    {
                                        Debug.CheckYeh(4, $"{learnableMod.DisplayName} \"Learned\"", Indent: indent + 2, Toggle: doDebug);
                                    }
                                }
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

        public static void VendorDisassemblyEnd(GameObject Vendor, Disassembly Disassembly)
        {
            if (Disassembly == null)
            {
                MetricsManager.LogPotentialModError(UD_Tinkering_Bytes.Utils.ThisMod, 
                    $"{nameof(UD_VendorDisassembly)}.{nameof(VendorDisassemblyEnd)} passed null {nameof(Disassembly)}");
                return;
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
                    SB.AppendLines(2);
                    if (Disassembly.ReverseEngineeringMessage.Contains('\n'))
                    {
                        SB.Append(Disassembly.ReverseEngineeringMessage).AppendLines(2);
                    }
                    else
                    {
                        SB.Append(Disassembly.ReverseEngineeringMessage).AppendLine();
                    }
                }
                string finishedMessage = null;
                if (!Disassembly.BitsDone.IsNullOrEmpty())
                {
                    The.Player.RequirePart<BitLocker>().AddBits(Disassembly.BitsDone);
                    if (Disassembly.DoBitMessage)
                    {
                        finishedMessage = $"{Vendor.It}{Vendor.GetVerb("give")} " +
                            $"{The.Player.t()} tinkering bits " +
                            $"<{BitType.GetDisplayString(Disassembly.BitsDone)}>.";
                    }
                }
                else if (Disassembly.WasTemporary)
                {
                    finishedMessage = "The parts crumble into dust.";
                }
                if (!finishedMessage.IsNullOrEmpty())
                {
                    SB.AppendLine().Compound(finishedMessage, ' ');
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
