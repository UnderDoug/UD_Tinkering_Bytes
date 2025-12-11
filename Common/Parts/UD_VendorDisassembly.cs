using System;
using System.Collections.Generic;

using XRL.UI;
using XRL.Language;
using XRL.World.Parts.Skill;
using XRL.World.Tinkering;
using XRL.World.Capabilities;

using UD_Modding_Toolbox;
using UD_Vendor_Actions;

using UD_Tinkering_Bytes;
using Utils = UD_Tinkering_Bytes.Utils;

namespace XRL.World.Parts
{
    [AlwaysHandlesVendor_UD_VendorActions]
    [Serializable]
    public class UD_VendorDisassembly : IScribedPart, I_UD_VendorActionEventHandler, IModEventHandler<GetVendorTinkeringBonusEvent>
    {
        private static bool doDebug = true;

        private Version? MigrateFrom = null;

        public const string COMMAND_DISASSEMBLE = "CmdVendorDisassemble";
        public const string COMMAND_DISASSEMBLE_ALL = "CmdVendorDisassembleAll";

        public bool WantVendorActions => (bool)ParentObject?.HasSkill(nameof(Tinkering_Disassemble)) && (bool)!ParentObject?.IsPlayer();

        public List<TinkerData> KnownRecipes => ParentObject?.GetPart<UD_VendorTinkering>()?.KnownRecipes;

        public Disassembly Disassembly;

        public int DisassembleBonus; // Added v0.1.0

        public int ReverseEngineerBonus; // Added v0.1.0

        public bool ReverseEngineerIgnoresSkillRequirement; // Added v0.1.0

        public bool ScribesReverseEngineeredRecipes; // Added v0.1.0

        public int ReverseEngineeredScribeChance; // Added v0.1.0

        public UD_VendorDisassembly()
        {
            ResetDisassembly();
            DisassembleBonus = 0;
            ReverseEngineerBonus = 0;
            ReverseEngineerIgnoresSkillRequirement = false;
            ScribesReverseEngineeredRecipes = false;
            ReverseEngineeredScribeChance = 0;
        }

        public virtual void ResetDisassembly()
        {
            Disassembly = null;
        }

        public static bool CheckHostiles(
            GameObject Vendor,
            UD_VendorActionEvent E,
            bool MultipleItems,
            bool Silent = false)
        {
            if (MultipleItems
                && (AutoAct.ShouldHostilesInterrupt("o")
                    || (Vendor.AreHostilesNearby() && Vendor.FireEvent("CombatPreventsTinkering"))))
            {
                string hostilesMessage = "=subject.Name= can't disassemble so many items at once with hostiles nearby."
                    .StartReplace()
                    .AddObject(Vendor)
                    .ToString();

                if (!Silent)
                {
                    Popup.ShowFail(hostilesMessage);
                }
                E.RequestCancelSecond();
                return false;
            }
            return true;
        }
        public bool CheckHostiles(UD_VendorActionEvent E, bool MultipleItems)
        {
            return CheckHostiles(ParentObject, E, MultipleItems);
        }

        public static bool CheckImportant(
            GameObject Item,
            int ItemCount,
            UD_VendorActionEvent E,
            bool MultipleItems)
        {
            if (Item.IsImportant() 
                && !Item.ConfirmUseImportant(
                    Actor: The.Player,
                    Verb: "disassemble",
                    Amount: (!MultipleItems) ? 1 : ItemCount))
            {
                E.RequestCancelSecond();
                return false;
            }
            return true;
        }

