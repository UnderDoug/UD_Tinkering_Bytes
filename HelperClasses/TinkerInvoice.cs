using System;
using System.Collections.Generic;
using System.Text;
using UD_Modding_Toolbox;
using XRL;
using XRL.Language;
using XRL.UI;
using XRL.Wish;
using XRL.World;
using XRL.World.Capabilities;
using XRL.World.Effects;
using XRL.World.Parts;
using XRL.World.Tinkering;
using static UD_Modding_Toolbox.Const;
using static UD_Tinkering_Bytes.Utils;

namespace UD_Tinkering_Bytes
{
    [HasWishCommand]
    public class TinkerInvoice
    {
        public GameObject Vendor = null;
        public bool Besties => Vendor != null && Vendor.IsPlayerLed();

        public TinkerData Recipe = null;
        public bool VendorOwnsRecipe = true;

        public GameObject SampleItem = null;

        private bool GotPerformance = false;
        private double Performance = 1.0;

        public string ItemName = "";
        public int NumberMade = 1;
        public double ItemValue = 0;

        public double ExpertiseValue = 0;

        public List<string> IngredientsList = null;
        public double IngredientsValue = 0;
        public bool VendorSuppliesIngredients = true;

        public BitCost BitCost = null;
        public double BitsValue = 0;
        public bool VendorSuppliesBits = true;

        public double LabourValue = 0;

        public double TotalCost = 0;
        public double DepositCost = 0;

        public TinkerInvoice()
        {
        }
        public TinkerInvoice(GameObject Vendor, TinkerData Recipe, BitCost BitCost, bool VendorOwnsRecipe = true, GameObject ApplyModTo = null)
            : this()
        {
            this.Vendor = Vendor;
            this.Recipe = Recipe;
            this.VendorOwnsRecipe = VendorOwnsRecipe;

            if (Recipe?.Type == "Build")
            {
                SampleItem = GameObjectFactory.Factory.CreateSampleObject(Recipe.Blueprint);
                TinkeringHelpers.StripForTinkering(SampleItem);
                ItemName = SampleItem?.GetDisplayName(AsIfKnown: true, Single: true, Short: true)?.Color("y");
                if (SampleItem.TryGetPart(out TinkerItem tinkerItem) && tinkerItem.NumberMade != 1)
                {
                    NumberMade = tinkerItem.NumberMade;
                    ItemName = Grammar.Pluralize(ItemName);
                }
                ItemValue = Math.Round(TradeUI.GetValue(SampleItem, true), 2) * NumberMade;
            }
            else
            if (Recipe?.Type == "Mod")
            {
                SampleItem = ApplyModTo;
                ItemName = SampleItem?.t(AsIfKnown: true, Single: true);
            }

            this.BitCost = BitCost;
        }

        public void Clear()
        {
            if (Recipe?.Type == "Build" && GameObject.Validate(ref SampleItem))
            {
                SampleItem?.Obliterate();
            }
            Vendor = null;
            Recipe = null;
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
            if (Recipe != null && Recipe.Type == "Build")
            {
                SampleItem ??= GameObjectFactory.Factory.CreateSampleObject(Recipe.Blueprint);
                TinkeringHelpers.StripForTinkering(SampleItem);
                return Math.Round(SampleItem.ValueEach / GetPerformance(), 2);
            }
            return  0.0;
        }
        public static double GetExamineCost(GameObject Item)
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
            return Math.Max(2.0, -0.0667 + 1.24 * (double)x + 0.0967 * Math.Pow(x, 2.0) + 0.0979 * Math.Pow(x, 3.0));
        }
        public static double GetExamineCost(GameObjectBlueprint Item)
        {
            return GetExamineCost(Item?.Name);
        }
        public static double GetExamineCost(string Item)
        {
            double value = 0;
            GameObject sampleObject = null;
            try
            {
                value = GetExamineCost(GameObjectFactory.Factory.CreateSampleObject(Item));
            }
            finally
            {
                if (GameObject.Validate(ref sampleObject))
                {
                    sampleObject.Obliterate();
                }
            }
            return value;
        }

        public double GetExpertiseValue()
        {
            if (ExpertiseValue == 0 && VendorOwnsRecipe)
            {
                GameObject sampleDataDiskObject = TinkerData.createDataDisk(Recipe);
                ExpertiseValue = Math.Round(TradeUI.GetValue(sampleDataDiskObject, true) / 2, 2);

                if (GameObject.Validate(ref sampleDataDiskObject))
                {
                    sampleDataDiskObject.Obliterate();
                }
            }
            return ExpertiseValue;
        }

        public double GetLabourValue()
        {
            if (LabourValue == 0)
            {
                LabourValue = Math.Round(GetExamineCost(SampleItem), 2);
            }
            return LabourValue;
        }

