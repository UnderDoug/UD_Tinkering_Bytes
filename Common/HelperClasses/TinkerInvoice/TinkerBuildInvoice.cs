using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

using Qud.API;

using XRL;
using XRL.Language;
using XRL.Rules;
using XRL.UI;
using XRL.Wish;
using XRL.World;
using XRL.World.Capabilities;
using XRL.World.Effects;
using XRL.World.Parts;
using XRL.World.Parts.Skill;
using XRL.World.Tinkering;

using UD_Modding_Toolbox;

using static UD_Modding_Toolbox.Const;
using static UD_Tinkering_Bytes.Utils;
using XRL.Liquids;

namespace UD_Tinkering_Bytes
{
    [HasWishCommand]
    public class TinkerBuildInvoice : TinkerInvoice
    {
        public TinkerBuildInvoice()
            : base()
        {
        }

        public TinkerBuildInvoice(
            GameObject Vendor,
            TinkerData Recipe,
            GameObject SelectedIngredient,
            BitCost BitCost,
            bool VendorSuppliesBits,
            bool VendorOwnsRecipe = true)
            : base(Vendor, Recipe, SelectedIngredient, BitCost, VendorOwnsRecipe)
        {
            Service ??= BUILD;
            DepositAllowed = true;

            VendorSuppliesIngredients = SelectedIngredient?.InInventory == Vendor;
            this.VendorSuppliesBits = VendorSuppliesBits;

            IncludeItemValue = !IsItemValueIrrelevant() && PreferItemValue();

            bool wantLabourValue = GetLabourValue() > -1;
            bool wantExpertiseValue = VendorOwnsRecipe;
            IncludeMarkUpValue = wantLabourValue && wantExpertiseValue;
            IncludeLabourValue = !IncludeMarkUpValue && wantLabourValue;
            IncludeExpertiseValue = !IncludeMarkUpValue && wantExpertiseValue;

            IncludeIngredientValue = SelectedIngredient != null;
            IncludeBitsValue = BitCost != null;
            IncludeMaterialsValue = GetMaterialsValue() > -1;
        }

        public static implicit operator string(TinkerBuildInvoice TinkerBuildInvoice)
        {
            return TinkerBuildInvoice.ToString();
        }

        public override bool UseInvoiceOverride()
        {
            return true;
        }

        public override string GetItemName()
        {
            string itemName = Item.GetDisplayName(AsIfKnown: true, Single: true, Short: true);
            if (NumberMade != 1)
            {
                itemName = Grammar.Pluralize(itemName);
            }
            return itemName;
        }
    }
}
