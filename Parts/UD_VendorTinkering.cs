using System;
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UD_Blink_Mutation;
using UD_Tinkering_Bytes;
using XRL.Language;
using XRL.Messages;
using XRL.Rules;
using XRL.UI;
using XRL.World.Capabilities;
using XRL.World.Parts.Skill;
using XRL.World.Tinkering;
using SerializeField = UnityEngine.SerializeField;

namespace XRL.World.Parts
{
    [Serializable]
    public class UD_VendorTinkering : IScribedPart, IVendorActionEventHandler
    {
        private static bool doDebug = true;

        public const string COMMAND_BUILD = "VendorCommand_Build";
        public const string COMMAND_MOD = "VendorCommand_Mod";

        public bool WantVendorActions => ParentObject != null && ParentObject.HasSkill(nameof(Skill.Tinkering));

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
            RestockScribeChance = 50;
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
            foreach (TinkerData tinkerData in FindKnownRecipes(ParentObject, Filter))
            {
                yield return tinkerData;
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
        public static bool VendorDoMod(GameObject Vendor, GameObject Item, TinkerData TinkerData, int DramsCost)
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
                The.Player.UseDrams(DramsCost);
                Vendor.GiveDrams(DramsCost);
            }
            Item.CheckStack();

            return didMod;
        }

        public override bool WantEvent(int ID, int Cascade)
        {
            return base.WantEvent(ID, Cascade)
                || (WantVendorActions && ID == StockedEvent.ID)
                || (WantVendorActions && ID == GetVendorActionsEvent.ID)
                || (WantVendorActions && ID == VendorActionEvent.ID);
        }
        public override bool HandleEvent(StockedEvent E)
        {
            if (E.Object == ParentObject && WantVendorActions && ScribesKnownRecipesOnRestock)
            {
                GameObject Vendor = E.Object;
                LearnRecipes();
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
                GameObject Vendor = E.Vendor;
                GameObject Item = E.Item;
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
                        if (VendorDoBuild(Vendor, dataDisk.Data, this))
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
                        selectedObject = Item;

                        List<GameObject> vendorHeldDataDiskObjects = Vendor?.Inventory?.GetObjectsViaEventList(
                            GO => GO.TryGetPart(out DataDisk D) 
                            && D.Data.Type == "Mod");

                        List<GameObject> playerHeldDataDiskObjects = player?.Inventory?.GetObjectsViaEventList(
                            GO => GO.TryGetPart(out DataDisk D) 
                            && D.Data.Type == "Mod");

                        if (KnownMods.IsNullOrEmpty() && vendorHeldDataDiskObjects.IsNullOrEmpty() && playerHeldDataDiskObjects.IsNullOrEmpty())
                        {
                            Popup.ShowFail($"{Vendor.T()}{Vendor.GetVerb("do")} not know any item modifications.");
                            return false;
                        }
                        Dictionary<TinkerData, string> applicableRecipes = new();
                        if (!KnownMods.IsNullOrEmpty())
                        {
                            foreach (TinkerData knownMod in KnownMods)
                            {
                                if (ItemModding.ModAppropriate(selectedObject, knownMod))
                                {
                                    applicableRecipes.Add(knownMod, "known recipe");
                                }
                            }
                        }
                        if (!vendorHeldDataDiskObjects.IsNullOrEmpty())
                        {
                            foreach (GameObject vendorHeldDataDiskObject in vendorHeldDataDiskObjects)
                            {
                                if (vendorHeldDataDiskObject.TryGetPart(out DataDisk heldDataDisk)
                                    && !applicableRecipes.ContainsKey(heldDataDisk.Data)
                                    && ItemModding.ModAppropriate(selectedObject, heldDataDisk.Data))
                                {
                                    applicableRecipes.Add(heldDataDisk.Data, "trader inventory");
                                }
                            }
                        }
                        if (!playerHeldDataDiskObjects.IsNullOrEmpty())
                        {
                            foreach (GameObject playerHeldDataDiskObject in vendorHeldDataDiskObjects)
                            {
                                if (playerHeldDataDiskObject.TryGetPart(out DataDisk heldDataDisk)
                                    && !applicableRecipes.ContainsKey(heldDataDisk.Data)
                                    && ItemModding.ModAppropriate(selectedObject, heldDataDisk.Data))
                                {
                                    applicableRecipes.Add(heldDataDisk.Data, "your inventory");
                                }
                            }
                        }
                        if (applicableRecipes.IsNullOrEmpty())
                        {
                            Popup.ShowFail($"{Vendor.T()}{Vendor.GetVerb("do")} not know any item modifications for {selectedObject.t()}.");
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
                            PopupID: "VendorTinkeringApplyModMenu:" + (Item?.IDIfAssigned ?? "(noid)"));
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

                        if (!ItemModding.ModificationApplicable(modRecipe.PartName, selectedObject, Vendor))
                        {
                            Popup.ShowFail($"{selectedObject.T()}{Vendor.GetVerb("can")} not have for {modName} applied.");
                            return false;
                        }

                        int totalCost = (int)E.DramsCost;

                        if (The.Player.GetFreeDrams() < totalCost)
                        {
                            Popup.ShowFail($"You do not have the required {totalCost.Things("dram").Color("C")} to mod this item.");
                        }
                        else if (Popup.ShowYesNo(
                            $"You may mod this item for {totalCost.Things("dram")} of fresh water.") == DialogResult.Yes)
                        {
                        }

                        if (VendorDoMod(Vendor, selectedObject, modRecipe, totalCost))
                        {
                            return true;
                        }
                    }
                }
            }
            return base.HandleEvent(E);
        }
    }
}
