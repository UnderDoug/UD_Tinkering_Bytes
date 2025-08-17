using System;
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using ConsoleLib.Console;

using XRL.Language;
using XRL.Rules;
using XRL.UI;
using XRL.Wish;
using XRL.World.Anatomy;
using XRL.World.Capabilities;
using XRL.World.Parts.Skill;
using XRL.World.Tinkering;

using UD_Modding_Toolbox;
using UD_Vendor_Actions;

using static UD_Modding_Toolbox.Const;

using UD_Tinkering_Bytes;

using static UD_Tinkering_Bytes.Utils;

using SerializeField = UnityEngine.SerializeField;

namespace XRL.World.Parts
{
    [HasWishCommand]
    [Serializable]
    public class UD_VendorTinkering : IScribedPart, IVendorActionEventHandler
    {
        private static bool doDebug = true;

        public const string COMMAND_BUILD = "CmdVendorBuild";
        public const string COMMAND_MOD = "CmdVendorMod";

        public bool WantVendorActions => ParentObject != null && ParentObject.HasSkill(nameof(Skill.Tinkering)) && !ParentObject.IsPlayer();

        [SerializeField]
        private List<TinkerData> _KnownRecipes;
        public List<TinkerData> KnownRecipes
        {
            get => _KnownRecipes ??= new();
            set => _KnownRecipes = value;
        }

        public List<TinkerData> KnownBuilds => new(GetKnownRecipes(D => D.Type == "Build"));

        public List<TinkerData> KnownMods => new(GetKnownRecipes(D => D.Type == "Mod"));

        public bool ScribesKnownRecipesOnRestock;

        public int RestockScribeChance;

        public UD_VendorTinkering()
        {
            ScribesKnownRecipesOnRestock = true;
            RestockScribeChance = 15;
            LearnRecipes();
        }

        public List<TinkerData> LearnRecipes()
        {
            List<GameObject> dataDiskObjects = ParentObject?.Inventory?.GetObjectsViaEventList(GO => GO.HasPart<DataDisk>());
            if (!dataDiskObjects.IsNullOrEmpty())
            {
                foreach (GameObject dataDiskObject in dataDiskObjects)
                {
                    LearnRecipe(ParentObject, dataDiskObject, KnownRecipes);
                }
            }
            return KnownRecipes;
        }

        public static bool LearnRecipe(GameObject Vendor, TinkerData TinkerData, List<TinkerData> KnownRecipes, bool CreateDisk = false)
        {
            if (Vendor.HasSkill(DataDisk.GetRequiredSkill(TinkerData.Tier)) && !KnownRecipes.Contains(TinkerData))
            {
                KnownRecipes ??= new();
                KnownRecipes.Add(TinkerData);
            }
            return KnownRecipes.Contains(TinkerData) && (!CreateDisk || ScribeDisk(Vendor, TinkerData));
        }
        public bool LearnRecipe(TinkerData TinkerData, bool CreateDisk = false)
        {
            return LearnRecipe(ParentObject, TinkerData, KnownRecipes, CreateDisk);
        }
        public static bool LearnRecipe(GameObject Vendor, DataDisk DataDisk, List<TinkerData> KnownRecipes, bool CreateDisk = false)
        {
            return LearnRecipe(Vendor, DataDisk.Data, KnownRecipes);
        }
        public bool LearnRecipe(DataDisk DataDisk, bool CreateDisk = false)
        {
            return LearnRecipe(ParentObject, DataDisk, KnownRecipes, CreateDisk);
        }
        public static bool LearnRecipe(GameObject Vendor, GameObject DataDiskObject, List<TinkerData> KnownRecipes, bool CreateDisk = false)
        {
            if (DataDiskObject.TryGetPart(out DataDisk dataDisk))
            {
                return LearnRecipe(Vendor, dataDisk, KnownRecipes);
            }
            return false;
        }
        public bool LearnRecipe(GameObject DataDiskObject, bool CreateDisk = false)
        {
            return LearnRecipe(ParentObject, DataDiskObject, KnownRecipes, CreateDisk);
        }

        public static IEnumerable<TinkerData> FindKnownRecipes(GameObject Vendor, Predicate<TinkerData> Filter = null)
        {
            if (Vendor != null && Vendor.TryGetPart(out UD_VendorTinkering vendorTinkering))
            {
                List<TinkerData> recipeList = new();
                List<GameObject> dataDiskObjects = Vendor?.Inventory?.GetObjectsViaEventList(GO => GO.HasPart<DataDisk>());
                if (!dataDiskObjects.IsNullOrEmpty())
                {
                    foreach (GameObject dataDiskObject in dataDiskObjects)
                    {
                        if (dataDiskObject.TryGetPart(out DataDisk dataDisk)
                            && Vendor.HasSkill(dataDisk.GetRequiredSkill())
                            && (Filter == null || Filter(dataDisk.Data)))
                        {
                            yield return dataDisk.Data;
                        }
                    }
                }
            }
            yield break;
        }

        public IEnumerable<TinkerData> GetKnownRecipes(Predicate<TinkerData> Filter = null)
        {
            foreach (TinkerData tinkerData in KnownRecipes)
            {
                if (Filter == null || Filter(tinkerData))
                {
                    yield return tinkerData;
                }
            }
            yield break;
        }

