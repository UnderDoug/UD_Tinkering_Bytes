using System;
using System.Collections.Generic;

using XRL.UI;
using XRL.Language;
using XRL.World.Parts.Skill;
using XRL.World.Tinkering;
using XRL.World.Capabilities;

using static XRL.World.Parts.UD_VendorTinkering;

using UD_Modding_Toolbox;
using UD_Vendor_Actions;

using UD_Tinkering_Bytes;
using Utils = UD_Tinkering_Bytes.Utils;

namespace XRL.World.Parts
{
    [Serializable]
    public class UD_VendorRecipeDrafter : IScribedPart, I_UD_VendorActionEventHandler
    {
        private static bool doDebug = true;

        private Version? MigrateFrom = null;

        public const string COMMAND_DRAFT_RECIPE_DISK = "CmdVendorDraftRecipeDisk";

        public UD_VendorTinkering VendorTinkering => ParentObject?.RequirePart<UD_VendorTinkering>();

        public List<TinkerData> KnownRecipes => VendorTinkering?.KnownRecipes;

        public int DisassembleBonus; // Added v0.1.0

        public int ReverseEngineerBonus; // Added v0.1.0

        public bool ReverseEngineerIgnoresSkillRequirement; // Added v0.1.0

        public bool ScribesReverseEngineeredRecipes; // Added v0.1.0

        public int ReverseEngineeredScribeChance; // Added v0.1.0

        public UD_VendorRecipeDrafter()
        {
            DisassembleBonus = 0;
            ReverseEngineerBonus = 0;
            ReverseEngineerIgnoresSkillRequirement = false;
            ScribesReverseEngineeredRecipes = false;
            ReverseEngineeredScribeChance = 0;
        }

