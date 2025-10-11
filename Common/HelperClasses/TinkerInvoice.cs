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

namespace UD_Tinkering_Bytes
{
    [HasWishCommand]
    public class TinkerInvoice
    {
        public const string RECHARGE = "Recharge";
        public const string REPAIR = "Repair";
        public const string BUILD = "Build";
        public const string MOD = "Mod";

        public GameObject Vendor = null;
        public bool Besties => Vendor != null && Vendor.IsPlayerLed();

        public string Service = null;

        public TinkerData Recipe = null;
        public bool VendorOwnsRecipe = false;

        public GameObject Item = null;
        public bool IsItemSample = false;

        private bool GotPerformance = false;
        private double Performance = 1.0;

        public string ItemName = "";
        public int NumberMade = 1;
        public double ItemValue = 0;

        public double ExpertiseValue = 0;

        public GameObject SelectedIngredient = null;
        public double IngredientValue = 0;
        public bool VendorSuppliesIngredients = true;
        public bool IngredientUsed = false;

        public BitCost BitCost = null;
        public double BitsValue = 0;
        public bool VendorSuppliesBits = true;

        public double LabourValue = 0;

        public double TotalCost = 0;
        public double DepositCost = 0;

        public bool HoldForPlayer = false;

        public TinkerInvoice()
        {
        }

        public TinkerInvoice(GameObject Vendor, string Service, GameObject SelectedIngredient = null, BitCost BitCost = null, GameObject Item = null)
            : this()
        {
            this.Vendor = Vendor;
            this.Service ??= Service ?? "Sundry";

            this.SelectedIngredient = SelectedIngredient;
            this.BitCost = BitCost;
            this.Item = Item;
        }

        public TinkerInvoice(GameObject Vendor, TinkerData Recipe, GameObject SelectedIngredient, BitCost BitCost, bool VendorOwnsRecipe = true, GameObject ApplyModTo = null)
            : this(Vendor, Recipe.Type, SelectedIngredient, BitCost)
        {
            this.Recipe = Recipe;
            this.VendorOwnsRecipe = VendorOwnsRecipe;

            if (Service == BUILD)
            {
                Item = GameObject.CreateSample(Recipe.Blueprint);
                IsItemSample = true;
                TinkeringHelpers.StripForTinkering(Item);
                if (Item.TryGetPart(out TinkerItem tinkerItem) && tinkerItem.NumberMade != 1)
                {
                    NumberMade = tinkerItem.NumberMade;
                }
            }
            else
            if (Service == MOD)
            {
                Item = ApplyModTo;
            }
        }
        public TinkerInvoice(GameObject Vendor, TinkerData Recipe, BitCost BitCost, bool VendorOwnsRecipe = true, GameObject ApplyModTo = null)
            : this(Vendor, Recipe, null, BitCost, VendorOwnsRecipe, ApplyModTo)
        {
        }

        public void Clear()
        {
            if (IsItemSample)
            {
                ScrapTinkerSample(ref Item);
            }
            Vendor = null;
            Recipe = null;
        }

        public static GameObject CreateTinkerSample(string Blueprint)
        {
            GameObject tinkerSample = GameObject.CreateSample(Blueprint);
            TinkeringHelpers.StripForTinkering(tinkerSample);
            TinkeringHelpers.ForceToBePowered(tinkerSample);
            return tinkerSample;
        }
        public static void ScrapTinkerSample(ref GameObject TinkerSample)
        {
            if (GameObject.Validate(ref TinkerSample))
            {
                TinkerSample.Obliterate();
            }
        }

        public string GetItemName()
        {
            if (ItemName.IsNullOrEmpty() && Item != null)
            {
                ItemName = Item.GetDisplayName(AsIfKnown: true, Single: true, Short: true);
                if (Service == BUILD && NumberMade != 1)
                {
                    ItemName = Grammar.Pluralize(ItemName);
                }
                else
                if (Service == MOD)
                {
                    ItemName = Item.t(Single:true, Short:true);
                }
                if (Service == REPAIR)
                {
                    ItemName = Item.t(Short:true);
                }
                if (Item.InInventory != Vendor && Service == RECHARGE)
                {
                    ItemName = Item.t(Short: true);
                }
                ItemName = ItemName.Color("y");
            }
            return ItemName;
        }

        public double GetPerformance()
        {
            if (!GotPerformance)
            {
                Performance = GetTradePerformanceEvent.GetFor(The.Player, Vendor);
                GotPerformance = true;
            }
            return Performance;
        }

