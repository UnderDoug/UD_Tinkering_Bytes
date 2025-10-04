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
        private static bool doDebug => true;

        public const string HELD_FOR_PLAYER = "HeldForPlayer";

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

        public override void Attach()
        {
            base.Attach();
            ParentObject.SetIntProperty("norestock", 1);
            ParentObject.SetIntProperty("_stock", 0, true);
        }

        public override void Remove()
        {
            ParentObject.SetIntProperty("norestock", 0, true);
            if (ParentObject?.InInventory != HeldFor)
            {
                ParentObject.SetIntProperty("_stock", 1);
                ParentObject.SetIntProperty("TradeUI_DisplayOnly", 0, true);
            }
            base.Remove();
        }

        public override bool SameAs(IPart p)
        {
            if (p is UD_HeldForPlayer hfp)
            {
                return hfp.ExtraHoldChance == ExtraHoldChance
                    && hfp.RestocksLeft == RestocksLeft
                    && hfp.HeldFor == HeldFor
                    && base.SameAs(p);
            }
            return base.SameAs(p);
        }

        public bool IsHoldFulfilled()
        {
            return ParentObject?.InInventory == HeldFor;
        }

        public bool CheckStillHeld(GameObject Vendor, bool RemoveOnFalse = false)
        {
            int indent = Debug.LastIndent;
            string methodDebug = nameof(UD_HeldForPlayer) + "." + nameof(CheckStillHeld);
            if (ParentObject?.InInventory is not GameObject holder 
                || holder != Vendor
                || IsHoldFulfilled()
                || RestocksLeft < 1)
            {
                if (RemoveOnFalse || IsHoldFulfilled())
                {
                    ParentObject?.RemovePart(this);
                }
                Debug.CheckNah(4, methodDebug, Indent: indent + 1, Toggle: doDebug);
                Debug.LastIndent = indent;
                return false;
            }
            Debug.CheckYeh(4, methodDebug, Indent: indent + 1, Toggle: doDebug);
            Debug.LastIndent = indent;
            return true;
        }

        public int Restocked(GameObject Vendor)
        {
            int indent = Debug.LastIndent;
            string methodDebug = nameof(UD_HeldForPlayer) + "." + nameof(Restocked);

            --RestocksLeft;

            Debug.LoopItem(4, methodDebug, RestocksLeft.ToString(), Indent: indent + 1, Toggle: doDebug);

            CheckStillHeld(Vendor, RemoveOnFalse: true);

            Debug.LastIndent = indent;
            return RestocksLeft;
        }

        public override bool WantEvent(int ID, int Cascade)
        {
            return base.WantEvent(ID, Cascade)
                || ID == GetDisplayNameEvent.ID
                || ID == GetShortDescriptionEvent.ID
                || ID == EnteredCellEvent.ID
                || ID == StartTradeEvent.ID;
        }
        public override bool HandleEvent(GetDisplayNameEvent E)
        {
            if (ParentObject?.InInventory is GameObject vendor
                && CheckStillHeld(vendor))
            {
                if (E.Context == nameof(TradeLine) || E.Context == nameof(UD_VendorAction.ShowVendorActionMenu))
                {
                    string heldForTag = "[{{C|held for =subject.refname=}}]"
                        .StartReplace()
                        .AddObject(HeldFor)
                        .ToString();

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
                    " for =object.refname=, who had it tinkered for a deposit of " + depositPaidString + ". " +
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
            int indent = Debug.LastIndent;
            string methodDebug = nameof(UD_HeldForPlayer) + "." + nameof(HandleEvent) + "(" + nameof(StartTradeEvent) + ")";

            Debug.Entry(4, 
                methodDebug + " " +
                nameof(E.Trader) + ": " + E.Trader?.DebugName + ", " +
                nameof(E.Actor) + ": " + E.Actor?.DebugName + ", " +
                nameof(HeldFor) + ": " + HeldFor?.DebugName,
                Indent: indent + 1, Toggle: doDebug);

            if (CheckStillHeld(E.Trader, RemoveOnFalse: true)
                && E.Actor != null)
            {
                if (E.Actor != HeldFor)
                {
                    ParentObject.SetIntProperty("TradeUI_DisplayOnly", 1);
                    Debug.CheckNah(4, "Not " + HeldFor.them, Indent: indent + 1, Toggle: doDebug);
                }
                else
                {
                    ParentObject.SetIntProperty("TradeUI_DisplayOnly", 0, true);
                    Debug.CheckYeh(4, "It's " + HeldFor.them, Indent: indent + 1, Toggle: doDebug);
                }
            }
            Debug.LastIndent = indent;
            return base.HandleEvent(E);
        }
        public override bool HandleEvent(EnteredCellEvent E)
        {
            if (IsHoldFulfilled())
            {
                ParentObject.RemovePart(this);
            }
            return base.HandleEvent(E);
        }
    }
}
