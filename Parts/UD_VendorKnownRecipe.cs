using System;
using System.Collections.Generic;
using System.Security.AccessControl;
using System.Text;
using UD_Modding_Toolbox;
using UD_Tinkering_Bytes;
using UD_Vendor_Actions;
using XRL.Language;
using XRL.World.Capabilities;
using XRL.World.Effects;
using XRL.World.Tinkering;
using static UD_Modding_Toolbox.Const;

namespace XRL.World.Parts
{
    [Serializable]
    public class UD_VendorKnownRecipe : IScribedPart, IVendorActionEventHandler
    {
        private static bool doDebug => false;

        public TinkerData KnownRecipe;

        public bool ImplantedKnowledge;

        [NonSerialized]
        public string ObjectName;

        [NonSerialized]
        private static StringBuilder SB = new StringBuilder();

        [NonSerialized]
        private static BitCost Cost = new BitCost();

        public UD_VendorKnownRecipe()
        {
            KnownRecipe = null;
            ImplantedKnowledge = false;
        }

        public override bool CanGenerateStacked()
        {
            return false;
        }

        public TinkerData SetKnownRecipe(TinkerData KnownRecipe)
        {
            return this.KnownRecipe = KnownRecipe;
        }

        public override bool AllowStaticRegistration()
        {
            return true;
        }
        public override bool WantEvent(int ID, int Cascade)
        {
            return base.WantEvent(ID, Cascade)
                || ID == GetDisplayNameEvent.ID
                || ID == GetShortDescriptionEvent.ID;
        }
        public override bool HandleEvent(GetDisplayNameEvent E)
        {
            SB.Clear();
            if (KnownRecipe == null)
            {
                ObjectName = "invalid blueprint: " + ParentObject.Blueprint;
            }
            else if (KnownRecipe.Type == "Build")
            {
                try
                {
                    if (ObjectName == null)
                    {
                        if (KnownRecipe.Blueprint == null)
                        {
                            ObjectName = "invalid blueprint: " + KnownRecipe.Blueprint;
                        }
                        else
                        {
                            ObjectName = TinkeringHelpers.TinkeredItemShortDisplayName(KnownRecipe.Blueprint);
                        }
                    }
                }
                catch (Exception)
                {
                    ObjectName = "error:" + KnownRecipe.Blueprint;
                }
                SB.Append(": ").AppendColored("C", ObjectName).Append(" <");
                Cost.Clear();
                Cost.Import(TinkerItem.GetBitCostFor(KnownRecipe.Blueprint));
                ModifyBitCostEvent.Process(ParentObject.InInventory ?? The.Player, Cost, "DataDisk");
                Cost.ToStringBuilder(SB);
                SB.Append('>');
            }
            else if (KnownRecipe.Type == "Mod")
            {
                string itemMod = "Item mod".Color("W");
                string recipeDisplayName = KnownRecipe.DisplayName.Color("C");
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
            if (KnownRecipe != null)
            {
                if (KnownRecipe.Type == "Mod")
                {
                    string modDesc = ItemModding.GetModificationDescription(KnownRecipe.Blueprint, 0);
                    E.Postfix.AppendLine().Append("Adds item modification: ").Append(modDesc);
                }
                else
                {
                    GameObject sampleObject = GameObject.CreateSample(KnownRecipe.Blueprint);
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
                E.Postfix.AppendRules("Requires: ").Append(DataDisk.GetRequiredSkillHumanReadable(KnownRecipe.Tier));
                if (TinkerData.RecipeKnown(KnownRecipe))
                {
                    E.Postfix.AppendLine().AppendRules("You also know this recipe.");
                }
            }
            return base.HandleEvent(E);
        }
    }
}