        public double GetItemValue()
        {
            if (Recipe != null && Service == BUILD && ItemValue == 0)
            {
                Item ??= GameObject.CreateSample(Recipe.Blueprint);
                TinkeringHelpers.StripForTinkering(Item);
                ItemValue = Math.Round(Item.ValueEach / GetPerformance(), 2);
                return ItemValue;
            }
            return ItemValue;
        }
        public static double GetExamineCost(GameObject Item, double Performance = 1.0)
        {
            if (Item == null)
            {
                return -1;
            }

            // from the source (courtesy Books):
            // comment: int Cost = (int) Math.Pow(2, Math.Max(1, ((complexity + difficulty) / 2f) + 1));
            // float x = (complexity + difficulty);
            // int Cost = (int)Math.Max(2, -0.0667 + 1.24 * x + 0.0967 * Math.Pow(x, 2) + 0.0979 * Math.Pow(x, 3));

            float x = Item.GetComplexity() + Item.GetExamineDifficulty();
            double examinCost = -0.0667 + 1.24 * (double)x + 0.0967 * Math.Pow(x, 2.0) + 0.0979 * Math.Pow(x, 3.0);
            return Math.Max(2.0, examinCost / Performance);
        }
        public static double GetExamineCost(GameObjectBlueprint Item, double Performance = 1.0)
        {
            return GetExamineCost(Item?.Name, Performance);
        }
        public static double GetExamineCost(string Item, double Performance = 1.0)
        {
            double value = 0.0;
            if (CreateTinkerSample(Item) is GameObject tinkerSampleItem)
            {
                value = GetExamineCost(tinkerSampleItem, Performance);
                ScrapTinkerSample(ref tinkerSampleItem);
            }
            return value;
        }
        public double GetExamineCost()
        {
            return GetExamineCost(Item, GetPerformance());
        }

        public double GetExpertiseValue()
        {
            if (VendorOwnsRecipe && ExpertiseValue == 0)
            {
                string priceOverrideString = Item.GetPropertyOrTag("VendorTinker_ExpertiseValueOverride", "-1");
                if (int.TryParse(priceOverrideString, out int priceOverride) && priceOverride > -1)
                {
                    ExpertiseValue = priceOverride;
                }
                else
                {
                    if (TinkerData.createDataDisk(Recipe) is GameObject sampleDataDiskObject 
                        && sampleDataDiskObject.TryGetPart(out DataDisk dataDisk) && dataDisk.Data == Recipe)
                    {
                        ExpertiseValue = Math.Round(Math.Max(2, TradeUI.GetValue(sampleDataDiskObject, true) / 3), 2);
                        ScrapTinkerSample(ref sampleDataDiskObject);
                    }
                }
            }
            return ExpertiseValue;
        }

        public double GetLabourValue()
        {
            if (LabourValue == 0)
            {
                double minCost = 2;
                double examineCost = GetExamineCost(Item, Performance);
                if (Service == RECHARGE)
                {
                    examineCost *= 0.3;
                }
                else
                if (Service == REPAIR)
                {
                    examineCost *= 0.5;
                }
                LabourValue = Math.Round(Math.Max(minCost, examineCost), 2);
            }
            return LabourValue;
        }

        public double GetMarkUpValue()
        {
            return GetLabourValue() + GetExpertiseValue();
        }

        public GameObject GetSelectedIngredient()
        {
            if (SelectedIngredient == null
                && UD_VendorTinkering.PickRecipeIngredientSupplier(
                    Vendor: Vendor,
                    ForObject: Item,
                    TinkerData: Recipe,
                    RecipeIngredientSupplier: out GameObject ingredientSupplier,
                    SelectedIngredient: out SelectedIngredient))
            {
                VendorSuppliesIngredients = ingredientSupplier == Vendor;
            }
            return SelectedIngredient;
        }

        public static double GetIngedientValueInDrams(string Ingredient, GameObject Vendor = null)
        {
            double ingredientValue = 0;
            if (CreateTinkerSample(Ingredient) is GameObject sampleIngredientObject)
            {
                ingredientValue = TradeUI.GetValue(sampleIngredientObject, Vendor != null);
                ScrapTinkerSample(ref sampleIngredientObject);
            }
            return ingredientValue;
        }

        public static bool UseSelectedIngredient(GameObject Vendor, TinkerInvoice TinkerInvoice, GameObject RecipeIngredientSupplier)
        {
            if (TinkerInvoice.SelectedIngredient != null 
                && !RecipeIngredientSupplier.RemoveFromInventory(TinkerInvoice.SelectedIngredient))
            {
                string invalidIngredientMsg = "=subject.T= cannot use =object.t= as an ingredient!="
                    .StartReplace()
                    .AddObject(Vendor)
                    .AddObject(TinkerInvoice.SelectedIngredient)
                    .ToString();

                Vendor.Fail(invalidIngredientMsg);
                return false;
            }
            return true;
        }
        public bool UseSelectedIngredient(GameObject RecipeIngredientSupplier)
        {
            return UseSelectedIngredient(Vendor, this, RecipeIngredientSupplier);
        }

        public static GameObject GetBitScrapItem(string Bit)
        {
            char bit = default;
            if (!Bit.IsNullOrEmpty())
            {
                bit = BitType.ReverseCharTranslateBit(Bit[0]);
            }
            return GetBitScrapItem(bit);
        }
        public static GameObject GetBitScrapItem(char Bit)
        {
            GameObject scrapItem = null;
            if (Bit != default)
            {
                char bit = BitType.CharTranslateBit(Bit);
                string scrapBlueprint = GetScrapBlueprintFromBit(bit);
                if (!scrapBlueprint.IsNullOrEmpty())
                {
                    scrapItem = CreateTinkerSample(scrapBlueprint);
                }
            }
            return scrapItem;
        }

        public static double GetIngredientsValueInDrams(GameObject Ingredient, GameObject Vendor = null)
        {
            return TradeUI.GetValue(Ingredient, Vendor != null);
        }
        public static double GetIngredientsValueInDrams(string Ingredient, GameObject Vendor = null)
        {
            double valueInDrams = 0;
            if (CreateTinkerSample(Ingredient) is GameObject sampleIngredientObject)
            {
                valueInDrams = TradeUI.GetValue(sampleIngredientObject, Vendor != null);
                ScrapTinkerSample(ref sampleIngredientObject);
            }
            return valueInDrams;
        }

