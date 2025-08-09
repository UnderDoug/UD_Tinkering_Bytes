using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UD_Tinkering_Bytes;
using XRL.Language;
using XRL.Rules;
using XRL.UI;
using XRL.World.Capabilities;
using XRL.World.Parts.Skill;
using static XRL.World.Parts.Skill.Tinkering;

namespace XRL.World.Parts
{
    [Serializable]
    public class UD_VendorActionHandler : IScribedPart, IVendorActionEventHandler
    {
        public const string COMMAND_LOOK = "VendorCommand_Look";
        public const string COMMAND_ADD_TO_TRADE = "VendorCommand_AddToTrade";
        public const string COMMAND_IDENTIFY = "VendorCommand_Identify";
        public const string COMMAND_REPAIR = "VendorCommand_Repair";
        public const string COMMAND_RECHARGE = "VendorCommand_Recharge";
        public const string COMMAND_READ = "VendorCommand_Read";

        public UD_VendorActionHandler()
        {
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
                int vendorIdentifyLevel = GetIdentifyLevel(E.Vendor);
                Tinkering_Repair vendorRepairSkill = E.Vendor.GetPart<Tinkering_Repair>();
                E.AddAction("Look", "look", COMMAND_LOOK, Key: 'l');
                if (E.IncludeModernTradeOptions)
                {
                    E.AddAction("Add to trade", "add to trade", COMMAND_ADD_TO_TRADE, Key: 't', WantsAsync: true);
                }
                if (vendorIdentifyLevel > 0 
                    && !E.Item.Understood())
                {
                    E.AddAction("Identify", "identify", COMMAND_IDENTIFY, Key: 'i', ClearAndSetUpTradeUI: true);
                }
                if (vendorRepairSkill != null 
                    && IsRepairableEvent.Check(E.Vendor, E.Item, null, vendorRepairSkill))
                {
                    E.AddAction("Repair", "repair", COMMAND_REPAIR, Key: 'r');
                }
                if (E.Vendor.HasSkill(nameof(Tinkering_Tinker1)) 
                    && (E.Item.Understood() || vendorIdentifyLevel >= E.Item.GetComplexity()) && E.Item.NeedsRecharge())
                {
                    E.AddAction("Recharge", "recharge", COMMAND_RECHARGE, Key: 'c', ClearAndSetUpTradeUI: true);
                }
                if (E.Vendor.GetIntProperty("Librarian") != 0 
                    && E.Item.HasInventoryActionWithCommand("Read") 
                    && E.TradeLine.context.data.traderInventory)
                {
                    E.AddAction("Read", "read", COMMAND_RECHARGE, Key: 'b');
                }
            }
            return base.HandleEvent(E);
        }
        public virtual bool HandleEvent(VendorActionEvent E)
        {
            if (E.Vendor != null && ParentObject == E.Vendor)
            {
                if (E.Command == COMMAND_LOOK)
                {
                    TradeUI.DoVendorLook(E.Item, E.Vendor);
                }
                if (E.Command == COMMAND_IDENTIFY)
                {
                    TradeUI.DoVendorExamine(E.Item, E.Vendor);
                }
                if (E.Command == COMMAND_REPAIR)
                {
                    TradeUI.DoVendorRepair(E.Item, E.Vendor);
                }
                if (E.Command == COMMAND_RECHARGE)
                {
                    TradeUI.DoVendorRecharge(E.Item, E.Vendor);
                }
                if (E.Command == COMMAND_READ)
                {
                    TradeUI.DoVendorRead(E.Item, E.Vendor);
                }
                if (E.Command == COMMAND_ADD_TO_TRADE)
                {
                    E.TradeLine.HandleTradeSome();
                }
            }
            return base.HandleEvent(E);
        }
    }
}
