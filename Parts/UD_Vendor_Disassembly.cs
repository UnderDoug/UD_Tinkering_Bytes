using System;
using System.Collections;
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

using UD_Tinkering_Bytes;

namespace XRL.World.Parts
{
    [Serializable]
    public class UD_Vendor_Disassembly : IScribedPart, IVendorActionEventHandler
    {
        public const string COMMAND_DISASSEMBLE = "VendorCommand_Disassemble";
        public const string COMMAND_DISASSEMBLE_ALL = "VendorCommand_DisassembleAll";

        public bool WantVendorActions => ParentObject != null && ParentObject.HasSkill(nameof(Tinkering_Disassemble));

        // Disassembly 
        public Disassembly Disassembly;

        public UD_Vendor_Disassembly()
        {
            ResetDisassembly();
        }

        public virtual void ResetDisassembly()
        {
            Disassembly = null;
        }

        public static void ProcessVendorDisassemblyWhat(Disassembly Disassembly)
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
                        if (Disassembly.DisassembledWhats == null)
                        {
                            Disassembly.DisassembledWhats = new List<string>();
                        }
                        if (Disassembly.DisassembledWhatsWhere == null)
                        {
                            Disassembly.DisassembledWhatsWhere = new Dictionary<string, string>();
                        }
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