        public double GetIngredientValue()
        {
            if (IngredientValue == 0
                && VendorSuppliesIngredients
                && GetSelectedIngredient() is GameObject selectedIngredient)
            {
                IngredientValue = Math.Round(GetIngredientsValueInDrams(selectedIngredient, Vendor), 2);
            }
            return IngredientValue;
        }

        public static BitCost GetBuildBitCost(TinkerData TinkerData)
        {
            if (TinkerData.Type != "Build")
            {
                return null;
            }
            BitCost bitCost = new();
            bitCost.Import(TinkerItem.GetBitCostFor(TinkerData.Blueprint));
            return bitCost;
        }
        public BitCost GetBuildBitCost()
        {
            if (Service != BUILD)
            {
                return null;
            }
            return GetBuildBitCost(Recipe);
        }

        public static BitCost GetModBitCostForObject(TinkerData TinkerData, GameObject Object)
        {
            if (TinkerData.Type != "Mod")
            {
                return null;
            }
            int recipeTier = Tier.Constrain(TinkerData.Tier);

            int modSlotsUsed = Object.GetModificationSlotsUsed();
            int noCostMods = Object.GetIntProperty("NoCostMods");

            int existingModsTier = Tier.Constrain(modSlotsUsed - noCostMods + Object.GetTechTier());

            BitCost bitCost = new();
            bitCost.Increment(BitType.TierBits[recipeTier]);
            bitCost.Increment(BitType.TierBits[existingModsTier]);
            return bitCost;
        }
        public BitCost GetModBitCostForObject()
        {
            if (Service != MOD)
            {
                return null;
            }
            return GetModBitCostForObject(Recipe, Item);
        }

        public static double GetBitsValueInDrams(string BitCost, GameObject Vendor = null)
        {
            double totalValue = 0;
            if (!BitCost.IsNullOrEmpty())
            {
                GameObject sampleScrap = null;
                foreach (char bit in BitCost)
                {
                    sampleScrap = GetBitScrapItem(bit);
                    if (GameObject.Validate(ref sampleScrap))
                    {
                        totalValue += TradeUI.GetValue(sampleScrap, Vendor != null ? true : null);
                        ScrapTinkerSample(ref sampleScrap);
                    }
                }
            }
            return totalValue;
        }
        public static double GetBitsValueInDrams(BitCost BitCost, GameObject Vendor = null)
        {
            return GetBitsValueInDrams(BitCost.ToBits(), Vendor);
        }

        public double GetBitsValue()
        {
            if (BitsValue == 0 && VendorSuppliesBits && Vendor != null)
            {
                BitsValue = Math.Round(GetBitsValueInDrams(BitCost, Vendor), 2);
            }
            return BitsValue;
        }

        public double GetMaterialsValue()
        {
            return Math.Round(GetIngredientValue() + GetBitsValue(), 2);
        }

        public int GetTotalCost()
        {
            if (TotalCost == 0)
            {
                bool isItemValueIrrelevant = !VendorSuppliesBits || !VendorSuppliesIngredients;
                if (Service == MOD)
                {
                    TotalCost = GetMarkUpValue() + GetMaterialsValue();
                }
                else
                if (Service == BUILD)
                {
                    TotalCost = GetLabourValue() + (!isItemValueIrrelevant && GetItemValue() > GetMaterialsValue() ? GetItemValue() : (GetMaterialsValue() + GetExpertiseValue()));
                }
                else
                if (Service == RECHARGE || Service == REPAIR)
                {
                    TotalCost = GetLabourValue() + GetMaterialsValue();
                }

                if (TotalCost > 0)
                {
                    if (!VendorSuppliesIngredients)
                    {
                        TotalCost -= GetIngredientValue();
                    }

                    if (!VendorSuppliesBits)
                    {
                        TotalCost -= GetBitsValue();
                    }
                }
            }
            return Besties ? 0 : (int)Math.Ceiling(TotalCost);
        }

        public int GetDepositCost()
        {
            if (Service == BUILD
                && DepositCost == 0
                && (SelectedIngredient == null || GetIngredientValue() > 0)
                && GetBitsValue() > 0)
            {
                DepositCost = GetTotalCost() - GetItemValue();
            }
            return (int)Math.Ceiling(DepositCost);
        }

        public static string DividerLine()
        {
            string output = "";
            for (int i = 0; i < 25; i++)
            {
                output += HONLY;
            }
            return output;
        }

        public static string DramsCostString(double DramsCost)
        {
            return DramsCost.Things("dram").Color("C");
        }
        public static string DramsCostString(int DramsCost)
        {
            return DramsCostString((double)DramsCost);
        }

