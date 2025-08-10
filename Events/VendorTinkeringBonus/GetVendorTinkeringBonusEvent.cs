using System;
using System.Collections.Generic;
using System.Text;
using XRL.Messages;
using XRL.World;

namespace UD_Tinkering_Bytes
{
    [GameEvent(Cascade = CASCADE_NONE, Cache = Cache.Pool)]
    public class GetVendorTinkeringBonusEvent : ModPooledEvent<GetVendorTinkeringBonusEvent>
    {
        public new static readonly int CascadeLevel = CASCADE_NONE;

        public static string RegisteredEventID => nameof(GetVendorTinkeringBonusEvent);

        public GameObject Vendor;

        public GameObject Item;

        public string Type;

        public int BaseRating;

        public int Bonus;

        public int SecondaryBonus;

        public bool Interruptable;

        public GetVendorTinkeringBonusEvent()
        {
        }

        public override void Reset()
        {
            base.Reset();
            Vendor = null;
            Item = null;
            Type = null;
            BaseRating = 0;
            Bonus = 0;
            SecondaryBonus = 0;
            Interruptable = false;
        }

        public static int GetFor(GameObject Vendor, GameObject Item, string Type, int BaseRating, int Bonus, ref int SecondaryBonus, ref bool Interrupt, bool Interruptable = true)
        {
            if (GameObject.Validate(ref Item) && Item.WantEvent(ID, CascadeLevel))
            {
                UnityEngine.Debug.LogError($"{Item.T()}{Item.GetVerb("want")} {nameof(GetVendorTinkeringBonusEvent)}!");
                GetVendorTinkeringBonusEvent E = FromPool();
                E.Vendor = Vendor;
                E.Item = Item;
                E.Type = Type;
                E.BaseRating = BaseRating;
                E.Bonus = Bonus;
                E.SecondaryBonus = SecondaryBonus;
                E.Interruptable = Interruptable;
                if (!Item.HandleEvent(E))
                {
                    Interrupt = true;
                }
                Bonus = E.Bonus;
                SecondaryBonus = E.SecondaryBonus;
            }
            UnityEngine.Debug.LogError($"{Item?.T()} doesn't{Item?.GetVerb("want")} {nameof(GetVendorTinkeringBonusEvent)}!");
            return Bonus;
        }

        public static int GetFor(GameObject Vendor, GameObject Item, string Type, int BaseRating, int Bonus, ref bool Interrupt, bool Interruptable = true)
        {
            int SecondaryBonus = 0;
            return GetFor(Vendor, Item, Type, BaseRating, Bonus, ref SecondaryBonus, ref Interrupt, Interruptable);
        }

        public static int GetFor(GameObject Vendor, GameObject Item, string Type, int BaseRating, int Bonus, bool Interruptable = true)
        {
            int SecondaryBonus = 0;
            bool Interrupt = false;
            return GetFor(Vendor, Item, Type, BaseRating, Bonus, ref SecondaryBonus, ref Interrupt, Interruptable);
        }
    }

}
