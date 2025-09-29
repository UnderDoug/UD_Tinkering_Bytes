using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using XRL.World.Text;
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

        public UD_VendorDisassembly()
        {
            ResetDisassembly();
        }

        public virtual void ResetDisassembly()
        {
            Disassembly = null;
        }

        public static bool VendorDoDisassembly(GameObject Vendor, GameObject Item, TinkerItem TinkerItem, int CostPerItem)
        {
            if (Vendor == null || Item == null || TinkerItem == null || !Vendor.HasPart<UD_VendorDisassembly>())
            {
                Popup.ShowFail($"That trader or item doesn't exist, the trader is unable to disassemble or the item can't be disassembled (this is an error).");
                return false;
            }

            UD_SyncedDisassembly syncedDisassembly = new(Vendor, CostPerItem);

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
                && E.Vendor is GameObject Vendor
                && E.Item is GameObject Item
                && The.Player is GameObject player
                && Item.TryGetPart(out TinkerItem tinkerItem))
            {
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
                    if (VendorDoDisassembly(Vendor, Item, tinkerItem, RealCostPerItem))
                    {
                        ResetDisassembly();
                        return true;
                    }
                    ResetDisassembly();
                }
                if (!E.Second)
                {
                    StringBuilder SB = Event.NewStringBuilder();
                    ReplaceBuilder RB = GameText.StartReplace(SB);
                    RB.AddObject(Vendor);
                    RB.AddObject(player);

                    string thisThese = multipleItems ? ("these " + itemCount.Things("item")) : "this item";
                    if (player.GetFreeDrams() < totalCost)
                    {
                        SB.Append("=object.T= =verb:do:afterpronoun= not have the required ");
                        SB.AppendColored("C", totalCost).Append((totalCost == 1) ? "dram" : "drams");
                        SB.Append(thisThese).Append(".");

                        Popup.ShowFail(RB.ToString());
                    }
                    else
                    {
                        SB.Append("=object.T= may have =object.t= disassemble ").Append(thisThese).Append(" for ");
                        SB.AppendColored("C", totalCost).Append((totalCost == 1) ? "dram" : "drams").Append(" of fresh water.");
                        SB.AppendLines(2).Append("Note: this will close the trade window, ending the conversation that opened it.");

                        if (Popup.ShowYesNo(RB.ToString()) == DialogResult.Yes)
                        {
                            List<Action<GameObject>> broadcastActions = null;

                            if (multipleItems && (AutoAct.ShouldHostilesInterrupt("o") || (Vendor.AreHostilesNearby() && Vendor.FireEvent("CombatPreventsTinkering"))))
                            {
                                string hostilesMessage = "=subject.T= cannot disassemble so many items at once with hostiles nearby.";
                                Popup.ShowFail(hostilesMessage.Replacer(Vendor));
                                E.RequestCancelSecond();
                                return false;
                            }

                            if (Item.IsImportant())
                            {
                                if (Item.ConfirmUseImportant(player, "disassemble", null, (!multipleItems) ? 1 : itemCount))
                                {
                                    E.RequestCancelSecond();
                                    return false;
                                }
                            }

                            if (TinkerItem.ConfirmBeforeDisassembling(Item))
                            {
                                string pluralItems = "=object.t=";
                                if (multipleItems)
                                {
                                    pluralItems = "all the ";
                                    pluralItems += Item.IsPlural ? Item.ShortDisplayName : Grammar.Pluralize(Item.ShortDisplayName);
                                }
                                string confirmDisassemble = "Are you sure you want =subject.t= to disassemble " + pluralItems + "?";
                                if (Popup.ShowYesNoCancel(confirmDisassemble.Replacer(Vendor, Item)) != DialogResult.Yes)
                                {
                                    E.RequestCancelSecond();
                                    return false;
                                }
                            }

                            if (!Item.Owner.IsNullOrEmpty() && !Item.HasPropertyOrTag("DontWarnOnDisassemble"))
                            {
                                string themIt = multipleItems ? "them" : Item.them;
                                string confirmOwnerMsg = "=object.T= =verb:are:afterpronoun= not owned by you. ";
                                confirmOwnerMsg += "Are you sure you want =subject.t= to disassemble " + themIt + "?";
                                if (Popup.ShowYesNoCancel(confirmOwnerMsg.Replacer(Vendor, Item)) != DialogResult.Yes)
                                {
                                    E.RequestCancelSecond();
                                    return false;
                                }
                                broadcastActions ??= new();
                                broadcastActions.Add(Item.Physics.BroadcastForHelp);
                            }

                            if (Item.InInventory is GameObject container
                                && container != player
                                && !container.Owner.IsNullOrEmpty() 
                                && container.Owner != Item.Owner 
                                && !container.HasPropertyOrTag("DontWarnOnDisassemble"))
                            {
                                string itemsAnItem = multipleItems ? "items" : Item.an();
                                string confirmContainerMsg = "=object.T= =verb:are:afterpronoun= not owned by you. ";
                                confirmContainerMsg += "Are you sure you want =subject.t= to disassemble " + itemsAnItem + "?";
                                if (Popup.ShowYesNoCancel(confirmContainerMsg.Replacer(Vendor, container)) != DialogResult.Yes)
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
                    }
                    E.RequestCancelSecond();
                }
                return false;
            }
            return base.HandleEvent(E);
        }
    }
}