        public override string ToString()
        {
            GameObject player = The.Player;
            var SB = Event.NewStringBuilder("Invoice".Color("W")).AppendLine();

            bool isItemValueIrrelevant = !VendorSuppliesBits || !VendorSuppliesIngredients;

            string itemName = GetItemName();
            string dividerLineK = DividerLine().Color("K");

            // Description
            if (Service == BUILD)
            {
                SB.Append("Description: Tinker ");
                if (HoldForPlayer)
                {
                    SB.Append("and hold ");
                }
                SB.Append($"{NumberMade}x ").Append(itemName).AppendLine();
            }
            else
            if (Service == MOD)
            {
                SB.Append("Description: Apply ").Append(Recipe.DisplayName.Color("y"));
                SB.Append(" to ").Append(itemName).AppendLine();
            }
            else
            if (Service == REPAIR)
            {
                SB.Append("Description: Repair ").Append(itemName).AppendLine();
            }
            else
            if (Service == RECHARGE)
            {
                SB.Append("Description: Recharge ").Append(itemName).AppendLine();
            }
            SB.Append(dividerLineK).AppendLine();

            // Item or Material Cost
            double itemValue = GetItemValue();
            double materialsValue = GetMaterialsValue();
            bool isItemWorthMore = itemValue > materialsValue;
            bool vendorSuppliesAll = VendorSuppliesIngredients && VendorSuppliesBits;
            double labourValue = GetLabourValue();
            double markUpValue = GetMarkUpValue();
            if (!isItemValueIrrelevant && isItemWorthMore && Service == BUILD)
            {
                SB.Append("Item Value: ").Append(DramsCostString(itemValue)).AppendLine();
                if (labourValue > -1)
                {
                    SB.Append("Labour: ").Append(DramsCostString(labourValue)).AppendLine();
                }
            }
            else
            if (VendorOwnsRecipe)
            {
                SB.Append("Labour && Expertise: ").Append(DramsCostString(markUpValue)).AppendLine();
            }
            else
            if (markUpValue > -1)
            {
                SB.Append("Labour: ").Append(DramsCostString(markUpValue)).AppendLine();
            }

            // Item ID.
            if (Service == BUILD && !Item.Understood())
            {
                SB.Append("Identification of item: ").Append(DramsCostString(labourValue));
                SB.Append(" (").AppendColored("K", "included").Append(")").AppendLine();
            }

            // Ingredient
            double ingredientValue = GetIngredientValue();
            if (GetSelectedIngredient() is GameObject selectedIngredient)
            {
                string ingredientDisplayName = selectedIngredient.GetDisplayName(Context: nameof(TinkerInvoice), Short: true);
                SB.Append("Ingredient, ").Append(ingredientDisplayName).Append(": ");
                if (VendorSuppliesIngredients)
                {
                    SB.Append(DramsCostString(ingredientValue));
                    if (isItemWorthMore && vendorSuppliesAll)
                    {
                        SB.Append(" (").AppendColored("K", "included in item value").Append(")");
                    }
                }
                else
                {
                    SB.Append(DramsCostString(0));
                    SB.Append(" (").AppendColored("K", $"provided by {player?.t(Short: true)}").Append(")");
                }
                SB.AppendLine();
            }

            // Bits
            double bitsValue = GetBitsValue();
            if (!BitCost.IsNullOrEmpty())
            {
                SB.Append($"Bits <{BitCost}>: ");
                if (VendorSuppliesBits)
                {
                    SB.Append(DramsCostString(bitsValue));
                    if (isItemWorthMore && vendorSuppliesAll)
                    {
                        SB.Append(" (").AppendColored("K", "included in item value").Append(")");
                    }
                }
                else
                {
                    SB.Append(DramsCostString(0));
                    SB.Append(" (").AppendColored("K", $"provided by {player?.t(Short: true)}").Append(")");
                }
                SB.AppendLine();
            }

            // Total Cost
            int totalCost = GetTotalCost();
            if (VendorSuppliesIngredients || VendorSuppliesBits)
            {
                string performServiceOn = Service switch
                {
                    RECHARGE => "recharge",
                    REPAIR => "repair",
                    BUILD => "tinker",
                    MOD => "tinker",
                    _ => "tinker",
                };
                SB.Append($"Total cost to {performServiceOn} item: ").Append(DramsCostString(totalCost));
                if (Besties)
                {
                    if (Vendor.HasEffect<Lovesick>())
                    {
                        SB.Append(" for ").AppendColored("love", "my love!");
                    }
                    else
                    if (Vendor.HasEffect<Proselytized>() || Vendor.HasEffect<Beguiled>())
                    {
                        SB.Append(" for my ").AppendColored("M", "bestie!");
                    }
                    else
                    {
                        string waterRitualLiquid = LiquidVolume.GetLiquid(Vendor.GetWaterRitualLiquid(player)).GetName();
                        SB.Append(" for my ").Append(waterRitualLiquid).Append(" ").Append(player.siblingTerm).Append("!");
                    }
                }
                SB.AppendLine();
            }

            // Deposit
            int depositCost = GetDepositCost();
            if (!Besties
                && depositCost > 0
                && depositCost < totalCost
                && player.GetFreeDrams() < totalCost)
            {
                SB.Append(dividerLineK).AppendLine();
                SB.Append("Will tinker and hold item for desposit of ").Append(DramsCostString(depositCost));
            }

            return Event.FinalizeString(SB);
        }

