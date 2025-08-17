using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

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

namespace XRL.World.Parts
{
    [Serializable]
    public class UD_VendorDisassembly : IScribedPart, IVendorActionEventHandler
    {
        private static bool doDebug = true;

        public const string COMMAND_DISASSEMBLE = "CmdVendorDisassemble";
        public const string COMMAND_DISASSEMBLE_ALL = "CmdVendorDisassembleAll";

        public bool WantVendorActions => ParentObject != null && ParentObject.HasSkill(nameof(Tinkering_Disassemble)) && !ParentObject.IsPlayer();

        // Disassembly 
        public Disassembly Disassembly;

        public UD_VendorDisassembly()
        {
            ResetDisassembly();
        }

        public virtual void ResetDisassembly()
        {
            Disassembly = null;
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

        public static bool VendorDisassemblyContinue(GameObject Vendor, Disassembly Disassembly, List<TinkerData> KnownRecipes)
        {
            int indent = Debug.LastIndent;
            GameObject Object = Disassembly.Object;

            if (!GameObject.Validate(ref Disassembly.Object))
            {
                Disassembly.InterruptBecause = $"the item {Vendor.t()} {Vendor.GetVerb("were")} working on disappeared";
                Debug.LastIndent = indent;
                return false;
            }
            if (Object.IsInGraveyard())
            {
                Disassembly.InterruptBecause = $"the item {Vendor.t()} {Vendor.GetVerb("were")} working on was destroyed";
                Debug.LastIndent = indent;
                return false;
            }
            if (Object.IsNowhere())
            {
                Disassembly.InterruptBecause = $"the item {Vendor.t()} {Vendor.GetVerb("were")} working on disappeared";
                Debug.LastIndent = indent;
                return false;
            }
            if (Object.IsInStasis())
            {
                Disassembly.InterruptBecause = $"{Vendor.t()} can no longer interact with {Object.t()}";
                Debug.LastIndent = indent;
                return false;
            }
            if (!Object.TryGetPart<TinkerItem>(out var tinkerItem))
            {
                Disassembly.InterruptBecause = $"{Object.t()} can no longer be disassembled";
                Debug.LastIndent = indent;
                return false;
            }
            if (!Vendor.HasSkill(nameof(Tinkering_Disassemble)))
            {
                Disassembly.InterruptBecause = $"{Vendor.t()} no longer know how to disassemble things";
                Debug.LastIndent = indent;
                return false;
            }
            if (!tinkerItem.CanBeDisassembled(Vendor))
            {
                Disassembly.InterruptBecause =  $"{Object.t()} can no longer be disassembled";
                Debug.LastIndent = indent;
                return false;
            }
            if (!Vendor.CanMoveExtremities("Disassemble", ShowMessage: false, Involuntary: false, AllowTelekinetic: true))
            {
                Disassembly.InterruptBecause = $"{Vendor.t()} can no longer move {Vendor.its} extremities";
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
                        disassembleBonus = GetTinkeringBonusEvent.GetFor(Vendor, Object, "Disassemble", Disassembly.BitChance, disassembleBonus, ref interrupt);
                        if (!interrupt)
                        {
                            disassembleBonus = GetVendorTinkeringBonusEvent.GetFor(Vendor, Object, "Disassemble", disassembleBonus, disassembleBonus, ref interrupt);
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
                TinkerData tinkerData = null;
                List<TinkerData> learnableMods = null;
                if (Vendor.HasSkill(nameof(Tinkering_ReverseEngineer)))
                {
                    Debug.Entry(4, $"{nameof(Tinkering_ReverseEngineer)}", Indent: indent + 1, Toggle: doDebug);

                    vendorTinkering = Vendor.RequirePart<UD_VendorTinkering>();
                    foreach (TinkerData tinkerRecipe in TinkerData.TinkerRecipes)
                    {
                        bool recipeTypeIsBuild = tinkerRecipe.Type == "Build";
                        bool recipeBlueprintIsThisItem = recipeTypeIsBuild && tinkerRecipe.Blueprint == activeBlueprint;

                        bool recipeTypeIsMod = tinkerRecipe.Type == "Mod";
                        bool recipeIsModThisItemHas = recipeTypeIsMod && Object.HasPart(tinkerRecipe.PartName);

                        if (!recipeBlueprintIsThisItem && !recipeIsModThisItemHas)
                        {
                            continue;
                        }

                        Debug.Divider(4, Const.HONLY, Count: 56, Indent: indent + 2, Toggle: doDebug);
                        Debug.LoopItem(4, $"{tinkerRecipe.DisplayName}", Indent: indent + 2, Toggle: doDebug);

                        Debug.LoopItem(4, $"{nameof(recipeBlueprintIsThisItem)}", $"{recipeBlueprintIsThisItem}",
                            Good: recipeBlueprintIsThisItem, Indent: indent + 3, Toggle: doDebug);
                        Debug.LoopItem(4, $"{nameof(recipeIsModThisItemHas)}", $"{recipeIsModThisItemHas}",
                            Good: recipeIsModThisItemHas, Indent: indent + 3, Toggle: doDebug);

                        if (recipeBlueprintIsThisItem)
                        {
                            tinkerData = tinkerRecipe;
                        }
                        bool alreadyKnowMod = false;
                        if (KnownRecipes.IsNullOrEmpty())
                        {
                            KnownRecipes ??= vendorTinkering.KnownRecipes;
                        }
                        if (!KnownRecipes.IsNullOrEmpty())
                        {
                            Debug.CheckYeh(4, $"{nameof(KnownRecipes)} not NullOrEmpty", Indent: indent + 2, Toggle: doDebug);
                            Debug.Entry(4, $"Looping known recipes", Indent: indent + 2, Toggle: doDebug);
                            foreach (TinkerData knownRecipe in KnownRecipes)
                            {
                                Debug.Divider(4, Const.HONLY, Count: 52, Indent: indent + 3, Toggle: doDebug);
                                Debug.LoopItem(4, $"{knownRecipe.DisplayName}", Indent: indent + 3, Toggle: doDebug);
                                if (recipeBlueprintIsThisItem)
                                {
                                    Debug.Entry(4, $"{nameof(recipeBlueprintIsThisItem)}", $"{recipeBlueprintIsThisItem}", Indent: indent + 4, Toggle: doDebug);
                                    if (knownRecipe.Blueprint == tinkerRecipe.Blueprint)
                                    {
                                        Debug.CheckNah(4, $"\"Already Know\" build recipe", Indent: indent + 5, Toggle: doDebug);
                                        tinkerData = null;
                                        break;
                                    }
                                    Debug.CheckYeh(4, $"Build recipe learnable", Indent: indent + 5, Toggle: doDebug);
                                }
                                if (recipeIsModThisItemHas)
                                {
                                    Debug.Entry(4, $"{nameof(recipeIsModThisItemHas)}", $"{recipeIsModThisItemHas}", Indent: indent + 4, Toggle: doDebug);
                                    if (knownRecipe.PartName == tinkerRecipe.PartName)
                                    {
                                        Debug.CheckNah(4, $"\"Already Know\" mod recipe", Indent: indent + 5, Toggle: doDebug);
                                        alreadyKnowMod = true;
                                        break;
                                    }
                                    else
                                    {
                                        Debug.CheckYeh(4, $"Mod recipe learnable", Indent: indent + 5, Toggle: doDebug);
                                    }
                                }
                            }
                            Debug.Divider(4, Const.HONLY, Count: 52, Indent: indent + 3, Toggle: doDebug);
                        }
                        if (!alreadyKnowMod && recipeIsModThisItemHas)
                        {
                            learnableMods ??= new();
                            learnableMods.Add(tinkerRecipe);
                            Debug.CheckYeh(4, $"mod recipe loaded up", Indent: indent + 3, Toggle: doDebug);
                        }
                        if (tinkerData != null)
                        {
                            Debug.CheckYeh(4, $"build recipe loaded up", Indent: indent + 3, Toggle: doDebug);
                        }
                    }
                    Debug.Divider(4, Const.HONLY, Count: 56, Indent: indent + 2, Toggle: doDebug);
                }

                int chance = 0;
                int reverseEngineerBonus = 0;
                if (tinkerData != null || (learnableMods != null && learnableMods.Count > 0))
                {
                    chance = 15;
                    reverseEngineerBonus = GetTinkeringBonusEvent.GetFor(Vendor, Object, "ReverseEngineer", chance, reverseEngineerBonus, ref interrupt);
                    if (!interrupt)
                    {
                        reverseEngineerBonus = GetVendorTinkeringBonusEvent.GetFor(Vendor, Object, "ReverseEngineer", reverseEngineerBonus, reverseEngineerBonus, ref interrupt);
                    }
                    if (interrupt)
                    {
                        Debug.LastIndent = indent;
                        return false;
                    }
                    chance += reverseEngineerBonus;
                }
                try
                {
                    InventoryActionEvent.Check(Object, Vendor, Object, "EmptyForDisassemble");
                }
                catch (Exception x)
                {
                    MetricsManager.LogError("EmptyForDisassemble", x);
                }
                bool multipleObjects = Disassembly.NumberWanted > 1 && Disassembly.OriginalCount > 1;
                if (!Object.IsTemporary)
                {
                    Disassembly.WasTemporary = false;
                }
                if (Disassembly.DisassemblingWhat == null)
                {
                    Disassembly.DisassemblingWhat = Object.t(Single: true);
                    if (multipleObjects || Disassembly.TotalNumberWanted > 1)
                    {
                        MessageQueue.AddPlayerMessage($"{Vendor.T()}{Vendor.GetVerb("start")} disassembling {Object.t()}.");
                    }
                }
                if (Disassembly.DisassemblingWhere == null && Object.CurrentCell != null)
                {
                    Disassembly.DisassemblingWhere = Vendor.DescribeDirectionToward(Object);
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
                        if (!chance.in100())
                        {
                            tinkerData = null;
                            learnableMods = null;
                        }
                        if (tinkerData != null || (learnableMods != null && learnableMods.Count > 0))
                        {
                            vendorTinkering = Vendor.RequirePart<UD_VendorTinkering>();
                            string reverseEngineerMessage = "";
                            if (tinkerData != null)
                            {
                                gameObject = GameObject.CreateSample(tinkerData.Blueprint);
                                tinkerData.DisplayName = gameObject.DisplayNameOnlyDirect;

                                string objectDisplayName = gameObject.IsPlural 
                                    ? gameObject.DisplayNameOnlyDirect 
                                    : gameObject.GetPluralName(AsIfKnown: true, NoConfusion: true, Stripped: false, BaseOnly: true);

                                reverseEngineerMessage = "build " + objectDisplayName;
                            }
                            if (learnableMods != null)
                            {
                                List<string> learnedModsDisplayNames = new();
                                foreach (TinkerData learnableMod in learnableMods)
                                {
                                    learnedModsDisplayNames.Add(learnableMod.DisplayName);
                                }
                                string learnedModsMessage = $"mod items with the {Grammar.MakeAndList(learnedModsDisplayNames)} {learnedModsDisplayNames.Count.Things("mod")}";
                                
                                if (!reverseEngineerMessage.IsNullOrEmpty())
                                {
                                    reverseEngineerMessage += " and ";
                                }
                                reverseEngineerMessage += learnedModsMessage;
                            }
                            if (!reverseEngineerMessage.IsNullOrEmpty())
                            {
                                string eurikaMessage = $"\nEureka! {Vendor.it} may now {reverseEngineerMessage}.".Color("G");

                                if (Disassembly.ReverseEngineeringMessage.IsNullOrEmpty())
                                {
                                    Disassembly.ReverseEngineeringMessage = eurikaMessage;
                                }
                                else
                                {
                                    Disassembly.ReverseEngineeringMessage = $"{Disassembly.ReverseEngineeringMessage}\n{eurikaMessage}";
                                }
                            }
                            if (tinkerData != null)
                            {
                                bool shouldScribeRecipe = vendorTinkering.ScribesKnownRecipesOnRestock && vendorTinkering.RestockScribeChance.in100();
                                if (vendorTinkering.LearnRecipe(tinkerData, shouldScribeRecipe))
                                { 
                                    KnownRecipes.Add(tinkerData);
                                    Debug.CheckYeh(4, $"{tinkerData.DisplayName} \"Learned\"", Indent: indent + 2, Toggle: doDebug);
                                }
                            }
                            if (!learnableMods.IsNullOrEmpty())
                            {
                                foreach (TinkerData learnableMod in learnableMods)
                                {
                                    bool shouldScribeRecipe = vendorTinkering.ScribesKnownRecipesOnRestock && vendorTinkering.RestockScribeChance.in100();
                                    if (vendorTinkering.LearnRecipe(learnableMod, shouldScribeRecipe))
                                    {
                                        KnownRecipes.Add(learnableMod);
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
                            Event @event = Event.New("ModifyBitsReceived", "Item", Object, "Bits", bitsToAward);
                            Vendor.FireEvent(@event);
                            bitsToAward = @event.GetStringParameter("Bits", "");
                        }
                        Disassembly.BitsDone += bitsToAward;
                    }
                    totalEnergyCost += Disassembly.EnergyCostPer;
                    Disassembly.DoBitMessage = true;
                    Object.PlayWorldOrUISound("Sounds/Misc/sfx_interact_artifact_disassemble", null);

                    Object.Destroy();
                }
                if (Disassembly.NumberDone >= Disassembly.NumberWanted)
                {
                    VendorDisassemblyProcessDisassemblingWhat(Disassembly);
                    if (!Disassembly.Abort)
                    {
                        if (!Disassembly.Queue.IsNullOrEmpty())
                        {
                            Object = Disassembly.Queue[0];
                            Disassembly.Queue.RemoveAt(0);
                            Disassembly.NumberDone = 0;
                            Disassembly.OriginalCount = Object.Count;
                            if (Disassembly.QueueNumberWanted == null || !Disassembly.QueueNumberWanted.TryGetValue(Object, out Disassembly.NumberWanted))
                            {
                                Disassembly.NumberWanted = Disassembly.OriginalCount;
                            }
                            Disassembly.Alarms = null;
                            Disassembly.QueueAlarms?.TryGetValue(Object, out Disassembly.Alarms);
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
            Debug.Divider(4, Const.HONLY, Indent: indent + 1, Toggle: doDebug);
            Debug.LastIndent = indent;
            return true;
        }

        public static void VendorDisassemblyEnd(GameObject Vendor, Disassembly Disassembly)
        {
            if (Disassembly.TotalNumberDone > 0)
            {
                VendorDisassemblyProcessDisassemblingWhat(Disassembly);
                StringBuilder SB = Event.NewStringBuilder();
                SB.Append(Vendor.T()).Append(Vendor.GetVerb("disassemble")).Append(" ");
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

        public static bool VendorDoDisassembly(GameObject Vendor, GameObject Item, TinkerItem TinkerItem, int CostPerItem, ref Disassembly Disassembly, IEnumerable<TinkerData> KnownRecipes)
        {
            if (Vendor == null || Item == null || TinkerItem == null)
            {
                Popup.ShowFail($"That trader or item doesn't exist, or the item can't be disassembled (this is an error).");
                return false;
            }
            int energyCost = Disassembly.EnergyCostPer;

            Disassembly.EnergyCostPer = 0;
            bool interrupt = false;

            while (!interrupt
                && !Disassembly.Abort)
            {
                The.Player.UseDrams(CostPerItem);
                Vendor.GiveDrams(CostPerItem);

                Vendor.UseEnergy(energyCost, "Skill Tinkering Disassemble");
                The.Player.UseEnergy(energyCost, "Vendor Tinkering Disassemble");

                if (!VendorDisassemblyContinue(Vendor, Disassembly, KnownRecipes.ToList() ?? new())
                    && !Disassembly.GetInterruptBecause().IsNullOrEmpty())
                {
                    interrupt = true;
                }
            }
            bool completed = true;
            if (!interrupt && Disassembly.CanComplete())
            {
                Disassembly.Complete();
            }
            else
            {
                Disassembly.Interrupt();
                MessageQueue.AddPlayerMessage(Event.NewStringBuilder()
                    .Append(Vendor.T())
                    .Append(Vendor.GetVerb("stop"))
                    .Append(" ")
                    .Append(Disassembly.GetDescription())
                    .Append(" because ")
                    .Append(Disassembly.GetInterruptBecause())
                    .Append(".")
                    .ToString());
                completed = false;
            }
            VendorDisassemblyEnd(Vendor, Disassembly);
            Disassembly = null;
            if (completed)
            {
                Loading.SetLoadingStatus(null);
                Loading.SetHideLoadStatus(hidden: true);
            }
            return completed;
        }

        public override void Register(GameObject Object, IEventRegistrar Registrar)
        {
            Registrar.Register(AllowTradeWithNoInventoryEvent.ID, EventOrder.EARLY);
            base.Register(Object, Registrar);
        }
        public override bool WantEvent(int ID, int Cascade)
        {
            return base.WantEvent(ID, Cascade)
                || (WantVendorActions && ID == GetVendorActionsEvent.ID)
                || (WantVendorActions && ID == VendorActionEvent.ID);
        }
        public override bool HandleEvent(AllowTradeWithNoInventoryEvent E)
        {
            if (E.Trader != null && ParentObject == E.Trader && WantVendorActions)
            {
                return true;
            }
            return base.HandleEvent(E);
        }
        public virtual bool HandleEvent(GetVendorActionsEvent E)
        {
            if (E.Vendor != null && ParentObject == E.Vendor && WantVendorActions)
            {
                if (E.Item.InInventory != ParentObject
                    && E.Item.TryGetPart(out TinkerItem tinkerItem)
                    && tinkerItem.CanBeDisassembled(E.Vendor))
                {
                    E.AddAction("Disassemble", "disassemble", COMMAND_DISASSEMBLE, Key: 'd', Priority: -4, ClearAndSetUpTradeUI: true);
                    if (E.Item.Count > 1)
                    {
                        E.AddAction("Disassemble all", "disassemble all", COMMAND_DISASSEMBLE_ALL, Key: 'D', Priority: -5, ClearAndSetUpTradeUI: true);
                    }
                }
            }
            return base.HandleEvent(E);
        }
        public virtual bool HandleEvent(VendorActionEvent E)
        {
            if ((E.Command == COMMAND_DISASSEMBLE || E.Command == COMMAND_DISASSEMBLE_ALL) 
                && E.Item != null 
                && E.Item.TryGetPart(out TinkerItem tinkerItem))
            {
                GameObject Vendor = E.Vendor;
                GameObject Item = E.Item;

                int itemCount = Item.Count;
                bool multipleItems = E.Command == COMMAND_DISASSEMBLE_ALL && itemCount > 1;

                string bits = tinkerItem.Bits;
                int numberOfBits = bits.Length;
                int bestBit = UD_BytePunnet.GetByteIndex(bits[^1..]);
                double minCost = 1.0;
                double costPerItem;
                double bestBitCost = bestBit * 0.2;
                double numberBitCost = numberOfBits * 0.667;

                if (numberOfBits == 1 && UD_BytePunnet.GetByteIndex(bits[0]) < 4)
                {
                    costPerItem = 1.0;
                }
                else
                {
                    costPerItem = Math.Max(1.0, numberBitCost + bestBitCost);
                }
                int totalCost = (int)Math.Max(minCost, multipleItems ? itemCount * costPerItem : costPerItem);
                int realCostPerItem = multipleItems ? totalCost / itemCount : totalCost;
                totalCost = multipleItems ? itemCount * realCostPerItem : realCostPerItem;

                if (The.Player.GetFreeDrams() < totalCost)
                {
                    Popup.ShowFail(
                        $"You do not have the required {totalCost.Color("C")} {((totalCost == 1) ? "dram" : "drams")} " +
                        $"to disassemble {(multipleItems ? $"these {itemCount.Things("item")}" : "this item")}.");
                }
                else if (Popup.ShowYesNo(
                    $"You may disassemble {(multipleItems ? $"these {itemCount.Things("item")}" : "this item")} " +
                    $"for {totalCost.Things("dram")} of fresh water.") == DialogResult.Yes)
                {
                    List<Action<GameObject>> broadcastActions = null;

                    if (multipleItems && (AutoAct.ShouldHostilesInterrupt("o") || (Vendor.AreHostilesNearby() && Vendor.FireEvent("CombatPreventsTinkering"))))
                    {
                        Popup.ShowFail($"{Vendor.T()} cannot disassemble so many items at once with hostiles nearby.");
                        return false;
                    }
                    if (Item.IsImportant())
                    {
                        if (Item.ConfirmUseImportant(The.Player, "disassemble", null, (!multipleItems) ? 1 : itemCount))
                        {
                            return false;
                        }
                    }
                    else if (TinkerItem.ConfirmBeforeDisassembling(Item)
                        && Popup.ShowYesNoCancel($"Are you sure you want {Vendor.t()} to disassemble {(multipleItems ? $"all the {(Item.IsPlural ? Item.ShortDisplayName : Grammar.Pluralize(Item.ShortDisplayName))}" : Item.t())}?") != 0)
                    {
                        return false;
                    }
                    if (!Item.Owner.IsNullOrEmpty() && !Item.HasPropertyOrTag("DontWarnOnDisassemble"))
                    {
                        if (Popup.ShowYesNoCancel(
                            $"{Item.T()} {(multipleItems ? "are" : Item.Is)} not owned by you. " +
                            $"Are you sure you want {Vendor.t()} to disassemble {(multipleItems ? "them" : Item.them)}?") != 0)
                        {
                            return false;
                        }
                        broadcastActions ??= new();
                        broadcastActions.Add(Item.Physics.BroadcastForHelp);
                    }
                    GameObject container = Item.InInventory;
                    if (container != null && container != The.Player && !container.Owner.IsNullOrEmpty() && container.Owner != Item.Owner && !container.HasPropertyOrTag("DontWarnOnDisassemble"))
                    {
                        if (Popup.ShowYesNoCancel(
                            $"{container.Does("are")} not owned by you. " +
                            $"Are you sure you want {Vendor.t()} to disassemble {(multipleItems ? "items" : Item.an())} inside {container.them}?") != 0)
                        {
                            return false;
                        }
                        broadcastActions ??= new();
                        broadcastActions.Add(container.Physics.BroadcastForHelp);
                    }
                    if (!broadcastActions.IsNullOrEmpty())
                    {
                        foreach (Action<GameObject> broadcastAction in broadcastActions)
                        {
                            broadcastAction(The.Player);
                        }
                    }

                    Disassembly = new(E.Item, multipleItems ? itemCount : 1);

                    if (VendorDoDisassembly(E.Vendor, E.Item, tinkerItem, realCostPerItem, ref Disassembly, UD_VendorTinkering.FindKnownRecipes(Vendor)))
                    {
                        return true;
                    }
                }
            }
            return base.HandleEvent(E);
        }
    }
}