        public static bool ScribeDisk(GameObject Vendor, TinkerData TinkerData)
        {
            if (Vendor == null || TinkerData == null)
            {
                return false;
            }
            GameObject newDataDisk = TinkerData.createDataDisk(TinkerData);
            newDataDisk.SetIntProperty("_stock", 1);
            TinkeringHelpers.CheckMakersMark(newDataDisk, Vendor, null, null);
            return Vendor.ReceiveObject(newDataDisk);
        }
        public bool ScribeDisk(TinkerData TinkerData)
        {
            return ScribeDisk(ParentObject, TinkerData);
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
            if (Item == null)
            {
                return -1;
            }
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
        public static double GetExamineCost(string Item)
        {
            if (Item.IsNullOrEmpty())
            {
                return -1;
            }
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

        public static BitLocker GiveRandomBits(GameObject Tinker, bool ClearFirst = true)
        {
            if (Tinker.TryGetPart(out BitLocker bitLocker) && ClearFirst)
            {
                Tinker.RemovePart(bitLocker);
            }
            bitLocker = Tinker.RequirePart<BitLocker>();
            List<char> bitList = BitType.BitOrder;
            Dictionary<char, (int low, int high)> bitRanges = new();
            if (!bitList.IsNullOrEmpty())
            {
                foreach (char bit in bitList)
                {
                    bitRanges.Add(bit, (0, 0));
                }
                if (Tinker.HasSkill(nameof(Tinkering_Disassemble)))
                {
                    int upperLimit = bitList.Count / 3;
                    for (int i = 0; i < upperLimit; i++)
                    {
                        char bitIndex = bitList[i];
                        (int low, int high) currentRange = bitRanges[bitIndex];
                        currentRange.low += 25;
                        currentRange.high += 50;
                        bitRanges[bitIndex] = currentRange;
                    }
                }
                if (Tinker.HasSkill(nameof(Tinkering_ReverseEngineer)))
                {
                    int breakPoint = bitList.Count / 3;
                    int upperLimit = breakPoint * 2;
                    for (int i = 0; i < upperLimit; i++)
                    {
                        char bitIndex = bitList[i];
                        (int low, int high) currentRange = bitRanges[bitIndex];
                        if (i < breakPoint)
                        {
                            currentRange.high += 25;
                        }
                        else
                        {
                            currentRange.low += 25;
                            currentRange.high += 50;
                        }
                        bitRanges[bitIndex] = currentRange;
                    }
                }
                if (Tinker.HasSkill(nameof(Tinkering_Tinker1)))
                {
                    int upperLimit = bitList.Count / 3;
                    for (int i = 0; i < upperLimit; i++)
                    {
                        char bitIndex = bitList[i];
                        (int low, int high) currentRange = bitRanges[bitIndex];
                        currentRange.low += 25;
                        currentRange.high += 75;
                        bitRanges[bitIndex] = currentRange;
                    }
                }
                if (Tinker.HasSkill(nameof(Tinkering_Tinker2)))
                {
                    int breakPoint = bitList.Count / 3;
                    int upperLimit = breakPoint * 2;
                    for (int i = 0; i < upperLimit; i++)
                    {
                        char bitIndex = bitList[i];
                        (int low, int high) currentRange = bitRanges[bitIndex];
                        if (i < breakPoint)
                        {
                            currentRange.high += 25;
                        }
                        else
                        {
                            currentRange.low += 25;
                            currentRange.high += 75;
                        }
                        bitRanges[bitIndex] = currentRange;
                    }
                }
                if (Tinker.HasSkill(nameof(Tinkering_Tinker3)))
                {
                    int firstBreakPoint = bitList.Count / 3;
                    int secondBreakPoint = firstBreakPoint * 2;
                    for (int i = 0; i < bitList.Count; i++)
                    {
                        char bitIndex = bitList[i];
                        (int low, int high) currentRange = bitRanges[bitIndex];
                        if (i < firstBreakPoint)
                        {
                            currentRange.low += 50;
                            currentRange.high += 50;
                        }
                        else if (i < secondBreakPoint)
                        {
                            currentRange.low += 25;
                            currentRange.high += 25;
                        }
                        else if (i < bitList.Count - 1)
                        {
                            currentRange.low += 15;
                            currentRange.high += 35;
                        }
                        else
                        {
                            currentRange.high += 5;
                        }
                        bitRanges[bitIndex] = currentRange;
                    }
                }
                List<string> bitsToAdd = new();
                foreach ((char bit, (int low, int high)) in bitRanges)
                {
                    string bits = "";
                    int amountToAdd = Stat.RandomCosmetic(low, high);
                    for (int i = 0; i < amountToAdd; i++)
                    {
                        bits += bit;
                    }
                    if (!bits.IsNullOrEmpty())
                    {
                        bitsToAdd.Add(bits);
                    }
                }
                if (!bitsToAdd.IsNullOrEmpty())
                {
                    foreach (string bits in bitsToAdd)
                    {
                        bitLocker.AddBits(bits);
                    }
                }
            }
            return bitLocker;
        }

        public static GameObject PickASupplier(GameObject Vendor, GameObject ForObject, string Title, string Message = null, bool CenterIntro = false, BitCost BitCost = null, bool Multiple = false)
        {
            BitCost playerBitCost = null;
            BitCost vendorBitCost = null;
            string itThem = Multiple ? "them" : "it";
            if (BitCost != null)
            {
                playerBitCost = new();
                vendorBitCost = new();
                playerBitCost.Import(BitCost.ToBits());
                vendorBitCost.Import(BitCost.ToBits());
                ModifyBitCostEvent.Process(The.Player, playerBitCost, "Build");
                ModifyBitCostEvent.Process(Vendor, vendorBitCost, "Build");
            }
            List<string> supplyOptions = new()
            {
                $"Use my own if I have {(playerBitCost != null ? $"the required <{playerBitCost}> bits" : itThem)}.",
                $"Pay {Vendor.t()} to supply {(playerBitCost != null ? $"the required <{vendorBitCost}> bits" : itThem)}.",
            };
            List<char> supplyHotkeys = new()
            {
                'a',
                'b',
            };
            List<IRenderable> supplyIcons = new()
            {
                The.Player.RenderForUI(),
                Vendor.RenderForUI()
            };
            return Popup.PickOption(
                Title: Title,
                Intro: Message,
                Options: supplyOptions,
                Hotkeys: supplyHotkeys,
                Icons: supplyIcons,
                IntroIcon: ForObject.RenderForUI(),
                AllowEscape: true,
                CenterIntro: CenterIntro) switch
            {
                0 => The.Player,
                1 => Vendor,
                _ => null,
            };
        }
        public GameObject PickASupplier(GameObject ForObject, string Title, string Message = null, bool CenterIntro = false, BitCost BitCost = null, bool Multiple = false)
        {
            return PickASupplier(ParentObject, ForObject, Title, Message, CenterIntro, BitCost, Multiple);
        }

        public static bool PickIngredientSupplier(GameObject Vendor, GameObject ForObject, TinkerData TinkerData, out GameObject RecipeIngredientSupplier)
        {
            RecipeIngredientSupplier = Vendor;

            string itemOrMod = TinkerData.Type == "Build" ? "item" : "mod";
            string craftOrApply = TinkerData.Type == "Build" ? "tinker" : "apply";

            GameObject ingredientObject = null;
            GameObject temporaryIngredientObject = null;
            if (!TinkerData.Ingredient.IsNullOrEmpty())
            {
                string modPrefix = TinkerData.Type == "Mod" ? $"[{"Mod".Color("W")}] " : "";

                string numberMadePrefix = null;
                if (TinkerData.Type == "Build"
                    && ForObject != null
                    && ForObject.TryGetPart(out TinkerItem tinkerItem)
                    && tinkerItem.NumberMade > 1)
                {
                    numberMadePrefix = $"{Grammar.Cardinal(tinkerItem.NumberMade)} ";
                }

                List<string> recipeIngredientBlueprints = TinkerData.Ingredient.CachedCommaExpansion();

                string ingredientsMessage = ""; // $"Ingredient{(recipeIngredientBlueprints.Count > 1 ? "s" : "")}:" + "\n";
                foreach (string recipeIngredient in recipeIngredientBlueprints)
                {
                    string ingredientDisplayName = GameObjectFactory.Factory?.GetBlueprint(recipeIngredient)?.DisplayName();
                    if (!ingredientDisplayName.IsNullOrEmpty())
                    {
                        ingredientsMessage += $"\u0007".Color("K") + $" {ingredientDisplayName}" + "\n";
                    }
                }

                RecipeIngredientSupplier = PickASupplier(
                    Vendor: Vendor,
                    ForObject: ForObject,
                    Title: $"{modPrefix}{numberMadePrefix}{TinkerData.DisplayName}" + "\n"
                    + $"| {recipeIngredientBlueprints.Count.Things("Ingredient")} Required |".Color("Y") + "\n",
                    Message: ingredientsMessage,
                    Multiple: recipeIngredientBlueprints.Count > 1);

                if (RecipeIngredientSupplier == null)
                {
                    return false;
                }

                foreach (string recipeIngredient in recipeIngredientBlueprints)
                {
                    ingredientObject = RecipeIngredientSupplier.Inventory.FindObjectByBlueprint(recipeIngredient, Temporary.IsNotTemporary);
                    if (ingredientObject != null)
                    {
                        break;
                    }
                    temporaryIngredientObject ??= RecipeIngredientSupplier.Inventory.FindObjectByBlueprint(recipeIngredient);
                }
                if (ingredientObject == null)
                {
                    if (temporaryIngredientObject != null)
                    {
                        Popup.ShowFail($"{temporaryIngredientObject.T()}{temporaryIngredientObject.Is} too unstable to craft with.");
                    }
                    else
                    {
                        string ingredientName = "";
                        foreach (string recipeIngredient in recipeIngredientBlueprints)
                        {
                            if (ingredientName != "")
                            {
                                ingredientName += " or ";
                            }
                            ingredientName += TinkeringHelpers.TinkeredItemShortDisplayName(recipeIngredient);
                        }
                        Popup.ShowFail($"{RecipeIngredientSupplier.T()}{RecipeIngredientSupplier.GetVerb("don't")} have the required ingredient: {ingredientName}!");
                    }
                    return false;
                }
            }
            return true;
        }
        public bool PickIngredientSupplier(GameObject ForObject, TinkerData TinkerData, out GameObject RecipeBitSupplier)
        {
            return PickIngredientSupplier(ParentObject, ForObject, TinkerData, out RecipeBitSupplier);
        }

        public static bool PickBitsSupplier(GameObject Vendor, GameObject ForObject, TinkerData TinkerData, BitCost BitCost, out GameObject RecipeBitSupplier, out BitLocker BitSupplierBitLocker)
        {
            BitSupplierBitLocker = null;

            string modPrefix = TinkerData.Type == "Mod" ? $"[{"Mod".Color("W")}] " : "";

            string numberMadePrefix = null;
            if (TinkerData.Type == "Build"
                && ForObject != null
                && ForObject.TryGetPart(out TinkerItem tinkerItem)
                && tinkerItem.NumberMade > 1)
            {
                numberMadePrefix = $"{Grammar.Cardinal(tinkerItem.NumberMade)} ";
            }

            RecipeBitSupplier = PickASupplier(
                Vendor: Vendor,
                ForObject: ForObject,
                Title: $"{modPrefix}{numberMadePrefix}{TinkerData.DisplayName}" + "\n"
                + $"| Bit Cost |".Color("y") + "\n"
                + $"<{BitCost}>" + "\n",
                CenterIntro: true,
                BitCost: BitCost);

            if (RecipeBitSupplier == null)
            {
                return false;
            }

            BitSupplierBitLocker = RecipeBitSupplier.RequirePart<BitLocker>();

            ModifyBitCostEvent.Process(RecipeBitSupplier, BitCost, "Build");

            if (!BitSupplierBitLocker.HasBits(BitCost))
            {
                Popup.ShowFail(Message:
                    $"{RecipeBitSupplier.T()}{RecipeBitSupplier.GetVerb("do")} not have the required <{BitCost}> bits! " +
                    $"{RecipeBitSupplier.It}{RecipeBitSupplier.GetVerb("have")}:\n\n " +
                    $"{BitSupplierBitLocker.GetBitsString()}");
                return false;
            }

            return true;
        }
        public bool PickBitsSupplier(GameObject ForObject, TinkerData TinkerData, BitCost BitCost, out GameObject RecipeBitSupplier, out BitLocker BitSupplierBitLocker)
        {
            return PickBitsSupplier(ParentObject, ForObject, TinkerData, BitCost, out RecipeBitSupplier, out BitSupplierBitLocker);
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

        public static bool VendorDoBuild(GameObject Vendor, TinkerData TinkerData, GameObject RecipeIngredientSupplier, bool VendorKeepsItem)
        {
            if (Vendor == null || TinkerData == null)
            {
                Popup.ShowFail($"That trader or recipe doesn't exist (this is an error).");
                return false;
            }
            GameObject sampleItem = GameObject.CreateSample(TinkerData.Blueprint);
            try
            {
                GameObject player = The.Player;
                sampleItem.MakeUnderstood();
                bool Interrupt = false;
                int tinkeringBonus = GetTinkeringBonusEvent.GetFor(Vendor, sampleItem, "BonusMod", 0, 0, ref Interrupt);
                if (Interrupt)
                {
                    return false;
                }
                GameObject ingredientObject = null;
                if (!TinkerData.Ingredient.IsNullOrEmpty())
                {
                    ingredientObject.SplitStack(1, RecipeIngredientSupplier);
                    if (!RecipeIngredientSupplier.Inventory.FireEvent(Event.New("CommandRemoveObject", "Object", ingredientObject)))
                    {
                        RecipeIngredientSupplier.Fail($"{Vendor.T()} cannot use {ingredientObject.t()} as an ingredient!");
                        ingredientObject.CheckStack();
                        return false;
                    }
                }
                Inventory inventory = VendorKeepsItem ? Vendor.Inventory : player.Inventory;
                TinkerItem tinkerItem = sampleItem.GetPart<TinkerItem>();
                GameObject tinkeredItem = null;
                for (int i = 0; i < Math.Max(tinkerItem.NumberMade, 1); i++)
                {
                    tinkeredItem = GameObject.Create(TinkerData.Blueprint, 0, tinkeringBonus.in100() ? 1 : 0, null, null, null, "Tinkering");
                    TinkeringHelpers.ProcessTinkeredItem(tinkeredItem, player);
                    inventory.AddObject(tinkeredItem);
                }

                string whatWasTinkeredUp = tinkerItem.NumberMade > 1
                    ? $"{Grammar.Cardinal(tinkerItem.NumberMade)} {Grammar.Pluralize(sampleItem.ShortDisplayName)}"
                    : $"{tinkeredItem.an()}";

                string comeBackToPickItUp = "";
                if (VendorKeepsItem)
                {
                    string themIt = $"{(tinkerItem.NumberMade > 1 || sampleItem.IsPlural ? "them" : sampleItem.it)}";
                    comeBackToPickItUp += "\n\n"
                        + $"Once you have the drams for {themIt}, come back to pick {themIt} up!";
                }

                Popup.Show($"{Vendor.T()}{Vendor.GetVerb("tinker")} up {whatWasTinkeredUp}!{comeBackToPickItUp}");

                SoundManager.PlayUISound("sfx_ability_buildRecipeItem");
            }
            finally
            {
                if (GameObject.Validate(ref sampleItem))
                {
                    sampleItem.Obliterate();
                }
            }
            return true;
        }
        public static bool VendorDoMod(GameObject Vendor, GameObject Item, TinkerData TinkerData, GameObject RecipeIngredientSupplier)
        {
            if (Vendor == null || Item == null || TinkerData == null)
            {
                Popup.ShowFail($"That trader, item, or recipe doesn't exist (this is an error).");
                return false;
            }
            GameObject player = The.Player;
            BodyPart bodyPart = player.FindEquippedObject(Item);
            bool didMod = false;
            try
            {
                if (Item.Equipped == player)
                {
                    if (bodyPart == null)
                    {
                        MetricsManager.LogError($"could not find equipping part for {Item.Blueprint} {Item.DebugName} tracked as equipped on player");
                        return true;
                    }
                    Event @event = Event.New("CommandUnequipObject");
                    @event.SetParameter(nameof(BodyPart), bodyPart);
                    @event.SetParameter("EnergyCost", 0);
                    @event.SetParameter("Context", "Tinkering");
                    @event.SetFlag("NoStack", State: true);
                    if (!player.FireEvent(@event))
                    {
                        Popup.ShowFail($"You can't unequip {Item.t()}.");
                        return false;
                    }
                }
                GameObject ingredientObject = null;
                if (!TinkerData.Ingredient.IsNullOrEmpty())
                {
                    ingredientObject.SplitStack(1, RecipeIngredientSupplier);
                    if (!RecipeIngredientSupplier.Inventory.FireEvent(Event.New("CommandRemoveObject", "Object", ingredientObject)))
                    {
                        RecipeIngredientSupplier.Fail($"{Vendor.T()} cannot use {ingredientObject.t()} as an ingredient!");
                        ingredientObject.CheckStack();
                        return false;
                    }
                }

                GameObject modItem = Item.SplitFromStack();
                int itemTier = modItem.GetTier();
                string itemNameBeforeMod = modItem.t(Single: true, Stripped: true);

                didMod = ItemModding.ApplyModification(
                    Object: modItem,
                    ModPartName: TinkerData.PartName,
                    ModPart: out IModification ModPart,
                    Tier: itemTier,
                    DoRegistration: true,
                    Actor: Vendor);

                TinkeringHelpers.CheckMakersMark(modItem, Vendor, ModPart, "Vendor");

                if (didMod)
                {
                    modItem.MakeUnderstood();
                    SoundManager.PlayUISound("Sounds/Abilities/sfx_ability_tinkerModItem");
                    Popup.Show(
                        $"{Vendor.T()}{Vendor.GetVerb("mod")} {itemNameBeforeMod} to be " +
                        $"{(ModPart.GetModificationDisplayName() ?? TinkerData.DisplayName).Color("C")}");

                    if (modItem.Equipped == null && modItem.InInventory == null)
                    {
                        player.ReceiveObject(modItem, Context: "Tinkering");
                    }
                }
                modItem.CheckStack();
                Item.CheckStack();
            }
            catch (Exception x)
            {
                MetricsManager.LogError("Exception applying mod", x);
            }
            finally
            {
                if (GameObject.Validate(ref Item) && bodyPart != null && bodyPart.Equipped == null)
                {
                    Event @event = Event.New("CommandEquipObject");
                    @event.SetParameter("Object", Item);
                    @event.SetParameter("BodyPart", bodyPart);
                    @event.SetParameter("EnergyCost", 0);
                    @event.SetParameter("Context", "Tinkering");
                    player.FireEvent(@event);
                }
            }
            return didMod;
        }

        public override void Register(GameObject Object, IEventRegistrar Registrar)
        {
            Registrar.Register(AllowTradeWithNoInventoryEvent.ID, EventOrder.EARLY);
            base.Register(Object, Registrar);
        }
        public override bool WantEvent(int ID, int Cascade)
        {
            return base.WantEvent(ID, Cascade)
                || (WantVendorActions && ID == AfterObjectCreatedEvent.ID)
                || (WantVendorActions && ID == GetShortDescriptionEvent.ID)
                || (WantVendorActions && ID == StockedEvent.ID)
                || (WantVendorActions && ID == GetVendorActionsEvent.ID)
                || (WantVendorActions && ID == VendorActionEvent.ID);
        }
        public override bool HandleEvent(AllowTradeWithNoInventoryEvent E)
        {
            if (E.Trader != null && ParentObject == E.Trader && WantVendorActions)
            {
                return true;
            }
            return base.HandleEvent(E);
        }
        public override bool HandleEvent(AfterObjectCreatedEvent E)
        {
            if (E.Object != null && ParentObject == E.Object && WantVendorActions)
            {
                GiveRandomBits(E.Object);
                List<GameObjectBlueprint> byteGameObjectBlueprints = GameObjectFactory.Factory.GetBlueprintsInheritingFrom("BaseByte");
                List<string> byteBlueprints = new();
                if (!byteGameObjectBlueprints.IsNullOrEmpty())
                {
                    Debug.Entry(3, $"Spinning up data disks for {ParentObject?.DebugName ?? NULL}...", Indent: 0);
                    foreach (GameObjectBlueprint byteBlueprint in byteGameObjectBlueprints)
                    {
                        Debug.LoopItem(3, $"{byteBlueprint.DisplayName().Strip()}", Indent: 1);
                        byteBlueprints.Add(byteBlueprint.Name);
                    }
                }
                if (!byteBlueprints.IsNullOrEmpty())
                {
                    foreach (TinkerData tinkerDatum in TinkerData.TinkerRecipes)
                    {
                        if (byteBlueprints.Contains(tinkerDatum.Blueprint) && !KnownRecipes.Contains(tinkerDatum))
                        {
                            KnownRecipes.Add(tinkerDatum);
                        }
                    }
                }
                List<TinkerData> avaialable = new(TinkerData.TinkerRecipes);
                avaialable.RemoveAll(TD => !E.Object.HasSkill(DataDisk.GetRequiredSkill(TD.Tier)) && !KnownRecipes.Contains(TD));
                KnownRecipes ??= new();
                if (!avaialable.IsNullOrEmpty())
                {
                    int low = 2;
                    int high = 4;
                    if (E.Object.HasSkill(nameof(Tinkering_Tinker1)))
                    {
                        high++;
                    }
                    if (E.Object.HasSkill(nameof(Tinkering_Tinker2)))
                    {
                        high += 2;
                    }
                    if (E.Object.HasSkill(nameof(Tinkering_Tinker3)))
                    {
                        high += 3;
                    }
                    if (E.Object.HasSkill(nameof(Tinkering_ReverseEngineer)))
                    {
                        high *= 2;
                    }
                    high = Math.Min(high, avaialable.Count);
                    int numberToKnow = Stat.Random(low, high);
                    for (int i = 0; i < numberToKnow && !avaialable.IsNullOrEmpty(); i++)
                    {
                        TinkerData recipeToKnow = avaialable.DrawRandomToken();
                        if (recipeToKnow != null && !KnownRecipes.Contains(recipeToKnow))
                        {
                            KnownRecipes.Add(recipeToKnow);
                        }
                    }
                }
            }
            return base.HandleEvent(E);
        }
        public override bool HandleEvent(GetShortDescriptionEvent E)
        {
            if (E.Object != null && ParentObject == E.Object && WantVendorActions)
            {
                string description = E.Object.GetPart<BitLocker>()?.GetBitsString();
                if (!description.IsNullOrEmpty())
                {
                    E.Infix.AppendRules("Bit Locker:");
                    E.Infix.AppendRules(description);
                }
                if (!KnownRecipes.IsNullOrEmpty())
                {
                    E.Infix.AppendRules("Known Recipes:");
                    foreach (TinkerData knownRecipe in KnownRecipes)
                    {
                        string recipeDisplayName = knownRecipe.DisplayName;
                        if (knownRecipe.Type == "Mod")
                        {
                            recipeDisplayName = $"[{"Mod".Color("W")}] {recipeDisplayName}";
                        }
                        E.Infix.AppendRules("\u0007 ".Color("K") + recipeDisplayName);
                    }
                }
                Skills tinkersSkills = ParentObject?.GetPart<Skills>();
                if (tinkersSkills != null)
                {
                    E.Infix.AppendLine().AppendRules("Tinkering Skills:");
                    foreach (BaseSkill skill in ParentObject.GetPart<Skills>().SkillList)
                    {
                        if (skill.GetType().Name.StartsWith(nameof(Skill.Tinkering)))
                        {
                            E.Infix.AppendRules("\u0007 ".Color("K") + skill.DisplayName.Color("y"));
                        }
                    }
                }
            }
            return base.HandleEvent(E);
        }
        public override bool HandleEvent(StockedEvent E)
        {
            if (E.Object == ParentObject && WantVendorActions)
            {
                GameObject Vendor = E.Object;
                LearnRecipes();
                if (ScribesKnownRecipesOnRestock)
                {
                    if (!KnownRecipes.IsNullOrEmpty())
                    {
                        List<GameObject> knownDataDiskObjects = Vendor?.Inventory?.GetObjectsViaEventList(GO => GO.TryGetPart(out DataDisk dataDisk) && KnownRecipes.Contains(dataDisk.Data));
                        List<TinkerData> inventoryTinkerData = new();
                        foreach (GameObject knownDataDiskObject in knownDataDiskObjects)
                        {
                            if (knownDataDiskObject.TryGetPart(out DataDisk knownDataDisk))
                            {
                                inventoryTinkerData.Add(knownDataDisk.Data);
                            }
                        }
                        foreach (TinkerData knownRecipe in KnownRecipes)
                        {
                            if (!inventoryTinkerData.Contains(knownRecipe) && RestockScribeChance.in100())
                            {
                                ScribeDisk(knownRecipe);
                            }
                        }
                    }
                }
                GiveRandomBits(E.Object);
            }
            return base.HandleEvent(E);
        }
        public virtual bool HandleEvent(GetVendorActionsEvent E)
        {
            if (E.Vendor != null && ParentObject == E.Vendor && WantVendorActions)
            {
                if (E.Item != null)
                {
                    if (E.Item.TryGetPart(out DataDisk dataDisk))
                    {
                        if (dataDisk.Data.Type == "Build")
                        {
                            E.AddAction("BuildFromDataDisk", "tinker item", COMMAND_BUILD, "tinker", Key: 'T', Priority: -4, DramsCost: 100, ClearAndSetUpTradeUI: true);
                        }
                        else if (dataDisk.Data.Type == "Mod")
                        {
                            E.AddAction("ModFromDataDisk", "mod an item with tinkering", COMMAND_MOD, "tinkering", Key: 'T', Priority: -4, DramsCost: 100, ClearAndSetUpTradeUI: true);
                        }
                    }
                    else if (E.Item.InInventory != E.Vendor && !ItemModding.ModKey(E.Item).IsNullOrEmpty())
                    {
                        E.AddAction("ModFromDataDisk", "mod with tinkering", COMMAND_MOD, "tinkering", Key: 'T', Priority: -2, DramsCost: 100, ClearAndSetUpTradeUI: true);
                    }
                }
            }
            return base.HandleEvent(E);
        }
        public virtual bool HandleEvent(VendorActionEvent E)
        {
            if (E.Vendor != null && E.Vendor == ParentObject)
            {
                GameObject vendor = E.Vendor;
                GameObject player = The.Player;

                if (E.Command == COMMAND_MOD || E.Command == COMMAND_BUILD)
                {
                    if (vendor.AreHostilesNearby() && vendor.FireEvent("CombatPreventsTinkering"))
                    {
                        Popup.ShowFail($"{vendor.T()} can't tinker with hostiles nearby!");
                        return false;
                    }
                    if (!vendor.CanMoveExtremities("Tinker", ShowMessage: true, AllowTelekinetic: true))
                    {
                        return false;
                    }
                }

                if (E.Command == COMMAND_BUILD
                    && E.Item != null
                    && E.Item.TryGetPart(out DataDisk dataDisk))
                {
                    GameObject dataDiskObject = E.Item;
                    TinkerData buildRecipe = dataDisk.Data;
                    GameObject sampleItem = GameObjectFactory.Factory.CreateSampleObject(buildRecipe.Blueprint);
                    TinkeringHelpers.StripForTinkering(sampleItem);
                    TinkeringHelpers.ForceToBePowered(sampleItem);

                    if (!vendor.HasSkill(DataDisk.GetRequiredSkill(buildRecipe.Tier)))
                    {
                        Popup.ShowFail($"{vendor.T()}{vendor.GetVerb("do")} not have the required skill: {DataDisk.GetRequiredSkillHumanReadable(buildRecipe.Tier)}!");
                        return false;
                    }
                    try
                    {
                        if (!PickIngredientSupplier(sampleItem, buildRecipe, out GameObject recipeIngredientSupplier))
                        {
                            return false;
                        }
                        bool vendorSuppliesIngredients = recipeIngredientSupplier == vendor;

                        BitCost bitCost = new();
                        bitCost.Import(TinkerItem.GetBitCostFor(buildRecipe.Blueprint));

                        if (!PickBitsSupplier(sampleItem, buildRecipe, bitCost, out GameObject recipeBitSupplier, out BitLocker bitSupplierBitLocker))
                        {
                            return false;
                        }
                        bool vendorSuppliesBits = recipeBitSupplier == vendor;

                        int totalDramsCost = (int)E.DramsCost;

                        double itemDramValue = Math.Round(TradeUI.GetValue(sampleItem, true), 2);

                        double expertiseDramValue = Math.Round(TradeUI.GetValue(dataDiskObject, true) / 4, 2);

                        List<string> ingredientsList = null;
                        double ingredientsDramValue = 0;
                        if (!buildRecipe.Ingredient.IsNullOrEmpty())
                        {
                            ingredientsList = buildRecipe.Ingredient.CachedCommaExpansion();
                            ingredientsDramValue = Math.Round(GetIngedientsValueInDrams(buildRecipe.Ingredient), 2);
                        }

                        double bitsDramValue = Math.Round(GetBitsValueInDrams(bitCost, vendor), 2);

                        double materialsDramValue = Math.Round(expertiseDramValue + ingredientsDramValue + bitsDramValue, 2);

                        double labourDramValue = Math.Round(GetExamineCost(sampleItem), 2);

                        totalDramsCost = (int)Math.Ceiling(labourDramValue + (itemDramValue > materialsDramValue ? itemDramValue : materialsDramValue));

                        if (!vendorSuppliesIngredients)
                        {
                            totalDramsCost -= (int)Math.Floor(ingredientsDramValue);
                        }

                        if (!vendorSuppliesBits)
                        {
                            totalDramsCost -= (int)Math.Floor(bitsDramValue);
                        }

                        int depositDramCost = totalDramsCost - (int)Math.Floor(itemDramValue);

                        if ((!buildRecipe.Ingredient.IsNullOrEmpty() && !vendorSuppliesIngredients) || !vendorSuppliesBits)
                        {
                            depositDramCost = 0;
                        }

                        if (vendor.IsPlayerLed())
                        {
                            itemDramValue = 0;
                            expertiseDramValue = 0;
                            ingredientsDramValue = 0;
                            bitsDramValue = 0;
                            labourDramValue = 0;
                        }

                        string dividerLine = "";
                        for (int i = 0; i < 25; i++)
                        {
                            dividerLine += HONLY;
                        }

                        int numberMade = 1;
                        TinkerItem tinkerItem = sampleItem.GetPart<TinkerItem>();
                        if (tinkerItem != null)
                        {
                            numberMade = tinkerItem.NumberMade;
                        }

                        string sampleItemDisplayName = sampleItem?.t(AsIfKnown: true, Single: true).Color("y");
                        StringBuilder SB = Event.NewStringBuilder("Invoice".Color("W")).AppendLine();
                        SB.Append("Description: Tinker ").Append(numberMade.Things($"x {sampleItemDisplayName}")).AppendLine();
                        SB.Append(dividerLine.Color("K")).AppendLine();

                        // Item or Material Cost
                        if (itemDramValue > materialsDramValue)
                        {
                            SB.Append("Item Value: ").AppendColored("C", itemDramValue.Things("dram")).AppendLine();
                            SB.Append("Labour: ").AppendColored("C", labourDramValue.Things("dram")).AppendLine();
                        }
                        else
                        {
                            SB.Append("Labour && Expertise: ").AppendColored("C", (labourDramValue + expertiseDramValue).Things("dram")).AppendLine();
                        }

                        // Ingredients
                        if (!buildRecipe.Ingredient.IsNullOrEmpty())
                        {
                            SB.Append($"{ingredientsList.Count.Things("Ingredient")}: ");
                            if (vendorSuppliesIngredients)
                            {
                                SB.AppendColored("C", ingredientsDramValue.Things("dram"));
                                if (itemDramValue > materialsDramValue)
                                {
                                    SB.Append(" (").AppendColored("K", "included in item value").Append(")");
                                }
                            }
                            else
                            {
                                SB.Append($"Provided by {player.ShortDisplayName}");
                            }
                            SB.AppendLine();
                            foreach (string ingredient in ingredientsList)
                            {
                                string ingredientDisplayName = GameObjectFactory.Factory?.GetBlueprint(ingredient)?.DisplayName();
                                SB.AppendColored("K", "\u0007").Append($" {ingredientDisplayName}\n").AppendLine();
                            }
                        }

                        // Bits
                        SB.Append($"Bits <{bitCost}>: ");
                        if (vendorSuppliesBits)
                        {
                            SB.AppendColored("C", bitsDramValue.Things("dram"));
                            if (itemDramValue > materialsDramValue)
                            {
                                SB.Append(" (").AppendColored("K", "included in item value").Append(")");
                            }
                        }
                        else
                        {
                            SB.Append($"Provided by {player.ShortDisplayName}");
                        }
                        SB.AppendLine();

                        // Total Cost
                        if (vendorSuppliesIngredients || vendorSuppliesBits)
                        {
                            SB.Append("Total cost to tinker item: ").AppendColored("C", totalDramsCost.Things("dram")).AppendLine();
                        }

                        // Deposit
                        if (depositDramCost > 0 && depositDramCost < totalDramsCost && player.GetFreeDrams() < totalDramsCost)
                        {
                            SB.Append(dividerLine.Color("K")).AppendLine();
                            SB.Append("Will tinker and hold item for desposit of ").AppendColored("C", depositDramCost.Things("dram")).AppendLine();
                        }

                        string invoice = Event.FinalizeString(SB);

                        bool vendorHoldsItem = false;
                        if (player.GetFreeDrams() < totalDramsCost && (depositDramCost == 0 || player.GetFreeDrams() < depositDramCost))
                        {
                            Popup.ShowFail($"{player.T()}{player.GetVerb("do")} not have the required {totalDramsCost.Things("dram").Color("C")} to have {vendor.t()} tinker this item.");
                            Popup.Show(invoice, "Invoice");
                            return false;
                        }
                        if (player.GetFreeDrams() < totalDramsCost
                            && (depositDramCost > 0 && player.GetFreeDrams() > depositDramCost))
                        {
                            DialogResult takeDeposit = Popup.ShowYesNo(
                                $"{player.T()}{player.GetVerb("do")} not have the required {totalDramsCost.Things("dram").Color("C")} to tinker this item.\n\n" +
                                $"{vendor.T()} will tinker this item for {depositDramCost.Things("dram").Color("C")} of fresh water, " +
                                $"however {vendor.it} will hold onto it until you have the remaining {((int)itemDramValue).Things("dram").Color("C")} of fresh water.\n\n" +
                                $"Please note: {vendor.T()} will only hold onto this item for 2 restocks.\n\n{invoice}");
                            if (takeDeposit == DialogResult.Yes)
                            {
                                vendorHoldsItem = true;
                            }
                            else
                            {
                                return false;
                            }
                        }                        
                        if (!vendorHoldsItem
                            && totalDramsCost > 0
                            && Popup.ShowYesNo($"{vendor.T()} will tinker this item for {totalDramsCost.Things("dram")} of fresh water." +
                                $"\n\n{invoice}") != DialogResult.Yes)
                        {
                            return false;
                        }
                        if (VendorDoBuild(vendor, dataDisk.Data, recipeIngredientSupplier, vendorHoldsItem))
                        {
                            bitSupplierBitLocker.UseBits(bitCost);

                            player.UseDrams(vendorHoldsItem ? depositDramCost : totalDramsCost);
                            vendor.GiveDrams(vendorHoldsItem ? depositDramCost : totalDramsCost);

                            player.UseEnergy(1000, "Trade Tinkering Build");
                            vendor.UseEnergy(1000, "Skill Tinkering Build");

                            return true;
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
                if (E.Command == COMMAND_MOD)
                {
                    GameObject selectedObject = null;
                    TinkerData modRecipe = null;
                    string modName = null;
                    bool vendorsRecipe = true;
                    if (!E.Item.TryGetPart(out dataDisk))
                    {
                        selectedObject = E.Item;

                        List<GameObject> vendorHeldDataDiskObjects = vendor?.Inventory?.GetObjectsViaEventList(
                            GO => GO.TryGetPart(out DataDisk D) 
                            && D.Data.Type == "Mod");

                        List<GameObject> playerHeldDataDiskObjects = player?.Inventory?.GetObjectsViaEventList(
                            GO => GO.TryGetPart(out DataDisk D) 
                            && D.Data.Type == "Mod");

                        if (KnownMods.IsNullOrEmpty() && vendorHeldDataDiskObjects.IsNullOrEmpty() && playerHeldDataDiskObjects.IsNullOrEmpty())
                        {
                            Popup.ShowFail($"{vendor.T()}{vendor.GetVerb("do")} not know any item modifications.");
                            return false;
                        }
                        Dictionary<TinkerData, string> applicableRecipes = new();
                        if (!playerHeldDataDiskObjects.IsNullOrEmpty())
                        {
                            foreach (GameObject playerHeldDataDiskObject in playerHeldDataDiskObjects)
                            {
                                if (playerHeldDataDiskObject.TryGetPart(out DataDisk playerHeldDataDisk)
                                    && ItemModding.ModAppropriate(selectedObject, playerHeldDataDisk.Data)
                                    && vendor.HasSkill(playerHeldDataDisk.GetRequiredSkill())
                                    && !applicableRecipes.ContainsKey(playerHeldDataDisk.Data))
                                {
                                    applicableRecipes.Add(playerHeldDataDisk.Data, "your inventory");
                                }
                            }
                        }
                        if (!KnownMods.IsNullOrEmpty())
                        {
                            foreach (TinkerData knownMod in KnownMods)
                            {
                                if (ItemModding.ModAppropriate(selectedObject, knownMod)
                                    && vendor.HasSkill(DataDisk.GetRequiredSkill(knownMod.Tier))
                                    && !applicableRecipes.ContainsKey(knownMod))
                                {
                                    applicableRecipes.Add(knownMod, "known by trader");
                                }
                            }
                        }
                        if (!vendorHeldDataDiskObjects.IsNullOrEmpty())
                        {
                            foreach (GameObject vendorHeldDataDiskObject in vendorHeldDataDiskObjects)
                            {
                                if (vendorHeldDataDiskObject.TryGetPart(out DataDisk vendorHeldDataDisk)
                                    && ItemModding.ModAppropriate(selectedObject, vendorHeldDataDisk.Data)
                                    && vendor.HasSkill(vendorHeldDataDisk.GetRequiredSkill())
                                    && !applicableRecipes.ContainsKey(vendorHeldDataDisk.Data))
                                {
                                    applicableRecipes.Add(vendorHeldDataDisk.Data, "trader inventory");
                                }
                            }
                        }
                        if (applicableRecipes.IsNullOrEmpty())
                        {
                            Popup.ShowFail(
                                $"{vendor.T()}{vendor.GetVerb("do")} not know any item modifications " +
                                $"for {selectedObject.t(Single: true)}.");
                            return false;
                        }
                        List<char> hotkeys = new();
                        List<string> lineItems = new();
                        List<RenderEvent> lineIcons = new();
                        List<TinkerData> recipes = new();
                        char nextHotkey = 'a';
                        BitCost recipeBitCost = new();
                        GameObject sampleDiskObject = null;
                        foreach ((TinkerData applicableRecipe, string context) in applicableRecipes)
                        {
                            recipeBitCost = new();
                            int recipeTier = Tier.Constrain(applicableRecipe.Tier);

                            int modSlotsUsed = selectedObject.GetModificationSlotsUsed();
                            int noCostMods = selectedObject.GetIntProperty("NoCostMods");

                            int existingModsTier = Tier.Constrain(modSlotsUsed - noCostMods + selectedObject.GetTechTier());

                            recipeBitCost.Increment(BitType.TierBits[recipeTier]);
                            recipeBitCost.Increment(BitType.TierBits[existingModsTier]);

                            if (nextHotkey == ' ' || hotkeys.Contains('z'))
                            {
                                nextHotkey = ' ';
                                hotkeys.Add(nextHotkey);
                            }
                            else
                            {
                                hotkeys.Add(nextHotkey++);
                            }
                            string lineItem = $"{applicableRecipe.DisplayName} <{recipeBitCost}> [{context.Color("K")}]";
                            lineItems.Add(lineItem);
                            recipes.Add(applicableRecipe);

                            sampleDiskObject = TinkerData.createDataDisk(applicableRecipe);

                            lineIcons.Add(sampleDiskObject.RenderForUI());
                        }
                        if (GameObject.Validate(ref sampleDiskObject))
                        {
                            sampleDiskObject.Obliterate();
                        }

                        int selectedOption = Popup.PickOption(
                            Title: $"select which item mod to apply",
                            Sound: "Sounds/UI/ui_notification",
                            Options: lineItems.ToArray(),
                            Hotkeys: hotkeys.ToArray(),
                            Icons: lineIcons,
                            Context: selectedObject,
                            IntroIcon: selectedObject.RenderForUI(),
                            AllowEscape: true,
                            PopupID: "VendorTinkeringApplyModMenu:" + (selectedObject?.IDIfAssigned ?? "(noid)"));
                        if (selectedOption < 0)
                        {
                            return false;
                        }
                        modRecipe = recipes[selectedOption];
                        modName = $"{modRecipe.DisplayName}";
                        vendorsRecipe = !lineItems[selectedOption].Contains("your inventory");
                    }
                    else
                    {
                        vendorsRecipe = E.Item.InInventory != player;
                        modRecipe = dataDisk.Data;
                        if (!vendor.HasSkill(DataDisk.GetRequiredSkill(modRecipe.Tier)))
                        {
                            Popup.ShowFail($"{vendor.T()}{vendor.GetVerb("do")} not have the required skill: {DataDisk.GetRequiredSkillHumanReadable(modRecipe.Tier)}!");
                            return false;
                        }
                        modName = $"{modRecipe.DisplayName}";

                        List<GameObject> applicableObjects = Event.NewGameObjectList(List:
                            player?.GetInventoryAndEquipment(GO => ItemModding.ModAppropriate(GO, modRecipe))
                            );

                        if (applicableObjects.IsNullOrEmpty())
                        {
                            Popup.ShowFail($"{player.T()}{player.GetVerb("do")} not have any items that can be modified with {modName}.");
                            return false;
                        }

                        List<char> hotkeys = new();
                        List<string> lineItems = new();
                        List<RenderEvent> lineIcons = new();
                        char nextHotkey = 'a';
                        BitCost recipeBitCost = new();
                        foreach (GameObject applicableObject in applicableObjects)
                        {
                            recipeBitCost = new();
                            int recipeTier = Tier.Constrain(modRecipe.Tier);

                            int modSlotsUsed = applicableObject.GetModificationSlotsUsed();
                            int noCostMods = applicableObject.GetIntProperty("NoCostMods");

                            int existingModsTier = Tier.Constrain(modSlotsUsed - noCostMods + applicableObject.GetTechTier());

                            recipeBitCost.Increment(BitType.TierBits[recipeTier]);
                            recipeBitCost.Increment(BitType.TierBits[existingModsTier]);

                            if (nextHotkey == ' ' || hotkeys.Contains('z'))
                            {
                                nextHotkey = ' ';
                                hotkeys.Add(nextHotkey);
                            }
                            else
                            {
                                hotkeys.Add(nextHotkey++);
                            }
                            string context = "";
                            if (applicableObject.Equipped == player)
                            {
                                context = $" [{"Equipped".Color("K")}]";
                            }
                            string lineItem = $"<{recipeBitCost}> {applicableObject.ShortDisplayNameSingle}{context}";
                            lineItems.Add(lineItem);

                            lineIcons.Add(applicableObject.RenderForUI());
                        }
                        int selectedOption = Popup.PickOption(
                            Title: $"select an item to apply {modName} to",
                            Sound: "Sounds/UI/ui_notification",
                            Options: lineItems.ToArray(),
                            Hotkeys: hotkeys.ToArray(),
                            Icons: lineIcons,
                            Context: E.Item,
                            IntroIcon: E.Item.RenderForUI(),
                            AllowEscape: true,
                            PopupID: "VendorTinkeringApplyModMenu:" + (E.Item?.IDIfAssigned ?? "(noid)"));
                        if (selectedOption < 0)
                        {
                            return false;
                        }
                        selectedObject = applicableObjects[selectedOption];

                        if (selectedObject == null)
                        {
                            return false;
                        }
                    }

                    if (selectedObject != null && modRecipe != null)
                    {
                        if (!ItemModding.ModificationApplicable(modRecipe.PartName, selectedObject, vendor))
                        {
                            Popup.ShowFail($"{selectedObject.T(Single: true)}{vendor.GetVerb("can")} not have {modName} applied.");
                            return false;
                        }

                        bool vendorSuppliesIngredients = false;
                        if (!PickIngredientSupplier(selectedObject, modRecipe, out GameObject recipeIngredientSupplier))
                        {
                            return false;
                        }
                        vendorSuppliesIngredients = recipeIngredientSupplier == vendor;

                        BitCost bitCost = new();
                        int recipeTier = Tier.Constrain(modRecipe.Tier);

                        int modSlotsUsed = selectedObject.GetModificationSlotsUsed();
                        int noCostMods = selectedObject.GetIntProperty("NoCostMods");

                        int existingModsTier = Tier.Constrain(modSlotsUsed - noCostMods + selectedObject.GetTechTier());

                        bitCost.Increment(BitType.TierBits[recipeTier]);
                        bitCost.Increment(BitType.TierBits[existingModsTier]);

                        if (!PickBitsSupplier(selectedObject, modRecipe, bitCost, out GameObject recipeBitSupplier, out BitLocker bitSupplierBitLocker))
                        {
                            return false;
                        }
                        bool vendorSuppliesBits = recipeBitSupplier == vendor;

                        int totalDramsCost = (int)E.DramsCost;

                        GameObject sampleDataDiskObject = TinkerData.createDataDisk(modRecipe);

                        double labourDramValue = Math.Round(GetExamineCost(selectedObject), 2);
                        double expertiseDramValue = vendorsRecipe ? Math.Round(TradeUI.GetValue(sampleDataDiskObject, true) / 2, 2) : 0;
                        double markUpDramValue = labourDramValue + expertiseDramValue;

                        if (GameObject.Validate(ref sampleDataDiskObject))
                        {
                            sampleDataDiskObject.Obliterate();
                        }

                        List<string> ingredientsList = null;
                        double ingredientsDramValue = 0;
                        if (vendorSuppliesIngredients)
                        {
                            ingredientsList = modRecipe.Ingredient.CachedCommaExpansion();
                            ingredientsDramValue = Math.Round(GetIngedientsValueInDrams(modRecipe.Ingredient), 2);
                        }

                        double bitsDramValue = 0;
                        if (vendorSuppliesBits)
                        {
                            bitsDramValue = Math.Round(GetBitsValueInDrams(bitCost, vendor), 2);
                        }

                        if (vendor.IsPlayerLed())
                        {
                            markUpDramValue = 0;
                            bitsDramValue = 0;
                            ingredientsDramValue = 0;
                        }

                        if (markUpDramValue > -1)
                        {
                            totalDramsCost = (int)Math.Ceiling(markUpDramValue + bitsDramValue + ingredientsDramValue);
                        }

                        string dividerLine = "";
                        for (int i = 0; i < 25; i++)
                        {
                            dividerLine += HONLY;
                        }
                        StringBuilder SB = Event.NewStringBuilder("Invoice".Color("W")).AppendLine();
                        SB.Append("Description: Apply ").Append(modRecipe.DisplayName.Color("y"));
                        SB.Append(" to ").Append(selectedObject?.t(AsIfKnown: true, Single: true)).AppendLine();
                        SB.Append(dividerLine.Color("K")).AppendLine();
                       
                        // Labour & Material Costs
                        if (vendorsRecipe)
                        {
                            SB.Append("Labour && Expertise: ");
                        }
                        else
                        {
                            SB.Append("Labour: ");
                        }
                        SB.AppendColored("C", (markUpDramValue).Things("dram")).AppendLine();

                        // Ingredients
                        if (!modRecipe.Ingredient.IsNullOrEmpty())
                        {
                            SB.Append($"{ingredientsList.Count.Things("Ingredient")}: ");
                            if (vendorSuppliesIngredients)
                            {
                                SB.AppendColored("C", (ingredientsDramValue).Things("dram"));
                            }
                            else
                            {
                                SB.Append($"Provided by {player.ShortDisplayName}");
                            }
                            SB.AppendLine();
                            foreach (string ingredient in ingredientsList)
                            {
                                string ingredientDisplayName = GameObjectFactory.Factory?.GetBlueprint(ingredient)?.DisplayName();
                                SB.AppendColored("K", "\u0007").Append($" {ingredientDisplayName}\n").AppendLine();
                            }
                        }

                        // Bits
                        SB.Append($"Bits <{bitCost}>: ");
                        if (vendorSuppliesBits)
                        {
                            SB.AppendColored("C", bitsDramValue.Things("dram"));
                        }
                        else
                        {
                            SB.Append($"Provided by {player.ShortDisplayName}");
                        }
                        SB.AppendLine();


                        // Total Cost
                        if (vendorSuppliesIngredients || vendorSuppliesBits)
                        {
                            SB.Append("Total cost to apply mod: ").AppendColored("C", totalDramsCost.Things("dram")).AppendLine();
                        }

                        string invoice = Event.FinalizeString(SB);

                        if (player.GetFreeDrams() < totalDramsCost)
                        {
                            Popup.ShowFail($"You do not have the required {totalDramsCost.Things("dram").Color("C")} to mod this item.");
                            Popup.Show(invoice, "Invoice");
                            return false;
                        }

                        if (totalDramsCost == 0 
                            || Popup.ShowYesNo($"{vendor.T()} will mod this item for {totalDramsCost.Things("dram")} of fresh water." +
                                $"\n\n{invoice}") == DialogResult.Yes)
                        {
                            if (VendorDoMod(vendor, selectedObject, modRecipe, recipeIngredientSupplier))
                            {
                                bitSupplierBitLocker.UseBits(bitCost);

                                player.UseDrams(totalDramsCost);
                                vendor.GiveDrams(totalDramsCost);

                                player.UseEnergy(1000, "Trade Tinkering Mod");
                                vendor.UseEnergy(1000, "Skill Tinkering Mod");

                                return true;
                            }
                        }
                    }
                }
            }
            return base.HandleEvent(E);
        }

        [WishCommand(Command = "give random bits")]
        public static void GiveRandomBitsWish()
        {
            BitLocker bitLocker = GiveRandomBits(The.Player, false);
            Popup.Show(bitLocker.GetBitsString());
        }

        [WishCommand(Command = "give random new bits")]
        public static void GiveRandomNewBitsWish()
        {
            BitLocker bitLocker = GiveRandomBits(The.Player);
            Popup.Show(bitLocker.GetBitsString());
        }

        [WishCommand(Command = "random bits summary")]
        public static void RandomBitsSummaryWish()
        {
            BitLocker bitLocker = null;
            BitLocker originalBitLocker = The.Player.RequirePart<BitLocker>().DeepCopy(The.Player) as BitLocker;
            for (int i = 0; i < 100; i++)
            {
                bitLocker = GiveRandomBits(The.Player, false);
            }
            foreach (char bit in BitType.BitOrder)
            {
                if (bitLocker.BitStorage.ContainsKey(bit))
                {
                    bitLocker.BitStorage[bit] /= 100;
                }
            }
            Popup.Show(bitLocker.GetBitsString());
            The.Player.RemovePart(bitLocker);
            The.Player.AddPart(originalBitLocker);
        }
    }
}