        public string GetDepositMessage()
        {
            string items =  "item";
            string itThem = Item.it;
            if (NumberMade != 1)
            {
                items = Grammar.Pluralize(items);
                itThem = "them";
            }
            string thisTheseItems = Item.indicativeProximal + " " + items;

            string totalCost = DramsCostString(GetTotalCost());
            string depositCost = DramsCostString(GetDepositCost()) + " of fresh water";
            string itemValue = DramsCostString(GetItemValue());
            string restocks = "2 restocks".Color("g");

            string tooExpensiveMsg = 
                ("=subject.T= =verb:don't:afterpronoun= have the required " + totalCost +
                " to have =object.t= tinker " + thisTheseItems + ".")
                    .StartReplace()
                    .AddObject(The.Player)
                    .AddObject(Vendor)
                    .ToString();

            string willTinkerForDepositMsg = 
                ("=object.Subjective= will tinker " + thisTheseItems + " for a deposit of " + depositCost +
                ", however =object.subjective= will hold onto " + itThem + " until =subject.subjective= " +
                "=verb:have:afterpronoun= the remaining " + itemValue + ".")
                    .StartReplace()
                    .AddObject(The.Player)
                    .AddObject(Vendor)
                    .ToString();

            string pleaseNoteMsg = 
                ("Please note: =object.T= will only hold onto " + thisTheseItems + " for " + restocks + ".")
                    .StartReplace()
                    .AddObject(The.Player)
                    .AddObject(Vendor)
                    .ToString();

            string likeToPayMsg = "Would =subject.t= like to pay this deposit?"
                    .StartReplace()
                    .AddObject(The.Player)
                    .AddObject(Vendor)
                    .ToString();

            return tooExpensiveMsg +
                "\n\n" +
                willTinkerForDepositMsg +
                "\n\n" +
                pleaseNoteMsg +
                "\n\n" +
                likeToPayMsg;
        }

        public static string DebugString(TinkerInvoice TinkerInvoice = null, string Service = null)
        {
            if (TinkerInvoice != null)
            {
                return TinkerInvoice.DebugString();
            }
            else
            {
                var SB = Event.NewStringBuilder();
                SB.Append(nameof(Recipe)).Append(",");
                SB.Append(nameof(TinkerInvoice.Recipe.Type)).Append(",");
                SB.Append(nameof(TinkerInvoice.Recipe.Tier)).Append(",");
                SB.Append(nameof(VendorOwnsRecipe)).Append(",");
                SB.Append(nameof(Performance)).Append(",");
                SB.Append(nameof(ItemName)).Append(",");
                SB.Append(nameof(NumberMade)).Append(",");
                SB.Append(nameof(ItemValue)).Append(",");
                SB.Append(nameof(GetExpertiseValue).Replace("Get", "")).Append(",");
                SB.Append(nameof(SelectedIngredient)).Append(",");
                SB.Append(nameof(IngredientValue)).Append(",");
                SB.Append(nameof(BitCost)).Append(",");
                SB.Append(nameof(BitsValue)).Append(",");
                SB.Append(nameof(GetMaterialsValue).Replace("Get", "")).Append(",");
                SB.Append(nameof(GetMarkUpValue).Replace("Get", "")).Append(",");
                SB.Append(nameof(LabourValue)).Append(",");
                SB.Append(nameof(TotalCost)).Append(",");
                SB.Append(nameof(DepositCost));
                if (Service == REPAIR)
                {
                    SB.Append(",").Append("BaseGameRepair");
                }

                return Event.FinalizeString(SB);
            }
        }
        public string DebugString()
        {
            var SB = Event.NewStringBuilder();

            bool armorStatsOnlyWhenEquipped = Item.HasPropertyOrTag("DisplayArmorStatsOnlyWhenEquipped");
            if (!armorStatsOnlyWhenEquipped)
            {
                Item.SetStringProperty("DisplayArmorStatsOnlyWhenEquipped", "Don't want to see it!");
            }
            string itemName = GetItemName();
            if (!armorStatsOnlyWhenEquipped)
            {
                Item.SetStringProperty("DisplayArmorStatsOnlyWhenEquipped", null, true);
            }
            if (Item.TryGetPart(out MeleeWeapon mw))
            {
                itemName = itemName.Replace(mw.GetSimplifiedStats(XRL.UI.Options.ShowDetailedWeaponStats), "");
            }
            itemName = itemName.Strip();

            int tier = 0;
            if (Recipe != null)
            {
                tier = Recipe.Tier;
            }
            else
            if (Item != null)
            {
                tier = Item.GetTier();
            }

            SB.Append(Recipe?.Blueprint?.Strip() ?? Recipe?.PartName?.Strip() ?? NULL).Append(",");
            SB.Append(Recipe?.Type ?? NULL).Append(",");
            SB.Append(tier).Append(",");
            SB.Append(VendorOwnsRecipe).Append(",");
            SB.Append(GetPerformance()).Append(",");
            SB.Append(itemName).Append(",");
            SB.Append(NumberMade).Append(",");
            SB.Append(GetItemValue()).Append(",");
            SB.Append(GetExpertiseValue()).Append(",");
            SB.Append(GetSelectedIngredient()).Append(",");
            SB.Append(GetIngredientValue()).Append(",");
            SB.Append(BitCost?.ToString()?.Strip() ?? NULL).Append(",");
            SB.Append(GetBitsValue()).Append(",");
            SB.Append(GetMaterialsValue()).Append(",");
            SB.Append(GetMarkUpValue()).Append(",");
            SB.Append(GetLabourValue()).Append(",");
            SB.Append(GetTotalCost()).Append(",");
            SB.Append(GetDepositCost());
            if (Service == REPAIR)
            {
                SB.Append((TradeUI.GetValue(Item, true) / 25) * Item.Count);
            }

            return Event.FinalizeString(SB);
        }

        public static implicit operator string(TinkerInvoice TinkerInvoice)
        {
            return TinkerInvoice.ToString();
        }