        public static bool CheckTinkerItemConfirm(
            GameObject Vendor,
            GameObject Item,
            UD_VendorActionEvent E,
            bool MultipleItems)
        {
            if (TinkerItem.ConfirmBeforeDisassembling(Item))
            {
                string pluralItems = "=item.t=";
                if (MultipleItems)
                {
                    pluralItems = "all the ";
                    pluralItems += Item.IsPlural ? Item.ShortDisplayName : Grammar.Pluralize(Item.ShortDisplayName);
                }
                string confirmDisassemble =
                    ("=subject.verb:Is= =subject.name= sure =subject.subjective= " +
                    "=subject.verb:want:afterpronoun= =object.name= to disassemble " + pluralItems + "?")
                        .StartReplace()
                        .AddObject(The.Player)
                        .AddObject(Vendor)
                        .AddObject(Item, "item")
                        .ToString();

                if (Popup.ShowYesNoCancel(confirmDisassemble) != DialogResult.Yes)
                {
                    E.RequestCancelSecond();
                    return false;
                }
            }
            return true;
        }
        public bool CheckTinkerItemConfirm(
            GameObject Item,
            UD_VendorActionEvent E,
            bool MultipleItems)
        {
            return CheckTinkerItemConfirm(
                Vendor: ParentObject,
                Item: Item,
                E: E,
                MultipleItems: MultipleItems);
        }

        public static bool CheckOwner(
            GameObject Vendor,
            GameObject Item,
            UD_VendorActionEvent E,
            bool MultipleItems,
            ref List<Action<GameObject>> BroadcastActions)
        {
            if (!Item.Owner.IsNullOrEmpty() && !Item.HasPropertyOrTag("DontWarnOnDisassemble"))
            {
                string notItemOwnerMsg = "=subject.T= =subject.verb:isn't= owned by =object.t=."
                    .StartReplace()
                    .AddObject(Item)
                    .AddObject(The.Player)
                    .ToString();

                string themIt = MultipleItems ? "them" : "=subject.objective="
                        .StartReplace()
                        .AddObject(Item)
                        .ToString();

                string confirmNotOwnedMsg =
                    ("=subject.verb:Is= =subject.name= sure =subject.subjective= " +
                    "=subject.verb:want:afterpronoun= =object.name= to disassemble " + themIt + "?")
                        .StartReplace()
                        .AddObject(The.Player)
                        .AddObject(Vendor)
                        .ToString();

                if (Popup.ShowYesNoCancel(notItemOwnerMsg + " " + confirmNotOwnedMsg) != DialogResult.Yes)
                {
                    E.RequestCancelSecond();
                    return false;
                }
                BroadcastActions ??= new();
                BroadcastActions.Add(Item.Physics.BroadcastForHelp);
            }
            return true;
        }
        public bool CheckOwner(
            GameObject Item,
            UD_VendorActionEvent E,
            bool MultipleItems,
            ref List<Action<GameObject>> BroadcastActions)
        {
            return CheckOwner(
                Vendor: ParentObject,
                Item: Item,
                E: E,
                MultipleItems: MultipleItems,
                BroadcastActions: ref BroadcastActions);
        }

