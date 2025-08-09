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
    public class UD_TinkeringBytes : IScribedPart, IVendorActionEventHandler
    {
        public const string COMMAND_DISASSEMBLE = "VendorCommand_Disassemble";
        public const string COMMAND_DISASSEMBLE_ALL = "VendorCommand_DisassembleAll";

        public bool BitsAllocated;

        // Disassembly 
        public Disassembly Disassembly;

        public UD_TinkeringBytes()
        {
            BitsAllocated = false;
            ResetDisassembly();
        }

        public virtual void ResetDisassembly()
        {
            Disassembly = null;
        }

        public override bool WantEvent(int ID, int Cascade)
        {
            return base.WantEvent(ID, Cascade)
                || ID == GetVendorActionsEvent.ID
                || ID == VendorActionEvent.ID;
        }
        public virtual bool HandleEvent(GetVendorActionsEvent E)
        {
            if (E.Vendor != null && ParentObject == E.Vendor)
            {
                if (E.Vendor.HasSkill(nameof(Tinkering_Disassemble))
                    && E.Item.InInventory != ParentObject
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
                            Disassembly.ProcessVendorDisassemblyWhat();

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
