using System;
using System.Collections.Generic;
using System.Text;

using Qud.UI;

using XRL.UI;
using XRL.Rules;
using XRL.Language;
using XRL.World.Capabilities;
using XRL.World.Effects;
using XRL.World.Parts.Mutation;
using XRL.World.Tinkering;

using static XRL.World.Parts.Skill.Tinkering;

using UD_Vendor_Actions;
using UD_Modding_Toolbox;

using static UD_Modding_Toolbox.Const;
using static UD_Modding_Toolbox.Utils;

using UD_Tinkering_Bytes;
using static UD_Tinkering_Bytes.Options;

using SerializeField = UnityEngine.SerializeField;

namespace XRL.World.Parts
{
    [Serializable]
    public class UD_HeldForPlayer
        : IScribedPart
        , I_UD_VendorActionEventHandler
    {
        private static bool doDebug => false;

        public const string HELD_FOR_PLAYER = "HeldForPlayer";

        public const int GUARANTEED_RESTOCKS = 2;

        private string HeldForID;

        private GameObject _HeldFor;

        public GameObject HeldFor
        {
            get => _HeldFor ??= GameObject.FindByID(HeldForID);
            set
            {
                HeldForID = value?.ID;
                _HeldFor = value;
            }
        }

        public string HeldForName => HeldFor != null ? "=object.refname=" : "somebody";

        public string VendorID;

        public double DepositPaid;

        public int RestocksLeft;

        public int ExtraHoldChance;

        private long TurnHoldStarted;

        public bool WeeksInstead;

        public string ServiceVerbed;

        public UD_HeldForPlayer()
        {
            HeldFor = null;
            VendorID = null;
            DepositPaid = 0;
            RestocksLeft = GUARANTEED_RESTOCKS;
            ExtraHoldChance = 25;
            TurnHoldStarted = The.CurrentTurn;
            WeeksInstead = false;
            ServiceVerbed = "tinkered";
        }

        public UD_HeldForPlayer(
            GameObject Vendor,
            GameObject HeldFor,
            double DepositPaid)
            : this()
        {
            VendorID = Vendor.ID;
            this.HeldFor = HeldFor;
            this.DepositPaid = DepositPaid;
        }
        public UD_HeldForPlayer(
            GameObject Vendor,
            GameObject HeldFor,
            double DepositPaid,
            bool WeeksInstead = false)
            : this(Vendor, HeldFor, DepositPaid)
        {
            this.WeeksInstead = WeeksInstead;
        }
        public UD_HeldForPlayer(
            GameObject Vendor,
            GameObject HeldFor,
            string ServiceVerbed,
            double DepositPaid,
            bool WeeksInstead = false)
            : this(Vendor, HeldFor, DepositPaid)
        {
            this.ServiceVerbed = ServiceVerbed;
            this.WeeksInstead = WeeksInstead;
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

        public bool CheckStillHeld(bool RemoveOnFalse = false)
        {
            int indent = Debug.LastIndent;
            string methodDebug = nameof(UD_HeldForPlayer) + "." + nameof(CheckStillHeld);

            if ((HeldFor == null && RemoveOnFalse)
                || ParentObject?.InInventory is not GameObject holder
                || holder.ID != VendorID
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

        public int Restocked()
        {
            if (RestocksLeft > 1 || Stat.RollCached("1d3") > 1)
            {
                Debug.GetIndent(out int indent);
                string methodDebug = nameof(UD_HeldForPlayer) + "." + nameof(Restocked);
                --RestocksLeft;

                Debug.LoopItem(4, methodDebug, RestocksLeft.ToString(), Indent: indent + 1, Toggle: doDebug);

                CheckStillHeld(RemoveOnFalse: true);

                Debug.SetIndent(indent);
            }
            return RestocksLeft;
        }

        public bool CheckWeeks()
        {
            int restocksLeftBefore = RestocksLeft;
            if (WeeksInstead)
            {
                int wholeWeeks = (int)Math.Floor(TurnHoldStarted.GameDaysSince() / 7);
                for (int i = 0; i < wholeWeeks; i++)
                {
                    if (Restocked() < 1)
                    {
                        break;
                    }
                }
            }
            return restocksLeftBefore > RestocksLeft;
        }

        public override bool WantTurnTick()
        {
            return base.WantTurnTick()
                || WeeksInstead;
        }
        public override void TurnTick(long TimeTick, int Amount)
        {
            CheckWeeks();
            base.TurnTick(TimeTick, Amount);
        }
        public override bool WantEvent(int ID, int Cascade)
        {
            return base.WantEvent(ID, Cascade)
                || ID == AfterGameLoadedEvent.ID
                || ID == GetDisplayNameEvent.ID
                || ID == GetShortDescriptionEvent.ID
                || ID == StockedEvent.ID
                || ID == EnteredCellEvent.ID
                || ID == StartTradeEvent.ID;
        }
        public override bool HandleEvent(AfterGameLoadedEvent E)
        {
            if (ServiceVerbed.IsNullOrEmpty())
            {
                ServiceVerbed = "commissioned";
            }
            return base.HandleEvent(E);
        }
        public override bool HandleEvent(GetDisplayNameEvent E)
        {
            if (ParentObject?.InInventory is GameObject vendor
                && CheckStillHeld())
            {
                if (E.Context == nameof(TradeLine) || E.Context == nameof(UD_VendorAction.ShowVendorActionMenu))
                {
                    string heldForTag = ("[{{C|held for " + HeldForName + "}}]")
                        .StartReplace()
                        .AddObject(HeldFor, "object")
                        .ToString();

                    E.AddTag(heldForTag, DescriptionBuilder.ORDER_ADJUST_VERY_LATE + DescriptionBuilder.ORDER_ADJUST_VERY_LATE);
                }
            }
            return base.HandleEvent(E);
        }
        public override bool HandleEvent(GetShortDescriptionEvent E)
        {
            if (ParentObject?.InInventory is GameObject vendor
                && CheckStillHeld())
            {
                string timeMeasurement = WeeksInstead ? "week" : "restock";
                double depositPaid = Math.Round(DepositPaid, 2);
                string depositPaidString = TinkerInvoice.DramsCostString(depositPaid) + " of fresh water";
                string heldForDescription =
                    "Deposit Paid: =subject.Refname= =subject.verb:are= holding this " + 
                    ParentObject.GetDescriptiveCategory() + " for " + HeldForName + ", " +
                    "who had it " + ServiceVerbed + " " + TurnHoldStarted.TimeAgo() + 
                    " for a deposit of " + depositPaidString + ".";

                string holdLongerDescription = 
                    "=subject.Subjective= will hold it for at least " + 
                    RestocksLeft.Things("more " + timeMeasurement) + ".";

                string holdOnlyDescription =
                    "=subject.Subjective= will only sell it to " + HeldForName + " until at least " +
                    RestocksLeft.Things("more " + timeMeasurement) + " has passed.";

                string holdDescription = HeldFor == The.Player ? holdLongerDescription : holdOnlyDescription;

                string descriptionString = (heldForDescription + " " + holdDescription)
                    .StartReplace()
                    .AddObject(vendor)
                    .AddObject(HeldFor)
                    .ToString();

                E.Postfix.AppendRules(descriptionString);
            }
            return base.HandleEvent(E);
        }
        public override bool HandleEvent(StockedEvent E)
        {
            if (!WeeksInstead)
            {
                if (RestocksLeft > 1 || Stat.RollCached("1d3") > 1)
                {
                    Restocked();
                }
            }
            return base.HandleEvent(E);
        }
        public override bool HandleEvent(StartTradeEvent E)
        {
            int indent = Debug.LastIndent;
            string methodDebug = nameof(UD_HeldForPlayer) + "." + nameof(HandleEvent) + "(" + nameof(StartTradeEvent) + ")";

            static string shortDebugName(GameObject GO)
            {
                return "[" + GO?.ID + "]" + (GO?.Render?.DisplayName ?? NULL);
            }

            Debug.Entry(4, 
                methodDebug + " " +
                nameof(E.Trader) + ": " + shortDebugName(E.Trader) + ", " +
                nameof(E.Actor) + ": " + shortDebugName(E.Actor) + ", " +
                nameof(HeldFor) + ": " + shortDebugName(HeldFor) + " as "+ HeldForName,
                Indent: indent + 1, Toggle: doDebug);

            if (CheckStillHeld(RemoveOnFalse: true)
                && E.Actor is GameObject shopper)
            {
                if (shopper != HeldFor)
                {
                    ParentObject.SetIntProperty("TradeUI_DisplayOnly", 1);
                    Debug.CheckNah(4, "You're not " + HeldFor?.them, Indent: indent + 2, Toggle: doDebug);
                }
                else
                {
                    ParentObject.SetIntProperty("TradeUI_DisplayOnly", 0, true);
                    bool secondPersonAllowed = Grammar.AllowSecondPerson;
                    Grammar.AllowSecondPerson = false;
                    Debug.CheckYeh(4, "You're " + HeldFor?.them, Indent: indent + 2, Toggle: doDebug);
                    Grammar.AllowSecondPerson = secondPersonAllowed;
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

        public override void Write(GameObject Basis, SerializationWriter Writer)
        {
            base.Write(Basis, Writer);
            Writer.WriteOptimized(HeldForID);
            Writer.WriteOptimized(TurnHoldStarted);
        }
        public override void Read(GameObject Basis, SerializationReader Reader)
        {
            base.Read(Basis, Reader);
            HeldForID = Reader.ReadOptimizedString();
            TurnHoldStarted = Reader.ReadOptimizedInt64();
        }
    }
}