        public static bool CheckContainerOwner(
            GameObject Vendor,
            GameObject Item,
            UD_VendorActionEvent E,
            bool MultipleItems,
            ref List<Action<GameObject>> BroadcastActions)
        {
            if (Item.InInventory is GameObject container
                && container != The.Player
                && !container.Owner.IsNullOrEmpty()
                && container.Owner != Item.Owner
                && !container.HasPropertyOrTag("DontWarnOnDisassemble"))
            {
                string notContainerOwnerMsg = "=subject.T= =subject.verb:isn't= owned by =object.name=."
                    .StartReplace()
                    .AddObject(Item)
                    .AddObject(The.Player)
                    .ToString();

                string itemsAnItem = MultipleItems ? "items" : "=subject.a==subject.t="
                    .StartReplace()
                    .AddObject(Item)
                    .ToString();

                string containerIt = "=subject.objective="
                    .StartReplace()
                    .AddObject(container)
                    .ToString();

                string confirmContainerMsg =
                    ("=subject.verb:Is= =subject.subjective= sure =subject.subjective= " +
                    "=subject.verb:want:afterpronoun= =object.name= to disassemble " + itemsAnItem +
                    " inside " + containerIt + "?")
                        .StartReplace()
                        .AddObject(The.Player)
                        .AddObject(Vendor)
                        .ToString();

                if (Popup.ShowYesNoCancel(notContainerOwnerMsg + " " + confirmContainerMsg) != DialogResult.Yes)
                {
                    E.RequestCancelSecond();
                    return false;
                }
                BroadcastActions ??= new();
                BroadcastActions.Add(container.Physics.BroadcastForHelp);
            }
            return true;
        }
        public bool CheckContainerOwner(
            GameObject Item,
            UD_VendorActionEvent E,
            bool MultipleItems,
            ref List<Action<GameObject>> BroadcastActions)
        {
            return CheckContainerOwner(
                Vendor: ParentObject,
                Item: Item,
                E: E,
                MultipleItems: MultipleItems,
                BroadcastActions: ref BroadcastActions);
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
                || ID == AfterGameLoadedEvent.ID
                || (WantVendorActions && ID == UD_GetVendorActionsEvent.ID)
                || (WantVendorActions && ID == UD_VendorActionEvent.ID)
                || (WantVendorActions && ID == GetVendorTinkeringBonusEvent.ID);
        }
        public override bool HandleEvent(AllowTradeWithNoInventoryEvent E)
        {
            if (E.Trader != null && ParentObject == E.Trader && WantVendorActions)
            {
                return true;
            }
            return base.HandleEvent(E);
        }
        public override bool HandleEvent(AfterGameLoadedEvent E)
        {
            if (MigrateFrom != null
                && MigrateFrom < new Version("0.1.0")
                && ParentObject is GameObject vendor
                && vendor.GetBlueprint() is GameObjectBlueprint vendorBlueprint)
            {
                UD_Modding_Toolbox.Utils.MigratePartFieldFromBlueprint(
                    Part: this, 
                    Field: ref DisassembleBonus,
                    FieldName: nameof(DisassembleBonus),
                    Blueprint: vendorBlueprint);

                UD_Modding_Toolbox.Utils.MigratePartFieldFromBlueprint(
                    Part: this, 
                    Field: ref ReverseEngineerBonus,
                    FieldName: nameof(ReverseEngineerBonus),
                    Blueprint: vendorBlueprint);

                UD_Modding_Toolbox.Utils.MigratePartFieldFromBlueprint(
                    Part: this, 
                    Field: ref ReverseEngineerIgnoresSkillRequirement,
                    FieldName: nameof(ReverseEngineerIgnoresSkillRequirement),
                    Blueprint: vendorBlueprint);

                UD_Modding_Toolbox.Utils.MigratePartFieldFromBlueprint(
                    Part: this, 
                    Field: ref ScribesReverseEngineeredRecipes,
                    FieldName: nameof(ScribesReverseEngineeredRecipes),
                    Blueprint: vendorBlueprint);

                UD_Modding_Toolbox.Utils.MigratePartFieldFromBlueprint(
                    Part: this, 
                    Field: ref ReverseEngineeredScribeChance,
                    FieldName: nameof(ReverseEngineeredScribeChance),
                    Blueprint: vendorBlueprint);
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
                && E.Vendor is GameObject vendor
                && E.Item is GameObject item
                && The.Player is GameObject player
                && item.TryGetPart(out TinkerItem tinkerItem))
            {
                int itemCount = item.Count;
                int amountToDisassemble = E.Command == COMMAND_DISASSEMBLE_ALL ? itemCount : 1;
                bool multipleItems = amountToDisassemble > 1;

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

                if (vendor.IsPlayerLed())
                {
                    costPerItem = 0;
                    RealCostPerItem = 0;
                    totalCost = 0;
                }

                if (E.Staggered && E.Second
                    && VendorDoDisassembly(vendor, item, tinkerItem, RealCostPerItem))
                {
                    return true;
                }
                if (!E.Second)
                {
                    int costToDisassemble = totalCost;

                    string thisThese = multipleItems ? ("these " + amountToDisassemble.Things("item")) : "this item";
                    string dramsCost = costToDisassemble.Things("dram").Color("C");

                    bool tooExpensive = !player.CanAfford(costToDisassemble);
                    if (tooExpensive || (!multipleItems && itemCount > 1))
                    {
                        string tooExpensiveMsg =
                            ("=subject.Name= =subject.verb:don't= have the required " + dramsCost + " to have " +
                            "=object.name= disassemble " + "item".ThisTheseN(amountToDisassemble, multipleItems) + "\n\n" + ".")
                                .StartReplace()
                                .AddObject(player)
                                .AddObject(vendor)
                                .ToString();

                        if (player.CanAfford(RealCostPerItem))
                        {
                            int maxAfford = (int)Math.Floor(player.GetFreeDrams() / (double)RealCostPerItem);
                            int maxHave = item.Count;
                            int maxAsk = Math.Min(maxAfford, maxHave);
                            string realCostPerItemString = RealCostPerItem.Things("dram").Color("C");
                            string canAfford = "can afford";
                            string doesHave = "=subject.verb:have=";
                            bool haveCountSmaller = maxAsk == maxHave;
                            string whyMax = haveCountSmaller ? doesHave : canAfford;
                            string itemRefName = item.GetReferenceDisplayName(Short: true);
                            string disassembleSomeMsg =
                                ("How many of the " + maxAsk.Things(itemRefName) + " that =subject.name= " + whyMax + " " +
                                "would =subject.subjective= like =object.name= to disassemble?\n\n" +
                                "It costs " + realCostPerItemString + " of fresh water each to disassemble this item.")
                                    .StartReplace()
                                    .AddObject(player)
                                    .AddObject(vendor)
                                    .ToString();

                            string numberToDisassembleMessage = tooExpensiveMsg;
                            if (!tooExpensive)
                            {
                                numberToDisassembleMessage = disassembleSomeMsg;
                            }
                            else
                            {
                                numberToDisassembleMessage += disassembleSomeMsg;
                            }
                            amountToDisassemble = Popup.AskNumber(
                                Message: numberToDisassembleMessage,
                                Start: maxAsk,
                                Max: maxAsk)
                                .GetValueOrDefault();
                        }
                        else
                        {
                            Popup.ShowFail(tooExpensiveMsg);
                        }
                    }

                    if (amountToDisassemble > 0)
                    {
                        multipleItems = amountToDisassemble > 1;
                        costToDisassemble = amountToDisassemble * RealCostPerItem;
                        dramsCost = costToDisassemble.Things("dram").Color("C");
                        thisThese = multipleItems ? ("these " + amountToDisassemble.Things("item")) : "this item";
                        tooExpensive = !player.CanAfford(costToDisassemble);
                    }
                    if (!tooExpensive 
                        && amountToDisassemble > 0
                        && UD_VendorTinkering.ConfirmTinkerService(
                            Vendor: vendor,
                            Shopper: player,
                            DramsCost: costToDisassemble,
                            DoWhat: "disassemble " + thisThese,
                            ExtraAfter: "Note: the trade window will be closed, ending the conversation that opened it."))
                    {
                        List<Action<GameObject>> broadcastActions = null;
                        if (!CheckHostiles(E, multipleItems)
                            || !CheckImportant(item, itemCount, E, multipleItems)
                            || !CheckTinkerItemConfirm(item, E, multipleItems)
                            || !CheckOwner(item, E, multipleItems, ref broadcastActions)
                            || !CheckContainerOwner(item, E, multipleItems, ref broadcastActions))
                        {
                            return false;
                        }
                        if (!broadcastActions.IsNullOrEmpty())
                        {
                            foreach (Action<GameObject> broadcastAction in broadcastActions)
                            {
                                broadcastAction(player);
                            }
                        }
                        Disassembly = new(E.Item, amountToDisassemble, EnergyCostPer: 0);
                        return true;
                    }
                    E.RequestCancelSecond();
                }
                return false;
            }
            return base.HandleEvent(E);
        }
        public virtual bool HandleEvent(GetVendorTinkeringBonusEvent E)
        {
            if (E.Vendor != null
                && E.Vendor == ParentObject
                && E.Type == "ReverseEngineer")
            {
                E.Bonus += ReverseEngineerBonus;
            }
            return base.HandleEvent(E);
        }

        public override void Read(GameObject Basis, SerializationReader Reader)
        {
            var modVersion = Reader.ModVersions[Utils.ThisMod.ID];
            if (modVersion < new Version("0.1.0"))
            {
                MigrateFrom = modVersion;
            }
            base.Read(Basis, Reader);
        }
    }
}
