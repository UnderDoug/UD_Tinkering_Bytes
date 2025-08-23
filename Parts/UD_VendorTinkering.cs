using ConsoleLib.Console;
using System;
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using XRL.Language;
using XRL.Rules;
using XRL.UI;
using XRL.Wish;
using XRL.World.Anatomy;
using XRL.World.Capabilities;
using XRL.World.Conversations.Parts;
using XRL.World.Parts.Skill;
using XRL.World.Tinkering;

using UD_Modding_Toolbox;
using UD_Vendor_Actions;

using static UD_Modding_Toolbox.Const;


using UD_Tinkering_Bytes;

using static UD_Tinkering_Bytes.Options;
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
        public const string HELD_FOR_PLAYER = "HeldForPlayer";

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

        public static BitLocker GiveRandomBits(GameObject Tinker, bool ClearFirst = true)
        {
            int indent = Debug.LastIndent;
            Debug.Entry(4, 
                $"{nameof(UD_VendorTinkering)}." +
                $"{nameof(GiveRandomBits)}(" +
                $"{nameof(Tinker)}: {Tinker?.DebugName ?? NULL}, " +
                $"{nameof(ClearFirst)}: {ClearFirst})", 
                Indent: indent + 1, Toggle: doDebug);

            if (Tinker.TryGetPart(out BitLocker bitLocker) && ClearFirst)
            {
                Tinker.RemovePart(bitLocker);
            }

            bitLocker = Tinker.RequirePart<BitLocker>();
            List<char> bitList = BitType.BitOrder;
            Dictionary<char, (int low, int high)> bitRanges = new();

            Debug.Entry(4, $"Finding {nameof(bitRanges)} of scrap to disassemble...", Indent: indent + 2, Toggle: doDebug);
            if (!bitList.IsNullOrEmpty())
            {
                Debug.CheckYeh(4, $"{nameof(bitList)} not null or empty", Indent: indent + 3, Toggle: doDebug);
                foreach (char bit in bitList)
                {
                    bitRanges.Add(bit, (0, 0));
                }

                int upperLimit = bitList.Count;
                int firstBreakPoint = upperLimit / 3;
                int secondBreakPoint = upperLimit - (firstBreakPoint / 2);

                bool hasDisassemble = Tinker.HasSkill(nameof(Tinkering_Disassemble));
                bool hasScavenger = Tinker.HasSkill(nameof(Tinkering_Scavenger));
                bool hasReverseEngineer = Tinker.HasSkill(nameof(Tinkering_ReverseEngineer));
                int tinkeringSkill = 0;

                if (Tinker.HasSkill(nameof(Tinkering_Tinker1)))
                {
                    tinkeringSkill++;
                }
                if (Tinker.HasSkill(nameof(Tinkering_Tinker2)))
                {
                    tinkeringSkill++;
                }
                if (Tinker.HasSkill(nameof(Tinkering_Tinker3)))
                {
                    tinkeringSkill++;
                }

                Debug.LoopItem(4, $"{nameof(hasDisassemble)}", $"{hasDisassemble}",
                    Good: hasDisassemble, Indent: indent + 3, Toggle: doDebug);

                Debug.LoopItem(4, $"{nameof(hasReverseEngineer)}", $"{hasDisassemble}",
                    Good: hasReverseEngineer, Indent: indent + 3, Toggle: doDebug);

                Debug.LoopItem(4, $"{tinkeringSkill}] {nameof(tinkeringSkill)}",
                    Indent: indent + 3, Toggle: doDebug);

                Debug.Entry(4, $"Iterating over {nameof(bitList)}...", Indent: indent + 3, Toggle: doDebug);
                for (int i = 0; i < upperLimit; i++)
                {
                    char bitIndex = bitList[i];
                    (int low, int high) currentRange = bitRanges[bitIndex];

                    int bitTier = DataDisk.GetRequiredSkill((int)Math.Max(0, i - 4.0)) switch
                    {
                        nameof(Tinkering_Tinker1) => 1,
                        nameof(Tinkering_Tinker2) => 2,
                        nameof(Tinkering_Tinker3) => 3,
                        _ => 0,
                    };

                    string iterationLabel = i.ToString().PadLeft(upperLimit.ToString().Length);
                    Debug.Divider(4, HONLY, Count: 40, Indent: indent + 3, Toggle: doDebug);
                    Debug.LoopItem(4, 
                        $"{iterationLabel}] " +
                        $"{nameof(bitIndex)}: {bitIndex}, " +
                        $"{nameof(bitTier)}: {bitTier}",
                        Indent: indent + 4, Toggle: doDebug);

                    if (hasDisassemble || hasReverseEngineer)
                    {
                        if (i < firstBreakPoint)
                        {
                            currentRange.low += 5;
                            currentRange.high += 10;
                        }
                        if (i < secondBreakPoint)
                        {
                            currentRange.low += 2;
                            currentRange.high += 5;
                        }
                        if (i == upperLimit && !Math.Max(0, i - 4).ChanceIn(upperLimit - 3))
                        {
                            currentRange.high += 1;
                        }
                        Debug.CheckYeh(4, $"Have Ancillary Skill, {nameof(currentRange)}: {currentRange}",
                            Indent: indent + 5, Toggle: doDebug);
                    }
                    if (hasScavenger && hasDisassemble)
                    {
                        if (i < firstBreakPoint)
                        {
                            currentRange.low += 5;
                            currentRange.high += 10;
                        }
                        if (i < secondBreakPoint)
                        {
                            currentRange.low += 2;
                            currentRange.high += 5;
                        }
                        Debug.CheckYeh(4, $"Have Scavenger, {nameof(currentRange)}: {currentRange}",
                            Indent: indent + 5, Toggle: doDebug);
                    }
                    if (hasReverseEngineer)
                    {
                        if (i == upperLimit && !Math.Max(0, i - 4).ChanceIn(upperLimit - 3))
                        {
                            currentRange.high += 1;
                        }
                        Debug.CheckYeh(4, $"Have Reverse Engineering, {nameof(currentRange)}: {currentRange}",
                            Indent: indent + 5, Toggle: doDebug);
                    }
                    if (bitTier < tinkeringSkill)
                    {
                        currentRange.low += 10;
                        currentRange.high += 20;
                        Debug.CheckYeh(4, 
                            $"{nameof(bitTier)} < {nameof(tinkeringSkill)}, " +
                            $"{nameof(currentRange)}: {currentRange}",
                            Indent: indent + 5, Toggle: doDebug);
                    }
                    if (bitTier == tinkeringSkill)
                    {
                        if (i < secondBreakPoint)
                        {
                            currentRange.low += 2;
                            currentRange.high += 7;
                        }
                        else if (i < upperLimit)
                        {
                            currentRange.low += 1;
                            currentRange.high += 2;
                        }
                        else
                        {
                            currentRange.high += 1;
                        }
                        Debug.CheckYeh(4,
                            $"{nameof(bitTier)} == {nameof(tinkeringSkill)}, " +
                            $"{nameof(currentRange)}: {currentRange}",
                            Indent: indent + 5, Toggle: doDebug);
                    }

                    bitRanges[bitIndex] = currentRange;

                    Debug.Entry(4, $"{nameof(bitRanges)}[{nameof(bitIndex)}]: {bitRanges[bitIndex]}",
                        Indent: indent + 4, Toggle: doDebug);
                }
                Debug.Divider(4, HONLY, Count: 40, Indent: indent + 3, Toggle: doDebug);

                Debug.Entry(4, $"Disassembling bits...", Indent: indent + 3, Toggle: doDebug);
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
                    Debug.LoopItem(4, $"{bit}]" +
                        $"{nameof(low)}: {low}, " +
                        $"{nameof(high)}: {high}, " +
                        $"{nameof(amountToAdd)}: {amountToAdd}", Indent: indent + 4, Toggle: doDebug);
                }

                Debug.Entry(4, $"Throwing bits in Locker...", Indent: indent + 3, Toggle: doDebug);
                if (!bitsToAdd.IsNullOrEmpty())
                {
                    foreach (string bits in bitsToAdd)
                    {
                        Debug.LoopItem(4, $"{nameof(bits)}: {bits[0]} x {bits.Length}", Indent: indent + 4, Toggle: doDebug);
                        bitLocker.AddBits(bits);
                    }
                }
                Debug.Entry(4, $"Scrap found, disassembled, and stored...", Indent: indent + 2, Toggle: doDebug);
            }
            Debug.LastIndent = indent;
            return bitLocker;
        }

        public static bool LearnByteRecipes(GameObject Vendor, List<TinkerData> KnownRecipes)
        {
            if (Vendor == null)
            {
                return false;
            }
            KnownRecipes ??= new();
            List<string> byteBlueprints = new();
            Debug.Entry(3, $"Spinning up byte data disks for {Vendor?.DebugName ?? NULL}...", Indent: 0, Toggle: doDebug);
            foreach (GameObjectBlueprint byteBlueprint in GameObjectFactory.Factory.GetBlueprintsInheritingFrom("BaseByte"))
            {
                Debug.LoopItem(3, $"{byteBlueprint.DisplayName().Strip()}", Indent: 1, Toggle: doDebug);
                byteBlueprints.Add(byteBlueprint.Name);
            }
            bool learned = false;
            if (!byteBlueprints.IsNullOrEmpty())
            {
                foreach (TinkerData tinkerDatum in TinkerData.TinkerRecipes)
                {
                    if (byteBlueprints.Contains(tinkerDatum.Blueprint) && !KnownRecipes.Contains(tinkerDatum))
                    {
                        KnownRecipes.Add(tinkerDatum);
                        learned = true;
                    }
                }
            }
            return learned;
        }
        public static bool LearnGiganticRecipe(GameObject Vendor, List<TinkerData> KnownRecipes)
        {
            if (Vendor == null)
            {
                return false;
            }    
            KnownRecipes ??= new();
            bool learned = false;
            if (Vendor.IsGiganticCreature || Vendor.HasPart("GigantismPlus"))
            {
                Debug.Entry(3, $"Spinning up {nameof(ModGigantic)} data disks for {Vendor?.DebugName ?? NULL}...", Indent: 0, Toggle: doDebug);
                foreach (TinkerData tinkerDatum in TinkerData.TinkerRecipes)
                {
                    if (tinkerDatum.PartName == nameof(ModGigantic))
                    {
                        Debug.CheckYeh(3, $"{nameof(ModGigantic)}: Found", Indent: 1, Toggle: doDebug);
                        KnownRecipes.Add(tinkerDatum);
                        break;
                    }
                }
            }
            return learned;
        }
        public static bool LearnSkillRecipes(GameObject Vendor, List<TinkerData> KnownRecipes)
        {
            if (Vendor == null)
            {
                return false;
            }    
            KnownRecipes ??= new();
            bool learned = false; List<TinkerData> avaialableRecipes = new(TinkerData.TinkerRecipes);
            avaialableRecipes.RemoveAll(r => !Vendor.HasSkill(DataDisk.GetRequiredSkill(r.Tier)) && !KnownRecipes.Contains(r));

            if (!avaialableRecipes.IsNullOrEmpty())
            {
                Debug.Entry(3, $"Spinning up other data disks for {Vendor?.DebugName ?? NULL}...", Indent: 0, Toggle: doDebug);

                Debug.LoopItem(3,
                    $"{nameof(Vendor.HasSkill)}({nameof(Tinkering_Tinker1)})",
                    $"{Vendor.HasSkill(nameof(Tinkering_Tinker1))}",
                    Good: Vendor.HasSkill(nameof(Tinkering_Tinker1)), Indent: 1, Toggle: doDebug);

                Debug.LoopItem(3,
                    $"{nameof(Vendor.HasSkill)}({nameof(Tinkering_Tinker2)})",
                    $"{Vendor.HasSkill(nameof(Tinkering_Tinker2))}",
                    Good: Vendor.HasSkill(nameof(Tinkering_Tinker2)), Indent: 1, Toggle: doDebug);

                Debug.LoopItem(3,
                    $"{nameof(Vendor.HasSkill)}({nameof(Tinkering_Tinker3)})",
                    $"{Vendor.HasSkill(nameof(Tinkering_Tinker3))}",
                    Good: Vendor.HasSkill(nameof(Tinkering_Tinker3)), Indent: 1, Toggle: doDebug);

                Debug.LoopItem(3,
                    $"{nameof(Vendor.HasSkill)}({nameof(Tinkering_ReverseEngineer)})",
                    $"{Vendor.HasSkill(nameof(Tinkering_ReverseEngineer))}",
                    Good: Vendor.HasSkill(nameof(Tinkering_ReverseEngineer)), Indent: 1, Toggle: doDebug);

                int low = 2;
                int high = 4;
                if (Vendor.HasSkill(nameof(Tinkering_Tinker1)))
                {
                    high++;
                }
                if (Vendor.HasSkill(nameof(Tinkering_Tinker2)))
                {
                    low += 1;
                    high += 2;
                }
                if (Vendor.HasSkill(nameof(Tinkering_Tinker3)))
                {
                    low += 1;
                    high += 3;
                }
                if (Vendor.HasSkill(nameof(Tinkering_ReverseEngineer)))
                {
                    low = (int)Math.Floor(low * 1.5);
                    high *= 2;
                }
                high = Math.Min(high, avaialableRecipes.Count);
                low = Math.Min(low, high);
                int numberToKnow = Stat.Random(low, high);

                Debug.LoopItem(3,
                    $"{nameof(low)}: {low}, " +
                    $"{nameof(high)}: {high}, " +
                    $"{nameof(numberToKnow)}: {numberToKnow}",
                    Indent: 1, Toggle: doDebug);

                string vendorRecipeSeed = $"{The.Game.GetWorldSeed()}-{Vendor.ID}";
                Debug.Entry(3,
                    $"Spinning up skill-based data disks for {Vendor?.DebugName ?? NULL} (" +
                    $"{nameof(vendorRecipeSeed)}: {vendorRecipeSeed})...", Indent: 0);

                for (int i = 0; i < numberToKnow && !avaialableRecipes.IsNullOrEmpty(); i++)
                {
                    TinkerData recipeToKnow = avaialableRecipes.DrawSeededToken(vendorRecipeSeed, i, nameof(AfterObjectCreatedEvent));
                    if (recipeToKnow != null && !KnownRecipes.Contains(recipeToKnow))
                    {
                        Debug.LoopItem(3, $"{recipeToKnow.DisplayName.Strip()}", Indent: 1);
                        KnownRecipes.Add(recipeToKnow);
                    }
                }
            }
            return learned;
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
                $"{(Vendor.IsPlayerLed() ? "Ask" : "Pay")} {Vendor.t()} to supply {(playerBitCost != null ? $"the required <{vendorBitCost}> bits" : itThem)}.",
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
                string message = GameText.VariableReplace(
                    $"{RecipeBitSupplier.T()}{RecipeBitSupplier.GetVerb("do")} not have the required <{BitCost}> bits! " +
                    $"{RecipeBitSupplier.It} =verb:have:afterpronoun=:\n\n " +
                    $"{BitSupplierBitLocker.GetBitsString()}", RecipeBitSupplier);
                Popup.ShowFail(Message: message);
                return false;
            }

            return true;
        }
        public bool PickBitsSupplier(GameObject ForObject, TinkerData TinkerData, BitCost BitCost, out GameObject RecipeBitSupplier, out BitLocker BitSupplierBitLocker)
        {
            return PickBitsSupplier(ParentObject, ForObject, TinkerData, BitCost, out RecipeBitSupplier, out BitSupplierBitLocker);
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

                    TinkeringHelpers.StripForTinkering(tinkeredItem);
                    tinkeredItem.MakeUnderstood();
                    tinkeredItem.SetIntProperty("TinkeredItem", 1);
                    TinkeringHelpers.CheckMakersMark(tinkeredItem, Vendor, null, "Tinkering");

                    if (VendorKeepsItem)
                    {
                        tinkeredItem.SetIntProperty(HELD_FOR_PLAYER, 2);
                    }

                    inventory.AddObject(tinkeredItem);
                }

                string singleShortKnownDisplayName = sampleItem.GetDisplayName(AsIfKnown: true, Single: true, Short: true);
                string whatWasTinkeredUp = tinkerItem.NumberMade > 1
                    ? $"{Grammar.Cardinal(tinkerItem.NumberMade)} {Grammar.Pluralize(singleShortKnownDisplayName)}"
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
                || (WantVendorActions && ID == AnimateEvent.ID)
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

                LearnByteRecipes(E.Object, KnownRecipes);

                LearnGiganticRecipe(E.Object, KnownRecipes);

                LearnSkillRecipes(E.Object, KnownRecipes);
            }
            return base.HandleEvent(E);
        }
        public override bool HandleEvent(AnimateEvent E)
        {
            if (E.Object != null && ParentObject == E.Object && WantVendorActions)
            {
                GiveRandomBits(E.Object);

                LearnByteRecipes(E.Object, KnownRecipes);

                LearnGiganticRecipe(E.Object, KnownRecipes);

                LearnSkillRecipes(E.Object, KnownRecipes);
            }
            return base.HandleEvent(E);
        }
        public override bool HandleEvent(GetShortDescriptionEvent E)
        {
            if (E.Object != null && ParentObject == E.Object && WantVendorActions)
            {
                if (DebugBitLockerDebugDescriptions)
                {
                    string bitLockerDescription = E.Object.GetPart<BitLocker>()?.GetBitsString();
                    if (!bitLockerDescription.IsNullOrEmpty())
                    {
                        E.Infix.AppendRules("Bit Locker".Color("M") + ":");
                        E.Infix.AppendRules(bitLockerDescription);
                    }
                }
                if (DebugKnownRecipesDebugDescriptions)
                {
                    if (!KnownRecipes.IsNullOrEmpty())
                    {
                        List<string> byteBlueprints = new(UD_TinkeringByte.GetByteBlueprints());
                        List<string> tierIRecipes = new();
                        List<string> tierIIRecipes = new();
                        List<string> tierIIIRecipes = new();

                        foreach (TinkerData knownRecipe in KnownRecipes)
                        {
                            if (byteBlueprints.Contains(knownRecipe.Blueprint))
                            {
                                continue;
                            }
                            string recipeDisplayName = knownRecipe.DisplayName;
                            if (knownRecipe.Type == "Mod")
                            {
                                recipeDisplayName = $"[{"Mod".Color("W")}] {recipeDisplayName}";
                            }
                            switch (DataDisk.GetRequiredSkill(knownRecipe.Tier))
                            {
                                case nameof(Tinkering_Tinker1):
                                    tierIRecipes.TryAdd(recipeDisplayName);
                                    break;
                                case nameof(Tinkering_Tinker2):
                                    tierIIRecipes.TryAdd(recipeDisplayName);
                                    break;
                                case nameof(Tinkering_Tinker3):
                                    tierIIIRecipes.TryAdd(recipeDisplayName);
                                    break;
                                default:
                                    break;
                            }
                        }

                        E.Infix.AppendRules("Known Recipes".Color("M") + ":");

                        E.Infix.AppendRules("Tier I".Color("W") + ":");
                        if (!tierIRecipes.IsNullOrEmpty())
                        {
                            foreach (string tier1IRecipe in tierIRecipes)
                            {
                                E.Infix.AppendRules("\u0007 ".Color("K") + tier1IRecipe);
                            }
                        }
                        else
                        {
                            E.Infix.AppendRules("\u0007 ".Color("K") + "none");
                        }

                        E.Infix.AppendRules("Tier II".Color("W") + ":");
                        if (!tierIIRecipes.IsNullOrEmpty())
                        {
                            foreach (string tier1IIRecipe in tierIIRecipes)
                            {
                                E.Infix.AppendRules("\u0007 ".Color("K") + tier1IIRecipe);
                            }
                        }
                        else
                        {
                            E.Infix.AppendRules("\u0007 ".Color("K") + "none");
                        }

                        E.Infix.AppendRules("Tier III".Color("W") + ":");
                        if (!tierIIIRecipes.IsNullOrEmpty())
                        {
                            foreach (string tier1IIIRecipe in tierIIIRecipes)
                            {
                                E.Infix.AppendRules("\u0007 ".Color("K") + tier1IIIRecipe);
                            }
                        }
                        else
                        {
                            E.Infix.AppendRules("\u0007 ".Color("K") + "none");
                        }

                        E.Infix.AppendLine();
                    }
                }
                if (DebugTinkerSkillsDebugDescriptions)
                {
                    Skills tinkersSkills = ParentObject?.GetPart<Skills>();
                    if (tinkersSkills != null)
                    {
                        E.Infix.AppendRules("Tinkering Skills".Color("M") + ":");
                        foreach (BaseSkill skill in ParentObject.GetPart<Skills>().SkillList)
                        {
                            if (skill.GetType().Name.StartsWith(nameof(Skill.Tinkering)) || skill.GetType().Name == nameof(UD_Basics))
                            {
                                E.Infix.AppendRules("\u0007 ".Color("K") + skill.DisplayName.Color("y"));
                            }
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
                        List<string> byteBlueprints = new(UD_TinkeringByte.GetByteBlueprints());
                        foreach (TinkerData knownRecipe in KnownRecipes)
                        {
                            if (!byteBlueprints.Contains(knownRecipe.Blueprint) 
                                && !inventoryTinkerData.Contains(knownRecipe) 
                                && RestockScribeChance.in100())
                            {
                                ScribeDisk(knownRecipe);
                            }
                        }
                    }
                }
                GiveRandomBits(E.Object);

                foreach (GameObject item in Vendor.Inventory.GetObjectsViaEventList(GO => GO.HasIntProperty(HELD_FOR_PLAYER)))
                {
                    if (item.ModIntProperty(HELD_FOR_PLAYER, -1, true) < 1)
                    {
                        item.SetIntProperty("_stock", 1);
                    }
                }
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
                    else if (E.Item.InInventory != E.Vendor && !ItemModding.ModKey(E.Item).IsNullOrEmpty() && E.Item.Understood())
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
                TinkerInvoice tinkerInvoice = null;
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
                    bool vendorsRecipe = E.Item.InInventory != player;
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

                        tinkerInvoice = new(vendor, buildRecipe, bitCost, vendorsRecipe)
                        {
                            VendorSuppliesIngredients = vendorSuppliesIngredients,
                            VendorSuppliesBits = vendorSuppliesBits,
                        };

                        int totalDramsCost = tinkerInvoice.GetTotalCost();
                        int depositDramCost = tinkerInvoice.GetDepositCost();
                        double itemDramValue = tinkerInvoice.GetItemValue();

                        bool vendorHoldsItem = false;

                        if (player.GetFreeDrams() < totalDramsCost && (depositDramCost == 0 || player.GetFreeDrams() < depositDramCost))
                        {
                            Popup.ShowFail($"{player.T()}{player.GetVerb("do")} not have the required {totalDramsCost.Things("dram").Color("C")} to have {vendor.t()} tinker this item.");
                            Popup.Show(tinkerInvoice, "Invoice");
                            return false;
                        }
                        if (player.GetFreeDrams() < totalDramsCost
                            && (depositDramCost > 0 && player.GetFreeDrams() > depositDramCost))
                        {
                            DialogResult takeDeposit = Popup.ShowYesNo(tinkerInvoice.GetDepositMessage());
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
                            && Popup.ShowYesNo($"{vendor.T()} will tinker this item for " +
                                $"{totalDramsCost.Things("dram").Color("C")} of fresh water." +
                                $"\n\n{tinkerInvoice}") != DialogResult.Yes)
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
                        tinkerInvoice?.Clear();
                    }
                }
                if (E.Command == COMMAND_MOD)
                {
                    GameObject selectedObject = null;
                    TinkerData modRecipe = null;
                    string modName = null;
                    bool vendorsRecipe = true;
                    try
                    {
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
                                player?.GetInventoryAndEquipment(
                                    GO => ItemModding.ModAppropriate(GO, modRecipe)
                                    && GO.Understood())
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
                                string singleShortDisplayName = applicableObject.GetDisplayName(Single: true, Short: true);
                                string lineItem = $"<{recipeBitCost}> {singleShortDisplayName}{context}";
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
                            if (!ItemModding.ModificationApplicable(modRecipe.PartName, selectedObject))
                            {
                                Popup.ShowFail($"{selectedObject.T(Single: true)} can not have {modName} applied.");
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

                            tinkerInvoice = new(vendor, modRecipe, bitCost, vendorsRecipe, selectedObject)
                            {
                                VendorSuppliesIngredients = vendorSuppliesIngredients,
                                VendorSuppliesBits = vendorSuppliesBits,
                            };

                            int totalDramsCost = tinkerInvoice.GetTotalCost();

                            if (player.GetFreeDrams() < totalDramsCost)
                            {
                                Popup.ShowFail($"You do not have the required {totalDramsCost.Things("dram").Color("C")} to mod this item.");
                                Popup.Show(tinkerInvoice, "Invoice");
                                return false;
                            }

                            if (Popup.ShowYesNo($"{vendor.T()} will mod this item for " +
                                $"{totalDramsCost.Things("dram").Color("C")} of fresh water." +
                                $"\n\n{tinkerInvoice}") == DialogResult.Yes)
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
                    finally
                    {
                        tinkerInvoice?.Clear();
                    }
                    return false;
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
