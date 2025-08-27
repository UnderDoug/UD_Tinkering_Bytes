using System;
using System.Collections.Generic;
using System.Security.AccessControl;
using System.Text;
using UD_Modding_Toolbox;
using UD_Tinkering_Bytes;
using UD_Vendor_Actions;
using XRL.Language;
using XRL.UI;
using XRL.World.Capabilities;
using XRL.World.Effects;
using XRL.World.Tinkering;
using static UD_Modding_Toolbox.Const;

namespace XRL.World.Parts
{
    [Serializable]
    public class UD_VendorKnownRecipe 
        : IScribedPart
        , IVendorActionEventHandler
        , IModEventHandler<EndTradeEvent>
    {
        private static bool doDebug => false;

        public TinkerData Data;

        public bool FromImplant;

        [NonSerialized]
        public string ObjectName;

        [NonSerialized]
        private static StringBuilder SB = new StringBuilder();

        [NonSerialized]
        private static BitCost Cost = new BitCost();

        public UD_VendorKnownRecipe()
        {
            Data = null;
            FromImplant = false;
        }

        public override bool CanGenerateStacked()
        {
            return false;
        }

        public TinkerData SetData(TinkerData KnownRecipe)
        {
            return this.Data = KnownRecipe;
        }

        public override bool AllowStaticRegistration()
        {
            return true;
        }

        public override bool WantTurnTick()
        {
            return base.WantTurnTick();
        }
        public override void TurnTick(long TimeTick, int Amount)
        {
            if (GameObject.Validate(ParentObject))
            {
                ParentObject.Obliterate();
            }
            base.TurnTick(TimeTick, Amount);
        }

        public override bool WantEvent(int ID, int Cascade)
        {
            return base.WantEvent(ID, Cascade)
                || ID == GetDisplayNameEvent.ID
                || ID == GetShortDescriptionEvent.ID
                || ID == EndTradeEvent.ID;
        }
        public override bool HandleEvent(GetDisplayNameEvent E)
        {
            SB.Clear();
            if (Data == null)
            {
                ObjectName = "invalid blueprint: " + ParentObject.Blueprint;
            }
            else if (Data.Type == "Build")
            {
                try
                {
                    if (ObjectName == null)
                    {
                        if (Data.Blueprint == null)
                        {
                            ObjectName = "invalid blueprint: " + Data.Blueprint;
                        }
                        else
                        {
                            ObjectName = TinkeringHelpers.TinkeredItemShortDisplayName(Data.Blueprint);
                        }
                    }
                }
                catch (Exception)
                {
                    ObjectName = "error:" + Data.Blueprint;
                }
                SB.Append(": ").AppendColored("C", ObjectName).Append(" <");
                Cost.Clear();
                Cost.Import(TinkerItem.GetBitCostFor(Data.Blueprint));
                ModifyBitCostEvent.Process(ParentObject.InInventory ?? The.Player, Cost, "DataDisk");
                Cost.ToStringBuilder(SB);
                SB.Append('>');
            }
            else if (Data.Type == "Mod")
            {
                string itemMod = "Item mod".Color("W");
                string recipeDisplayName = Data.DisplayName.Color("C");
                ObjectName = $"[{itemMod}] - {recipeDisplayName}";
                SB.Append(": ").Append(ObjectName);
            }
            if (SB.Length > 0)
            {
                E.AddBase(SB.ToString(), 5);
            }
            return base.HandleEvent(E);
        }
        public override bool HandleEvent(GetShortDescriptionEvent E)
        {
            if (Data != null)
            {
                if (Data.Type == "Mod")
                {
                    string modDesc = ItemModding.GetModificationDescription(Data.Blueprint, 0);
                    E.Postfix.AppendLine().Append("Adds item modification: ").Append(modDesc);
                }
                else
                {
                    GameObject sampleObject = GameObject.CreateSample(Data.Blueprint);
                    if (sampleObject != null)
                    {
                        TinkeringHelpers.StripForTinkering(sampleObject);
                        TinkerItem tinkerItem = sampleObject.GetPart<TinkerItem>();
                        Description description = sampleObject.GetPart<Description>();
                        E.Postfix.AppendRules("Creates: ");
                        if (tinkerItem != null && tinkerItem.NumberMade > 1)
                        {
                            E.Postfix.Append(Grammar.Cardinal(tinkerItem.NumberMade)).Append(' ');
                            E.Postfix.Append(Grammar.Pluralize(sampleObject.DisplayNameOnlyDirect));
                        }
                        else
                        {
                            E.Postfix.Append(sampleObject.DisplayNameOnlyDirect);
                        }
                        E.Postfix.AppendLine();
                        if (description != null)
                        {
                            E.Postfix.AppendLine().Append(description._Short);
                        }
                        if (GameObject.Validate(ref sampleObject))
                        {
                            sampleObject.Obliterate();
                        }
                    }
                }
                E.Postfix.AppendLine().AppendRules("Requires: ").Append(DataDisk.GetRequiredSkillHumanReadable(Data.Tier));
                if (FromImplant)
                {
                    E.Postfix.Append(" [").AppendColored("c", "implanted recipe").Append("]");
                }
                if (TinkerData.RecipeKnown(Data))
                {
                    E.Postfix.AppendLine().AppendRules("You also know this recipe.");
                }
            }
            return base.HandleEvent(E);
        }
        public virtual bool HandleEvent(EndTradeEvent E)
        {
            if (GameObject.Validate(ParentObject))
            {
                ParentObject.Obliterate();
            }
            return base.HandleEvent(E);
        }
    }
}
