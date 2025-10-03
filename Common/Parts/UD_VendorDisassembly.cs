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
                Popup.ShowFail(
                    "That trader or item doesn't exist, the item can't be disassembled, " +
                    "or the trader is unable to disassemble (this is an error)." +
                    "\n" + UD_Tinkering_Bytes.Utils.TellModAuthor);
                if (Vendor == null)
                {
                    MetricsManager.LogModError(UD_Tinkering_Bytes.Utils.ThisMod, "Passed null " + nameof(Vendor));
                }
                if (Item == null)
                {
                    MetricsManager.LogModError(UD_Tinkering_Bytes.Utils.ThisMod, "Passed null " + nameof(Item));
                }
                if (TinkerItem == null)
                {
                    MetricsManager.LogModError(UD_Tinkering_Bytes.Utils.ThisMod, "Passed null " + nameof(TinkerItem));
                }
                if (!Vendor.HasPart<UD_VendorDisassembly>())
                {
                    MetricsManager.LogModError(UD_Tinkering_Bytes.Utils.ThisMod, nameof(Vendor) + " lacks " + nameof(UD_VendorDisassembly));
                }
                return false;
            }

            UD_SyncedDisassembly syncedDisassembly = new(Vendor, CostPerItem);

            AutoAct.Action = syncedDisassembly;
            AutoAct.Setting = "o";
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
                int amountToDisassemble = itemCount;
                bool multipleItems = E.Command == COMMAND_DISASSEMBLE_ALL && amountToDisassemble > 1;

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
                totalCost = (int)Math.Max(minCost, multipleItems ? amountToDisassemble * costPerItem : costPerItem);
                int RealCostPerItem = multipleItems ? totalCost / amountToDisassemble : totalCost;
                totalCost = multipleItems ? amountToDisassemble * RealCostPerItem : RealCostPerItem;

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
                        return true;
                    }
                }
                if (!E.Second)
                {
                    int costToDisassemble = totalCost;

                    string thisThese = multipleItems ? ("these " + amountToDisassemble.Things("item")) : "this item";
                    string dramsCost = costToDisassemble.Things("dram").Color("C");

                    bool tooExpensive = player.GetFreeDrams() < costToDisassemble;
                    if (tooExpensive)
                    {
                        string tooExpensiveMsg =
                            ("=subject.T= =verb:don't:afterpronoun= have the required " +
                            dramsCost + " to have =object.t= disassemble " + "item".ThisTheseN(amountToDisassemble, multipleItems))
                                .StartReplace()
                                .AddObject(player)
                                .AddObject(Vendor)
                                .ToString();

                        if (player.GetFreeDrams() > RealCostPerItem)
                        {
                            int maxAffordable = (int)Math.Floor(player.GetFreeDrams() / (double)RealCostPerItem);
                            string realCostPerItemString = RealCostPerItem.Things("dram").Color("C");
                            string disassembleSomeMsg =
                                ("\n\n" + "How many of the " + maxAffordable.ToString().Color("C") + " =subject.t= =verb:can:afterpronoun= " +
                                "afford would =subject.subjective= like =object.t= to disassemble?\n\n" +
                                "It costs " + realCostPerItemString + " of fresh water each to disassemble these items.")
                                    .StartReplace()
                                    .AddObject(player)
                                    .AddObject(Vendor)
                                    .ToString();

                            amountToDisassemble = (int)Popup.AskNumber(
                                Message: tooExpensiveMsg + disassembleSomeMsg,
                                Start: maxAffordable,
                                Max: maxAffordable);
                        }
                        else
                        {
                            Popup.ShowFail(tooExpensiveMsg);
                        }
                    }

                    if (amountToDisassemble > 0)
                    {
                        costToDisassemble = amountToDisassemble * RealCostPerItem;
                        dramsCost = costToDisassemble.Things("dram").Color("C");
                        thisThese = multipleItems ? ("these " + amountToDisassemble.Things("item")) : "this item";
                        tooExpensive = player.GetFreeDrams() < costToDisassemble;
                    }
                    if (!tooExpensive)
                    {
                        string confirmDisassembleMsg =
                            ("=subject.T= may have =object.t= disassemble " + thisThese +
                            " for " + dramsCost + " of fresh water.\n\n" +
                            "Note: the trade window will be closed, ending the conversation that opened it.")
                                .StartReplace()
                                .AddObject(player)
                                .AddObject(Vendor)
                                .ToString();

                        if (Popup.ShowYesNo(confirmDisassembleMsg) == DialogResult.Yes)
                        {
                            List<Action<GameObject>> broadcastActions = null;

                            if (multipleItems 
                                && (AutoAct.ShouldHostilesInterrupt("o") 
                                    || (Vendor.AreHostilesNearby() && Vendor.FireEvent("CombatPreventsTinkering"))))
                            {
                                string hostilesMessage = "=subject.T= cannot disassemble so many items at once with hostiles nearby."
                                    .StartReplace()
                                    .AddObject(Vendor)
                                    .ToString();

                                Popup.ShowFail(hostilesMessage);
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
                                string pluralItems = "=item.t=";
                                if (multipleItems)
                                {
                                    pluralItems = "all the ";
                                    pluralItems += Item.IsPlural ? Item.ShortDisplayName : Grammar.Pluralize(Item.ShortDisplayName);
                                }
                                string confirmDisassemble = 
                                    ("=verb:Is:afterpronoun= =subject.t= sure =subject.t= " +
                                    "=verb:want:afterpronoun= =object.t= to disassemble " + pluralItems + "?")
                                        .StartReplace()
                                        .AddObject(player)
                                        .AddObject(Vendor)
                                        .AddObject(Item, "item")
                                        .ToString();
                                if (Popup.ShowYesNoCancel(confirmDisassemble) != DialogResult.Yes)
                                {
                                    E.RequestCancelSecond();
                                    return false;
                                }
                            }

                            if (!Item.Owner.IsNullOrEmpty() && !Item.HasPropertyOrTag("DontWarnOnDisassemble"))
                            {
                                string notItemOwnerMsg = "=subject.T= =verb:isn't:afterpronoun= owned by =object.t=."
                                    .StartReplace()
                                    .AddObject(Item)
                                    .AddObject(player)
                                    .ToString();

                                string themIt = multipleItems ? "them" : "=subject.objective="
                                        .StartReplace()
                                        .AddObject(Item)
                                        .ToString();

                                string confirmNotOwnedMsg = 
                                    ("=verb:Is:afterpronoun= =subject.subjective= sure =subject.subjective= " +
                                    "=verb:want:afterpronoun= =object.t= to disassemble " + themIt + "?")
                                        .StartReplace()
                                        .AddObject(player)
                                        .AddObject(Vendor)
                                        .ToString();

                                if (Popup.ShowYesNoCancel(notItemOwnerMsg + " " + confirmNotOwnedMsg) != DialogResult.Yes)
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
                                string notContainerOwnerMsg = "=subject.T= =verb:isn't:afterpronoun= owned by =object.t=."
                                    .StartReplace()
                                    .AddObject(Item)
                                    .AddObject(player)
                                    .ToString();

                                string itemsAnItem = multipleItems ? "items" : "=subject.a= =subject.t="
                                    .StartReplace()
                                    .AddObject(Item)
                                    .ToString();
                                string containerIt = "=subject.objective="
                                    .StartReplace()
                                    .AddObject(container)
                                    .ToString();

                                string confirmContainerMsg =
                                    ("=verb:Is:afterpronoun= =subject.subjective= sure =subject.subjective= " +
                                    "=verb:want:afterpronoun= =object.t= to disassemble " + itemsAnItem + 
                                    " inside " + containerIt + "?")
                                        .StartReplace()
                                        .AddObject(player)
                                        .AddObject(Vendor)
                                        .ToString();

                                if (Popup.ShowYesNoCancel(notContainerOwnerMsg + " " + confirmContainerMsg) != DialogResult.Yes)
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

                            Disassembly = new(E.Item, multipleItems ? amountToDisassemble : 1, EnergyCostPer: 0);
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
