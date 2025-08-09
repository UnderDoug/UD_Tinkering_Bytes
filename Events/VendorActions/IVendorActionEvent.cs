using Qud.UI;
using System;
using System.Collections.Generic;
using System.Text;

using XRL;
using XRL.World;

namespace UD_Tinkering_Bytes
{
    [GameEvent(Base = true, Cascade = CASCADE_NONE, Cache = Cache.Pool)]
    public abstract class IVendorActionEvent<T> : ModPooledEvent<T>
        where T : IVendorActionEvent<T>, new()
    {
        public new static readonly int CascadeLevel = CASCADE_NONE;

        public static string RegisteredEventID => typeof(T).Name;

        public GameObject Vendor;

        public GameObject Item;

        public string Command;

        public int? DramsCost;

        public TradeLine TradeLine;

        public IVendorActionEvent()
        {
        }

        public virtual string GetRegisteredEventID()
        {
            return RegisteredEventID;
        }

        public override void Reset()
        {
            base.Reset();
            Vendor = null;
            Item = null;
            Command = null;
            DramsCost = null;
            TradeLine = null;
        }

        public static T FromPool(TradeLine TradeLine, GameObject Vendor, GameObject Item, string Command = null, int? DramsCost = null)
        {
            if (GameObject.Validate(ref Vendor))
            {
                T E = FromPool();
                E.TradeLine = TradeLine;
                E.Vendor = Vendor;
                E.Item = Item;
                E.Command = Command;
                E.DramsCost = DramsCost;
                return E;
            }
            return null;
        }

        public static T FromPool(T Source)
        {
            if (GameObject.Validate(ref Source.Vendor))
            {
                T E = FromPool();
                E.TradeLine = Source.TradeLine;
                E.Vendor = Source.Vendor;
                E.Item = Source.Item;
                E.Command = Source.Command;
                E.DramsCost = Source.DramsCost;
                return E;
            }
            return null;
        }

        public static void SendAfter(GameObject Object, T Source)
        {
            if (GameObject.Validate(ref Object) && Object.WantEvent(ID, CascadeLevel))
            {
                T E = FromPool(Source);
                if (E != null)
                {
                    Object.HandleEvent(E);
                    Source.ProcessChildEvent(E);
                    Source.TradeLine = E.TradeLine;
                    Source.Vendor = E.Vendor;
                    Source.Item = E.Item;
                    Source.Command = E.Command;
                    Source.DramsCost = E.DramsCost;
                }
            }
        }
    }
}
