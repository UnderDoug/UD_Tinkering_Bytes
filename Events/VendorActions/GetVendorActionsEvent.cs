using Qud.UI;
using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.World;

namespace UD_Tinkering_Bytes
{
    [GameEvent(Cascade = CASCADE_NONE, Cache = Cache.Pool)]
    public class GetVendorActionsEvent : IVendorActionEvent<GetVendorActionsEvent>
    {
        public Dictionary<string, VendorAction> Actions;

        public bool IncludeModernTradeOptions;

        public GetVendorActionsEvent()
        {
        }

        public override void Reset()
        {
            base.Reset();
            Actions = null;
        }

        public static GetVendorActionsEvent FromPool(TradeLine TradeLine, GameObject Vendor, GameObject Item, Dictionary<string, VendorAction> Actions = null, bool IncludeModernTradeOptions = false)
        {
            GetVendorActionsEvent E = FromPool(TradeLine, Vendor, Item, null, null);
            if (E != null)
            {
                E.Actions = Actions ?? new();
                E.IncludeModernTradeOptions = IncludeModernTradeOptions;
            }
            return E;
        }

        public static void Send(TradeLine TradeLine, GameObject Vendor, GameObject Item, Dictionary<string, VendorAction> Actions, bool IncludeModernTradeOptions = false)
        {
            GetVendorActionsEvent E = FromPool(TradeLine, Vendor, Item, Actions, IncludeModernTradeOptions);

            bool validVendor = GameObject.Validate(ref Vendor);
            bool validItem = GameObject.Validate(ref Item);

            bool vendorWants = validVendor && Vendor.WantEvent(ID, CascadeLevel);
            bool itemWants = validItem && Item.WantEvent(ID, CascadeLevel);
            bool playerWants = The.Player.WantEvent(ID, CascadeLevel);

            bool anyWants = vendorWants || itemWants || playerWants;

            if (E != null && validVendor && validItem)
            {
                bool blocked = false;

                if (anyWants)
                {
                    if (!blocked && vendorWants)
                    {
                        blocked = !Vendor.HandleEvent(E);
                    }
                    if (!blocked && itemWants)
                    {
                        blocked = !Item.HandleEvent(E);
                    }
                    if (!blocked && playerWants)
                    {
                        blocked = !The.Player.HandleEvent(E);
                    }
                }
            }
        }
        public bool AddAction(string Name, string Display = null, string Command = null, char Key = ' ', int Default = 0, int Priority = 0, bool FireOnVendor = true, bool FireOnItem = false, bool FireOnPlayer = false, bool Override = false, GameObject FireOn = null, bool WantsAsync = false, bool ClearAndSetUpTradeUI = false)
        {
            Actions ??= new();

            if (!Override && Actions.ContainsKey(Name))
            {
                return false;
            }
            VendorAction vendorAction = new()
            {
                Name = Name,
                Key = Key,
                Display = Display,
                Command = Command,
                Default = Default,
                Priority = Priority,
                FireOnVendor = FireOnVendor,
                FireOnItem = FireOnItem,
                FireOnPlayer = FireOnPlayer,
                FireOn = FireOn,
                WantsAsync = WantsAsync,
                ClearAndSetUpTradeUI = ClearAndSetUpTradeUI,
            };
            Actions[Name] = vendorAction;
            return true;
        }
    }
}