        public double GetMarkUpValue()
        {
            return GetLabourValue() + GetExpertiseValue();
        }

        public static double GetIngedientsValueInDrams(List<string> Ingredients, GameObject Vendor = null)
        {
            double totalValue = 0;
            if (!Ingredients.IsNullOrEmpty())
            {
                foreach (string ingredient in Ingredients)
                {
                    GameObject ingredientObject = GameObjectFactory.Factory.CreateSampleObject(ingredient);
                    if (ingredientObject != null)
                    {
                        totalValue += TradeUI.GetValue(ingredientObject, Vendor != null ? true : null);
                    }
                }
            }
            return totalValue;
        }
        public static double GetIngedientsValueInDrams(string Ingredients, GameObject Vendor = null)
        {
            return GetIngedientsValueInDrams(Ingredients?.CachedCommaExpansion(), Vendor);
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
                    scrapItem = GameObjectFactory.Factory.CreateSampleObject(scrapBlueprint);
                }
            }
            return scrapItem;
        }

        public double GetIngredientsValue()
        {
            if (IngredientsValue == 0 && VendorSuppliesIngredients && !Recipe.Ingredient.IsNullOrEmpty())
            {
                IngredientsList = Recipe.Ingredient.CachedCommaExpansion();
                IngredientsValue = Math.Round(GetIngedientsValueInDrams(Recipe.Ingredient), 2);
            }
            return IngredientsValue;
        }

        public static double GetBitsValueInDrams(string BitCost, GameObject Vendor = null)
        {
            double totalValue = 0;
            if (!BitCost.IsNullOrEmpty())
            {
                GameObject scrap = null;
                foreach (char bit in BitCost)
                {
                    scrap = GetBitScrapItem(bit);
                    if (GameObject.Validate(ref scrap))
                    {
                        totalValue += TradeUI.GetValue(scrap, Vendor != null ? true : null);
                        scrap.Obliterate();
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
            return Math.Round(GetExpertiseValue() + GetIngredientsValue() + GetBitsValue(), 2);
        }

        public int GetTotalCost()
        {
            if (TotalCost == 0)
            {
                if (Recipe.Type == "Mod")
                {
                    TotalCost = GetMarkUpValue() + GetMaterialsValue();
                }
                else
                if (Recipe.Type == "Build")
                {
                    TotalCost = GetLabourValue() + (GetItemValue() > GetMaterialsValue() ? GetItemValue() : GetMaterialsValue());
                }

                if (!VendorSuppliesIngredients)
                {
                    TotalCost -= GetIngredientsValue();
                }

                if (!VendorSuppliesBits)
                {
                    TotalCost -= GetBitsValue();
                }
            }
            return Besties ? 0 : (int)Math.Ceiling(TotalCost);
        }

        public int GetDepositCost()
        {
            if (Recipe.Type == "Build"
                && (IngredientsList.IsNullOrEmpty() || GetIngredientsValue() > 0)
                && GetBitsValue() > 0
                && DepositCost == 0)
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

        public override string ToString()
        {
            GameObject player = The.Player;
            StringBuilder SB = Event.NewStringBuilder("Invoice".Color("W")).AppendLine();

            // Description
            if (Recipe.Type == "Build")
            {
                SB.Append("Description: Tinker ").Append($"{NumberMade}x ").Append(ItemName).AppendLine();
            }
            else
            if (Recipe.Type == "Mod")
            {
                SB.Append("Description: Apply ").Append(Recipe.DisplayName.Color("y"));
                SB.Append(" to ").Append(ItemName).AppendLine();
            }
            SB.Append(DividerLine().Color("K")).AppendLine();

            // Item or Material Cost
            if (GetItemValue() > GetMaterialsValue())
            {
                SB.Append("Item Value: ").AppendColored("C", GetItemValue().Things("dram")).AppendLine();
                SB.Append("Labour: ").AppendColored("C", LabourValue.Things("dram")).AppendLine();

            }
            else
            {
                SB.Append("Labour && Expertise: ").AppendColored("C", GetMarkUpValue().Things("dram")).AppendLine();
            }

            // Ingredients
            if (!IngredientsList.IsNullOrEmpty())
            {
                SB.Append($"{IngredientsList.Count.Things("Ingredient")}: ");
                if (VendorSuppliesIngredients)
                {
                    SB.AppendColored("C", IngredientsValue.Things("dram"));
                    if (ItemValue > GetMaterialsValue())
                    {
                        SB.Append(" (").AppendColored("K", "included in item value").Append(")");
                    }
                }
                else
                {
                    SB.Append($"Provided by {player.ShortDisplayName}");
                }
                SB.AppendLine();
                foreach (string ingredient in IngredientsList)
                {
                    string ingredientDisplayName = GameObjectFactory.Factory?.GetBlueprint(ingredient)?.DisplayName();
                    SB.AppendColored("K", "\u0007").Append($" {ingredientDisplayName}\n").AppendLine();
                }
            }

            // Bits
            SB.Append($"Bits <{BitCost}>: ");
            if (VendorSuppliesBits)
            {
                SB.AppendColored("C", BitsValue.Things("dram"));
                if (ItemValue > GetMaterialsValue())
                {
                    SB.Append(" (").AppendColored("K", "included in item value").Append(")");
                }
            }
            else
            {
                SB.Append($"Provided by {player?.ShortDisplayName}");
            }
            SB.AppendLine();

            // Total Cost
            if (VendorSuppliesIngredients || VendorSuppliesBits)
            {
                SB.Append("Total cost to tinker item: ").AppendColored("C", GetTotalCost().Things("dram"));
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
                        SB.Append(" for my ").Append(Vendor.GetWaterRitualLiquidName()).Append(" ").Append(player.siblingTerm).Append("!");
                    }
                }
                SB.AppendLine();
            }

            // Deposit
            if (!Besties
                && GetDepositCost() > 0 
                && GetDepositCost() < GetTotalCost() 
                && player.GetFreeDrams() < GetTotalCost())
            {
                SB.Append(DividerLine().Color("K")).AppendLine();
                SB.Append("Will tinker and hold item for desposit of ").AppendColored("C", GetDepositCost().Things("dram")).AppendLine();
            }

            return Event.FinalizeString(SB);
        }

        public string GetDepositMessage()
        {
            GameObject player = The.Player;
            string thisThese = SampleItem.indicativeProximal;
            string items = NumberMade == 1 ? "item" : "items";
            string thisTheseItems = $"{thisThese} {items}";
            string itThem = NumberMade == 1 ? SampleItem.it : "them";

            string totalCost = GetTotalCost().Things("dram").Color("C");
            string depositCost = GetDepositCost().Things("dram").Color("C") + " of fresh water";
            string itemValue = GetItemValue().Things("dram").Color("C") + " of fresh water";
            string restocks = "2 restocks".Color("g");

            StringBuilder SB = Event.NewStringBuilder();

            SB.Append(player.T()).Append(player.GetVerb("do")).Append(" not have the required ").Append(totalCost);
            SB.Append(" for ").Append(Vendor.T()).Append(" to tinker ").Append(thisTheseItems).Append(".");
            SB.AppendLine().AppendLine();
            SB.Append(Vendor.It).Append(" will tinker ").Append(thisTheseItems).Append(" for a deposit of ").Append(depositCost);
            SB.Append(", however ").Append(Vendor.it).Append(" will hold onto ").Append(itThem);
            SB.Append(" until you have the remaining ").Append(itemValue).Append(".");
            SB.AppendLine().AppendLine();
            SB.Append("Please note: ").Append(Vendor.T()).Append(" will only hold onto this item for ").Append(restocks).Append(".");
            SB.AppendLine().AppendLine();
            SB.Append(this);

            return Event.FinalizeString(SB);
        }

        public static string DebugString(TinkerInvoice TinkerInvoice = null)
        {
            if (TinkerInvoice != null)
            {
                return TinkerInvoice.DebugString();
            }
            else
            {
                StringBuilder SB = Event.NewStringBuilder();
                SB.Append(nameof(Recipe)).Append(",");
                SB.Append(nameof(TinkerInvoice.Recipe.Type)).Append(",");
                SB.Append(nameof(TinkerInvoice.Recipe.Tier)).Append(",");
                SB.Append(nameof(VendorOwnsRecipe)).Append(",");
                SB.Append(nameof(Performance)).Append(",");
                SB.Append(nameof(ItemName)).Append(",");
                SB.Append(nameof(NumberMade)).Append(",");
                SB.Append(nameof(ItemValue)).Append(",");
                SB.Append(nameof(GetExpertiseValue).Replace("Get", "")).Append(",");
                SB.Append(nameof(IngredientsList)).Append("Count").Append(",");
                SB.Append(nameof(IngredientsValue)).Append(",");
                SB.Append(nameof(BitCost)).Append(",");
                SB.Append(nameof(BitsValue)).Append(",");
                SB.Append(nameof(GetMaterialsValue).Replace("Get", "")).Append(",");
                SB.Append(nameof(GetMarkUpValue).Replace("Get", "")).Append(",");
                SB.Append(nameof(LabourValue)).Append(",");
                SB.Append(nameof(TotalCost)).Append(",");
                SB.Append(nameof(DepositCost));

                return Event.FinalizeString(SB);
            }
        }
        public string DebugString()
        {
            StringBuilder SB = Event.NewStringBuilder();

            bool armorStatsOnlyWhenEquipped = SampleItem.HasPropertyOrTag("DisplayArmorStatsOnlyWhenEquipped");
            if (!armorStatsOnlyWhenEquipped)
            {
                SampleItem.SetStringProperty("DisplayArmorStatsOnlyWhenEquipped", "Don't want to see it!");
            }
            string itemName = SampleItem?.GetDisplayName(AsIfKnown: true, Short: true);
            if (!armorStatsOnlyWhenEquipped)
            {
                SampleItem.SetStringProperty("DisplayArmorStatsOnlyWhenEquipped", null, true);
            }
            if (SampleItem.TryGetPart(out MeleeWeapon mw))
            {
                itemName = itemName.Replace(mw.GetSimplifiedStats(XRL.UI.Options.ShowDetailedWeaponStats), "");
            }
            itemName = itemName.Strip();

            SB.Append(Recipe?.Blueprint?.Strip() ?? Recipe?.PartName?.Strip() ?? NULL).Append(",");
            SB.Append(Recipe?.Type ?? NULL).Append(",");
            SB.Append(Recipe?.Tier).Append(",");
            SB.Append(VendorOwnsRecipe).Append(",");
            SB.Append(GetPerformance()).Append(",");
            SB.Append(itemName).Append(",");
            SB.Append(NumberMade).Append(",");
            SB.Append(GetItemValue()).Append(",");
            SB.Append(GetExpertiseValue()).Append(",");
            SB.Append(IngredientsList.IsNullOrEmpty() ? 0 : IngredientsList.Count).Append(",");
            SB.Append(GetIngredientsValue()).Append(",");
            SB.Append(BitCost?.ToString()?.Strip() ?? NULL).Append(",");
            SB.Append(GetBitsValue()).Append(",");
            SB.Append(GetMaterialsValue()).Append(",");
            SB.Append(GetMarkUpValue()).Append(",");
            SB.Append(GetLabourValue()).Append(",");
            SB.Append(GetTotalCost()).Append(",");
            SB.Append(GetDepositCost());

            return Event.FinalizeString(SB);
        }

        public static implicit operator string(TinkerInvoice TinkerInvoice)
        {
            return TinkerInvoice.ToString();
        }

        [WishCommand(Command = "all build invoices")]
        public static void AllBuildRecipesWish()
        {
            GameObject sampleBep = GameObjectFactory.Factory.CreateSampleObject("Bep");

            Debug.Entry(4, DebugString(), Indent: 0, Toggle: true);
            TinkerInvoice tinkerInvoice = null;
            BitCost bitCost;
            try
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

                    tinkerInvoice = new(sampleBep, buildRecipe, bitCost, true);

                    Debug.Entry(4, tinkerInvoice?.DebugString(), Indent: 0, Toggle: true);
                    tinkerInvoice?.Clear();
                }
            }
            finally
            {
                Loading.SetLoadingStatus(null);
                if (GameObject.Validate(ref sampleBep))
                {
                    sampleBep?.Obliterate();
                }
                tinkerInvoice?.Clear();
            }
        }

        [WishCommand(Command = "all mod invoices")]
        public static void AllModRecipesWish()
        {
            GameObject sampleBep = GameObjectFactory.Factory.CreateSampleObject("Bep");

            Debug.Entry(4, DebugString(), Indent: 0, Toggle: true);
            GameObject sampleItem = null;
            TinkerInvoice tinkerInvoice = null;
            BitCost bitCost;
            try
            {
                int blueprintCount = GameObjectFactory.Factory.BlueprintList.Count;
                int recipeCount = TinkerData.TinkerRecipes.Count;
                int totalProgress = blueprintCount * recipeCount;
                int currentBlueprint = 0;
                int currentRecipe = 0;
                int progress = 0;
                foreach (GameObjectBlueprint gameObjectBlueprint in GameObjectFactory.Factory.BlueprintList)
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
                    sampleItem = GameObjectFactory.Factory.CreateSampleObject(gameObjectBlueprint);
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

                            tinkerInvoice = new(sampleBep, modRecipe, bitCost, true, sampleItem);

                            Debug.Entry(4, tinkerInvoice.DebugString(), Indent: 0, Toggle: true);
                            tinkerInvoice?.Clear();
                        }
                    }
                    finally
                    {
                        if (GameObject.Validate(ref sampleItem))
                        {
                            sampleItem.Obliterate();
                        }
                    }
                }
            }
            finally
            {
                Loading.SetLoadingStatus(null);
                if (GameObject.Validate(ref sampleBep))
                {
                    sampleBep.Obliterate();
                }
                tinkerInvoice?.Clear();
            }
        }
    }
}
