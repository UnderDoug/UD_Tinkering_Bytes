using Qud.UI;
using System;
using System.Collections.Generic;
using System.Text;

using XRL;
using XRL.World;

namespace UD_Tinkering_Bytes
{
    [GameEvent(Cascade = CASCADE_NONE, Cache = Cache.Pool)]
    public class VendorActionEvent : IVendorActionEvent<VendorActionEvent>
    {
        public VendorActionEvent()
        {
        }

        public override void Reset()
        {
            base.Reset();
        }

        public static bool Check(TradeLine TradeLine, GameObject Handler, GameObject Vendor, GameObject Item, GameObject Owner, string Command, int? DramsCost = null)
        {
            VendorActionEvent E = FromPool(TradeLine, Vendor, Item, Command, DramsCost);

            if (E != null && GameObject.Validate(ref Handler) && Handler.WantEvent(ID, CascadeLevel))
            {
                if (!Handler.HandleEvent(E))
                {
                    return false;
                }
                AfterVendorActionEvent.SendAfter(Handler, E);
                OwnerAfterVendorActionEvent.SendAfter(Owner, E);
            }
            else
            {
                E = null;
            }
            return true;
        }

        public static implicit operator VendorActionEvent(AfterVendorActionEvent E)
        {
            return FromPool(E.TradeLine, E.Vendor, E.Item, E.Command, E.DramsCost);
        }
        public static implicit operator AfterVendorActionEvent(VendorActionEvent E)
        {
            return AfterVendorActionEvent.FromPool(E.TradeLine, E.Vendor, E.Item, E.Command, E.DramsCost);
        }
        public static implicit operator VendorActionEvent(OwnerAfterVendorActionEvent E)
        {
            return FromPool(E.TradeLine, E.Vendor, E.Item, E.Command, E.DramsCost);
        }
        public static implicit operator OwnerAfterVendorActionEvent(VendorActionEvent E)
        {
            return OwnerAfterVendorActionEvent.FromPool(E.TradeLine, E.Vendor, E.Item, E.Command, E.DramsCost);
        }
    }
}