        [WishCommand(Command = "all invoices")]
        public static void AllInvoices_WishHandler(string Service)
        {
            if (Service == null)
            {
                Popup.Show($"please specify a {nameof(Service)}: {BUILD.Quote()}, {MOD.Quote()}, {REPAIR.Quote()}, or {RECHARGE.Quote()}");
                return;
            }
            GameObject sampleBep = GameObject.CreateSample("Bep");
            GameObject sampleItem = null;

            Debug.Entry(4, DebugString(), Indent: 0, Toggle: true);
            TinkerInvoice tinkerInvoice = null;
            BitCost bitCost;
            try
            {
                if (Service.ToLower() == BUILD.ToLower())
                {
                    int recipeCount = TinkerData.TinkerRecipes.Count;
                    int currentRecipe = 0;
                    foreach (TinkerData buildRecipe in TinkerData.TinkerRecipes)
                    {
                        currentRecipe++;
                        Loading.SetLoadingStatus(
                            $"{currentRecipe} of {recipeCount} | " +
                            $"{buildRecipe?.Blueprint ?? buildRecipe?.PartName ?? NULL}...");
                        if (buildRecipe.Type != "Build")
                        {
                            continue;
                        }

                        bitCost = new();
                        bitCost.Import(TinkerItem.GetBitCostFor(buildRecipe.Blueprint));

                        if (buildRecipe.Ingredient.IsNullOrEmpty())
                        {
                            tinkerInvoice = new(sampleBep, buildRecipe, bitCost, true);

                            Debug.Entry(4, tinkerInvoice?.DebugString(), Indent: 0, Toggle: true);
                            tinkerInvoice?.Clear();
                        }
                        else
                        {
                            foreach (string ingredient in buildRecipe.Ingredient.CachedCommaExpansion())
                            {
                                if (CreateTinkerSample(ingredient) is GameObject sampleIngredientObject)
                                {
                                    tinkerInvoice = new(sampleBep, buildRecipe, sampleIngredientObject, bitCost, true);

                                    Debug.Entry(4, tinkerInvoice?.DebugString(), Indent: 0, Toggle: true);
                                    tinkerInvoice?.Clear();
                                    ScrapTinkerSample(ref sampleIngredientObject);
                                }
                            }
                        }
                    }
                }
                else
                if (Service.ToLower() == MOD.ToLower())
                {
                    List<GameObjectBlueprint> blueprintList = GameObjectFactory.Factory.SafelyGetBlueprintsInheritingFrom("Item");
                    if (blueprintList.IsNullOrEmpty())
                    {
                        Popup.Show($"There are some how no blueprints that inherit from \"Item\"");
                        return;
                    }

                    int blueprintCount = blueprintList.Count;
                    int recipeCount = TinkerData.TinkerRecipes.Count;
                    int totalProgress = blueprintCount * recipeCount;
                    int currentBlueprint = 0;
                    int currentRecipe = 0;
                    int progress = 0;
                    foreach (GameObjectBlueprint gameObjectBlueprint in blueprintList)
                    {
                        currentBlueprint++;
                        if (!gameObjectBlueprint.HasTagOrProperty("Mods")
                            || gameObjectBlueprint.HasTagOrProperty("BaseObject")
                            || gameObjectBlueprint.HasTagOrProperty("ExcludeFromDynamicEncounters"))
                        {
                            currentRecipe += recipeCount;
                            progress = currentBlueprint + currentRecipe;
                            Loading.SetLoadingStatus(
                                $"{progress} of {totalProgress} | " +
                                $"{gameObjectBlueprint?.DisplayName()?.Strip()} has No Mods...");
                            continue;
                        }
                        sampleItem = GameObject.CreateSample(gameObjectBlueprint.Name);
                        TinkeringHelpers.StripForTinkering(sampleItem);
                        string sampleItemDisplayName = sampleItem?.GetDisplayName(AsIfKnown: true, Short: true, Stripped: true) ?? NULL;
                        try
                        {
                            foreach (TinkerData modRecipe in TinkerData.TinkerRecipes)
                            {
                                currentRecipe++;
                                progress = currentBlueprint + currentRecipe;
                                if (modRecipe.Type != "Mod" || !ItemModding.ModAppropriate(sampleItem, modRecipe))
                                {
                                    Loading.SetLoadingStatus(
                                        $"{progress} of {totalProgress} | " +
                                        $"{sampleItemDisplayName} skipping Build Recipe or Unapplicable Mod...");
                                    continue;
                                }
                                Loading.SetLoadingStatus(
                                    $"{progress} of {totalProgress} | " +
                                    $"{sampleItemDisplayName} and {modRecipe?.PartName}...");
                                bitCost = new();
                                int recipeTier = Tier.Constrain(modRecipe.Tier);

                                int modSlotsUsed = sampleItem.GetModificationSlotsUsed();
                                int noCostMods = sampleItem.GetIntProperty("NoCostMods");

                                int existingModsTier = Tier.Constrain(modSlotsUsed - noCostMods + sampleItem.GetTechTier());

                                bitCost.Increment(BitType.TierBits[recipeTier]);
                                bitCost.Increment(BitType.TierBits[existingModsTier]);

                                if (modRecipe.Ingredient.IsNullOrEmpty())
                                {
                                    tinkerInvoice = new(sampleBep, modRecipe, bitCost, true, sampleItem);

                                    Debug.Entry(4, tinkerInvoice?.DebugString(), Indent: 0, Toggle: true);
                                    tinkerInvoice?.Clear();
                                }
                                else
                                {
                                    foreach (string ingredient in modRecipe.Ingredient.CachedCommaExpansion())
                                    {
                                        if (CreateTinkerSample(ingredient) is GameObject sampleIngredientObject)
                                        {
                                            tinkerInvoice = new(sampleBep, modRecipe, sampleIngredientObject, bitCost, true, sampleItem);

                                            Debug.Entry(4, tinkerInvoice?.DebugString(), Indent: 0, Toggle: true);
                                            tinkerInvoice?.Clear();
                                            ScrapTinkerSample(ref sampleIngredientObject);
                                        }
                                    }
                                }
                            }
                        }
                        finally
                        {
                            ScrapTinkerSample(ref sampleItem);
                        }
                    }
                }
                else
                if (Service.ToLower() == REPAIR.ToLower())
                {
                    List<GameObjectBlueprint> blueprintList = GameObjectFactory.Factory.SafelyGetBlueprintsInheritingFrom("Item");
                    if (blueprintList.IsNullOrEmpty())
                    {
                        Popup.Show($"There are some how no blueprints that inherit from \"Item\"");
                        return;
                    }

                    int blueprintCount = blueprintList.Count;
                    int totalProgress = blueprintCount;
                    int currentBlueprint = 0;
                    int progress = 0;
                    foreach (GameObjectBlueprint gameObjectBlueprint in blueprintList)
                    {
                        currentBlueprint++;
                        progress = currentBlueprint;
                        if (gameObjectBlueprint.HasTagOrProperty("NoRepair")
                            || (gameObjectBlueprint.HasTagOrProperty("NoEffects") && !gameObjectBlueprint.HasStat("Hitpoints"))
                            || gameObjectBlueprint.HasTagOrProperty("BaseObject")
                            || gameObjectBlueprint.HasTagOrProperty("ExcludeFromDynamicEncounters")
                            || gameObjectBlueprint.IsNatural())
                        {
                            Loading.SetLoadingStatus(
                                $"{progress} of {totalProgress} | " +
                                $"{gameObjectBlueprint?.DisplayName()?.Strip()} is irreparable...");
                            continue;
                        }
                        sampleItem = CreateTinkerSample(gameObjectBlueprint.Name);
                        string sampleItemDisplayName = sampleItem?.GetDisplayName(AsIfKnown: true, Short: true, Stripped: true) ?? NULL;
                        try
                        {
                            Loading.SetLoadingStatus(
                                $"{progress} of {totalProgress} | " +
                                $"{sampleItemDisplayName}...");

                            if (sampleItem == null)
                            {
                                continue;
                            }
                            bool lowHP = false;
                            if (sampleItem.GetStat("Hitpoints") is Statistic hitpoints)
                            {
                                hitpoints.Penalty = hitpoints.BaseValue - 1;
                                lowHP = true;
                            }
                            if (!sampleItem.IsBroken())
                            {
                                sampleItem.ForceApplyEffect(new Broken());
                            }
                            if (!lowHP && !sampleItem.IsBroken())
                            {
                                continue;
                            }

                            bitCost = new(Tinkering_Repair.GetRepairCost(sampleItem));

                            tinkerInvoice = new(sampleBep, REPAIR, null, bitCost, sampleItem)
                            {
                                VendorSuppliesBits = true,
                            };

                            Debug.Entry(4, tinkerInvoice.DebugString(), Indent: 0, Toggle: true);
                            tinkerInvoice?.Clear();
                        }
                        finally
                        {
                            ScrapTinkerSample(ref sampleItem);
                        }
                    }
                }
                else
                if (Service.ToLower() == RECHARGE.ToLower())
                {
                    if (Service.ToLower() == RECHARGE.ToLower())
                    {
                        Popup.Show($"{RECHARGE.Quote()} debug not implemented yet");
                        return;
                    }

                    List<GameObjectBlueprint> blueprintList = GameObjectFactory.Factory.SafelyGetBlueprintsInheritingFrom("Item");
                    if (blueprintList.IsNullOrEmpty())
                    {
                        Popup.Show($"There are some how no blueprints that inherit from \"Item\"");
                        return;
                    }

                    int blueprintCount = blueprintList.Count;
                    int totalProgress = blueprintCount;
                    int currentBlueprint = 0;
                    int progress = 0;
                    foreach (GameObjectBlueprint gameObjectBlueprint in blueprintList)
                    {
                        currentBlueprint++;
                        progress = currentBlueprint;
                        if (!gameObjectBlueprint.HasTagOrProperty("NoRepair")
                            || (gameObjectBlueprint.HasTagOrProperty("NoEffects") && !gameObjectBlueprint.HasStat("Hitpoints"))
                            || gameObjectBlueprint.HasTagOrProperty("BaseObject")
                            || gameObjectBlueprint.HasTagOrProperty("ExcludeFromDynamicEncounters"))
                        {
                            Loading.SetLoadingStatus(
                                $"{progress} of {totalProgress} | " +
                                $"{gameObjectBlueprint?.DisplayName()?.Strip()} is irreparable...");
                            continue;
                        }
                        sampleItem = GameObject.CreateSample(gameObjectBlueprint.Name);
                        TinkeringHelpers.StripForTinkering(sampleItem);
                        string sampleItemDisplayName = sampleItem?.GetDisplayName(AsIfKnown: true, Short: true, Stripped: true) ?? NULL;
                        try
                        {
                            Loading.SetLoadingStatus(
                                $"{progress} of {totalProgress} | " +
                                $"{sampleItemDisplayName}...");

                            bool lowHP = false;
                            if (sampleItem.GetStat("Hitpoints") is Statistic hitpoints)
                            {
                                hitpoints.Penalty = hitpoints.BaseValue - 1;
                                lowHP = true;
                            }
                            if (!sampleItem.IsBroken())
                            {
                                sampleItem.ForceApplyEffect(new Broken());
                            }
                            if (!lowHP && !sampleItem.IsBroken())
                            {
                                continue;
                            }

                            bitCost = new(Tinkering_Repair.GetRepairCost(sampleItem));

                            tinkerInvoice = new(sampleBep, REPAIR, null, bitCost, sampleItem)
                            {
                                VendorSuppliesBits = true,
                            };

                            Debug.Entry(4, tinkerInvoice.DebugString(), Indent: 0, Toggle: true);
                            tinkerInvoice?.Clear();
                        }
                        finally
                        {
                            ScrapTinkerSample(ref sampleItem);
                        }
                    }
                }
                else
                {
                    Popup.Show($"{nameof(Service)} {Service} is not valid. {nameof(Service)}s are: {BUILD.Quote()}, {MOD.Quote()}, {REPAIR.Quote()}, or {RECHARGE.Quote()}");
                }
            }
            finally
            {
                Loading.SetLoadingStatus(null);
                ScrapTinkerSample(ref sampleBep);
                ScrapTinkerSample(ref sampleItem);
                tinkerInvoice?.Clear();
            }
        }

