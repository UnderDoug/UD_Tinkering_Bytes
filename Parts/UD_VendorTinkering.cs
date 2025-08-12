using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ConsoleLib.Console;

using XRL.Language;
using XRL.Messages;
using XRL.Rules;
using XRL.UI;
using XRL.World.Anatomy;
using XRL.World.Capabilities;
using XRL.World.Parts.Skill;
using XRL.World.Tinkering;
using XRL.Wish;

using UD_Blink_Mutation;

using UD_Tinkering_Bytes;

using SerializeField = UnityEngine.SerializeField;

namespace XRL.World.Parts
{
    [HasWishCommand]
    [Serializable]
    public class UD_VendorTinkering : IScribedPart, IVendorActionEventHandler
    {
        private static bool doDebug = true;

        public const string COMMAND_BUILD = "VendorCommand_Build";
        public const string COMMAND_MOD = "VendorCommand_Mod";

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
                char bit = BitType.ReverseCharTranslateBit(Bit);
                if (bit == '?') bit = Bit;
                string scrapBlueprint = bit switch
                {
                    'A' => "Scrap Metal",
                    'B' => "Scrap Crystal",
                    'C' => "Scrap Electronics",
                    'D' => "Scrap Energy",
                    '1' => "Scrap 1",
                    '2' => "Scrap 2",
                    '3' => "Scrap 3",
                    '4' => "Scrap 4",
                    '5' => "Scrap 5",
                    '6' => "Scrap 6",
                    '7' => "Scrap 7",
                    '8' => "Scrap 8",
                    _ => null,
                };
                if (!scrapBlueprint.IsNullOrEmpty())
                {
                    scrapItem = GameObjectFactory.Factory.CreateSampleObject(scrapBlueprint);
                }
            }
            return scrapItem;
        }

        public static int GetExamineCost(GameObject Item)
        {
            // from the source (courtesy Books):
            // comment: int Cost = (int) Math.Pow(2, Math.Max(1, ((complexity + difficulty) / 2f) + 1));
            // float x = (complexity + difficulty);
            // int Cost = (int)Math.Max(2, -0.0667 + 1.24 * x + 0.0967 * Math.Pow(x, 2) + 0.0979 * Math.Pow(x, 3));

            float x = Item.GetComplexity() + Item.GetExamineDifficulty();
            return (int)Math.Max(2.0, -0.0667 + 1.24 * (double)x + 0.0967 * Math.Pow(x, 2.0) + 0.0979 * Math.Pow(x, 3.0));
        }

        public static GameObject PickASupplier(GameObject Vendor, GameObject ForObject, string Title, string Message, bool CenterIntro = false)
        {
            List<string> bitSupplyOptions = new()
            {
                $"Use my own if I have them.",
                $"Pay {Vendor.t()} to supply them.",
            };
            List<char> bitSupplyHotkeys = new()
            {
                'a',
                'b',
            };
            List<IRenderable> bitSupplyIcons = new()
            {
                The.Player.RenderForUI(),
                Vendor.RenderForUI()
            };
            return Popup.PickOption(
                Title: Title,
                Intro: Message,
                Options: bitSupplyOptions,
                Hotkeys: bitSupplyHotkeys,
                Icons: bitSupplyIcons,
                IntroIcon: ForObject.RenderForUI(),
                AllowEscape: true,
                CenterIntro: CenterIntro) switch
            {
                0 => The.Player,
                1 => Vendor,
                _ => null,
            };
        }
        public GameObject PickASupplier(GameObject ForObject, string Title, string Message, bool CenterIntro = false)
        {
            return PickASupplier(ParentObject, ForObject, Title, Message, CenterIntro);
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

        public static bool VendorDoBuild(GameObject Vendor, TinkerData TinkerData, UD_VendorTinkering VendorTinkering)
        {
            if (Vendor == null || TinkerData == null)
            {
                Popup.ShowFail($"That trader or recipe doesn't exist (this is an error).");
                return false;
            }
            Popup.Show("Let's pretend this item was tinkered!");
            return true;
        }
        public static bool VendorDoMod(GameObject Vendor, GameObject Item, TinkerData TinkerData)
        {
            if (Vendor == null || Item == null || TinkerData == null)
            {
                Popup.ShowFail($"That trader or item doesn't exist or recipe doesn't exist (this is an error).");
                return false;
            }

            Item.SplitFromStack();

            string itemNameBeforeMod = Item.t(Stripped: true);
            bool didMod = ItemModding.ApplyModification(Item, TinkerData.PartName, out var ModPart, Item.GetTier(), DoRegistration: true, The.Player);
            if (didMod)
            {
                Item.MakeUnderstood();
                SoundManager.PlayUISound("Sounds/Abilities/sfx_ability_tinkerModItem");
                Popup.Show(
                    $"{Vendor.T()}{Vendor.GetVerb("mod")} {itemNameBeforeMod} to be " +
                    $"{(ModPart.GetModificationDisplayName() ?? TinkerData.DisplayName).Color("C")}");

                ItemModding.ApplyModification(Item, TinkerData.PartName, Actor: Vendor);
                if (Item.Equipped == null && Item.InInventory == null)
                {
                    The.Player.ReceiveObject(Item);
                }
            }
            Item.CheckStack();

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
                List<TinkerData> allRecipes = new(TinkerData.TinkerRecipes);
                allRecipes.RemoveAll(TD => !E.Object.HasSkill(DataDisk.GetRequiredSkill(TD.Tier)));
                KnownRecipes ??= new();
                if (!allRecipes.IsNullOrEmpty())
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
                    high = Math.Min(high, allRecipes.Count);
                    int numberToKnow = Stat.Random(low, high);
                    for (int i = 0; i < numberToKnow && !allRecipes.IsNullOrEmpty(); i++)
                    {
                        TinkerData recipeToKnow = allRecipes.DrawRandomToken();
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
                RenderEvent iconObject = GameObjectFactory.Factory.CreateSampleObject("DataDisk").RenderForUI();
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
                        E.Infix.AppendRules("\u0007 ".Color("K") + recipeDisplayName); // iconObject
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
                            E.AddAction("BuildFromDataDisk", "tinker item", COMMAND_BUILD, "tinker", Key: 'T', Priority: -4, DramsCost: 100);
                        }
                        else if (dataDisk.Data.Type == "Mod")
                        {
                            E.AddAction("ModFromDataDisk", "mod an item with tinkering", COMMAND_MOD, "tinkering", Key: 'T', Priority: -4, DramsCost: 100);
                        }
                    }
                    else if (E.Item.InInventory != E.Vendor && !ItemModding.ModKey(E.Item).IsNullOrEmpty())
                    {
                        E.AddAction("ModFromDataDisk", "mod with tinkering", COMMAND_MOD, "tinkering", Key: 'T', Priority: -2, DramsCost: 100);
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
                if (E.Command == COMMAND_BUILD
                    && E.Item != null
                    && E.Item.TryGetPart(out DataDisk dataDisk))
                {
                    int totalCost = (int)E.DramsCost;
                    if (player.GetFreeDrams() < totalCost)
                    {
                        Popup.ShowFail( $"{player.T()}{player.GetVerb("do")} not have the required {totalCost.Things("dram").Color("C")} to tinker this item.");
                    }
                    else if (Popup.ShowYesNo(
                        $"You may tinker this item for {totalCost.Things("dram")} of fresh water.") == DialogResult.Yes)
                    {
                        if (VendorDoBuild(vendor, dataDisk.Data, this))
                        {
                            return true;
                        }
                    }
                }
                if (E.Command == COMMAND_MOD)
                {
                    GameObject selectedObject = null;
                    TinkerData modRecipe = null;
                    string modName = null;
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
                        if (applicableRecipes.IsNullOrEmpty())
                        {
                            Popup.ShowFail($"{vendor.T()}{vendor.GetVerb("do")} not know any item modifications for {selectedObject.t()}.");
                            return false;
                        }
                        List<char> hotkeys = new();
                        List<string> lineItems = new();
                        List<TinkerData> recipes = new();
                        char nextHotkey = 'a';
                        foreach ((TinkerData applicableRecipe, string context) in applicableRecipes)
                        {
                            if (nextHotkey == ' ' || hotkeys.Contains('z'))
                            {
                                nextHotkey = ' ';
                                hotkeys.Add(nextHotkey);
                            }
                            else
                            {
                                hotkeys.Add(nextHotkey++);
                            }
                            string lineItem = $"{applicableRecipe.DisplayName} [{context}]";
                            lineItems.Add(lineItem);
                            recipes.Add(applicableRecipe);
                        }
                        int selectedOption = Popup.PickOption(
                            Title: $"select which item mod to apply",
                            Sound: "Sounds/UI/ui_notification", 
                            Options: lineItems.ToArray(), 
                            Hotkeys: hotkeys.ToArray(),
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
                    }
                    else
                    {
                        modRecipe = dataDisk.Data;
                        modName = $"{modRecipe.DisplayName}";
                        List<GameObject> applicableObjects = player?.Inventory?.GetObjectsViaEventList(
                            GO => ItemModding.ModAppropriate(GO, modRecipe));
                        if (applicableObjects.IsNullOrEmpty())
                        {
                            Popup.ShowFail($"{player.T()}{player.GetVerb("do")} not have any items that can be modified with {modName}.");
                            return false;
                        }
                        selectedObject = Popup.PickGameObject(
                            Title: $"select an item to apply {modName} to",
                            Objects: applicableObjects,
                            AllowEscape: true);

                        if (selectedObject == null)
                        {
                            return false;
                        }
                    }

                    if (selectedObject != null && modRecipe != null)
                    {

                        if (!ItemModding.ModificationApplicable(modRecipe.PartName, selectedObject, vendor))
                        {
                            Popup.ShowFail($"{selectedObject.T()}{vendor.GetVerb("can")} not have {modName} applied.");
                            return false;
                        }

                        GameObject recipeIngredientSupplier = null;
                        bool vendorSuppliesIngredients = false;

                        GameObject ingredientObject = null;
                        GameObject temporaryIngredientObject = null;
                        if (!modRecipe.Ingredient.IsNullOrEmpty())
                        {
                            List<string> recipeIngredientBlueprints = modRecipe.Ingredient.CachedCommaExpansion();

                            string ingredientsMessage = $"This mod requires {recipeIngredientBlueprints.Count.Things("ingredient").Color("Y")} to apply:\n";
                            foreach (string recipeIngredient in recipeIngredientBlueprints)
                            {
                                string ingredientDisplayName = GameObjectFactory.Factory?.GetBlueprint(recipeIngredient)?.DisplayName();
                                if (!ingredientDisplayName.IsNullOrEmpty())
                                {
                                    ingredientsMessage += $"\u0007 {ingredientDisplayName}\n";
                                }
                            }
                            for (int i = 0; i < 25; i++)
                            {
                                ingredientsMessage += Const.HONLY;
                            }

                            recipeIngredientSupplier = PickASupplier(
                                ForObject: selectedObject,
                                Title: "Choose who supplies ingredients",
                                Message: ingredientsMessage);

                            if (recipeIngredientSupplier == null)
                            {
                                return false;
                            }
                            vendorSuppliesIngredients = recipeIngredientSupplier == vendor;
                            foreach (string recipeIngredient in recipeIngredientBlueprints)
                            {
                                ingredientObject = recipeIngredientSupplier.Inventory.FindObjectByBlueprint(recipeIngredient, Temporary.IsNotTemporary);
                                if (ingredientObject != null)
                                {
                                    break;
                                }
                                temporaryIngredientObject ??= recipeIngredientSupplier.Inventory.FindObjectByBlueprint(recipeIngredient);
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
                                    Popup.ShowFail($"{recipeIngredientSupplier.T()}{recipeIngredientSupplier.GetVerb("don't")} have the required ingredient: {ingredientName}!");
                                }
                                return false;
                            }
                        }

                        BitCost bitCost = new();
                        int recipeTier = Tier.Constrain(modRecipe.Tier);

                        int modSlotsUsed = selectedObject.GetModificationSlotsUsed();
                        int noCostMods = selectedObject.GetIntProperty("NoCostMods");

                        int existingModsTier = Tier.Constrain(modSlotsUsed - noCostMods + selectedObject.GetTechTier());

                        bitCost.Increment(BitType.TierBits[recipeTier]);
                        bitCost.Increment(BitType.TierBits[existingModsTier]);

                        GameObject recipeBitSupplier = PickASupplier(
                            ForObject: selectedObject,
                            Title: $"| Bit Cost |".Color("y") + "\n"
                            + $"{bitCost}" + "\n"
                            + $":::".Color("y") + "\n",
                            Message: "Choose who supplies bits",
                            CenterIntro: true);

                        if (recipeBitSupplier == null)
                        {
                            return false;
                        }
                        bool vendorSuppliesBits = recipeBitSupplier == vendor;

                        BitLocker bitSupplierBitLocker = recipeBitSupplier.RequirePart<BitLocker>();

                        ModifyBitCostEvent.Process(recipeBitSupplier, bitCost, "Mod");

                        if (!bitSupplierBitLocker.HasBits(bitCost))
                        {
                            Popup.ShowFail(Message:
                                $"{recipeBitSupplier.T()}{recipeBitSupplier.GetVerb("don't")} have the required <{bitCost}> bits! " +
                                $"{recipeBitSupplier.It}{recipeBitSupplier.GetVerb("have")}:\n\n " +
                                $"{bitSupplierBitLocker.GetBitsString()}");
                            return false;
                        }

                        // Do cost calcs here.

                        int totalCost = (int)E.DramsCost;
                        int baseCost = GetExamineCost(selectedObject);

                        if (!modRecipe.Ingredient.IsNullOrEmpty() && vendorSuppliesIngredients)
                        {
                            // Get each ingredient object, sum their value as though being sold.
                            // store the list for consumption later?
                        }

                        if (vendorSuppliesBits)
                        {
                            // get the value of each bit's equivalent scrap item.
                            // add them to the cost.
                        }

                        // end cost calcs 

                        if (player.GetFreeDrams() < totalCost)
                        {
                            Popup.ShowFail($"You do not have the required {totalCost.Things("dram").Color("C")} to mod this item.");
                            return false;
                        }

                        // write an invoice that includes the ingredients and bits being purchsed, if any.

                        if (Popup.ShowYesNo(
                            $"You may mod this item for {totalCost.Things("dram")} of fresh water.") == DialogResult.Yes)
                        {
                            if (VendorDoMod(vendor, selectedObject, modRecipe))
                            {

                                // deduct ingedients.
                                // deduct bits.

                                player.UseDrams(totalCost);
                                vendor.GiveDrams(totalCost);

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
