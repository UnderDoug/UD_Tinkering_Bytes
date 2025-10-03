using System;
using System.Collections.Generic;
using System.Text;
using XRL.Language;
using XRL.UI;
using XRL.World.Capabilities;
using XRL.World.Effects;
using XRL.World.Parts.Mutation;
using XRL.World.Tinkering;

using static XRL.World.Parts.Skill.Tinkering;


using UD_Vendor_Actions;
using UD_Modding_Toolbox;

using static UD_Modding_Toolbox.Const;

using UD_Tinkering_Bytes;
using static UD_Tinkering_Bytes.Options;
using Qud.UI;

namespace XRL.World.Parts
{
    [Serializable]
    public class UD_HeldForPlayer
        : IScribedPart
        , I_UD_VendorActionEventHandler
    {
        private static bool doDebug => false;

        public const int GUARANTEED_RESTOCKS = 2;

        public GameObject HeldFor;

        public double DepositPaid;

        public int RestocksLeft;

        public int ExtraHoldChance;

        public UD_HeldForPlayer()
        {
            HeldFor = null;
            DepositPaid = 0;
            RestocksLeft = GUARANTEED_RESTOCKS;
            ExtraHoldChance = 25;
        }
        public UD_HeldForPlayer(GameObject HeldFor, double DepositPaid, int RestocksLeft, int ExtraHoldChance = 25)
            : this()
        {
            this.HeldFor = HeldFor;
            this.DepositPaid = DepositPaid;
            this.RestocksLeft = RestocksLeft;
            this.ExtraHoldChance = ExtraHoldChance;
        }

        public override bool AllowStaticRegistration()
        {
            return true;
        }

        public bool CheckStillHeld(GameObject Vendor, bool RemoveOnFalse = false)
        {
            if (ParentObject?.InInventory != null && ParentObject?.InInventory != Vendor || RestocksLeft < 1)
            {
                if (RemoveOnFalse)
                {
                    ParentObject?.RemovePart(this);
                }
                return false;
            }
            return true;
        }

        public int Restocked(GameObject Vendor)
        {
            --RestocksLeft;
            CheckStillHeld(Vendor);
            return RestocksLeft;
        }

        public override bool WantEvent(int ID, int Cascade)
        {
            return base.WantEvent(ID, Cascade)
                || ID == GetDisplayNameEvent.ID
                || ID == GetShortDescriptionEvent.ID
                || ID == StartTradeEvent.ID;
        }
        public override bool HandleEvent(GetDisplayNameEvent E)
        {
            if (ParentObject?.InInventory is GameObject vendor
                && CheckStillHeld(vendor))
            {
                if (E.Context == nameof(TradeLine) || E.Context == nameof(UD_VendorAction.ShowVendorActionMenu))
                {
                    bool secondPersonAllowed = Grammar.AllowSecondPerson;
                    Grammar.AllowSecondPerson = false;
                    string heldForTag = "[{{C|held for =subject.name=}}]"
                        .StartReplace()
                        .AddObject(HeldFor)
                        .ToString();
                    Grammar.AllowSecondPerson = secondPersonAllowed;

                    E.AddTag(heldForTag);
                }
            }
            return base.HandleEvent(E);
        }
        public override bool HandleEvent(GetShortDescriptionEvent E)
        {
            if (ParentObject?.InInventory is GameObject vendor
                && CheckStillHeld(vendor))
            {
                double depositPaid = Math.Round(DepositPaid, 2);
                string depositPaidString = depositPaid.Things("dram").Color("C") + " of fresh water";
                string heldForDescription = 
                    ("Deposit Paid: =subject.T= =verb:are:afterpronoun= holding this " + ParentObject.GetDescriptiveCategory() + 
                    " for =object.t=, who had it tinkered for a deposit of " + depositPaidString + ". " +
                    "=subject.Subjective= will hold it for " + RestocksLeft.Things("more restock") + ".")
                    .StartReplace()
                    .AddObject(vendor)
                    .AddObject(HeldFor)
                    .ToString();

                E.Postfix.AppendRules(heldForDescription);
            }
            return base.HandleEvent(E);
        }
        public override bool HandleEvent(StartTradeEvent E)
        {
            if (ParentObject?.InInventory is GameObject vendor
                && CheckStillHeld(vendor)
                && E.Actor is GameObject shopper)
            {
                if (shopper != HeldFor && !UD_VendorAction.ItemIsTradeUIDisplayOnly(ParentObject))
                {
                    ParentObject.SetIntProperty("TradeUI_DisplayOnly", 1);
                }
                else
                if (shopper == HeldFor && UD_VendorAction.ItemIsTradeUIDisplayOnly(ParentObject))
                {
                    ParentObject.SetIntProperty("TradeUI_DisplayOnly", 0, true);
                }
            }
            return base.HandleEvent(E);
        }
    }
}