        public static bool IsDiskDraftable(TinkerData TinkerData)
        {
            if (TinkerData.Type == "Mod")
            {
                return true;
            }
            if (TinkerInvoice.CreateTinkerSample(TinkerData.Blueprint) is GameObject sampleObject)
            {
                if (sampleObject.InheritsFrom("BaseByte"))
                {
                    return false;
                }
                if (sampleObject.Understood())
                {
                    return true;
                }
                if (The.Player is GameObject player 
                    && player.HasSkill(nameof(Skill.Tinkering)))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool VendorDoDraft(GameObject Vendor, TinkerInvoice TinkerInvoice)
        {
            if (Vendor == null || TinkerInvoice == null || TinkerInvoice.Item is not GameObject recipeDiskObject)
            {
                Popup.ShowFail("That trader or recipe doesn't exist (this is an error).");
                MetricsManager.LogModError(Utils.ThisMod, "Missing one | " +
                    nameof(Vendor) + ": " + (Vendor == null).ToString() + ", " +
                    nameof(TinkerInvoice) + ": " + (TinkerInvoice == null).ToString() + ", " +
                    nameof(recipeDiskObject) + ": " + (TinkerInvoice?.Item == null).ToString() + ".");
                return false;
            }

            GameObject player = The.Player;
            Inventory inventory = TinkerInvoice.HoldForPlayer ? Vendor.Inventory : player.Inventory;

            if (TinkerInvoice.HoldForPlayer)
            {
                var heldForPlayer = recipeDiskObject.AddPart(new UD_HeldForPlayer(
                    Vendor: Vendor,
                    HeldFor: player,
                    ServiceVerbed: "drafted",
                    DepositPaid: TinkerInvoice.GetDepositCost(),
                    WeeksInstead: !Vendor.HasPart<GenericInventoryRestocker>()));

                Vendor.RegisterEvent(heldForPlayer, StartTradeEvent.ID, Serialize: true);
            }

            inventory.AddObject(recipeDiskObject, Vendor);
            if (!TinkerInvoice.HoldForPlayer)
            {
                recipeDiskObject?.CheckStack();
            }

            string singleShortKnownDisplayName = recipeDiskObject.GetDisplayName(AsIfKnown: true, Single: true, Short: true);
            string whatWasTinkeredUp = "=object.a= " + singleShortKnownDisplayName;

            string itemTinkeredMsg = ("=subject.Name= =subject.verb:draft= up " + whatWasTinkeredUp + "!")
                .StartReplace()
                .AddObject(Vendor)
                .AddObject(recipeDiskObject)
                .ToString();

            string comeBackToPickItUp = "";
            if (TinkerInvoice.HoldForPlayer)
            {
                comeBackToPickItUp += "\n\nOnce =subject.name= =subject.verb:have= the drams for it, come back to pick it up!"
                    .StartReplace()
                    .AddObject(player)
                    .ToString();
            }

            SoundManager.PlayUISound("sfx_ability_buildRecipeItem");
            Popup.Show(itemTinkeredMsg + comeBackToPickItUp);
            return true;
        }

        public override void Register(GameObject Object, IEventRegistrar Registrar)
        {
            Registrar.Register(AllowTradeWithNoInventoryEvent.ID, EventOrder.EARLY);
            base.Register(Object, Registrar);
        }
        public override bool WantEvent(int ID, int Cascade)
        {
            return base.WantEvent(ID, Cascade)
                || ID == AfterGameLoadedEvent.ID
                || ID == UD_GetVendorActionsEvent.ID
                || ID == UD_VendorActionEvent.ID;
        }
        public override bool HandleEvent(AllowTradeWithNoInventoryEvent E)
        {
            if (E.Trader is GameObject vendor 
                && vendor == ParentObject
                && !VendorTinkering.GetKnownRecipes().IsNullOrEmpty())
            {
                return true;
            }
            return base.HandleEvent(E);
        }
        public virtual bool HandleEvent(UD_GetVendorActionsEvent E)
        {
            if (E.Vendor is GameObject vendor
                && vendor == ParentObject
                && E.Item is GameObject item
                && item.TryGetPart(out UD_VendorKnownRecipe knownRecipePart))
            {
                GameObject sampleObject = TinkerInvoice.CreateTinkerSample(knownRecipePart.Data.Blueprint);
                bool isPlayerTinker = The.Player != null && The.Player.HasSkill(nameof(Skill.Tinkering));
                bool isUnderstood = isPlayerTinker || (sampleObject != null && sampleObject.Understood());
                bool isBaseByte = sampleObject != null && sampleObject.InheritsFrom("BaseByte");
                bool isItemMod = knownRecipePart.Data.Type == "Mod";

                if (isItemMod || (isUnderstood && !isBaseByte))
                {
                    E.AddAction(
                        Name: "Draft To Data Disk",
                        Display: "draft to data disk",
                        Command: COMMAND_DRAFT_RECIPE_DISK,
                        Key: 'f',
                        Priority: -5,
                        ClearAndSetUpTradeUI: true);
                }
                TinkerInvoice.ScrapTinkerSample(ref sampleObject);
            }
            return base.HandleEvent(E);
        }
        public virtual bool HandleEvent(UD_VendorActionEvent E)
        {
            if (E.Command == COMMAND_DRAFT_RECIPE_DISK
                && E.Vendor is GameObject vendor
                && vendor == ParentObject
                && E.Item is GameObject item
                && The.Player is GameObject player
                && item.TryGetPart(out UD_VendorKnownRecipe knownRecipePart)
                && knownRecipePart.Data is TinkerData recipeData
                && DraftDataDisk(vendor, recipeData, out GameObject recipeDiskObject, IsStock: false))
            {
                TinkerInvoice tinkerInvoice = null;
                bool purchased = false;
                try
                {
                    BitCost bitCost = new("Cc");

                    string recipeDiskName = recipeDiskObject.GetDisplayName(Context: COMMAND_DRAFT_RECIPE_DISK, Short: true, BaseOnly: true);
                    string bitSupplierTitle = "draft " + recipeDiskName;

                    if (!PickBitsSupplier(
                        Vendor: vendor,
                        ForObject: recipeDiskObject,
                        BitCost: bitCost,
                        RecipeBitSupplier: out GameObject recipeBitSupplier,
                        BitSupplierBitLocker: out BitLocker bitSupplierBitLocker,
                        Title: bitSupplierTitle))
                    {
                        return false;
                    }
                    bool vendorSuppliesBits = recipeBitSupplier == vendor;

                    tinkerInvoice = new(
                        Vendor: vendor,
                        Service: TinkerInvoice.DRAFT,
                        BitCost: bitCost,
                        Item: recipeDiskObject,
                        DepositAllowed: true)
                    {
                        VendorOwnsRecipe = true,
                        VendorSuppliesIngredients = true,
                        VendorSuppliesBits = vendorSuppliesBits,
                    };

                    int totalDramsCost = tinkerInvoice.GetTotalCost();
                    int depositDramCost = tinkerInvoice.GetDepositCost();
                    double itemDramValue = tinkerInvoice.GetItemValue();

                    string dramsCostString = totalDramsCost.Things("dram").Color("C");

                    if ((depositDramCost == 0 || !player.CanAfford(depositDramCost))
                        && CheckTooExpensive(
                            Vendor: vendor,
                            Shopper: player,
                            DramsCost: totalDramsCost,
                            ToDoWhat: "draft this recipe onto a data disk.",
                            TinkerInvoice: tinkerInvoice))
                    {
                        return false;
                    }
                    string holdTimeUnit = vendor.HasPart<GenericInventoryRestocker>() ? "restock" : "week";
                    if (!player.CanAfford(totalDramsCost)
                        && depositDramCost > 0
                        && (CheckTooExpensive(
                            Vendor: vendor,
                            Shopper: player,
                            DramsCost: depositDramCost,
                            ToDoWhat: "draft this recipe onto a data disk and hold it")
                            || !ConfirmTinkerService(
                                Vendor: vendor,
                                Shopper: player,
                                DramsCost: depositDramCost,
                                TinkerInvoice: tinkerInvoice,
                                ExtraBefore: tinkerInvoice.GetDepositMessage("draft", "recipe", " onto a data disk"),
                                ExtraAfter: tinkerInvoice.GetDepositPleaseNote(TimeUnit: holdTimeUnit),
                                SetTinkerInvoiceHold: true)))
                    {
                        return false;
                    }
                    if ((tinkerInvoice.HoldForPlayer
                            || ConfirmTinkerService(
                                Vendor: vendor,
                                Shopper: player,
                                DramsCost: totalDramsCost,
                                DoWhat: "draft this recipe onto a data disk",
                                TinkerInvoice: tinkerInvoice)
                            )
                        && VendorDoDraft(
                            Vendor: vendor,
                            TinkerInvoice: tinkerInvoice))
                    {
                        bitSupplierBitLocker.UseBits(bitCost);

                        player.UseDrams(tinkerInvoice.HoldForPlayer ? depositDramCost : totalDramsCost);
                        vendor.GiveDrams(tinkerInvoice.HoldForPlayer ? depositDramCost : totalDramsCost);

                        player.UseEnergy(1000, "Trade Tinkering Draft");
                        vendor.UseEnergy(1000, "Skill Tinkering Draft");
                        purchased = true;
                        return true;
                    }
                    return false;
                }
                finally
                {
                    if (!purchased)
                    {
                        TinkerInvoice.ScrapTinkerSample(ref recipeDiskObject);
                    }
                    tinkerInvoice?.Clear();
                }
            }
            return base.HandleEvent(E);
        }

        public override void Read(GameObject Basis, SerializationReader Reader)
        {
            var modVersion = Reader.ModVersions[Utils.ThisMod.ID];
            if (modVersion < new Version("0.1.0"))
            {
                MigrateFrom = modVersion;
            }
            base.Read(Basis, Reader);
        }
    }
}