        [WishCommand(Command = "bust my stuff")]
        public static void BustMyStuff_WishHandler()
        {
            GameObject player = The.Player;
            static bool itemThatCouldBeRepaired(GameObjectBlueprint gameObjectBlueprint)
            {
                return gameObjectBlueprint.InheritsFrom("Item")
                    && !gameObjectBlueprint.InheritsFrom("Corpse")
                    && !gameObjectBlueprint.InheritsFrom("Food")
                    && !gameObjectBlueprint.HasTagOrProperty("NoRepair")
                    && (!gameObjectBlueprint.HasTagOrProperty("NoEffects") || gameObjectBlueprint.HasStat("Hitpoints"))
                    && !gameObjectBlueprint.IsNatural()
                    && !gameObjectBlueprint.HasTagOrProperty("BaseObject")
                    && !gameObjectBlueprint.HasTagOrProperty("ExcludeFromDynamicEncounters")
                    && gameObjectBlueprint.GetPartParameter<string>(nameof(Physics), nameof(Physics.Owner)) is null
                    && (!gameObjectBlueprint.HasPartParameter(nameof(Physics), nameof(Physics.Takeable)) || gameObjectBlueprint.GetPartParameter<bool>(nameof(Physics), nameof(Physics.Takeable)));
            }
            for (int i = 0; i < 50; i++)
            {
                GameObject itemToBust = GameObject.Create(EncountersAPI.GetABlueprintModel(itemThatCouldBeRepaired), Context: "Wish");
                TinkeringHelpers.StripForTinkering(itemToBust);
                player.ReceiveObject(itemToBust, Context: "Wish");
            }
            List<GameObject> playerItems = Event.NewGameObjectList(player.GetInventoryAndEquipment(GO => itemThatCouldBeRepaired(GO.GetBlueprint())));

            if (playerItems.IsNullOrEmpty())
            {
                Popup.Show($"You oughtta get some stuff to bust, first.");
                return;
            }
            try
            {
                foreach (GameObject playerItem in playerItems)
                {
                    GameObject playerItemFromStack = playerItem;
                    for (int i = 0; i < playerItem.Count; i++)
                    {
                        if (playerItem.Count > 1)
                        {
                            playerItemFromStack = playerItem.SplitFromStack();
                        }
                        playerItemFromStack = playerItem.SplitFromStack(); 
                        int high = 120;
                        // Damage, Rust, or Break most of the items in the player inventory and equipment.
                        int roll = (Stat.RandomCosmetic(0, 7000) % high) + 1;

                        bool rusted = false;
                        bool busted = false;
                        if (roll < (high / 3))
                        {
                            rusted = playerItemFromStack.ForceApplyEffect(new Rusted());
                            if (rusted)
                            {
                                continue;
                            }
                        }
                        if (roll < (high / 3) * 2 || !rusted)
                        {
                            busted = playerItemFromStack.ForceApplyEffect(new Broken());
                            if (busted)
                            {
                                continue;
                            }
                        }
                        if ((roll < high || !busted) && playerItemFromStack.GetStat("Hitpoints") is Statistic hitpoints)
                        {
                            int damage = Stat.RandomCosmetic(1, hitpoints.BaseValue - 1);
                            damage = Math.Max(damage, Stat.RandomCosmetic(1, hitpoints.BaseValue - 1));
                            playerItemFromStack.TakeDamage(ref damage, "Cosmic", "you were too busted", "it was too busted", player, player, player, player, player, "from %t willing it to be the case!", true);
                        }
                    }
                }
            }
            finally
            {
                playerItems?.Clear();
            }

            Popup.Show($"Stuff: Busted.");
        }
    }
}