        public static bool ContinueVendor(GameObject Vendor, Disassembly Disassembly)
        {
            GameObject Object = Disassembly.Object;
            if (!GameObject.Validate(ref Disassembly.Object))
            {
                Disassembly.InterruptBecause = "the item you were working on disappeared";
                return false;
            }
            if (Object.IsInGraveyard())
            {
                Disassembly.InterruptBecause = "the item you were working on was destroyed";
                return false;
            }
            if (Object.IsNowhere())
            {
                Disassembly.InterruptBecause = "the item you were working on disappeared";
                return false;
            }
            if (Object.IsInStasis())
            {
                Disassembly.InterruptBecause = "you can no longer interact with " + Object.t();
                return false;
            }
            if (!Object.TryGetPart<TinkerItem>(out var tinkerItem))
            {
                Disassembly.InterruptBecause = Object.t() + " can no longer be disassembled";
                return false;
            }
            if (!Vendor.HasSkill(nameof(Tinkering_Disassemble)))
            {
                Disassembly.InterruptBecause = "you no longer know how to disassemble things";
                return false;
            }
            if (!tinkerItem.CanBeDisassembled(Vendor))
            {
                Disassembly.InterruptBecause = Object.t() + " can no longer be disassembled";
                return false;
            }
            if (!Vendor.CanMoveExtremities("Disassemble", ShowMessage: false, Involuntary: false, AllowTelekinetic: true))
            {
                Disassembly.InterruptBecause = "you can no longer move your extremities";
                return false;
            }
            int totalEnergyCost = 0;
            try
            {
                bool Interrupt = false;
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
                        disassembleBonus = GetTinkeringBonusEvent.GetFor(Vendor, Object, "Disassemble", Disassembly.BitChance, disassembleBonus, ref Interrupt);
                        if (Interrupt)
                        {
                            return false;
                        }
                        Disassembly.BitChance += disassembleBonus;
                    }
                }
                string activeBlueprint = tinkerItem.ActiveBlueprint;
                TinkerData tinkerData = null;
                List<TinkerData> learnableMods = null;
                List<GameObject> dataDiskObjects = Vendor.Inventory.GetObjectsViaEventList(GO => GO.HasPart<DataDisk>());
                if (Vendor.HasSkill(nameof(Tinkering_ReverseEngineer)))
                {
                    foreach (TinkerData tinkerRecipe in TinkerData.TinkerRecipes)
                    {
                        bool recipeTypeIsBuild = tinkerRecipe.Type == "Build";
                        bool recipeBlueprintIsItem = recipeTypeIsBuild && tinkerRecipe.Blueprint == activeBlueprint;
                        bool recipeIsBuildForThisItem = recipeTypeIsBuild && recipeBlueprintIsItem;

                        bool recipeTypeIsMod = tinkerRecipe.Type == "Mod";
                        bool recipePartNameIsModItemHas = recipeTypeIsMod && tinkerRecipe.Blueprint == activeBlueprint;
                        bool recipeIsModThisItemHas = recipeTypeIsBuild && Object.HasPart(tinkerRecipe.PartName);

                        if (!recipeIsBuildForThisItem || !recipeIsModThisItemHas)
                        {
                            continue;
                        }
                        bool alreadyKnowMod = false;
                        foreach (GameObject dataDiskObject in dataDiskObjects)
                        {
                            if (dataDiskObject.TryGetPart(out DataDisk dataDisk))
                            {
                                if (recipeIsBuildForThisItem)
                                {
                                    if (dataDisk.Data.Blueprint != activeBlueprint)
                                    {
                                        tinkerData = tinkerRecipe;
                                    }
                                    break;
                                }
                                if (recipeIsModThisItemHas)
                                {
                                    if (dataDisk.Data.Blueprint == tinkerRecipe.Blueprint)
                                    {
                                        alreadyKnowMod = true;
                                        break;
                                    }
                                }
                            }
                        }
                        if (!alreadyKnowMod)
                        {
                            learnableMods ??= new List<TinkerData>();
                            learnableMods.Add(tinkerRecipe);
                        }
                    }
                }
                int chance = 0;
                int reverseEngineerBonus = 0;
                if (tinkerData != null || (learnableMods != null && learnableMods.Count > 0))
                {
                    chance = 15;
                    reverseEngineerBonus = GetTinkeringBonusEvent.GetFor(Vendor, Object, "ReverseEngineer", chance, reverseEngineerBonus, ref Interrupt);
                    if (Interrupt)
                    {
                        return false;
                    }
                    chance += reverseEngineerBonus;
                }
                bool doSifrah = Options.SifrahReverseEngineer && (tinkerData != null || (learnableMods != null && learnableMods.Count > 0));
                bool flag3 = false;
                if (doSifrah)
                {
                    flag3 = Popup.ShowYesNo("Do you want to try to reverse engineer " + Object.t() + "?", "Sounds/UI/ui_notification", AllowEscape: false) == DialogResult.Yes;
                }
                ReverseEngineeringSifrah reverseEngineeringSifrah = null;
                int reverseEngineerRating = 0;
                int complexity = 0;
                int difficulty = 0;
                if (flag3)
                {
                    reverseEngineerRating = Vendor.Stat("Intelligence") + reverseEngineerBonus;
                    Examiner part = Object.GetPart<Examiner>();
                    complexity = part?.Complexity ?? Object.GetTier();
                    difficulty = part?.Difficulty ?? 0;
                }
                try
                {
                    InventoryActionEvent.Check(Object, Vendor, Object, "EmptyForDisassemble");
                }
                catch (Exception x)
                {
                    MetricsManager.LogError("EmptyForDisassemble", x);
                }
                bool flag4 = NumberWanted > 1 && OriginalCount > 1;
                if (!Object.IsTemporary)
                {
                    WasTemporary = false;
                }
                if (DisassemblingWhat == null)
                {
                    DisassemblingWhat = Object.t(int.MaxValue, null, null, AsIfKnown: false, Single: true, NoConfusion: false, NoColor: false, Stripped: false, WithoutTitles: true, Short: true, BaseOnly: false, null, IndicateHidden: false, SecondPerson: true, Reflexive: false, null);
                    if (flag4 || TotalNumberWanted > 1)
                    {
                        MessageQueue.AddPlayerMessage("You start disassembling " + Object.t(int.MaxValue, null, null, AsIfKnown: false, Single: false, NoConfusion: false, NoColor: false, Stripped: false, WithoutTitles: true, Short: true, BaseOnly: false, null, IndicateHidden: false, SecondPerson: true, Reflexive: false, null) + ".");
                    }
                }
                if (DisassemblingWhere == null && Object.CurrentCell != null)
                {
                    DisassemblingWhere = Vendor.DescribeDirectionToward(Object);
                }
                bool flag5 = false;
                if (NumberDone < NumberWanted)
                {
                    string text = "";
                    bool flag6 = true;
                    bool flag7 = false;
                    bool flag8 = false;
                    int num3 = 0;
                    GameObject gameObject = null;
                    if (!WasTemporary)
                    {
                        if (tinkerItem.Bits.Length == 1)
                        {
                            if (tinkerItem.NumberMade <= 1 || Stat.Random(1, tinkerItem.NumberMade + 1) == 1)
                            {
                                text += tinkerItem.Bits;
                            }
                        }
                        else
                        {
                            int num4 = tinkerItem.Bits.Length - 1;
                            for (int i = 0; i < tinkerItem.Bits.Length; i++)
                            {
                                if ((num4 == i || BitChance.in100()) && (tinkerItem.NumberMade <= 1 || Stat.Random(1, tinkerItem.NumberMade + 1) == 1))
                                {
                                    text += tinkerItem.Bits[i];
                                }
                            }
                        }
                        if (doSifrah)
                        {
                            if (flag3)
                            {
                                reverseEngineeringSifrah = new ReverseEngineeringSifrah(Object, complexity, difficulty, reverseEngineerRating, tinkerData);
                                reverseEngineeringSifrah.Play(Object);
                                if (reverseEngineeringSifrah.Succeeded)
                                {
                                    flag7 = true;
                                    if (learnableMods != null)
                                    {
                                        if (reverseEngineeringSifrah.Mods > 0)
                                        {
                                            if (learnableMods.Count > reverseEngineeringSifrah.Mods)
                                            {
                                                List<TinkerData> list2 = new List<TinkerData>();
                                                for (int j = 0; j < reverseEngineeringSifrah.Mods; j++)
                                                {
                                                    list2.Add(learnableMods[j]);
                                                }
                                                learnableMods = list2;
                                            }
                                        }
                                        else
                                        {
                                            learnableMods = null;
                                        }
                                    }
                                    if (reverseEngineeringSifrah.Critical)
                                    {
                                        flag6 = false;
                                        flag8 = true;
                                    }
                                    num3 = reverseEngineeringSifrah.XP;
                                }
                                else
                                {
                                    tinkerData = null;
                                    learnableMods = null;
                                    if (reverseEngineeringSifrah.Critical)
                                    {
                                        Abort = true;
                                        BitsDone = "";
                                    }
                                }
                            }
                            else
                            {
                                tinkerData = null;
                                learnableMods = null;
                            }
                        }
                        else if (!chance.in100())
                        {
                            tinkerData = null;
                            learnableMods = null;
                        }
                        if (tinkerData != null || (learnableMods != null && learnableMods.Count > 0))
                        {
                            bool flag9 = false;
                            string text2 = null;
                            if (tinkerData != null)
                            {
                                gameObject = GameObject.CreateSample(tinkerData.Blueprint);
                                tinkerData.DisplayName = gameObject.DisplayNameOnlyDirect;
                                text2 = "build " + (gameObject.IsPlural ? gameObject.DisplayNameOnlyDirect : gameObject.GetPluralName(AsIfKnown: true, NoConfusion: true, Stripped: false, BaseOnly: true));
                                flag9 = true;
                            }
                            if (learnableMods != null)
                            {
                                List<string> list3 = new List<string>();
                                foreach (TinkerData item in learnableMods)
                                {
                                    list3.Add(item.DisplayName);
                                }
                                string text3 = "mod items with the " + Grammar.MakeAndList(list3) + " " + ((list3.Count == 1) ? "mod" : "mods");
                                text2 = ((text2 != null) ? (text2 + " and " + text3) : text3);
                            }
                            if (text2 != null)
                            {
                                string text4 = "{{G|Eureka! You may now " + text2;
                                text4 = ((!flag6) ? (text4 + "... and were able to work out how without needing to destroy " + ((!flag9) ? Object.t(int.MaxValue, null, null, AsIfKnown: false, Single: false, NoConfusion: false, NoColor: false, Stripped: false, WithoutTitles: true, Short: true, BaseOnly: false, null, IndicateHidden: false, SecondPerson: true, Reflexive: false, null) : (Object.IsPlural ? "these" : "this one")) + "!") : (text4 + "."));
                                text4 += "}}";
                                if (ReverseEngineeringMessage.IsNullOrEmpty())
                                {
                                    ReverseEngineeringMessage = text4;
                                }
                                else
                                {
                                    ReverseEngineeringMessage = ReverseEngineeringMessage + "\n\n" + text4;
                                }
                            }
                            if (tinkerData != null)
                            {
                                TinkerData.KnownRecipes.Add(tinkerData);
                            }
                            if (learnableMods != null)
                            {
                                TinkerData.KnownRecipes.AddRange(learnableMods);
                            }
                        }
                        else if (flag7)
                        {
                            string text5 = "You are unable to make further progress reverse engineering " + Object.poss("modding") + ".";
                            if (ReverseEngineeringMessage.IsNullOrEmpty())
                            {
                                ReverseEngineeringMessage = text5;
                            }
                            else
                            {
                                ReverseEngineeringMessage = ReverseEngineeringMessage + "\n\n" + text5;
                            }
                        }
                        if (num3 > 0)
                        {
                            Vendor.AwardXP(num3, -1, 0, int.MaxValue, null, Object);
                        }
                        if (flag8)
                        {
                            TinkeringSifrah.AwardInsight();
                        }
                    }
                    NumberDone++;
                    TotalNumberDone++;
                    if (TotalNumberWanted > 1)
                    {
                        Loading.SetLoadingStatus("Disassembled " + TotalNumberDone.Things("item") + " of " + TotalNumberWanted + "...");
                    }
                    if (!Abort)
                    {
                        if (Vendor.HasRegisteredEvent("ModifyBitsReceived"))
                        {
                            Event @event = Event.New("ModifyBitsReceived", "Item", Object, "Bits", text);
                            Vendor.FireEvent(@event);
                            text = @event.GetStringParameter("Bits", "");
                        }
                        BitsDone += text;
                    }
                    totalEnergyCost += Disassembly.EnergyCostPer;
                    Disassembly.DoBitMessage = true;
                    Object.PlayWorldOrUISound("Sounds/Misc/sfx_interact_artifact_disassemble", null);
                    if (flag6)
                    {
                        if (Alarms != null)
                        {
                            foreach (Action<GameObject> alarm in Alarms)
                            {
                                alarm(Vendor);
                            }
                            Alarms = null;
                        }
                        Object.Destroy();
                        if (!GameObject.Validate(Object) || Object.IsNowhere())
                        {
                            flag5 = false;
                        }
                    }
                }
                if (NumberDone >= NumberWanted || flag5)
                {
                    ProcessDisassemblingWhat();
                    if (!Abort)
                    {
                        if (!Queue.IsNullOrEmpty())
                        {
                            Object = Queue[0];
                            Queue.RemoveAt(0);
                            NumberDone = 0;
                            OriginalCount = Object.Count;
                            if (QueueNumberWanted == null || !QueueNumberWanted.TryGetValue(Object, out NumberWanted))
                            {
                                NumberWanted = OriginalCount;
                            }
                            Alarms = null;
                            QueueAlarms?.TryGetValue(Object, out Alarms);
                        }
                        else
                        {
                            Abort = true;
                        }
                    }
                }
            }
            finally
            {
                if (totalEnergyCost > 0)
                {
                    Vendor.UseEnergy(totalEnergyCost, "Skill Tinkering Disassemble", null, null);
                }
            }
            return true;
        }

        public override bool WantEvent(int ID, int Cascade)
        {
            return base.WantEvent(ID, Cascade)
                || (WantVendorActions && ID == GetVendorActionsEvent.ID)
                || (WantVendorActions && ID == VendorActionEvent.ID);
        }
        public virtual bool HandleEvent(GetVendorActionsEvent E)
        {
            if (E.Vendor != null && ParentObject == E.Vendor && WantVendorActions)
            {
                if (E.Item.InInventory != ParentObject
                    && E.Item.TryGetPart(out TinkerItem tinkerItem)
                    && tinkerItem.CanBeDisassembled(E.Vendor))
                {
                    E.AddAction("Disassemble", "disassemble", COMMAND_DISASSEMBLE, Key: 'd', ClearAndSetUpTradeUI: true);
                    if (E.Item.Count > 1)
                    {
                        E.AddAction("Disassemble all", "disassemble all", COMMAND_DISASSEMBLE_ALL, Key: 'D', ClearAndSetUpTradeUI: true);
                    }
                }
            }
            return base.HandleEvent(E);
        }
        public virtual bool HandleEvent(VendorActionEvent E)
        {
            if ((E.Command == COMMAND_DISASSEMBLE || E.Command == COMMAND_DISASSEMBLE_ALL) && E.Item != null && E.Item.TryGetPart(out TinkerItem tinkerItem))
            {
                int itemCount = E.Item.Count;
                bool disassembleAll = E.Command == COMMAND_DISASSEMBLE_ALL;
                bool multipleItems = disassembleAll && itemCount > 1;

                int complexity = E.Item.GetComplexity();
                int examineDifficulty = E.Item.GetExamineDifficulty();
                float costFactor = complexity + examineDifficulty;
                int costPerItem = (int)Math.Max(2.0, -0.0667 + 1.24 * (double)costFactor + 0.0967 * Math.Pow(costFactor, 2.0) + 0.0979 * Math.Pow(costFactor, 3.0));

                int totalCost = multipleItems ? itemCount * costPerItem : costPerItem;

                if (The.Player.GetFreeDrams() < totalCost)
                {
                    Popup.ShowFail(
                        "You do not have the required {{C|" + totalCost + "}}" + $" {((totalCost == 1) ? "dram" : "drams")} " +
                        $"to disassemble {(multipleItems ? $"these {itemCount.Things("item")}" : "this item")}.");
                }
                else if (Popup.ShowYesNo(
                    $"You may disassemble {(multipleItems ? $"these {itemCount.Things("item")}" : "this item")} " +
                    $"for {totalCost.Things("dram")} of fresh water.") == DialogResult.Yes)
                {
                    List<Action<GameObject>> broadcastActions = null;

                    if (disassembleAll && multipleItems && AutoAct.ShouldHostilesInterrupt("o"))
                    {
                        Popup.ShowFail($"{E.Vendor.T()} cannot use disassemble all with hostiles nearby.");
                        return false;
                    }
                    if (E.Item.IsInStasis())
                    {
                        Popup.ShowFail($"{E.Vendor.T()} cannot seem to affect {E.Item.t()} in any way.");
                        return false;
                    }
                    if (!E.Item.Owner.IsNullOrEmpty() && !E.Item.HasPropertyOrTag("DontWarnOnDisassemble"))
                    {
                        if (Popup.ShowYesNoCancel(
                            $"{E.Item.The} {E.Item.DisplayNameOnly} {(multipleItems ? "are" : E.Item.Is)} not owned by you. " +
                            $"Are you sure you want {E.Vendor.T()} to disassemble {(multipleItems ? "them" : E.Item.them)}?") != 0)
                        {
                            return false;
                        }
                        broadcastActions ??= new();
                        broadcastActions.Add(E.Item.Physics.BroadcastForHelp);
                    }
                    if (E.Item.IsImportant())
                    {
                        if (!E.Item.ConfirmUseImportant(null, "disassemble", null, (!multipleItems) ? 1 : itemCount))
                        {
                            return false;
                        }
                    }
                    else if (TinkerItem.ConfirmBeforeDisassembling(E.Item)
                        && Popup.ShowYesNoCancel($"Are you sure you want {E.Vendor.T()} to disassemble {(multipleItems ? $"all the {(E.Item.IsPlural ? E.Item.ShortDisplayName : Grammar.Pluralize(E.Item.ShortDisplayName))}" : E.Item.t())}?") != 0)
                    {
                        return false;
                    }
                    GameObject container = E.Item.InInventory;
                    if (container != null && container != The.Player && !container.Owner.IsNullOrEmpty() && container.Owner != E.Item.Owner && !container.HasPropertyOrTag("DontWarnOnDisassemble"))
                    {
                        if (Popup.ShowYesNoCancel(
                            $"{container.Does("are")} not owned by you. " +
                            $"Are you sure you want {E.Vendor.T()} to disassemble {(multipleItems ? "items" : E.Item.an())} inside {container.them}?") != 0)
                        {
                            return false;
                        }
                        broadcastActions ??= new();
                        broadcastActions.Add(container.Physics.BroadcastForHelp);
                    }

                    Disassembly = new(E.Item, multipleItems ? itemCount : 1, EnergyCostPer: 0);

                    bool interrupt = false;
                    while (!interrupt && !Disassembly.Abort)
                    {
                        The.Player.UseDrams(costPerItem);
                        E.Vendor.GiveDrams(costPerItem);

                        if (Disassembly.BitChance == int.MinValue)
                        {
                            if (tinkerItem.Bits.Length == 1)
                            {
                                Disassembly.BitChance = 0;
                            }
                            else
                            {
                                int disassembleBonus = E.Vendor.GetIntProperty("DisassembleBonus");
                                Disassembly.BitChance = 50;
                                disassembleBonus = GetTinkeringBonusEvent.GetFor(E.Vendor, E.Item, "Disassemble", Disassembly.BitChance, disassembleBonus, ref interrupt);
                                if (interrupt)
                                {
                                    continue;
                                }
                                Disassembly.BitChance += disassembleBonus;
                            }
                        }
                        string activeBlueprint = tinkerItem.ActiveBlueprint;
                        int chance = 0;
                        try
                        {
                            InventoryActionEvent.Check(E.Item, E.Vendor, E.Item, "EmptyForDisassemble");
                        }
                        catch (Exception x)
                        {
                            MetricsManager.LogError("EmptyForDisassemble", x);
                        }
                        bool multipleDisassembling = Disassembly.NumberWanted > 1 && Disassembly.OriginalCount > 1;
                        if (!E.Item.IsTemporary)
                        {
                            Disassembly.WasTemporary = false;
                        }
                        if (Disassembly.DisassemblingWhat == null)
                        {
                            Disassembly.DisassemblingWhat = E.Item.t();
                            if (multipleDisassembling || Disassembly.TotalNumberWanted > 1)
                            {
                                MessageQueue.AddPlayerMessage($"{E.Vendor.T()} {E.Vendor.GetVerb("start")} disassembling {E.Item.t()}.");
                            }
                        }
                        if (Disassembly.DisassemblingWhere == null && E.Item.CurrentCell != null)
                        {
                            Disassembly.DisassemblingWhere = E.Vendor.DescribeDirectionToward(E.Item);
                        }
                        bool finished = false;
                        if (Disassembly.NumberDone < Disassembly.NumberWanted)
                        {
                            string bitsString = "";
                            if (!Disassembly.WasTemporary)
                            {
                                if (tinkerItem.Bits.Length == 1)
                                {
                                    if (tinkerItem.NumberMade <= 1 || Stat.Random(1, tinkerItem.NumberMade + 1) == 1)
                                    {
                                        bitsString += tinkerItem.Bits;
                                    }
                                }
                                else
                                {
                                    int num4 = tinkerItem.Bits.Length - 1;
                                    for (int i = 0; i < tinkerItem.Bits.Length; i++)
                                    {
                                        if ((num4 == i || Disassembly.BitChance.in100()) && (tinkerItem.NumberMade <= 1 || Stat.Random(1, tinkerItem.NumberMade + 1) == 1))
                                        {
                                            bitsString += tinkerItem.Bits[i];
                                        }
                                    }
                                }
                                if (!chance.in100())
                                {
                                    broadcastActions = null;
                                }
                            }
                            Disassembly.NumberDone++;
                            Disassembly.TotalNumberDone++;
                            if (Disassembly.TotalNumberWanted > 1)
                            {
                                Loading.SetLoadingStatus("Disassembled " + Disassembly.TotalNumberDone.Things("item") + " of " + Disassembly.TotalNumberWanted + "...");
                            }
                            if (!Disassembly.Abort)
                            {
                                if (E.Vendor.HasRegisteredEvent("ModifyBitsReceived"))
                                {
                                    Event @event = Event.New("ModifyBitsReceived", "Item", E.Item, "Bits", bitsString);
                                    E.Vendor.FireEvent(@event);
                                    bitsString = @event.GetStringParameter("Bits", "");
                                }
                                Disassembly.BitsDone += bitsString;
                            }
                            Disassembly.DoBitMessage = true;
                            E.Item.PlayWorldOrUISound("Sounds/Misc/sfx_interact_artifact_disassemble", null);

                            if (Disassembly.Alarms != null)
                            {
                                foreach (Action<GameObject> alarm in Disassembly.Alarms)
                                {
                                    alarm(E.Vendor);
                                }
                                Disassembly.Alarms = null;
                            }
                            E.Item.Destroy();
                            if (!GameObject.Validate(E.Item) || E.Item.IsNowhere())
                            {
                                finished = false;
                            }
                        }
                        if (Disassembly.NumberDone >= Disassembly.NumberWanted || finished)
                        {
                            ProcessVendorDisassemblyWhat(Disassembly);

                            if (!Disassembly.Abort)
                            {
                                if (!Disassembly.Queue.IsNullOrEmpty())
                                {
                                    E.Item = Disassembly.Queue[0];
                                    Disassembly.Queue.RemoveAt(0);
                                    Disassembly.NumberDone = 0;
                                    Disassembly.OriginalCount = E.Item.Count;
                                    if (Disassembly.QueueNumberWanted == null || !Disassembly.QueueNumberWanted.TryGetValue(E.Item, out Disassembly.NumberWanted))
                                    {
                                        Disassembly.NumberWanted = Disassembly.OriginalCount;
                                    }
                                    Disassembly.Alarms = null;
                                    Disassembly.QueueAlarms?.TryGetValue(E.Item, out Disassembly.Alarms);
                                }
                                else
                                {
                                    Disassembly.Abort = true;
                                }
                            }
                        }
                    }
                    Disassembly.Complete();
                    Disassembly.End();
                }
            }
            return base.HandleEvent(E);
        }
    }
}
