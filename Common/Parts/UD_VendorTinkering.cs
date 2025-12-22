using ConsoleLib.Console;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using UnityEngine.UI;

using XRL.Language;
using XRL.Rules;
using XRL.UI;
using XRL.Wish;
using XRL.World.Anatomy;
using XRL.World.Capabilities;
using XRL.World.Parts.Mutation;
using XRL.World.Parts.Skill;
using XRL.World.Skills;
using XRL.World.Tinkering;
using static XRL.World.Parts.Skill.Tinkering;

using UD_Modding_Toolbox;
using static UD_Modding_Toolbox.Const;
using Debug = UD_Modding_Toolbox.Debug;

using UD_Vendor_Actions;

using UD_Tinkering_Bytes;
using static UD_Tinkering_Bytes.Options;
using Startup = UD_Tinkering_Bytes.Startup;
using Utils = UD_Tinkering_Bytes.Utils;

namespace XRL.World.Parts
{
    [AlwaysHandlesVendor_UD_VendorActions]
    [HasWishCommand]
    [Serializable]
    public class UD_VendorTinkering 
        : IScribedPart
        , I_UD_VendorActionEventHandler
        , IModEventHandler<UD_EndTradeEvent>
    {
        // TODO:
        // debug wish for recipe skill reqs.

        private static bool doDebug = true;

        private static bool SaveStartedWithVendorActions => Startup.SaveStartedWithVendorActions;
        private static bool SaveStartedWithTinkeringBytes => Startup.SaveStartedWithTinkeringBytes;
        
        private Version? MigrateFrom = null;

        private static TimeSpan DebugWishBenchmark = TimeSpan.Zero;

        public const string COMMAND_BUILD = "CmdVendorBuild";
        public const string COMMAND_MOD = "CmdVendorMod";
        public const string COMMAND_IDENTIFY_BY_DATADISK = "CmdVendorExamineDataDisk";
        public const string COMMAND_IDENTIFY_SCALING = "CmdVendorExamineScaling";
        public const string COMMAND_RECHARGE_SCALING = "CmdVendorRechargeScaling";
        public const string COMMAND_REPAIR_SCALING = "CmdVendorRepairScaling";

        public const string BITLOCK_DISPLAY = "UD_BitLocker_Display";

        public const string CAN_IDENTIFY_STOCK = "CanIdentifyStock";

        private List<TinkerData> _KnownRecipes;
        public List<TinkerData> KnownRecipes
        {
            get => _KnownRecipes ??= new();
            set => _KnownRecipes = value;
        }

        private List<TinkerData> _InstalledRecipes;
        public List<TinkerData> InstalledRecipes
        {
            get => _InstalledRecipes ??= new();
            set => _InstalledRecipes = value;
        }

        public List<TinkerData> KnownBuilds => new(GetKnownRecipes(D => D.Type == "Build"));

        public List<TinkerData> KnownMods => new(GetKnownRecipes(D => D.Type == "Mod"));

        public bool ScribesKnownRecipesOnRestock;

        public int RestockScribeChance;

        public bool LearnsOneStockedDataDiskOnRestock; // added v0.1.0

        public int RestockLearnChance; // added v0.1.0

        public bool MatchMakersMarkToDetailColor; // added v0.1.0

        private bool CanRestockBits; // added v0.1.0

        private bool TinkerInitialized; // added v0.1.0

        public UD_VendorTinkering()
        {
            ScribesKnownRecipesOnRestock = true;
            RestockScribeChance = 10;

            LearnsOneStockedDataDiskOnRestock = true;
            RestockLearnChance = 25;

            MatchMakersMarkToDetailColor = false;

            CanRestockBits = true;
        }

        public override void AddedAfterCreation()
        {
            base.AddedAfterCreation();
            if (!SaveStartedWithVendorActions || !SaveStartedWithTinkeringBytes)
            {
                int indent = Debug.LastIndent;
                Debug.Entry(4, $"{nameof(UD_VendorTinkering)}.{nameof(AddedAfterCreation)}()", Indent: indent + 1, Toggle: doDebug);

                InitializeVendorTinker();

                Debug.LastIndent = indent;
            }
        }

        public static bool IsStockDataDisk(GameObject GO) => GO.HasPart<DataDisk>() && GO.HasProperty("_stock");
        public bool LearnFromOneDataDisk()
        {
            List<GameObject> dataDiskObjects = ParentObject?.Inventory?.GetObjectsViaEventList(IsStockDataDisk);
            if (!dataDiskObjects.IsNullOrEmpty())
            {
                bool ignoreSkillRequirement = false;
                if (ParentObject != null && ParentObject.TryGetPart(out UD_VendorDisassembly vendorDisassembly))
                {
                    ignoreSkillRequirement = vendorDisassembly.ReverseEngineerIgnoresSkillRequirement;
                }
                dataDiskObjects.ShuffleInPlace();
                foreach (GameObject dataDiskObject in dataDiskObjects)
                {
                    if (LearnFromDataDisk(
                        Vendor: ParentObject,
                        DataDiskObject: dataDiskObject,
                        KnownRecipes: KnownRecipes,
                        IgnoreSkillRequirement: ignoreSkillRequirement,
                        ConsumeDisk: true))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public static bool LearnTinkerData(
            GameObject Vendor,
            TinkerData TinkerData,
            List<TinkerData> KnownRecipes,
            bool IgnoreSkillRequirement = false,
            bool CreateDisk = false)
        {
            KnownRecipes ??= new();
            if ((IgnoreSkillRequirement || Vendor.HasSkill(DataDisk.GetRequiredSkill(TinkerData.Tier)))
                && !KnownRecipes.Any(datum => datum.IsSameDatumAs(TinkerData)))
            {
                KnownRecipes.Add(TinkerData);
                if (TinkerData.PartName == nameof(ModMasterwork) || TinkerData.PartName == nameof(ModLegendary))
                {
                    if (!Vendor.TryGetPart(out HasMakersMark hasMakersMark))
                    {
                        string mark = MakersMark.Generate();
                        string markColor;
                        Random vendorMarkColorRnd = Vendor.GetSeededRandom(nameof(UD_VendorTinkering) + nameof(MakersMark));
                        if (Vendor.TryGetPart(out UD_VendorTinkering vendorTinkering)
                            && vendorTinkering.MatchMakersMarkToDetailColor)
                        {
                            markColor = Vendor?.Render?.DetailColor;
                        }
                        else
                        if (60.in100())
                        {
                            markColor = Crayons.GetRandomColor(vendorMarkColorRnd);
                        }
                        else
                        {
                            markColor = Crayons.GetRandomDarkColor(vendorMarkColorRnd);
                        }
                        hasMakersMark = Vendor.RequirePart<HasMakersMark>();
                        hasMakersMark.Mark = mark;
                        hasMakersMark.Color = markColor;

                        // string learnedMasterworkMsg = 
                            ("=subject.Name= =subject.verb:have= learned how to tinker " + TinkerData.DisplayName + " items! " +
                            "=subject.Subjective= will henceforth mark =subject.possessive= tinkering with " +
                            "{{" + markColor + "|" + mark + "}} to indicate the quality of crafts=subject.ud_personTerm=ship.")
                                .StartReplace()
                                .AddObject(Vendor)
                                .EmitMessage(Source: Vendor, Color: 'Y', UsePopup: true);

                        // EmitMessage(Vendor, Msg: learnedMasterworkMsg, Color: 'Y');
                        SoundManager.PlayUISound("Sounds/UI/ui_notification");
                    }
                }
            }
            return KnownRecipes.Contains(TinkerData) && (!CreateDisk || DraftDataDisk(Vendor, TinkerData, out _));
        }
        public bool LearnTinkerData(
            TinkerData TinkerData,
            bool IgnoreSkillRequirement = false,
            bool CreateDisk = false)
        {
            return LearnTinkerData(
                Vendor: ParentObject,
                TinkerData: TinkerData,
                KnownRecipes: KnownRecipes,
                IgnoreSkillRequirement: IgnoreSkillRequirement,
                CreateDisk: CreateDisk);
        }
        public static bool LearnDataDisk(
            GameObject Vendor,
            DataDisk DataDisk,
            List<TinkerData> KnownRecipes,
            bool IgnoreSkillRequirement = false)
        {
            return LearnTinkerData(
                Vendor: Vendor,
                TinkerData: DataDisk.Data,
                KnownRecipes: KnownRecipes,
                IgnoreSkillRequirement: IgnoreSkillRequirement,
                CreateDisk: false);
        }
        public bool LearnDataDisk(DataDisk DataDisk, bool IgnoreSkillRequirement = false)
        {
            return LearnDataDisk(
                Vendor: ParentObject,
                DataDisk: DataDisk,
                IgnoreSkillRequirement: IgnoreSkillRequirement,
                KnownRecipes: KnownRecipes);
        }
        public static bool LearnFromDataDisk(
            GameObject Vendor,
            GameObject DataDiskObject,
            List<TinkerData> KnownRecipes,
            bool IgnoreSkillRequirement = false,
            bool ConsumeDisk = false)
        {
            if (DataDiskObject.TryGetPart(out DataDisk dataDisk)
                && LearnDataDisk(Vendor, dataDisk, KnownRecipes, IgnoreSkillRequirement))
            {
                if (ConsumeDisk)
                {
                    DataDiskObject.Destroy();
                }
                return true;
            }
            return false;
        }
        public bool LearnFromDataDisk(
            GameObject DataDiskObject,
            bool IgnoreSkillRequirement = false,
            bool ConsumeDisk = false)
        {
            return LearnFromDataDisk(
                Vendor: ParentObject,
                DataDiskObject: DataDiskObject,
                KnownRecipes: KnownRecipes,
                IgnoreSkillRequirement: IgnoreSkillRequirement,
                ConsumeDisk: ConsumeDisk);
        }

        public static IEnumerable<TinkerData> FindTinkerableDataDiskData(
            GameObject Vendor,
            bool IgnoreSkillRequirement = false,
            Predicate<TinkerData> Filter = null)
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
                            && (IgnoreSkillRequirement || Vendor.HasSkill(dataDisk.GetRequiredSkill()))
                            && (Filter == null || Filter(dataDisk.Data)))
                        {
                            yield return dataDisk.Data;
                        }
                    }
                }
            }
            yield break;
        }

        public IEnumerable<TinkerData> GetKnownRecipes(Predicate<TinkerData> Filter = null, bool IncludeInstalled = true)
        {
            Debug.GetIndent(out int indent);
            bool doDebug = false;
            Debug.Entry(3, 
                $"{nameof(UD_VendorTinkering)}.{nameof(GetKnownRecipes)}(" +
                $"{nameof(Filter)}? {Filter != null}, " +
                $"{nameof(IncludeInstalled)}: {IncludeInstalled})", 
                Indent: indent + 1, Toggle: doDebug);
            Debug.CheckYeh(3, $"KnownRecipes", Indent: indent + 2, Toggle: doDebug);
            foreach (TinkerData tinkerData in KnownRecipes)
            {
                bool recipePassedFilter = Filter == null || Filter(tinkerData);
                bool recipeNotSupersededByInstalled = !IncludeInstalled || !InstalledRecipes.Any(r => r.IsSameDatumAs(tinkerData));
                bool includeRecipe = recipePassedFilter && recipeNotSupersededByInstalled;
                Debug.CheckYeh(3, $"{tinkerData.Blueprint ?? tinkerData.PartName}",
                    Good: includeRecipe, Indent: indent + 3, Toggle: doDebug);
                if (includeRecipe)
                {
                    yield return tinkerData;
                }
            }
            if (IncludeInstalled && !InstalledRecipes.IsNullOrEmpty())
            {
                Debug.CheckYeh(3, $"InstalledRecipes", Indent: indent + 2, Toggle: doDebug);
                foreach (TinkerData installedRecipe in InstalledRecipes)
                {
                    bool includeRecipe = Filter == null || Filter(installedRecipe);
                    Debug.CheckYeh(3, $"{installedRecipe.Blueprint ?? installedRecipe.PartName}", 
                        Good: includeRecipe, Indent: indent + 3, Toggle: doDebug);
                    if (includeRecipe)
                    {
                        yield return installedRecipe;
                    }
                }
            }

            Debug.SetIndent(indent);
            yield break;
        }

        public static bool DraftDataDisk(GameObject Vendor, TinkerData TinkerData, out GameObject DataDisk, bool IsStock = true)
        {
            DataDisk = null;
            if (Vendor == null || TinkerData == null)
            {
                return false;
            }
            if (TinkerData.createDataDisk(TinkerData) is not GameObject newDataDisk)
            {
                return false;
            }
            DataDisk = newDataDisk;
            TinkeringHelpers.CheckMakersMark(newDataDisk, Vendor, null, "Drafting");
            if (IsStock)
            {
                newDataDisk.SetIntProperty("_stock", 1);
                return Vendor.ReceiveObject(newDataDisk);
            }
            return true;
        }
        public bool DraftDataDisk(TinkerData TinkerData, out GameObject DataDisk)
        {
            return DraftDataDisk(ParentObject, TinkerData, out DataDisk);
        }
        public bool DraftDataDisk(TinkerData TinkerData)
        {
            return DraftDataDisk(ParentObject, TinkerData, out _);
        }

        public static int GetTinkerTierFromSkillName(string Name) => Name switch
        {
            nameof(Tinkering_Tinker1) => 1,
            nameof(Tinkering_Tinker2) => 2,
            nameof(Tinkering_Tinker3) => 3,
            _ => 0,
        };
        public static int GetTinkerTierFromBit(char Bit) => GetTinkerTierFromSkillName(GetTinkerSkillFromBit(Bit));
        public static int GetTinkerTierFromRecipe(TinkerData TinkerData) => GetTinkerTierFromSkillName(DataDisk.GetRequiredSkill(TinkerData.Tier));
        public static string GetTinkerSkillFromBit(char Bit) => DataDisk.GetRequiredSkill(BitType.GetBitTier(Bit));
        public static string GetTinkerSkillFromBitHumanReadable(char Bit) => DataDisk.GetRequiredSkillHumanReadable(BitType.GetBitTier(Bit));

        public static Raffle<char> GetBitRaffle(int Weight, int? Tier = null, int? TierCap = null)
        {
            Raffle<char> bitRaffle = new();
            foreach (char bit in BitType.BitOrder)
            {
                if ((Tier == null || GetTinkerTierFromBit(bit) == Tier.GetValueOrDefault())
                    && (TierCap == null || GetTinkerTierFromBit(bit) <= TierCap.GetValueOrDefault()))
                {
                    bitRaffle.Add(BitType.BitMap[bit].Color, Weight);
                }
            }
            return bitRaffle;
        }
        public static bool FillBitRaffle(ref Raffle<char> BitRaffle, int Weight, int? Tier = null, int? TierCap = null)
        {
            bool anyAdded = false;
            foreach (char bit in BitType.BitOrder)
            {
                if ((Tier == null || GetTinkerTierFromBit(bit) == Tier.GetValueOrDefault())
                    && (TierCap == null || GetTinkerTierFromBit(bit) <= TierCap.GetValueOrDefault()))
                {
                    BitRaffle.Add(BitType.BitMap[bit].Color, Weight);
                    anyAdded = true;
                }
            }
            return anyAdded;
        }
        public static BitLocker GiveRandomBits(GameObject Tinker, bool ClearFirst = true, MinEvent FromEvent = null, bool suppressDebug = false)
        {
            int indent = Debug.LastIndent;
            bool doDebug = UD_VendorTinkering.doDebug && !suppressDebug;
            string fromEvent = null;
            if (FromEvent != null)
            {
                fromEvent = ", " + nameof(FromEvent) + ": " + FromEvent.GetType().Name;
            }
            Debug.Entry(4, 
                nameof(UD_VendorTinkering) + "." +
                nameof(GiveRandomBits) + "(" +
                nameof(Tinker) + ": " + (Tinker?.DebugName ?? NULL) + ", " +
                nameof(ClearFirst) + ": " + ClearFirst + ")" +
                fromEvent, 
                Indent: indent + 1, Toggle: doDebug);

            BitLocker bitLocker = Tinker.RequirePart<BitLocker>().SortBits();
            if (ClearFirst)
            {
                Dictionary<char, int> bitStorage = new(bitLocker.BitStorage);
                bitLocker.UseBits(bitStorage);
            }
            else
            {
                bitLocker.SortBits();
            }

            Debug.Entry(4, $"Finding scrap to disassemble...", Indent: indent + 2, Toggle: doDebug);
            if (!BitType.BitOrder.IsNullOrEmpty())
            {
                bool hasDisassemble = Tinker.HasSkill(nameof(Tinkering_Disassemble));
                bool hasScavenger = Tinker.HasSkill(nameof(Tinkering_Scavenger));
                bool hasReverseEngineer = hasDisassemble && Tinker.HasSkill(nameof(Tinkering_ReverseEngineer));
                bool hasRepair = Tinker.HasSkill(nameof(Tinkering_Repair));
                int tinkeringSkill = 0;

                if (Tinker.HasSkill(nameof(Tinkering_Tinker1)))
                {
                    tinkeringSkill = 1;
                }
                if (Tinker.HasSkill(nameof(Tinkering_Tinker2)))
                {
                    tinkeringSkill = 2;
                }
                if (Tinker.HasSkill(nameof(Tinkering_Tinker3)))
                {
                    tinkeringSkill = 3;
                }

                char bestBit = BitType.BitOrder[^1];
                char rechargeBit = BitType.BitOrder[0];

                Raffle<char> disassembleBitBag = GetBitRaffle(Weight: 0);
                Raffle<char> scavengerBitBag = GetBitRaffle(Weight: 0);
                Raffle<char> reverseEngineerBitBag = GetBitRaffle(Weight: 0);
                Raffle<char> repairBitBag = GetBitRaffle(Weight: 0);
                Raffle<char> tinkeringSkillBitBag = GetBitRaffle(Weight: 0);
                if (hasDisassemble)
                {
                    disassembleBitBag
                        .FillBitRaffle(Weight: 64, TierCap: 0)
                        .FillBitRaffle(Weight: 32, TierCap: 1)
                        .FillBitRaffle(Weight: 16, TierCap: 2)
                        .FillBitRaffle(Weight: 4, TierCap: 3);

                    if (disassembleBitBag.ActiveContains(bestBit))
                    {
                        disassembleBitBag[bestBit] -= 2;
                    }
                }
                if (hasScavenger)
                {
                    scavengerBitBag
                        .FillBitRaffle(Weight: 64, TierCap: 0)
                        .FillBitRaffle(Weight: 16, TierCap: 1);
                }
                if (hasReverseEngineer)
                {
                    reverseEngineerBitBag
                        .FillBitRaffle(Weight: 16, TierCap: 0)
                        .FillBitRaffle(Weight: 32, TierCap: 1)
                        .FillBitRaffle(Weight: 16, TierCap: 2)
                        .FillBitRaffle(Weight: 8, TierCap: 3);
                    if (reverseEngineerBitBag.ActiveContains(bestBit))
                    {
                        reverseEngineerBitBag[bestBit] -= 4;
                    }
                }
                if (hasRepair)
                {
                    repairBitBag
                        .FillBitRaffle(Weight: 16, TierCap: 0)
                        .FillBitRaffle(Weight: 32, TierCap: 1)
                        .FillBitRaffle(Weight: 16, TierCap: 2)
                        .FillBitRaffle(Weight: 8, TierCap: 3);
                    if (repairBitBag.ActiveContains(bestBit))
                    {
                        repairBitBag[bestBit] -= 4;
                    }
                }
                int tinkeringSkillWeight = 4;
                int tinkerSKillLow = 8;
                int tinkerSkillHigh = 16;
                for (int i = tinkeringSkill; i >= 0; i--)
                {
                    tinkeringSkillBitBag.FillBitRaffle(Weight: tinkeringSkillWeight, TierCap: i);
                    tinkeringSkillWeight *= 2;
                    if (i > (tinkeringSkill / 2))
                    {
                        tinkerSKillLow *= 2;
                        tinkerSkillHigh *= 2;
                    }
                }
                if (tinkeringSkillBitBag.ActiveContains(bestBit))
                {
                    tinkeringSkillBitBag[bestBit] -= 2;
                }
                if (tinkeringSkillBitBag.ActiveContains(rechargeBit))
                {
                    tinkeringSkillBitBag[rechargeBit] *= 2;
                }

                string vomitSource = nameof(UD_VendorTinkering) + "." + nameof(GiveRandomBits);

                Stopwatch sw = new();
                sw.Start();

                if (UD_Tinkering_Bytes.Options.doDebug)
                {
                    disassembleBitBag.VomitBits(4, vomitSource, nameof(disassembleBitBag), ShowChance: true, Short: true,
                        Indent: indent + 3, Toggle: doDebug);

                    scavengerBitBag.VomitBits(4, vomitSource, nameof(scavengerBitBag), ShowChance: true, Short: true,
                        Indent: indent + 3, Toggle: doDebug);

                    reverseEngineerBitBag.VomitBits(4, vomitSource, nameof(reverseEngineerBitBag), ShowChance: true, Short: true,
                        Indent: indent + 3, Toggle: doDebug);

                    repairBitBag.VomitBits(4, vomitSource, nameof(repairBitBag), ShowChance: true, Short: true,
                        Indent: indent + 3, Toggle: doDebug);

                    tinkeringSkillBitBag.VomitBits(4, vomitSource, nameof(tinkeringSkillBitBag), ShowChance: true, Short: true,
                        Indent: indent + 3, Toggle: doDebug);
                }

                TimeSpan elapsed = sw.Elapsed;
                sw.Stop();

                Debug.Entry(4, $"Time to vomit Bits", elapsed.TotalSeconds.Things("second"), Indent: indent + 2, Toggle: doDebug);

                int disassembleBitsToDraw = Stat.RandomCosmetic(16, 32);
                int scavengerBitsToDraw = Stat.RandomCosmetic(16, 32);
                int reverseEngineerBitsToDraw = Stat.RandomCosmetic(16, 32);
                int repairBitsToDraw = Stat.RandomCosmetic(16, 32);
                int tinkeringSkillBitsToDraw = Stat.RandomCosmetic(tinkerSKillLow, tinkerSkillHigh);

                sw.Start();

                string disassembleBits = new(disassembleBitBag.DrawUptoNCosmetic(disassembleBitsToDraw).ToArray());
                string scavengerBits = new(scavengerBitBag.DrawUptoNCosmetic(scavengerBitsToDraw).ToArray());
                string reverseEngineerBits = new(reverseEngineerBitBag.DrawUptoNCosmetic(reverseEngineerBitsToDraw).ToArray());
                string repairBits = new(repairBitBag.DrawUptoNCosmetic(repairBitsToDraw).ToArray());
                string tinkeringSkillBits = new(tinkeringSkillBitBag.DrawUptoNCosmetic(tinkeringSkillBitsToDraw).ToArray());

                elapsed = sw.Elapsed;
                sw.Stop();

                Debug.Entry(4, "Time to Draw Bits", elapsed.TotalSeconds.Things("second"),
                    Indent: indent + 2, Toggle: doDebug);

                disassembleBitBag.Clear();
                scavengerBitBag.Clear();
                reverseEngineerBitBag.Clear();
                repairBitBag.Clear();
                tinkeringSkillBitBag.Clear();

                static string debugBitsDisplay(string s) => s.IsNullOrEmpty() ? "none" : ("<" + s + ">");

                Debug.LoopItem(4,
                    nameof(hasDisassemble) + ": " + hasDisassemble + " (" +
                    disassembleBitsToDraw + "), bits: " + debugBitsDisplay(disassembleBits),
                    Good: hasDisassemble, Indent: indent + 3, Toggle: doDebug);

                Debug.LoopItem(4,
                    nameof(hasScavenger) + ": " + hasScavenger + " (" +
                    scavengerBitsToDraw + "), bits: " + debugBitsDisplay(scavengerBits),
                    Good: hasScavenger, Indent: indent + 3, Toggle: doDebug);

                Debug.LoopItem(4,
                    nameof(hasReverseEngineer) + ": " + hasReverseEngineer + " (" +
                    reverseEngineerBitsToDraw + "), bits: " + debugBitsDisplay(reverseEngineerBits),
                    Good: hasReverseEngineer, Indent: indent + 3, Toggle: doDebug);

                Debug.LoopItem(4,
                    nameof(hasRepair) + ": " + hasRepair + " (" +
                    repairBitsToDraw + "), bits: " + debugBitsDisplay(repairBits),
                    Good: hasRepair, Indent: indent + 3, Toggle: doDebug);

                Debug.LoopItem(4, 
                    tinkeringSkill + "] " + nameof(tinkeringSkill) + " (" +
                    tinkerSKillLow + "-" + tinkerSkillHigh + ": " + tinkeringSkillBitsToDraw + "), " +
                    "bits: " + debugBitsDisplay(tinkeringSkillBits),
                    Indent: indent + 3, Toggle: doDebug);

                Debug.Entry(4, $"Disassembling bits and throwing in Locker...", Indent: indent + 3, Toggle: doDebug);

                bitLocker.AddBits(disassembleBits);
                bitLocker.AddBits(scavengerBits);
                bitLocker.AddBits(reverseEngineerBits);
                bitLocker.AddBits(repairBits);
                bitLocker.AddBits(tinkeringSkillBits);

                if (bitLocker.BitStorage.Any(kv => kv.Value > 0))
                {
                    Debug.Entry(4, $"Scrap found, disassembled, and stored...", Indent: indent + 2, Toggle: doDebug);
                    if (UD_Tinkering_Bytes.Options.doDebug)
                    {
                        Debug.Entry(4, $"BitLocker contents:", Indent: indent + 2, Toggle: doDebug);
                        foreach (char bit in BitType.BitOrder)
                        {
                            int count = 0;
                            if (bitLocker.BitStorage.ContainsKey(bit))
                            {
                                count = bitLocker.BitStorage[bit];
                            }
                            Debug.LoopItem(4, BitType.TranslateBit(bit) + "] ", count.ToString(),
                                Indent: indent + 3, Toggle: doDebug);
                        }
                    }
                    return bitLocker;
                }
            }
            Debug.LastIndent = indent;
            return bitLocker;
        }

        public static bool LearnByteRecipes(GameObject Vendor, List<TinkerData> KnownRecipes)
        {
            if (Vendor == null || Vendor.GetIntProperty(nameof(LearnByteRecipes)) > 0 || !Vendor.HasSkill(nameof(UD_Basics)))
            {
                return false;
            }
            KnownRecipes ??= new();
            List<string> byteBlueprints = new(UD_TinkeringByte.GetByteBlueprints());
            Debug.Entry(3, "Spinning up byte data disks for " + Vendor?.DebugName ?? NULL + "...", Indent: 0, Toggle: doDebug);
            bool learned = false;
            int learnedCount = 0;
            if (!byteBlueprints.IsNullOrEmpty())
            {
                foreach (TinkerData tinkerDatum in TinkerData.TinkerRecipes)
                {
                    if (byteBlueprints.Contains(tinkerDatum.Blueprint) && LearnTinkerData(Vendor, tinkerDatum, KnownRecipes))
                    {
                        Debug.LoopItem(3, tinkerDatum.DisplayName.Strip(), Indent: 1, Toggle: false);
                        learned = true;
                        learnedCount++;
                    }
                }
                Debug.LoopItem(3, 
                    "Learned " + learnedCount + "/" + byteBlueprints.Count + " " + nameof(byteBlueprints),
                    Indent: 1, Toggle: doDebug);
            }
            if (learned)
            {
                Vendor.ModIntProperty(nameof(LearnByteRecipes), 1);
            }
            return learned;
        }
        public static bool LearnGiganticRecipe(GameObject Vendor, List<TinkerData> KnownRecipes)
        {
            if (Vendor == null || !EnableGiantsAllKnowModGiganticIfTinkerableAtAll)
            {
                return false;
            }    
            KnownRecipes ??= new();
            bool learned = false;
            bool hasGigantismMutationEntry = false;
            if (MutationFactory.GetMutationEntryByName(nameof(Gigantism)) is MutationEntry gigantismMutationEntry)
            {
                hasGigantismMutationEntry = Vendor.HasPart(gigantismMutationEntry.Class);
            }
            if (Vendor.IsGiganticCreature || Vendor.HasPart("GigantismPlus") || hasGigantismMutationEntry)
            {
                Debug.Entry(3, $"Spinning up {nameof(ModGigantic)} data disks for {Vendor?.DebugName ?? NULL}...", Indent: 0, Toggle: doDebug);
                foreach (TinkerData tinkerDatum in TinkerData.TinkerRecipes)
                {
                    if (tinkerDatum.PartName == nameof(ModGigantic))
                    {
                        Debug.CheckYeh(3, $"{nameof(ModGigantic)}: Found", Indent: 1, Toggle: doDebug);
                        LearnTinkerData(Vendor, tinkerDatum, KnownRecipes);
                        break;
                    }
                }
            }
            return learned;
        }
        public static string GetVendorRecipeSeed(GameObject Vendor)
        {
            return The.Game?.GetWorldSeed() + "-" + Vendor.ID;
        }
        public static bool LearnRandomRecipes(
            GameObject Vendor,
            Raffle<TinkerData> RecipeDeck,
            int Amount,
            List<TinkerData> KnownRecipes,
            bool IgnoreSkillRequirements = false,
            bool CreateDisk = false)
        {
            if (Vendor == null || RecipeDeck.IsNullOrEmpty() || Amount < 1)
            {
                return false;
            }
            int indent = Debug.LastIndent;

            KnownRecipes ??= new();
            bool learned = false;
            foreach (TinkerData recipeToKnow in RecipeDeck.DrawUptoN(Amount))
            {
                string recipeName = recipeToKnow?.PartName ?? recipeToKnow?.Blueprint ?? "something";
                string tinkerSkillTier = "T:" + GetTinkerTierFromRecipe(recipeToKnow);
                Debug.LoopItem(3,
                    tinkerSkillTier + "] Learning " + recipeName,
                    Indent: indent + 1, Toggle: doDebug);
                learned = LearnTinkerData(Vendor, recipeToKnow, KnownRecipes, IgnoreSkillRequirements, CreateDisk) || learned;
            }

            Debug.LastIndent = indent;
            return learned;
        }
        public static bool LearnTinkerXSkillRecipes(
            GameObject Vendor,
            string Tinkering_TinkerX,
            int Amount,
            List<TinkerData> KnownRecipes,
            string RecipeSeed = null,
            bool IgnoreSkillRequirements = false,
            bool CreateDisk = false)
        {
            if (Vendor == null 
                || !Tinkering_TinkerX.StartsWith(nameof(Skill.Tinkering) + "_") 
                || !Vendor.HasSkill(Tinkering_TinkerX)
                || !int.TryParse(Tinkering_TinkerX[^1].ToString(), out _))
            {
                return false;
            }
            KnownRecipes ??= new();

            bool includeBasic = Tinkering_TinkerX == nameof(Tinkering_Tinker1);

            static bool canLearn(TinkerData TinkerData, string TinkeringSkill, bool IncludeBasic, List<TinkerData> KnownRecipes)
            {
                if (KnownRecipes == null || KnownRecipes.Any(r => r.IsSameDatumAs(TinkerData)))
                {
                    return false;
                }
                if (DataDisk.GetRequiredSkill(TinkerData.Tier) is not string requiredSkill)
                {
                    return false;
                }
                if (requiredSkill == TinkeringSkill)
                { 
                    return true;
                }
                if (IncludeBasic && requiredSkill == nameof(UD_Basics))
                {
                    return true;
                }
                return false;
            }

            Raffle<TinkerData> recipeDeck = 
                new(RecipeSeed,
                    from datum in TinkerData.TinkerRecipes
                    where canLearn(datum, Tinkering_TinkerX, includeBasic, KnownRecipes)
                    select datum);

            return LearnRandomRecipes(Vendor, recipeDeck, Amount, KnownRecipes, IgnoreSkillRequirements, CreateDisk);
        }

        public static bool LearnSkillRecipes(
            GameObject Vendor,
            List<TinkerData> KnownRecipes,
            string TinkeringSkill,
            int Low,
            int High,
            bool IgnoreSkillRequirements = false,
            bool CreateDisk = false)
        {
            if (Vendor == null)
            {
                return false;
            }

            bool doDebug = true;
            Debug.GetIndent(out int indent);

            KnownRecipes ??= new();

            bool learned = false;
            bool hasSkill = Vendor.HasSkill(TinkeringSkill);
            string vendorRecipeSkillSeed = GetVendorRecipeSeed(Vendor) + "-" + nameof(LearnSkillRecipes) + "-" + TinkeringSkill;
            int amount = 0;
            if (hasSkill)
            {
                amount = Stat.SeededRandom(vendorRecipeSkillSeed, Low, High);

                if (TinkeringSkill.StartsWith(nameof(Tinkering_Tinker1)[..^1]))
                {
                    learned = LearnTinkerXSkillRecipes(
                        Vendor: Vendor,
                        Tinkering_TinkerX: TinkeringSkill,
                        Amount: amount,
                        KnownRecipes: KnownRecipes,
                        RecipeSeed: vendorRecipeSkillSeed,
                        IgnoreSkillRequirements: IgnoreSkillRequirements,
                        CreateDisk: CreateDisk);
                }
                else
                {
                    IEnumerable<TinkerData> learnableRecipes =
                        from datum in TinkerData.TinkerRecipes
                        where IsRecipeLearnable(Vendor, datum, KnownRecipes)
                        select datum;

                    Raffle<TinkerData> recipeDeck = new(vendorRecipeSkillSeed, learnableRecipes.ToList());

                    learned = LearnRandomRecipes(
                        Vendor: Vendor,
                        RecipeDeck: recipeDeck,
                        Amount: amount,
                        KnownRecipes: KnownRecipes,
                        IgnoreSkillRequirements: IgnoreSkillRequirements,
                        CreateDisk: CreateDisk);
                }
            }

            Debug.LoopItem(3,
                nameof(hasSkill) + "(" + TinkeringSkill + "): " + hasSkill.ToString() + ", " +
                nameof(amount) + " to learn: " + amount,
                Good: hasSkill, Indent: indent + 1, Toggle: doDebug);

            Debug.SetIndent(indent);
            return learned;
        }
        public static bool IsRecipeLearnable(GameObject Vendor, TinkerData TinkerDatum, IEnumerable<TinkerData> KnownRecipes)
        {
            return Vendor.HasSkill(DataDisk.GetRequiredSkill(TinkerDatum.Tier))
                && !KnownRecipes.Contains(TinkerDatum);
        }
        public static bool LearnSkillsRecipes(
            GameObject Vendor,
            List<TinkerData> KnownRecipes, 
            bool IgnoreSkillRequirements = false,
            bool CreateDisk = false)
        {
            if (Vendor == null || !IsVendorTinker(Vendor))
            {
                return false;
            }
            KnownRecipes ??= new();

            bool learned = false;
            string vendorRecipeSeed = GetVendorRecipeSeed(Vendor) + "-" + nameof(LearnSkillRecipes);

            Dictionary<string, (int low, int high)> skillRanges = new()
            {
                { nameof(Tinkering_ReverseEngineer), (2, 4) },
                { nameof(Tinkering_Tinker3), (0, 2) },
                { nameof(Tinkering_Tinker2), (2, 4) },
                { nameof(Tinkering_Tinker1), (3, 5) },
            };

            Debug.Entry(3,
                $"Spinning up skill-based data disks for {Vendor?.DebugName ?? NULL} (" +
                $"{nameof(vendorRecipeSeed)}: {vendorRecipeSeed})...", Indent: 0, Toggle: doDebug);

            Stopwatch sw = new();
            sw.Start();
            foreach ((string tinkerSkill, (int low, int high)) in skillRanges)
            {
                LearnSkillRecipes(Vendor, KnownRecipes, tinkerSkill, low, high, IgnoreSkillRequirements, CreateDisk);
            }
            TimeSpan elapsed = sw.Elapsed;
            sw.Stop();
            Debug.Entry(4, $"Time to Learn Skills", elapsed.TotalSeconds.Things("second"), Indent: 1, Toggle: doDebug);

            Debug.ResetIndent();
            return learned;
        }
        public static bool KnowImplantedRecipes(GameObject Vendor, List<TinkerData> InstalledRecipes)
        {
            if (Vendor == null)
            {
                return false;
            }
            InstalledRecipes.Clear();
            bool learned = false;
            List<GameObject> installedCybernetics = Event.NewGameObjectList(Vendor?.GetInstalledCybernetics() ?? new());

            if (!installedCybernetics.IsNullOrEmpty())
            {
                List<CyberneticsSchemasoft> implantedSchemasoftList = new();
                foreach (GameObject installedCybernetic in installedCybernetics)
                {
                    if (installedCybernetic.TryGetPart(out CyberneticsSchemasoft cyberneticsSchemasoft))
                    {
                        implantedSchemasoftList.TryAdd(cyberneticsSchemasoft);
                    }
                }
                if (!implantedSchemasoftList.IsNullOrEmpty())
                {
                    Debug.Entry(3, $"Spinning up installed schemasoft for {Vendor?.DebugName ?? NULL}...", Indent: 0, Toggle: doDebug);
                    foreach (CyberneticsSchemasoft implantedSchemasoft in implantedSchemasoftList)
                    {
                        Debug.LoopItem(3, $"{implantedSchemasoft?.ParentObject?.DebugName ?? NULL} Recipes", Indent: 1);
                        List<TinkerData> availableRecipes = new(TinkerData.TinkerRecipes);
                        if (!availableRecipes.IsNullOrEmpty())
                        {
                            foreach (TinkerData availableRecipe in availableRecipes)
                            {
                                if (availableRecipe.Tier <= implantedSchemasoft.MaxTier 
                                    && availableRecipe.Category == implantedSchemasoft.Category
                                    && !InstalledRecipes.Any(datum => datum.IsSameDatumAs(availableRecipe)))
                                {
                                    InstalledRecipes.Add(availableRecipe);
                                    Debug.LoopItem(3, 
                                        availableRecipe.Blueprint ?? 
                                        availableRecipe.PartName ?? 
                                        availableRecipe.DisplayName.Strip(), 
                                        Indent: 2);
                                    learned = true;
                                }
                            }
                            availableRecipes.Clear();
                        }
                    }
                }
            }
            return learned;
        }
        public bool KnowImplantedRecipes()
        {
            return KnowImplantedRecipes(ParentObject, InstalledRecipes);
        }

        public static bool IsVendorTinker(GameObject Vendor)
        {
            Debug.GetIndent(out int indent);
            bool doDebug = false;
            Debug.Entry(4, nameof(IsVendorTinker), Vendor?.DebugName ?? NULL, Indent: indent + 1, Toggle: doDebug);
            if (Vendor == null)
            {
                Debug.CheckNah(4, nameof(Vendor) + " is null", Indent: indent + 2, Toggle: doDebug);
                Debug.SetIndent(indent);
                return false;
            }
            if (Vendor.IsPlayer())
            {
                Debug.CheckNah(4, nameof(Vendor) + " " + nameof(GameObject.IsPlayer), Indent: indent + 2, Toggle: doDebug);
                Debug.SetIndent(indent);
                return false;
            }
            if (Vendor.GetPropertyOrTag("Role") is string vendorRole
                && vendorRole == "Tinker")
            {
                return true;
            }
            string tinkering = nameof(Skill.Tinkering);
            if (Vendor.GetSkillAndPowerCountInSkill(tinkering) is int tinkerSkillCount
                && tinkerSkillCount > 0)
            {
                Debug.CheckYeh(4, nameof(tinkerSkillCount) + " is " + tinkerSkillCount, Indent: indent + 2, Toggle: doDebug);
                Debug.SetIndent(indent);
                return true;
            }
            if (Vendor?.GetPart<Skills>() is Skills skills)
            {
                foreach (BaseSkill skill in skills.SkillList)
                {
                    if (skill.Name is string skillName 
                        && skillName.StartsWith(tinkering))
                    {
                        Debug.CheckYeh(4, 
                            nameof(skillName) + " " + skillName + 
                            " in " + nameof(Skills.SkillList) + 
                            " starts with " + tinkerSkillCount,
                            Indent: indent + 2, Toggle: doDebug);
                        Debug.SetIndent(indent);
                        return true;
                    }
                }
            }
            foreach (BaseSkill skill in Vendor.GetPartsDescendedFrom<BaseSkill>())
            {
                if (skill.Name is string skillName
                    && skillName.StartsWith(tinkering))
                {
                    Debug.CheckYeh(4,
                        nameof(skillName) + " " + skillName +
                        " in " + nameof(GameObject.GetPartsDescendedFrom) +
                        " starts with " + tinkerSkillCount,
                        Indent: indent + 2, Toggle: doDebug);
                    Debug.SetIndent(indent);
                    return true;
                }
            }
            Debug.CheckNah(4, nameof(Vendor) + " lacks indication that they're a tinker", Indent: indent + 2, Toggle: doDebug);
            Debug.SetIndent(indent);
            return false;
        }
        public bool IsVendorTinker()
        {
            return IsVendorTinker(ParentObject);
        }

        public static bool InitializeVendorTinker(
            GameObject Vendor,
            List<TinkerData> KnownRecipes,
            List<TinkerData> InstalledRecipes,
            MinEvent FromEvent = null,
            bool IgnoreSkillRequirements = false,
            bool CreateDisks = false,
            bool SkipBits = false)
        {
            Debug.ResetIndent(out int indent);
            bool doDebug = false;
            Debug.Entry(4, nameof(InitializeVendorTinker), Vendor?.DebugName ?? NULL, Indent: indent + 1, Toggle: doDebug);
            bool isVendorTinker = IsVendorTinker(Vendor);
            if (!isVendorTinker
                || KnownRecipes == null
                || InstalledRecipes == null)
            {
                Debug.CheckNah(4, "Not a Tinker, or provided null lists.", Indent: indent + 2, Toggle: doDebug);
                Debug.LoopItem(4, nameof(isVendorTinker), isVendorTinker.ToString(),
                    Good: isVendorTinker, Indent: indent + 3, Toggle: doDebug);
                Debug.LoopItem(4, nameof(KnownRecipes), (KnownRecipes == null).ToString(),
                    Good: KnownRecipes == null, Indent: indent + 3, Toggle: doDebug);
                Debug.LoopItem(4, nameof(InstalledRecipes), (InstalledRecipes == null).ToString(),
                    Good: InstalledRecipes == null, Indent: indent + 3, Toggle: doDebug);
                Debug.SetIndent(indent);
                return false;
            }
            doDebug = true;
            Debug.CheckYeh(4, nameof(Vendor) + " is a tinker!", Indent: indent + 2, Toggle: doDebug);

            if (!SkipBits)
            {
                GiveRandomBits(Vendor, FromEvent: FromEvent);
            }

            LearnByteRecipes(Vendor, KnownRecipes);

            LearnGiganticRecipe(Vendor, KnownRecipes);

            LearnSkillsRecipes(Vendor, KnownRecipes, IgnoreSkillRequirements, CreateDisks);

            KnowImplantedRecipes(Vendor, InstalledRecipes);

            if (Vendor.TryGetPart(out UD_VendorTinkering vendorTinkering))
            {
                vendorTinkering.CanRestockBits = false;
                vendorTinkering.TinkerInitialized = true;
            }

            Debug.SetIndent(indent);
            return true;
        }
        public bool InitializeVendorTinker(MinEvent FromEvent = null, bool SkipBits = false)
        {
            if (TinkerInitialized)
            {
                return false;
            }
            bool ignoreSkillRequirements = false;
            bool createDisks = false;
            if (ParentObject.GetPart<UD_VendorDisassembly>() is UD_VendorDisassembly vendorDisassembly)
            {
                ignoreSkillRequirements = vendorDisassembly.ReverseEngineerIgnoresSkillRequirement;
                createDisks = vendorDisassembly.ScribesReverseEngineeredRecipes;
            }
            KnownRecipes = new();
            InstalledRecipes = new();
            return InitializeVendorTinker(
                Vendor: ParentObject,
                KnownRecipes: KnownRecipes,
                InstalledRecipes: InstalledRecipes,
                FromEvent: FromEvent,
                IgnoreSkillRequirements: ignoreSkillRequirements,
                CreateDisks: createDisks,
                SkipBits: SkipBits);
        }

        public static bool CopyKnownRecipes(GameObject Vendor, UD_VendorTinkering Source)
        {
            if (Vendor == null || !Vendor.TryGetPart(out UD_VendorTinkering vendorTinkering))
            {
                return false;
            }
            int toLearn = 0;
            int learned = 0;
            foreach (TinkerData tinkerDatum in Source.GetKnownRecipes(IncludeInstalled: false))
            {
                toLearn++;
                if (vendorTinkering.LearnTinkerData(tinkerDatum, IgnoreSkillRequirement: true))
                {
                    learned++;
                }
            }
            return toLearn == learned;
        }
        public bool CopyKnownRecipes(UD_VendorTinkering Source)
        {
            return CopyKnownRecipes(ParentObject, Source);
        }

        public static bool PlayerCanReadDataDisk(GameObject DataDisk)
        {
            return DataDisk.Understood()
                && The.Player != null
                && (The.Player.HasSkill("Tinkering") || Scanning.HasScanningFor(The.Player, Scanning.Scan.Tech));
        }

        public static bool ReceiveBitLockerDisplayItem(GameObject Vendor)
        {
            int indent = Debug.LastIndent;
            bool doDebug = false;
            ClearBitLockerDisplayItem(Vendor);
            bool received = false;
            if (Vendor.HasPart<BitLocker>())
            {
                if (!DebugShowAllTinkerBitLockerInlineDisplay)
                {
                    if (GameObject.CreateSample(BITLOCK_DISPLAY) is GameObject bitLockerDisplayItem)
                    {
                        Debug.Entry(4, nameof(bitLockerDisplayItem), bitLockerDisplayItem.DisplayName, Indent: indent + 1, Toggle: doDebug);
                        received = Vendor.ReceiveObject(bitLockerDisplayItem);
                    }
                }
                else
                {
                    for (int i = 0; i < 7; i++)
                    {
                        if (GameObject.CreateSample(BITLOCK_DISPLAY) is GameObject bitLockerDisplayItem
                            && bitLockerDisplayItem.TryGetPart(out UD_BitLocker_Display bitLockerDisplayPart))
                        {
                            bitLockerDisplayPart.DisplayNameStyle = i;
                            Debug.Entry(4, nameof(bitLockerDisplayItem), bitLockerDisplayItem.DisplayName, Indent: indent + 1, Toggle: doDebug);
                            received = Vendor.ReceiveObject(bitLockerDisplayItem) || received;
                        }
                    }
                }
            }
            Debug.LastIndent = indent;
            return received;
        }
        public bool ReceiveBitLockerDisplayItem()
        {
            return ReceiveBitLockerDisplayItem(ParentObject);
        }

        public static void ClearBitLockerDisplayItem(GameObject Vendor)
        {
            foreach (GameObject bitlockerDisplayItem in UD_BitLocker_Display.GetBitLockerDisplayObjects(Vendor?.Inventory))
            {
                if (GameObject.Validate(bitlockerDisplayItem))
                {
                    bitlockerDisplayItem.Obliterate();
                }
            }
        }
        public void ClearBitLockerDisplayItem()
        {
            ClearBitLockerDisplayItem(ParentObject);
        }

        public static GameObject GetKnownRecipeDisplayItem(TinkerData KnownRecipe, IEnumerable<TinkerData> InstalledRecipes = null)
        {
            string recipeBlueprint = !InstalledRecipes.IsNullOrEmpty() && InstalledRecipes.Contains(KnownRecipe) 
                ? "UD_VendorImplantedRecipe" 
                : "UD_VendorKnownRecipe";

            if (GameObject.Create(recipeBlueprint) is GameObject knownRecipeObject 
                && knownRecipeObject.TryGetPart(out UD_VendorKnownRecipe knownRecipePart))
            {
                knownRecipePart.SetData(KnownRecipe);
                return knownRecipeObject;
            }
            return null;
        }
        public GameObject GetKnownRecipeDisplayItem(TinkerData KnownRecipe)
        {
            return GetKnownRecipeDisplayItem(KnownRecipe, InstalledRecipes);
        }
        public bool ReceiveKnownRecipeDisplayItem(TinkerData KnownRecipe)
        {
            return GetKnownRecipeDisplayItem(KnownRecipe, InstalledRecipes) is GameObject knownRecipeDisplayObject
                && ParentObject.ReceiveObject(knownRecipeDisplayObject);
        }

        public static void ReceiveKnownRecipeDisplayItems(
            GameObject Vendor,
            IEnumerable<TinkerData> KnownRecipes,
            IEnumerable<TinkerData> InstalledRecipes = null)
        {
            if (!KnownRecipes.IsNullOrEmpty())
            {
                foreach (TinkerData knownRecipe in KnownRecipes)
                {
                    Vendor.ReceiveObject(GetKnownRecipeDisplayItem(knownRecipe, InstalledRecipes));
                }
            }
        }
        public void ReceiveKnownRecipeDisplayItems()
        {
            ReceiveKnownRecipeDisplayItems(ParentObject, GetKnownRecipes(), InstalledRecipes);
        }

        public static bool CanTinkerRecipe(
            GameObject Vendor, 
            TinkerData Recipe, 
            List<TinkerData> InstalledRecipes = null, 
            bool Silent = false)
        {
            if (!Vendor.HasSkill(DataDisk.GetRequiredSkill(Recipe.Tier)) 
                && !(InstalledRecipes != null && InstalledRecipes.Contains(Recipe)))
            {
                if (!Silent)
                {
                    string requiredSkillName = DataDisk.GetRequiredSkillHumanReadable(Recipe.Tier);
                    string skillFailMsg = ("=subject.Name= =verb:don't= have the required skill: " + requiredSkillName + "!")
                        .StartReplace()
                        .AddObject(Vendor)
                        .ToString();

                    Popup.ShowFail(skillFailMsg);
                }
                return false;
            }
            return true;
        }
        public bool CanTinkerRecipe(TinkerData Recipe, bool Silent = false)
        {
            return CanTinkerRecipe(ParentObject, Recipe, InstalledRecipes, Silent);
        }

        public static bool IsModApplicableAndNotAlreadyInDictionary(
            GameObject Vendor, 
            GameObject ApplicableItem, 
            TinkerData ModData, 
            Dictionary<TinkerData, string> ExistingRecipes, 
            List<TinkerData> InstalledRecipes = null)
        {
            return ItemModding.ModAppropriate(ApplicableItem, ModData)
                && CanTinkerRecipe(Vendor, ModData, InstalledRecipes)
                && !ExistingRecipes.Keys.Any(datum => datum.IsSameDatumAs(ModData));
        }
        public bool IsModApplicableAndNotAlreadyInDictionary(
            GameObject ApplicableItem,
            TinkerData ModData,
            Dictionary<TinkerData, string> ExistingRecipes)
        {
            return IsModApplicableAndNotAlreadyInDictionary(
                Vendor: ParentObject,
                ApplicableItem: ApplicableItem,
                ModData: ModData,
                ExistingRecipes: ExistingRecipes,
                InstalledRecipes: InstalledRecipes);
        }

        public static GameObject FindIngredientInInventory(GameObject IngredientSupplier, string Blueprint, Predicate<GameObject> Filter = null)
        {
            return IngredientSupplier?.Inventory?.FindObjectByBlueprint(Blueprint, Filter);
        }
        public static GameObject FindRealIngredient(GameObject IngredientSupplier, string Blueprint)
        {
            return FindIngredientInInventory(IngredientSupplier, Blueprint, Temporary.IsNotTemporary);
        }

        private static void AddNextHotkey(ref List<char> Hotkeys)
        {
            Hotkeys ??= new();
            if (Hotkeys.IsNullOrEmpty())
            {
                Hotkeys.Add('a');
            }
            else
            if (Hotkeys[^1] == ' ' || Hotkeys.Contains('z'))
            {
                Hotkeys.Add(' ');
            }
            else
            {
                char nextHotkey = Hotkeys[^1];
                Hotkeys.Add(++nextHotkey);
            }
        }

        public static GameObject PickASupplier(
            GameObject Vendor,
            GameObject ForObject,
            string Title,
            string Message = null,
            bool CenterIntro = false,
            BitCost BitCost = null,
            bool Multiple = false)
        {
            BitCost playerBitCost = null;
            BitCost vendorBitCost = null;
            string itThem = Multiple ? "them" : "it";
            string askPay = Vendor.IsPlayerLed() ? "Ask" : "Pay";
            if (BitCost != null)
            {
                playerBitCost = new();
                vendorBitCost = new();
                playerBitCost.Import(BitCost.ToBits());
                vendorBitCost.Import(BitCost.ToBits());
                ModifyBitCostEvent.Process(The.Player, playerBitCost, "Build");
                ModifyBitCostEvent.Process(Vendor, vendorBitCost, "Build");
            }
            string playerBitCostString = playerBitCost != null ? ("the required <" + playerBitCost + "> bits") : itThem;
            string vendorBitCostString = vendorBitCost != null ? ("the required <" + vendorBitCost + "> bits") : itThem;
            List<string> supplyOptions = new()
            {
                "Use my own if I have " + playerBitCostString + ".",
                (askPay + " =subject.name= to supply " + vendorBitCostString + ".")
                    .StartReplace()
                    .AddObject(Vendor)
                    .ToString(),
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
        public GameObject PickASupplier(
            GameObject ForObject,
            string Title,
            string Message = null,
            bool CenterIntro = false,
            BitCost BitCost = null,
            bool Multiple = false)
        {
            return PickASupplier(
                Vendor: ParentObject,
                ForObject: ForObject,
                Title: Title,
                Message: Message,
                CenterIntro: CenterIntro,
                BitCost: BitCost,
                Multiple: Multiple);
        }

        public static string GetSupplierPickerTinkerDataTitle(TinkerData TinkerData)
        {
            if (TinkerData == null || TinkerData.DisplayName == null)
            {
                return null;
            }
            string modPrefix = "";
            string numberMadePrefix = "";
            if (TinkerData.Type == "Mod")
            {
                modPrefix = "[{{W|Mod}}] ";
            }
            else
            if (TinkerData.Type == "Build"
                && TinkerInvoice.CreateTinkerSample(TinkerData.Blueprint) is GameObject sampleObject)
            {
                if (sampleObject.TryGetPart(out TinkerItem tinkerItem)
                    && tinkerItem.NumberMade > 1)
                {
                    numberMadePrefix = $"{Grammar.Cardinal(tinkerItem.NumberMade)} ";
                }
                TinkerInvoice.ScrapTinkerSample(ref sampleObject);
            }
            return modPrefix + numberMadePrefix + TinkerData.DisplayName;
        }

        public static bool PickRecipeIngredientSupplier(
            GameObject Vendor,
            GameObject ForObject,
            TinkerData TinkerData,
            out GameObject RecipeIngredientSupplier,
            out GameObject SelectedIngredient,
            string Context = null)
        {
            RecipeIngredientSupplier = Vendor;
            SelectedIngredient = null;

            string itemOrMod = TinkerData.Type == "Build" ? "item" : "mod";
            string craftOrApply = TinkerData.Type == "Build" ? "tinker" : "apply";

            GameObject ingredientObject = null;
            GameObject temporaryIngredientObject = null;
            if (!TinkerData.Ingredient.IsNullOrEmpty())
            {
                string title = GetSupplierPickerTinkerDataTitle(TinkerData) + "\n" +
                    "| 1 Ingredient Required |".Color("Y") + "\n";

                List<string> recipeIngredientBlueprints = TinkerData.Ingredient.CachedCommaExpansion().ToList();

                for (int i = 0; i < recipeIngredientBlueprints.Count; i++)
                {
                    if (i > 0)
                    {
                        title += "-or-".Color("Y") + "\n";
                    }
                    string ingredientDisplayName = Utils.GetGameObjectBlueprint(recipeIngredientBlueprints[i])?.DisplayName();
                    if (TinkerInvoice.CreateTinkerSample(recipeIngredientBlueprints[i]) is GameObject sampleIngredient)
                    {
                        ingredientDisplayName = sampleIngredient.GetDisplayName(Context: Context, Short: true);
                        TinkerInvoice.ScrapTinkerSample(ref sampleIngredient);
                    }
                    if (!ingredientDisplayName.IsNullOrEmpty())
                    {
                        title += ingredientDisplayName + "\n";
                    }
                }

                RecipeIngredientSupplier = PickASupplier(
                    Vendor: Vendor,
                    ForObject: ForObject,
                    Title: title);

                if (RecipeIngredientSupplier == null)
                {
                    return false;
                }
                foreach (string recipeIngredient in recipeIngredientBlueprints)
                {
                    ingredientObject = FindRealIngredient(RecipeIngredientSupplier, recipeIngredient);
                    if (ingredientObject != null)
                    {
                        break;
                    }
                    temporaryIngredientObject ??= FindIngredientInInventory(RecipeIngredientSupplier, recipeIngredient);
                }
                if (ingredientObject == null)
                {
                    if (temporaryIngredientObject != null)
                    {
                        string tempObjectFailMsg = "=subject.t= =subject.verb:are= too unstable to craft with."
                            .StartReplace()
                            .AddObject(temporaryIngredientObject)
                            .ToString();

                        Popup.ShowFail(tempObjectFailMsg);
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
                        string noIngredientMsg = ("=subject.Name= =subject.verb:don't= have the required ingredient: " + ingredientName)
                            .StartReplace()
                            .AddObject(RecipeIngredientSupplier)
                            .ToString();

                        Popup.ShowFail(noIngredientMsg);
                    }
                    return false;
                }
                List<string> availableIngredients = new();
                foreach (string recipeIngredient in recipeIngredientBlueprints)
                {
                    if (RecipeIngredientSupplier.HasObjectInInventory(GO => GO.Blueprint == recipeIngredient))
                    {
                        availableIngredients.Add(recipeIngredient);
                    }
                }

                if (!PickRecipeIngredient(
                    RecipeIngredientSupplier: RecipeIngredientSupplier,
                    ForObject: ForObject,
                    VendorSuppliesIngredients: RecipeIngredientSupplier == Vendor,
                    AvailableIngredients: availableIngredients,
                    SelectedIngredient: out SelectedIngredient,
                    Title: title))
                {
                    return false;
                }
            }
            return true;
        }
        public bool PickRecipeIngredientSupplier(
            GameObject ForObject, 
            TinkerData TinkerData, 
            out GameObject RecipeIngredientSupplier,
            out GameObject SelectedIngredient,
            string Context = null)
        {
            return PickRecipeIngredientSupplier(
                Vendor: ParentObject,
                ForObject: ForObject,
                TinkerData: TinkerData, 
                RecipeIngredientSupplier: out RecipeIngredientSupplier,
                SelectedIngredient: out SelectedIngredient,
                Context: Context);
        }
       
        public static bool PickBitsSupplier(
            GameObject Vendor,
            GameObject ForObject,
            BitCost BitCost,
            out GameObject RecipeBitSupplier,
            out BitLocker BitSupplierBitLocker,
            string Title = null,
            string Context = null,
            bool CheckHasBits = true)
        {
            BitSupplierBitLocker = null;

            if (!Title.IsNullOrEmpty())
            {
                Title += "\n";
            }
            string title = (Title
                + "| Bit Cost |".Color("y") + "\n"
                + "<" + BitCost + ">" + "\n")
                    .StartReplace()
                    .AddObject(The.Player)
                    .AddObject(Vendor)
                    .AddObject(ForObject, "item")
                    .ToString();

            RecipeBitSupplier = PickASupplier(
                Vendor: Vendor,
                ForObject: ForObject,
                Title: title,
                CenterIntro: true,
                BitCost: BitCost);

            if (RecipeBitSupplier == null)
            {
                return false;
            }

            BitSupplierBitLocker = RecipeBitSupplier.RequirePart<BitLocker>();

            if (!Context.IsNullOrEmpty())
            {
                ModifyBitCostEvent.Process(RecipeBitSupplier, BitCost, Context);
            }

            return !CheckHasBits || BitSupplierHasBits(RecipeBitSupplier, BitSupplierBitLocker, BitCost);
        }
        public bool PickBitsSupplier(
            GameObject ForObject,
            BitCost BitCost,
            out GameObject RecipeBitSupplier,
            out BitLocker BitSupplierBitLocker,
            string Title = null,
            string Context = null,
            bool CheckHasBits = true)
        {
            return PickBitsSupplier(
                Vendor: ParentObject,
                ForObject: ForObject,
                BitCost: BitCost,
                RecipeBitSupplier: out RecipeBitSupplier,
                BitSupplierBitLocker: out BitSupplierBitLocker,
                Title: Title,
                Context: Context,
                CheckHasBits: CheckHasBits);
        }

        public static bool PickRecipeIngredient(
            GameObject RecipeIngredientSupplier,
            GameObject ForObject,
            bool VendorSuppliesIngredients,
            List<string> AvailableIngredients,
            out GameObject SelectedIngredient,
            string Title = null)
        {
            SelectedIngredient = null;
            int selectedOption = 0;
            if (AvailableIngredients.Count > 1)
            {
                List<char> hotkeys = new();
                List<string> lineItems = new();
                List<IRenderable> lineIcons = new();
                foreach (string availableIngredient in AvailableIngredients)
                {
                    if (TinkerInvoice.CreateTinkerSample(availableIngredient) is GameObject availableIngredientObject)
                    {
                        AddNextHotkey(ref hotkeys);
                        double ingredientValue = 0;
                        if (VendorSuppliesIngredients)
                        {
                            ingredientValue = Math.Round(TradeUI.GetValue(availableIngredientObject, VendorSuppliesIngredients), 2);
                        }
                        string lineItem = "Use =subject.a= =subject.refname=.".Color("y");
                        if (ingredientValue > 0)
                        {
                            // lineItem += " (worth {{C|" + ingredientValue + " " + (ingredientValue == 1 ? "dram" : "drams") + "}})";
                        }
                        lineItems.Add(lineItem.StartReplace().AddObject(availableIngredientObject).ToString());
                        lineIcons.Add(availableIngredientObject.RenderForUI("PickIngredientObject"));
                        TinkerInvoice.ScrapTinkerSample(ref availableIngredientObject);
                    }
                    else
                    {
                        string potentialErrorMsg = nameof(UD_VendorTinkering) + "." + nameof(PickRecipeIngredient);
                        MetricsManager.LogPotentialModError(
                            Mod: Utils.ThisMod,
                            Message: potentialErrorMsg + " failed to create sample from blueprint " + availableIngredient);
                    }
                }
                if (!hotkeys.IsNullOrEmpty() && !lineItems.IsNullOrEmpty() && !lineIcons.IsNullOrEmpty())
                {
                    selectedOption = Popup.PickOption(
                        Title: Title ?? TinkeringHelpers.TinkeredItemDisplayName(ForObject.Blueprint) ?? ForObject.GetReferenceDisplayName(),
                        Options: lineItems.ToArray(),
                        Hotkeys: hotkeys.ToArray(),
                        Icons: lineIcons,
                        Context: ForObject,
                        IntroIcon: ForObject.RenderForUI(),
                        AllowEscape: true,
                        PopupID: "VendorTinkeringSelectBuildIngredient:" + (ForObject?.IDIfAssigned ?? "(noid)"));

                    if (selectedOption < 0)
                    {
                        return false;
                    }
                }
            }
            SelectedIngredient = FindRealIngredient(RecipeIngredientSupplier, AvailableIngredients[selectedOption]);
            SelectedIngredient.SplitStack(1, RecipeIngredientSupplier);
            return SelectedIngredient != null;
        }

        public static bool BitSupplierHasBits(
            GameObject BitSupplier,
            BitLocker BitSupplierBitLocker,
            BitCost BitCost,
            bool Silent = false)
        {
            if (!BitSupplierBitLocker.HasBits(BitCost))
            {
                string missingRequiredBitsMsg =
                    ("=subject.Name= =subject.verb:don't= have the required <" + BitCost + "> bits!\n\n" +
                    "=subject.Subjective= =subject.verb:have:afterpronoun=:\n" + BitSupplierBitLocker.GetBitsString())
                        .StartReplace()
                        .AddObject(BitSupplier)
                        .ToString();

                if (!Silent)
                {
                    Popup.ShowFail(missingRequiredBitsMsg);
                }
                return false;
            }
            return true;
        }

        public static bool TryGetApplicableItems(
            GameObject Actor,
            TinkerData ApplicableRecipe,
            out List<string> LineItems,
            out List<char> Hotkeys,
            out List<IRenderable> LineIcons,
            out List<GameObject> ApplicableItems)
        {
            string modName = ApplicableRecipe.DisplayName;

            LineItems = new();
            Hotkeys = new();
            LineIcons = new();
            ApplicableItems = Actor?.GetInventoryAndEquipment(GO => ItemModding.ModAppropriate(GO, ApplicableRecipe) && GO.Understood());

            if (ApplicableItems.IsNullOrEmpty())
            {
                string noApplicableItemsMsg =
                    ("=subject.Name= =subject.verb:don't= have any items " +
                    "that can be modified with " + modName + ".")
                        .StartReplace()
                        .AddObject(Actor)
                        .ToString();

                Popup.ShowFail(noApplicableItemsMsg);

                LineItems = null;
                Hotkeys = null;
                LineIcons = null;
                ApplicableItems = null;
                return false;
            }

            BitCost recipeBitCost = new();
            foreach (GameObject applicableObject in ApplicableItems)
            {
                recipeBitCost = new();
                int recipeTier = Tier.Constrain(ApplicableRecipe.Tier);

                int modSlotsUsed = applicableObject.GetModificationSlotsUsed();
                int noCostMods = applicableObject.GetIntProperty("NoCostMods");

                int existingModsTier = Tier.Constrain(modSlotsUsed - noCostMods + applicableObject.GetTechTier());

                recipeBitCost.Increment(BitType.TierBits[recipeTier]);
                recipeBitCost.Increment(BitType.TierBits[existingModsTier]);

                AddNextHotkey(ref Hotkeys);

                string context = "";
                if (applicableObject.Equipped == Actor)
                {
                    context = " [" + "Equipped".Color("K") + "]";
                }
                string singleShortDisplayName = applicableObject.GetDisplayName(Single: true, Short: true);
                string lineItem = $"<{recipeBitCost}> {singleShortDisplayName}{context}";
                LineItems.Add(lineItem);

                LineIcons.Add(applicableObject.RenderForUI());
            }
            return true;
        }

        public static bool TryGetApplicableRecipes(
            GameObject Vendor,
            GameObject ApplicableItem,
            List<TinkerData> InstalledRecipes,
            out List<string> LineItems,
            out List<char> Hotkeys,
            out List<IRenderable> LineIcons,
            out List<TinkerData> ApplicableRecipes,
            out List<bool> IsVendorOwnedRecipe)
        {
            LineItems = new();
            Hotkeys = new();
            LineIcons = new();
            ApplicableRecipes = new();
            IsVendorOwnedRecipe = new();
            GameObject player = The.Player;
            List<GameObject> vendorHeldKnownRecipeObjects = Vendor?.Inventory?.GetObjectsViaEventList(
                GO => GO.TryGetPart(out UD_VendorKnownRecipe knownRecipe)
                && knownRecipe.Data.Type == "Mod");

            List<GameObject> vendorHeldDataDiskObjects = Vendor?.Inventory?.GetObjectsViaEventList(
                GO => GO.TryGetPart(out DataDisk D)
                && D.Data.Type == "Mod");

            List<GameObject> playerHeldDataDiskObjects = player?.Inventory?.GetObjectsViaEventList(
                GO => GO.TryGetPart(out DataDisk D)
                && D.Data.Type == "Mod");

            if (vendorHeldKnownRecipeObjects.IsNullOrEmpty()
                && vendorHeldDataDiskObjects.IsNullOrEmpty()
                && playerHeldDataDiskObjects.IsNullOrEmpty())
            {
                string noModsKnownMsg = "=subject.Name= =subject.verb:don't= know any item modification recipes."
                    .StartReplace()
                    .AddObject(Vendor)
                    .ToString();

                Popup.ShowFail(noModsKnownMsg);

                LineItems = null;
                Hotkeys = null;
                LineIcons = null;
                ApplicableRecipes = null;
                IsVendorOwnedRecipe = null;
                return false;
            }
            Dictionary<TinkerData, string> applicableRecipes = new();
            if (!playerHeldDataDiskObjects.IsNullOrEmpty())
            {
                foreach (GameObject playerHeldDataDiskObject in playerHeldDataDiskObjects)
                {
                    if (playerHeldDataDiskObject.TryGetPart(out DataDisk playerHeldDataDisk)
                        && IsModApplicableAndNotAlreadyInDictionary(
                            Vendor: Vendor,
                            ApplicableItem: ApplicableItem,
                            ModData: playerHeldDataDisk.Data,
                            ExistingRecipes: applicableRecipes,
                            InstalledRecipes: InstalledRecipes))
                    {
                        string shopperOwnedTag = "=subject.name's= inventory"
                            .StartReplace()
                            .AddObject(player)
                            .ToString();

                        applicableRecipes.Add(playerHeldDataDisk.Data, shopperOwnedTag);
                        IsVendorOwnedRecipe.Add(false); 
                        
                        LineIcons.Add(playerHeldDataDiskObject.RenderForUI());
                    }
                }
            }
            if (!vendorHeldKnownRecipeObjects.IsNullOrEmpty())
            {
                foreach (GameObject vendorHeldKnownRecipeObject in vendorHeldKnownRecipeObjects)
                {
                    if (vendorHeldKnownRecipeObject.TryGetPart(out UD_VendorKnownRecipe vendorHeldRecipePart)
                        && IsModApplicableAndNotAlreadyInDictionary(
                            Vendor: Vendor,
                            ApplicableItem: ApplicableItem,
                            ModData: vendorHeldRecipePart.Data,
                            ExistingRecipes: applicableRecipes,
                            InstalledRecipes: InstalledRecipes))
                    {
                        applicableRecipes.Add(vendorHeldRecipePart.Data, "known by trader");
                        IsVendorOwnedRecipe.Add(true);

                        LineIcons.Add(vendorHeldKnownRecipeObject.RenderForUI());
                    }
                }
            }
            if (!vendorHeldDataDiskObjects.IsNullOrEmpty())
            {
                foreach (GameObject vendorHeldDataDiskObject in vendorHeldDataDiskObjects)
                {
                    if (vendorHeldDataDiskObject.TryGetPart(out DataDisk vendorHeldDataDisk)
                        && IsModApplicableAndNotAlreadyInDictionary(
                            Vendor: Vendor,
                            ApplicableItem: ApplicableItem,
                            ModData: vendorHeldDataDisk.Data,
                            ExistingRecipes: applicableRecipes,
                            InstalledRecipes: InstalledRecipes))
                    {
                        applicableRecipes.Add(vendorHeldDataDisk.Data, "trader inventory");
                        IsVendorOwnedRecipe.Add(true);

                        LineIcons.Add(vendorHeldDataDiskObject.RenderForUI());
                    }
                }
            }
            if (applicableRecipes.IsNullOrEmpty())
            {
                string noModsKnownForItemMsg = "=subject.Name= =subject.verb:don't= know any item modifications for =object.t=."
                    .StartReplace()
                    .AddObject(Vendor)
                    .AddObject(ApplicableItem)
                    .ToString();

                Popup.ShowFail(noModsKnownForItemMsg);

                LineItems = null;
                Hotkeys = null;
                LineIcons = null;
                ApplicableRecipes = null;
                IsVendorOwnedRecipe = null;
                return false;
            }
            BitCost recipeBitCost = new();
            GameObject sampleRecipeObject = null;
            foreach ((TinkerData applicableRecipe, string context) in applicableRecipes)
            {
                recipeBitCost = new();
                int recipeTier = Tier.Constrain(applicableRecipe.Tier);

                int modSlotsUsed = ApplicableItem.GetModificationSlotsUsed();
                int noCostMods = ApplicableItem.GetIntProperty("NoCostMods");

                int existingModsTier = Tier.Constrain(modSlotsUsed - noCostMods + ApplicableItem.GetTechTier());

                recipeBitCost.Increment(BitType.TierBits[recipeTier]);
                recipeBitCost.Increment(BitType.TierBits[existingModsTier]);
                AddNextHotkey(ref Hotkeys);

                string bitCostString = "<" + recipeBitCost + ">";
                string contextString = "[" + context.Color("K") + "]";
                string lineItem =  applicableRecipe.DisplayName + " " + bitCostString + " " + contextString;

                LineItems.Add(lineItem);
                ApplicableRecipes.Add(applicableRecipe);

                sampleRecipeObject = TinkerData.createDataDisk(applicableRecipe);

                // LineIcons.Add(sampleRecipeObject.RenderForUI());
            }
            TinkerInvoice.ScrapTinkerSample(ref sampleRecipeObject);
            return true;
        }
        public bool TryGetApplicableRecipes(
            GameObject ApplicableItem,
            out List<string> LineItems,
            out List<char> Hotkeys,
            out List<IRenderable> LineIcons,
            out List<TinkerData> ApplicableRecipes,
            out List<bool> IsVendorOwnedRecipe)
        {
            return TryGetApplicableRecipes(
                Vendor: ParentObject, 
                ApplicableItem: ApplicableItem,
                InstalledRecipes: InstalledRecipes,
                LineItems: out LineItems,
                Hotkeys: out Hotkeys,
                LineIcons: out LineIcons,
                ApplicableRecipes: out ApplicableRecipes,
                IsVendorOwnedRecipe: out IsVendorOwnedRecipe);
        }

        public static bool VendorHasSkillToIdentify(GameObject Vendor, string What, bool Silent = false)
        {
            if (GetIdentifyLevel(Vendor) <= 0)
            {
                if (!Silent)
                {
                    string tinkerUnableIdentifyMsg = "=subject.Name= =subject.verb:don't= have the skill to identify " + What + "."
                        .StartReplace()
                        .AddObject(Vendor)
                        .ToString();

                    Popup.Show(tinkerUnableIdentifyMsg);
                }
                return false;
            }
            return true;
        }
        public bool VendorHasSkillToIdentify(string What, bool Silent = false)
        {
            return VendorHasSkillToIdentify(ParentObject, What, Silent);
        }

        public static bool CheckItemNotUnderstoodByShopper(GameObject Shopper, GameObject Item, string What, bool Silent = false)
        {
            if (Item.Understood() || (!Shopper.IsPlayer() && GetIdentifyLevel(Shopper) >= Item.GetComplexity()))
            {
                if (!Silent)
                {
                    string alreadyKnowItemMsg = "=subject.Name= already =subject.verb:understand= " + What + "."
                    .StartReplace()
                    .AddObject(Shopper)
                    .ToString();

                    Popup.ShowFail(alreadyKnowItemMsg);
                }
                return false;
            }
            return true;
        }

        public static bool CheckShopperCapableOfUnderstanding(GameObject Vendor, GameObject Shopper, bool Silent = false)
        {
            if (Vendor == null || Shopper == null)
            {
                return false;
            }
            if (Shopper.HasPart<Dystechnia>())
            {
                string dystechniaFailMsg = "=subject.Name= can't understand =object.name's= explanation."
                    .StartReplace()
                    .AddObject(Shopper)
                    .AddObject(Vendor)
                    .ToString();

                if (!Silent)
                {
                    Popup.ShowFail(dystechniaFailMsg);
                }
                return false;
            }
            return true;
        }
        public bool CheckShopperCapableOfUnderstanding(GameObject Shopper, bool Silent = false)
        {
            return CheckShopperCapableOfUnderstanding(ParentObject, Shopper, Silent);
        }

        public static bool VendorCanExplain(GameObject Vendor, GameObject Item, string What, bool Silent = false)
        {
            int identifyLevel = GetIdentifyLevel(Vendor);
            if (identifyLevel <= 0 && Item.HasProperty("_stock"))
            {
                identifyLevel += Vendor.GetIntProperty(CAN_IDENTIFY_STOCK, 0);
            }
            if (identifyLevel < Item.GetComplexity())
            {
                if (!Silent)
                {
                    string tooComplexForTinkerFailMsg = What + " is too complex for =subject.name= to explain."
                        .StartReplace()
                        .AddObject(Vendor)
                        .ToString();

                    Popup.ShowFail(tooComplexForTinkerFailMsg);
                }
                return false;
            }
            return true;
        }
        public bool VendorCanExplain(GameObject Item, string What, bool Silent = false)
        {
            return VendorCanExplain(ParentObject, Item, What, Silent);
        }

        public static bool CheckTooExpensive(
            GameObject Vendor,
            GameObject Shopper,
            int DramsCost,
            string ToDoWhat,
            GameObject WithItem = null,
            TinkerInvoice TinkerInvoice = null,
            string Extra = null,
            bool Silent = false)
        {
            if (!Shopper.CanAfford(DramsCost))
            {
                if (!Silent)
                {
                    string dramsCostString = DramsCost.Things("dram").Color("C");
                    string baseFailMsg =
                        ("=shopper.Name= =shopper.verb:don't= have the required " + dramsCostString +
                        " to have =vendor.name= " + ToDoWhat);

                    string tinkerInvoiceMsg = TinkerInvoice != null ? ("\n\n" + TinkerInvoice) : "";

                    string extraMsg = Extra != null ? ("\n\n" + Extra) : "";

                    string tooExpensiveFailMsg = (baseFailMsg + tinkerInvoiceMsg + extraMsg)
                        .StartReplace()
                        .AddObject(Shopper, "shopper")
                        .AddObject(Vendor, "vendor")
                        .AddObject(WithItem, "item")
                        .ToString();

                    Popup.ShowFail(tooExpensiveFailMsg);
                }
                return true;
            }
            return false;
        }
        public bool CheckTooExpensive(
            GameObject Shopper,
            int DramsCost,
            string ToDoWhat,
            GameObject WithItem = null,
            TinkerInvoice TinkerInvoice = null,
            string Extra = null,
            bool Silent = false)
        {
            return CheckTooExpensive(
                Vendor: ParentObject,
                Shopper: Shopper,
                DramsCost: DramsCost,
                ToDoWhat: ToDoWhat,
                WithItem: WithItem,
                TinkerInvoice: TinkerInvoice,
                Extra: Extra,
                Silent: Silent);
        }

        public static bool ConfirmTinkerService(
            GameObject Vendor,
            GameObject Shopper,
            int DramsCost,
            string DoWhat = null,
            GameObject WithItem = null,
            TinkerInvoice TinkerInvoice = null,
            string ExtraBefore = null,
            string ExtraAfter = null,
            bool SetTinkerInvoiceHold = false)
        {
            string dramsCostString = TinkerInvoice.DramsCostString(DramsCost);

            List<string> messageList = new()
            {
                !ExtraBefore.IsNullOrEmpty() ? ExtraBefore : "",
                !DoWhat.IsNullOrEmpty() ? ("=vendor.Name= will " + DoWhat + " for " + dramsCostString + " of fresh water.") : "",
                TinkerInvoice ?? "",
                !ExtraAfter.IsNullOrEmpty() ? ExtraAfter : ""
            };
            string combinedMessages = "";
            foreach (string message in messageList)
            {
                if (!combinedMessages.IsNullOrEmpty() && !message.IsNullOrEmpty())
                {
                    combinedMessages += "\n\n";
                }
                combinedMessages += message;
            }

            string confirmServiceMsg = combinedMessages
                .StartReplace()
                .AddObject(Shopper, "shopper")
                .AddObject(Vendor, "vendor")
                .AddObject(WithItem, "item")
                .ToString();

            bool output = Popup.ShowYesNo(confirmServiceMsg) == DialogResult.Yes;
            if (SetTinkerInvoiceHold && TinkerInvoice != null)
            {
                TinkerInvoice.HoldForPlayer = output;
            }
            return output;
        }
        public bool ConfirmTinkerService(
            int DramsCost,
            GameObject Shopper,
            string DoWhat,
            GameObject WithItem = null,
            TinkerInvoice TinkerInvoice = null,
            string Extra = null,
            bool SetTinkerInvoiceHold = false)
        {
            return ConfirmTinkerService(
                Vendor: ParentObject,
                Shopper: Shopper,
                DramsCost: DramsCost,
                DoWhat: DoWhat,
                WithItem: WithItem,
                TinkerInvoice: TinkerInvoice,
                ExtraAfter: Extra,
                SetTinkerInvoiceHold: SetTinkerInvoiceHold);
        }

        public static bool VendorDoBuild(GameObject Vendor, TinkerInvoice TinkerInvoice, GameObject RecipeIngredientSupplier)
        {
            if (Vendor == null || TinkerInvoice == null || TinkerInvoice.Recipe is not TinkerData tinkerDatum)
            {
                Popup.ShowFail("That trader or recipe doesn't exist (this is an error).");
                MetricsManager.LogModError(Utils.ThisMod, "Missing one | " + 
                    nameof(Vendor) + ": " + (Vendor == null).ToString() + ", " + 
                    nameof(TinkerInvoice) + ": " + (TinkerInvoice == null).ToString() + ", " + 
                    nameof(tinkerDatum) + ": " + (TinkerInvoice?.Recipe == null).ToString() + ".");
                return false;
            }
            GameObject tinkerSampleItem = TinkerInvoice.CreateTinkerSample(tinkerDatum.Blueprint);
            try
            {
                GameObject player = The.Player;
                bool Interrupt = false;
                int tinkeringBonus = GetTinkeringBonusEvent.GetFor(Vendor, tinkerSampleItem, "BonusMod", 0, 0, ref Interrupt);
                if (Interrupt)
                {
                    return false;
                }

                if (TinkerInvoice.SelectedIngredient != null)
                {
                    GameObject ingredientObject = TinkerInvoice.SelectedIngredient;
                    if (!RecipeIngredientSupplier.RemoveFromInventory(ingredientObject))
                    {
                        string invalidIngredientMsg = "=subject.Name= can't use =object.t= as an ingredient!="
                            .StartReplace()
                            .AddObject(Vendor)
                            .AddObject(ingredientObject)
                            .ToString();

                        Popup.ShowFail(invalidIngredientMsg);
                        return false;
                    }
                }
                Inventory inventory = TinkerInvoice.HoldForPlayer ? Vendor.Inventory : player.Inventory;
                TinkerItem tinkerItem = tinkerSampleItem.GetPart<TinkerItem>();
                GameObject tinkeredItem = null;
                for (int i = 0; i < Math.Max(tinkerItem.NumberMade, 1); i++)
                {
                    tinkeredItem = GameObject.Create(
                        Blueprint: tinkerDatum.Blueprint,
                        SetModNumber: tinkeringBonus.in100() ? 1 : 0, 
                        Context: "Tinkering");

                    TinkeringHelpers.StripForTinkering(tinkeredItem);
                    tinkeredItem.MakeUnderstood();
                    tinkeredItem.SetIntProperty("TinkeredItem", 1);
                    TinkeringHelpers.CheckMakersMark(tinkeredItem, Vendor, null, "Tinkering");

                    if (TinkerInvoice.HoldForPlayer)
                    {
                        var heldForPlayer = tinkeredItem.AddPart(new UD_HeldForPlayer(
                            Vendor: Vendor,
                            HeldFor: player,
                            DepositPaid: TinkerInvoice.GetDepositCost(),
                            WeeksInstead: !Vendor.HasPart<GenericInventoryRestocker>()));

                        Vendor.RegisterEvent(heldForPlayer, StartTradeEvent.ID, Serialize: true);
                    }
                    inventory.AddObject(tinkeredItem);
                    if (!TinkerInvoice.HoldForPlayer)
                    {
                        tinkeredItem?.CheckStack();
                    }
                }

                string singleShortKnownDisplayName = tinkerSampleItem.GetDisplayName(AsIfKnown: true, Single: true, Short: true);
                string whatWasTinkeredUp = tinkerItem.NumberMade > 1
                    ? (Grammar.Cardinal(tinkerItem.NumberMade) + " " + Grammar.Pluralize(singleShortKnownDisplayName))
                    : "=object.a= " + singleShortKnownDisplayName;


                string itemTinkeredMsg = ("=subject.Name= =subject.verb:tinker= up " + whatWasTinkeredUp + "!")
                    .StartReplace()
                    .AddObject(Vendor)
                    .AddObject(tinkerSampleItem)
                    .ToString();

                string comeBackToPickItUp = "";
                if (TinkerInvoice.HoldForPlayer)
                {
                    string themIt = tinkerSampleItem.themIt();
                    comeBackToPickItUp += 
                        ("\n\n" + "Once =subject.name= =subject.verb:have= the drams for "
                        + themIt + ", come back to pick " + themIt + " up!")
                            .StartReplace()
                            .AddObject(player)
                            .ToString();
                }

                SoundManager.PlayUISound("sfx_ability_buildRecipeItem");
                Popup.Show(itemTinkeredMsg + comeBackToPickItUp);
            }
            finally
            {
                TinkerInvoice.ScrapTinkerSample(ref tinkerSampleItem);
            }
            return true;
        }
        public static bool VendorDoMod(GameObject Vendor, GameObject Item, TinkerInvoice TinkerInvoice, GameObject RecipeIngredientSupplier)
        {
            if (Vendor == null || Item == null || TinkerInvoice.Recipe == null)
            {
                Popup.ShowFail($"That trader, item, or recipe doesn't exist (this is an error).");
                return false;
            }
            TinkerData tinkerData = TinkerInvoice.Recipe;
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
                        string noUnequipMsg = "=subject.Name= can't unequip =object.t=."
                            .StartReplace()
                            .AddObject(player)
                            .AddObject(Item)
                            .ToString();

                        Popup.ShowFail(noUnequipMsg);
                        return false;
                    }
                }

                if (!TinkerInvoice.UseSelectedIngredient(RecipeIngredientSupplier))
                {
                    return false;
                }

                GameObject modItem = Item.SplitFromStack();
                int itemTier = modItem.GetTier();
                string itemNameBeforeMod = modItem.t(Single: true, Stripped: true); // This will need updated with lang branch

                didMod = ItemModding.ApplyModification(
                    Object: modItem,
                    ModPartName: tinkerData.PartName,
                    ModPart: out IModification ModPart,
                    Tier: itemTier,
                    DoRegistration: true,
                    Actor: Vendor);

                TinkeringHelpers.CheckMakersMark(modItem, Vendor, ModPart, "Vendor");

                if (didMod)
                {
                    modItem.MakeUnderstood();
                    SoundManager.PlayUISound("Sounds/Abilities/sfx_ability_tinkerModItem");

                    string didModMsg = 
                        ("=subject.Name= =subject.verb:mod= " + itemNameBeforeMod + " to be " +
                        (ModPart.GetModificationDisplayName() ?? tinkerData.DisplayName).Color("C"))
                            .StartReplace()
                            .AddObject(Vendor)
                            .ToString();

                    Popup.Show(didModMsg);

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

        public static bool VendorDoIdentify(GameObject Vendor, GameObject UnknownItem, int DramsCost, string IdentifyWhat)
        {
            The.Player.UseDrams(DramsCost);
            Vendor.GiveDrams(DramsCost);

            // this is necessary until lang hits main, =object.a= respects examiner obfuscation
            string anIdentifiedItem = Grammar.A(UnknownItem.GetReferenceDisplayName(WithoutTitles: true, Short: true));

            string identifiedMsg = ("=subject.Name= =subject.verb:identify= " + IdentifyWhat + " as " + anIdentifiedItem + ".")
                .StartReplace()
                .AddObject(Vendor)
                .AddObject(UnknownItem, "item")
                .ToString();

            if (UnknownItem.MakeUnderstood())
            {
                Popup.Show(identifiedMsg);
                return true;
            }
            return false;
        }

        public static bool VendorDoRecharge(GameObject Vendor, GameObject Item)
        {
            if (Vendor == null || Item == null)
            {
                Popup.ShowFail($"That trader or item doesn't exist (this is an error).");
                return false;
            }
            GameObject player = The.Player;
            bool anyParts = false;
            bool anyRechargeable = false;
            bool anyRecharged = false;
            foreach (var rechargablePart in Item.GetPartsDescendedFrom<IRechargeable>())
            {
                anyParts = true;
                if (!rechargablePart.CanBeRecharged())
                {
                    continue;
                }

                anyRechargeable = true;
                int rechargeAmount = rechargablePart.GetRechargeAmount();
                if (rechargeAmount <= 0)
                {
                    continue;
                }
                char rechargeBit = rechargablePart.GetRechargeBit();
                int rechargeValue = rechargablePart.GetRechargeValue();
                string bits = rechargeBit.GetString();

                int bitsToRechargeFully = rechargeAmount / rechargeValue;
                if (bitsToRechargeFully < 1)
                {
                    bitsToRechargeFully = 1;
                }

                BitCost bitCost = new(bits);

                Item.SplitStack(1, Item.InInventory);


                if (!PickBitsSupplier(
                    Vendor: Vendor,
                    ForObject: Item,
                    BitCost: bitCost,
                    RecipeBitSupplier: out GameObject rechargeBitSupplier,
                    BitSupplierBitLocker: out BitLocker bitSupplierBitLocker,
                    Title: "Recharge =subject.t=",
                    CheckHasBits: false))
                {
                    anyParts = false;
                    anyRechargeable = false;
                    continue;
                }
                bool vendorSuppliesBits = rechargeBitSupplier == Vendor;

                int bitCount = BitLocker.GetBitCount(rechargeBitSupplier, rechargeBit);

                string bitTypeBitString = BitType.GetString(bits) + " bit";

                if (bitCount == 0)
                {
                    string noBitsMsg =
                        ("=subject.Name= =subject.verb:don't= have any " + bitTypeBitString.Pluralize() + ", " +
                        "which are required for recharging =object.t=.\n\n" +
                        "=subject.Subjective= =subject.verb:have:afterpronoun=:\n" +
                        bitSupplierBitLocker.GetBitsString())
                            .StartReplace()
                            .AddObject(rechargeBitSupplier)
                            .AddObject(Item)
                            .ToString();

                    Popup.ShowFail(Message: noBitsMsg);

                    anyParts = false;
                    anyRechargeable = false;
                    continue;
                }

                int availableBitsToRecharge = Math.Min(bitCount, bitsToRechargeFully);

                string bitsToRechargeFullyString = bitsToRechargeFully.Things(bitTypeBitString).Color("C");

                string takesToRechargeString = ("It would take " + bitsToRechargeFullyString + " for =vendor.Name= to fully recharge =subject.t=.")
                    .StartReplace()
                    .AddObject(Item)
                    .AddObject(player)
                    .AddObject(rechargeBitSupplier, "vendor")
                    .ToString();

                string supplierHasBitsString = ("=subject.Subjective= =subject.verb:have:afterpronoun= " + bitCount.Color("C") + ".")
                    .StartReplace()
                    .AddObject(rechargeBitSupplier)
                    .ToString();

                string howManyBitsString = "How many =subject.verb:do= =subject.name= want to use?"
                    .StartReplace()
                    .AddObject(player)
                    .ToString();

                string howManyBitsToUseMsg = takesToRechargeString + "\n" + supplierHasBitsString + " " + howManyBitsString;

                int chosenBitsToUse = Popup.AskNumber(
                    Message: howManyBitsToUseMsg,
                    Start: availableBitsToRecharge,
                    Max: availableBitsToRecharge)
                    .GetValueOrDefault();

                if (chosenBitsToUse < 1)
                {
                    anyParts = false;
                    anyRechargeable = false;
                    continue;
                }

                bitCost.Clear();
                bitCost.Add(rechargeBit, chosenBitsToUse);

                TinkerInvoice tinkerInvoice = new(Vendor, TinkerInvoice.RECHARGE, null, bitCost, Item)
                {
                    VendorSuppliesBits = vendorSuppliesBits,
                };
                int totalDramsCost = tinkerInvoice.GetTotalCost();
                
                if (!CheckTooExpensive(
                    Vendor: Vendor,
                    Shopper: player,
                    DramsCost: totalDramsCost,
                    ToDoWhat: "recharge this item.",
                    TinkerInvoice: tinkerInvoice)
                    && ConfirmTinkerService(
                        Vendor: Vendor,
                        Shopper: player,
                        DramsCost: totalDramsCost,
                        DoWhat: "recharge this item",
                        TinkerInvoice: tinkerInvoice))
                {
                    bitSupplierBitLocker.UseBits(bitCost);

                    player.UseDrams(totalDramsCost);
                    Vendor.GiveDrams(totalDramsCost);

                    player.UseEnergy(1000, "Trade Tinkering Recharge");
                    Vendor.UseEnergy(1000, "Skill Tinkering Recharge");

                    rechargablePart.AddCharge((chosenBitsToUse < bitsToRechargeFully) ? (chosenBitsToUse * rechargeValue) : rechargeAmount);

                    bool fullyRecharged = rechargablePart.GetRechargeAmount() == 0;

                    PlayUISound("Sounds/Abilities/sfx_ability_energyCell_recharge");

                    string partiallyOrFully = !fullyRecharged ? "partially " : "";
                    string endMark = !fullyRecharged ? "." : "!";
                    string rechargedMsg = ("=subject.Name= " + partiallyOrFully + "=subject.verb:recharge= =object.t=" + endMark)
                        .StartReplace()
                        .AddObject(Vendor)
                        .AddObject(Item)
                        .ToString();

                    Popup.Show(rechargedMsg);

                    anyRecharged = true;
                    break;
                }
                else
                {
                    anyParts = false;
                    anyRechargeable = false;
                    break;
                }
            }
            if (!anyRecharged)
            {
                if (!anyParts)
                {
                    Popup.ShowFail("=subject.T= =subject.verb:isn't= an energy cell and does not have a rechargeable capacitor."
                        .StartReplace()
                        .AddObject(Item)
                        .ToString());
                }
                else
                if (!anyRechargeable)
                {
                    Popup.ShowFail("=subject.T= can't be recharged that way."
                        .StartReplace()
                        .AddObject(Item)
                        .ToString());
                }
            }
            Item?.CheckStack();
            Item?.InInventory?.CheckStacks();
            return anyRecharged;
        }

        public override bool AllowStaticRegistration()
        {
            return true;
        }
        public override void Register(GameObject Object, IEventRegistrar Registrar)
        {
            Registrar.Register(AllowTradeWithNoInventoryEvent.ID, EventOrder.EARLY);
            Registrar.Register(AnimateEvent.ID, EventOrder.VERY_LATE);
            base.Register(Object, Registrar);
        }
        public override bool WantEvent(int ID, int Cascade)
        {
            if (ParentObject == null || ParentObject.IsPlayer())
            {
                return base.WantEvent(ID, Cascade);
            }
            return base.WantEvent(ID, Cascade)
                || ID == AfterObjectCreatedEvent.ID
                || ID == TakeOnRoleEvent.ID
                || ID == AfterGameLoadedEvent.ID
                || ID == ImplantAddedEvent.ID
                || ID == ImplantRemovedEvent.ID
                || ID == GetShortDescriptionEvent.ID
                || ID == EndTurnEvent.ID
                || ID == TakeOnRoleEvent.ID
                || ID == StockedEvent.ID
                || ID == StartTradeEvent.ID
                || ID == UD_AfterVendorActionEvent.ID
                || ID == UD_EndTradeEvent.ID
                || ID == UD_GetVendorActionsEvent.ID
                || ID == UD_VendorActionEvent.ID;
        }
        public override bool HandleEvent(AllowTradeWithNoInventoryEvent E)
        {
            if (E.Trader == ParentObject && IsVendorTinker())
            {
                return true;
            }
            return base.HandleEvent(E);
        }
        public override bool HandleEvent(AfterObjectCreatedEvent E)
        {
            if (E.Object == ParentObject)
            {
                InitializeVendorTinker(E);
            }
            return base.HandleEvent(E);
        }
        public override bool HandleEvent(AnimateEvent E)
        {
            if (E.Object == ParentObject)
            {
                InitializeVendorTinker(E);
            }
            return base.HandleEvent(E);
        }
        public override bool HandleEvent(TakeOnRoleEvent E)
        {
            if (E.Object == ParentObject)
            {
                InitializeVendorTinker(E);
            }
            return base.HandleEvent(E);
        }
        public override bool HandleEvent(AfterGameLoadedEvent E)
        {
            if (MigrateFrom != null
                && ParentObject is GameObject vendor
                && vendor.GetBlueprint() is GameObjectBlueprint vendorBlueprint)
            {
                if (MigrateFrom < new Version("0.1.0"))
                {
                    UD_Modding_Toolbox.Utils.MigratePartFieldFromBlueprint(
                        Part: this,
                        Field: ref LearnsOneStockedDataDiskOnRestock,
                        FieldName: nameof(LearnsOneStockedDataDiskOnRestock),
                        Blueprint: vendorBlueprint);

                    UD_Modding_Toolbox.Utils.MigratePartFieldFromBlueprint(
                        Part: this,
                        Field: ref RestockLearnChance,
                        FieldName: nameof(RestockLearnChance),
                        Blueprint: vendorBlueprint);

                    UD_Modding_Toolbox.Utils.MigratePartFieldFromBlueprint(
                        Part: this,
                        Field: ref MatchMakersMarkToDetailColor,
                        FieldName: nameof(MatchMakersMarkToDetailColor),
                        Blueprint: vendorBlueprint);

                    TinkerInitialized = true;

                    if (KnownRecipes.IsNullOrEmpty())
                    {
                        InitializeVendorTinker(E, SkipBits: vendor.HasPart<BitLocker>());
                    }
                }
            }
            return base.HandleEvent(E);
        }
        public override bool HandleEvent(ImplantAddedEvent E)
        {
            if (E.Implantee is GameObject implantee
                && implantee == ParentObject)
            {
                KnowImplantedRecipes(implantee, InstalledRecipes);
            }
            return base.HandleEvent(E);
        }
        public override bool HandleEvent(ImplantRemovedEvent E)
        {
            if (E.Implantee is GameObject implantee
                && implantee == ParentObject)
            {
                KnowImplantedRecipes(implantee, InstalledRecipes);
            }
            return base.HandleEvent(E);
        }
        public override bool HandleEvent(GetShortDescriptionEvent E)
        {
            if (E.Object is GameObject vendor
                && vendor == ParentObject)
            {
                if (DebugBitLockerDebugDescriptions)
                {
                    string bitLockerDescription = vendor.GetPart<BitLocker>()?.GetBitsString();
                    if (!bitLockerDescription.IsNullOrEmpty())
                    {
                        E.Infix.AppendRules("Bit Locker".Color("M") + ":");
                        E.Infix.AppendRules(bitLockerDescription);
                    }
                }
                string recipeBullet = "\u0007 ".Color("K");
                string creditBullet = "\u009B ".Color("c");
                if (DebugKnownRecipesDebugDescriptions)
                {
                    List<TinkerData> knownRecipes = new(GetKnownRecipes());
                    if (vendor.IsPlayer())
                    {
                        knownRecipes = new(TinkerData.KnownRecipes);
                    }
                    if (!knownRecipes.IsNullOrEmpty())
                    {
                        List<string> byteBlueprints = new(UD_TinkeringByte.GetByteBlueprints());
                        List<string> basicRecipes = new();
                        List<string> tierIRecipes = new();
                        List<string> tierIIRecipes = new();
                        List<string> tierIIIRecipes = new();

                        foreach (TinkerData knownRecipe in knownRecipes)
                        {
                            if (byteBlueprints.Contains(knownRecipe.Blueprint))
                            {
                                continue;
                            }
                            string recipeDisplayName = knownRecipe.DisplayName;
                            bool isInstalledRecipe = InstalledRecipes.Contains(knownRecipe);
                            if (knownRecipe.Type == "Mod")
                            {
                                recipeDisplayName = $"[{"Mod".Color("W")}]{recipeDisplayName}";
                            }
                            recipeDisplayName = $"{(isInstalledRecipe ? creditBullet : recipeBullet)}{recipeDisplayName}";
                            switch (DataDisk.GetRequiredSkill(knownRecipe.Tier))
                            {
                                case nameof(UD_Basics):
                                    basicRecipes.TryAdd(recipeDisplayName);
                                    break;
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

                        string noRecipe = recipeBullet + "none";

                        E.Infix.AppendRules("Known Recipes".Color("M") + ":");

                        E.Infix.AppendRules("Basics".Color("W") + ":");
                        if (!basicRecipes.IsNullOrEmpty())
                        {
                            foreach (string basicRecipe in basicRecipes)
                            {
                                E.Infix.AppendRules(basicRecipe);
                            }
                        }
                        else
                        {
                            E.Infix.AppendRules(noRecipe);
                        }

                        E.Infix.AppendRules("Tier I".Color("W") + ":");
                        if (!tierIRecipes.IsNullOrEmpty())
                        {
                            foreach (string tierIRecipe in tierIRecipes)
                            {
                                E.Infix.AppendRules(tierIRecipe);
                            }
                        }
                        else
                        {
                            E.Infix.AppendRules(noRecipe);
                        }

                        E.Infix.AppendRules("Tier II".Color("W") + ":");
                        if (!tierIIRecipes.IsNullOrEmpty())
                        {
                            foreach (string tierIIRecipe in tierIIRecipes)
                            {
                                E.Infix.AppendRules(tierIIRecipe);
                            }
                        }
                        else
                        {
                            E.Infix.AppendRules(noRecipe);
                        }

                        E.Infix.AppendRules("Tier III".Color("W") + ":");
                        if (!tierIIIRecipes.IsNullOrEmpty())
                        {
                            foreach (string tierIIIRecipe in tierIIIRecipes)
                            {
                                E.Infix.AppendRules(tierIIIRecipe);
                            }
                        }
                        else
                        {
                            E.Infix.AppendRules(noRecipe);
                        }

                        E.Infix.AppendLine();
                    }
                }
                if (DebugTinkerSkillsDebugDescriptions)
                {
                    List<BaseSkill> tinkersSkills = vendor.GetPartsDescendedFrom<BaseSkill>(s => s.Name.StartsWith(nameof(Skill.Tinkering)));
                    if (!tinkersSkills.IsNullOrEmpty())
                    {
                        E.Infix.AppendRules("Tinkering Skills".Color("M") + ":");
                        foreach (BaseSkill skill in vendor.GetPartsDescendedFrom<BaseSkill>())
                        {
                            if (skill.GetType().Name.StartsWith(nameof(Skill.Tinkering)))
                            {
                                E.Infix.AppendRules(recipeBullet + skill.DisplayName.Color("y"));
                            }
                        }
                    }
                }
            }
            return base.HandleEvent(E);
        }
        public override bool HandleEvent(EndTurnEvent E)
        {
            if (ParentObject != null && !CanRestockBits)
            {
                CanRestockBits = true;
            }
            return base.HandleEvent(E);
        }
        public override bool HandleEvent(StockedEvent E)
        {
            Debug.ResetIndent();
            if (E.Object is GameObject vendor 
                && vendor == ParentObject
                && IsVendorTinker())
            {
                if (LearnsOneStockedDataDiskOnRestock && RestockLearnChance.in100())
                {
                    LearnFromOneDataDisk();
                }
                if (ScribesKnownRecipesOnRestock)
                {
                    List<TinkerData> knownRecipes = new(GetKnownRecipes());
                    if (!knownRecipes.IsNullOrEmpty())
                    {
                        List<TinkerData> inventoryTinkerData = new();
                        foreach (GameObject dataDiskObject in vendor?.Inventory?.GetObjects(GO => GO.HasPart<DataDisk>()))
                        {
                            if (dataDiskObject.TryGetPart(out DataDisk dataDiskPart)
                                && knownRecipes.Contains(dataDiskPart.Data))
                            {
                                inventoryTinkerData.Add(dataDiskPart.Data);
                            }
                        }
                        foreach (TinkerData knownRecipe in knownRecipes)
                        {
                            if (!UD_TinkeringByte.IsByteBlueprint(knownRecipe.Blueprint) 
                                && !inventoryTinkerData.Contains(knownRecipe) 
                                && RestockScribeChance.in100())
                            {
                                DraftDataDisk(knownRecipe);
                            }
                        }
                    }
                }

                if (CanRestockBits || !vendor.HasPart<BitLocker>())
                {
                    GiveRandomBits(vendor, FromEvent: E);
                }
                CanRestockBits = true;
            }
            Debug.ResetIndent();
            return base.HandleEvent(E);
        }
        public override bool HandleEvent(StartTradeEvent E)
        {
            if (E.Trader is GameObject vendor
                && vendor == ParentObject)
            {
                bool doDebug = true;
                Debug.GetIndent(out int indent);

                if (IsVendorTinker())
                {
                    List<TinkerData> knownRecipes = new(GetKnownRecipes(IncludeInstalled: false));

                    Debug.Entry(4,
                        nameof(StartTradeEvent) + "(" + ParentObject.DebugName + ")",
                        nameof(knownRecipes) + "...",
                        Indent: indent + 1, Toggle: doDebug);

                    foreach (TinkerData tinkerDatum in knownRecipes)
                    {
                        Debug.LoopItem(4, tinkerDatum.PartName ?? tinkerDatum.Blueprint, Indent: indent + 2, Toggle: doDebug);
                    }

                    KnownRecipes.Clear();
                    foreach (TinkerData knownRecipe in knownRecipes)
                    {
                        LearnTinkerData(knownRecipe, IgnoreSkillRequirement: true);
                    }
                    ReceiveKnownRecipeDisplayItems();
                    ReceiveBitLockerDisplayItem();
                }

                List<GameObject> itemsOnHold = vendor.GetInventory(GO => GO.HasPart<UD_HeldForPlayer>());
                if (!itemsOnHold.IsNullOrEmpty())
                {
                    foreach (GameObject itemOnHold in itemsOnHold)
                    {
                        itemOnHold.GetPart<UD_HeldForPlayer>().CheckWeeks();
                    }
                }

                Debug.SetIndent(indent);
            }
            return base.HandleEvent(E);
        }
        public virtual bool HandleEvent(UD_AfterVendorActionEvent E)
        {
            if (E.Vendor == ParentObject)
            {
                // ReceiveBitLockerDisplayItem();
            }
            return base.HandleEvent(E);
        }
        public virtual bool HandleEvent(UD_GetVendorActionsEvent E)
        {
            if (E.Vendor is GameObject vendor
                && vendor == ParentObject
                && !vendor.IsPlayer()
                && E.Item is GameObject item
                && The.Player is GameObject player)
            {
                bool isVendorTinker = IsVendorTinker();
                int vendorIdentifyLevel = GetIdentifyLevel(vendor);
                bool itemUnderstood = item.Understood();

                Tinkering_Repair vendorRepairSkill = E.Vendor.GetPart<Tinkering_Repair>();

                if (item.TryGetPart(out DataDisk dataDisk))
                {
                    if (dataDisk?.Data?.Type == "Mod")
                    {
                        E.AddAction(
                            Name: "Mod Data Disk",
                            Display: "mod an item with tinkering",
                            Command: COMMAND_MOD,
                            PreferToHighlight: "tinkering",
                            Key: 'T',
                            Priority: -4,
                            ClearAndSetUpTradeUI: true);
                    }
                    else
                    if (dataDisk?.Data?.Type == "Build"
                        && TinkerInvoice.CreateTinkerSample(dataDisk?.Data.Blueprint) is GameObject sampleObject)
                    {
                        if (sampleObject.Understood()
                            || player.HasSkill(nameof(Skill.Tinkering))
                            || Scanning.HasScanningFor(player, Scanning.Scan.Tech))
                        {
                            E.AddAction(
                                Name: "Build Data Disk",
                                Display: "tinker item",
                                Command: COMMAND_BUILD,
                                PreferToHighlight: "tinker",
                                Key: 'T',
                                Priority: -4,
                                DramsCost: 100,
                                ClearAndSetUpTradeUI: true);
                        }
                        else
                        if (vendorIdentifyLevel > 0)
                        {
                            E.AddAction(
                                Name: "Identify Data Disk",
                                Display: "identify recipe",
                                Command: COMMAND_IDENTIFY_BY_DATADISK,
                                Key: 'i',
                                Priority: 8,
                                ClearAndSetUpTradeUI: true);
                        }
                        TinkerInvoice.ScrapTinkerSample(ref sampleObject);
                    }
                }
                else
                {
                    if (item.InInventory != vendor
                        && !ItemModding.ModKey(item).IsNullOrEmpty()
                        && itemUnderstood)
                    {
                        E.AddAction(
                            Name: "Mod This Item", 
                            Display: "mod with tinkering",
                            Command: COMMAND_MOD,
                            PreferToHighlight: "tinkering",
                            Key: 'T',
                            Priority: -2,
                            ClearAndSetUpTradeUI: true);
                    }

                    if (vendorIdentifyLevel <= 0 && item.HasProperty("_stock"))
                    {
                        vendorIdentifyLevel += vendor.GetIntProperty(CAN_IDENTIFY_STOCK, 0);
                    }
                    if (vendorIdentifyLevel > 0 && !itemUnderstood)
                    {
                        E.AddAction(
                            Name: "Identify",
                            Display: "identify",
                            Command: COMMAND_IDENTIFY_SCALING,
                            Key: 'i',
                            Priority: 8,
                            Override: true,
                            ClearAndSetUpTradeUI: true);
                    }

                    if (EnableOverrideTinkerRepair
                        && item.InInventory != vendor 
                        && vendorRepairSkill != null
                        && IsRepairableEvent.Check(vendor, item, null, vendorRepairSkill, null))
                    {
                        E.AddAction(
                            Name: "Repair",
                            Display: "repair",
                            Command: COMMAND_REPAIR_SCALING,
                            Key: 'r',
                            Priority: 7,
                            Override: true);
                    }

                    if (EnableOverrideTinkerRecharge
                        && vendor.HasSkill(nameof(Tinkering_Tinker1))
                        && (itemUnderstood || vendorIdentifyLevel > item.GetComplexity()) 
                        && item.NeedsRecharge())
                    {
                        E.AddAction(
                            Name: "Recharge",
                            Display: "recharge",
                            Command: COMMAND_RECHARGE_SCALING,
                            Key: 'c',
                            Priority: 6,
                            Override: true,
                            ClearAndSetUpTradeUI: true);
                    }
                }
            }
            return base.HandleEvent(E);
        }
        public virtual bool HandleEvent(UD_VendorActionEvent E)
        {
            if (E.Vendor != null && E.Vendor == ParentObject
                && E.Item != null
                && E.Vendor is GameObject vendor
                && The.Player is GameObject player)
            {
                TinkerInvoice tinkerInvoice = null;
                try
                {
                    if (E.Command == COMMAND_MOD || E.Command == COMMAND_BUILD)
                    {
                        if (vendor.AreHostilesNearby() && vendor.FireEvent("CombatPreventsTinkering"))
                        {
                            string hostileFailMsg = "=subject.Name= can't tinker with hostiles nearby!"
                                .StartReplace()
                                .AddObject(vendor)
                                .ToString();

                            Popup.ShowFail(hostileFailMsg);
                            return false;
                        }
                        if (!vendor.CanMoveExtremities("Tinker", ShowMessage: true, AllowTelekinetic: true))
                        {
                            return false;
                        }
                        GameObject recipeObject = null;
                        UD_VendorKnownRecipe knownRecipePart = null;
                        if (E.Command == COMMAND_BUILD
                            && (E.Item.TryGetPart(out DataDisk dataDiskPart) || E.Item.TryGetPart(out knownRecipePart)))
                        {
                            recipeObject = E.Item;
                            TinkerData tinkerRecipe = dataDiskPart?.Data ?? knownRecipePart?.Data;
                            bool vendorsRecipe = recipeObject.InInventory != player;

                            if (TinkerInvoice.CreateTinkerSample(tinkerRecipe.Blueprint) is not GameObject sampleItem)
                            {
                                MetricsManager.LogModError(Utils.ThisMod, tinkerRecipe);
                                return false;
                            }
                            if (!CanTinkerRecipe(tinkerRecipe, Silent: false))
                            {
                                return false;
                            }
                            try
                            {
                                if (!PickRecipeIngredientSupplier(
                                    ForObject: sampleItem,
                                    TinkerData: tinkerRecipe,
                                    RecipeIngredientSupplier: out GameObject recipeIngredientSupplier,
                                    SelectedIngredient: out GameObject selectedIngredient,
                                    Context: E.Command))
                                {
                                    return false;
                                }
                                bool vendorSuppliesIngredients = recipeIngredientSupplier != player;

                                BitCost bitCost = TinkerInvoice.GetBuildBitCost(tinkerRecipe);

                                if (!PickBitsSupplier(
                                    ForObject: sampleItem,
                                    BitCost: bitCost,
                                    RecipeBitSupplier: out GameObject recipeBitSupplier,
                                    BitSupplierBitLocker: out BitLocker bitSupplierBitLocker,
                                    Title: GetSupplierPickerTinkerDataTitle(tinkerRecipe),
                                    Context: tinkerRecipe.Type))
                                {
                                    return false;
                                }
                                bool vendorSuppliesBits = recipeBitSupplier == vendor;

                                tinkerInvoice = new(vendor, tinkerRecipe, selectedIngredient, bitCost, vendorsRecipe)
                                {
                                    VendorSuppliesIngredients = selectedIngredient == null ? null : vendorSuppliesIngredients,
                                    VendorSuppliesBits = vendorSuppliesBits,
                                };
                                 
                                int totalDramsCost = tinkerInvoice.GetTotalCost();
                                int depositDramCost = tinkerInvoice.GetDepositCost();
                                double itemDramValue = tinkerInvoice.GetItemValue();

                                string dramsCostString = totalDramsCost.Things("dram").Color("C");

                                if ((depositDramCost == 0 || !player.CanAfford(depositDramCost))
                                    && CheckTooExpensive(
                                        Shopper: player,
                                        DramsCost: totalDramsCost,
                                        ToDoWhat: "tinker " + "item".ThisTheseN(tinkerInvoice.NumberMade) + ".",
                                        TinkerInvoice: tinkerInvoice))
                                {
                                    return false;
                                }
                                string holdTimeUnit = vendor.HasPart<GenericInventoryRestocker>() ? "restock" : "week";
                                if (!player.CanAfford(totalDramsCost)
                                    && depositDramCost > 0
                                    && (CheckTooExpensive(
                                        Shopper: player,
                                        DramsCost: depositDramCost,
                                        ToDoWhat: "tinker and hold " + "item".ThisTheseN(tinkerInvoice.NumberMade))
                                        || !ConfirmTinkerService(
                                            Vendor: vendor,
                                            Shopper: player,
                                            DramsCost: depositDramCost,
                                            TinkerInvoice: tinkerInvoice,
                                            ExtraBefore: tinkerInvoice.GetDepositMessage(),
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
                                            DoWhat: "tinker " + "item".ThisTheseN(tinkerInvoice.NumberMade),
                                            TinkerInvoice: tinkerInvoice)
                                        )
                                    && VendorDoBuild(
                                        Vendor: vendor,
                                        TinkerInvoice: tinkerInvoice,
                                        RecipeIngredientSupplier: recipeIngredientSupplier))
                                {
                                    bitSupplierBitLocker.UseBits(bitCost);

                                    player.UseDrams(tinkerInvoice.HoldForPlayer ? depositDramCost : totalDramsCost);
                                    vendor.GiveDrams(tinkerInvoice.HoldForPlayer ? depositDramCost : totalDramsCost);

                                    player.UseEnergy(1000, "Trade Tinkering Build");
                                    vendor.UseEnergy(1000, "Skill Tinkering Build");

                                    return true;
                                }
                                return false;
                            }
                            finally
                            {
                                TinkerInvoice.ScrapTinkerSample(ref sampleItem);
                                tinkerInvoice?.Clear();

                                player?.Inventory?.CheckStacks();
                                vendor?.Inventory?.CheckStacks();
                            }
                        }
                        else
                        if (E.Command == COMMAND_MOD)
                        {
                            GameObject selectedObject = null;
                            TinkerData tinkerRecipe = null;
                            string modName = null;
                            bool isVendorsRecipe = true;
                            try
                            {
                                if (!E.Item.TryGetPart(out dataDiskPart) && !E.Item.TryGetPart(out knownRecipePart))
                                {
                                    selectedObject = E.Item;

                                    if (!TryGetApplicableRecipes(selectedObject,
                                        out List<string> lineItems,
                                        out List<char> hotkeys,
                                        out List<IRenderable> lineIcons,
                                        out List<TinkerData> recipes,
                                        out List<bool> isVendorRecipe))
                                    {
                                        return false;
                                    }

                                    int selectedOption = Popup.PickOption(
                                        Title: "select which item mod to apply",
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
                                    tinkerRecipe = recipes[selectedOption];
                                    modName = $"{tinkerRecipe.DisplayName}";
                                    isVendorsRecipe = isVendorRecipe[selectedOption];
                                }
                                else
                                {
                                    recipeObject = E.Item;
                                    tinkerRecipe = dataDiskPart?.Data ?? knownRecipePart?.Data;
                                    isVendorsRecipe = recipeObject.InInventory != player;

                                    if (!CanTinkerRecipe(tinkerRecipe, Silent: false))
                                    {
                                        return false;
                                    }
                                    modName = tinkerRecipe.DisplayName;

                                    if (!TryGetApplicableItems(
                                        Actor: player,
                                        ApplicableRecipe: tinkerRecipe,
                                        LineItems: out List<string> lineItems,
                                        Hotkeys: out List<char> hotkeys,
                                        LineIcons: out List<IRenderable> lineIcons,
                                        ApplicableItems: out List<GameObject> applicableItems))
                                    {
                                        return false;
                                    }

                                    int selectedOption = Popup.PickOption(
                                        Title: "select an item to apply " + modName + " to",
                                        Sound: "Sounds/UI/ui_notification",
                                        Options: lineItems.ToArray(),
                                        Hotkeys: hotkeys.ToArray(),
                                        Icons: lineIcons,
                                        Context: recipeObject,
                                        IntroIcon: recipeObject.RenderForUI(),
                                        AllowEscape: true,
                                        PopupID: "VendorTinkeringApplyModMenu:" + (recipeObject?.IDIfAssigned ?? "(noid)"));
                                    if (selectedOption < 0)
                                    {
                                        return false;
                                    }
                                    selectedObject = applicableItems[selectedOption];

                                    if (selectedObject == null)
                                    {
                                        return false;
                                    }
                                }

                                if (selectedObject != null && tinkerRecipe != null)
                                {
                                    if (!ItemModding.ModificationApplicable(tinkerRecipe.PartName, selectedObject))
                                    {
                                        string modInapplicableMsg = "=sunbject.T= can't have " + modName + " applied to " + selectedObject.themIt()
                                            .StartReplace()
                                            .AddObject(selectedObject)
                                            .ToString();

                                        Popup.ShowFail(modInapplicableMsg);
                                        return false;
                                    }

                                    if (!PickRecipeIngredientSupplier(
                                        ForObject: selectedObject,
                                        TinkerData: tinkerRecipe,
                                        RecipeIngredientSupplier: out GameObject recipeIngredientSupplier,
                                        SelectedIngredient: out GameObject selectedIngredient,
                                        Context: E.Command))
                                    {
                                        return false;
                                    }
                                    bool vendorSuppliesIngredients = recipeIngredientSupplier != player;

                                    BitCost bitCost = TinkerInvoice.GetModBitCostForObject(tinkerRecipe, selectedObject);

                                    if (!PickBitsSupplier(
                                        ForObject: selectedObject,
                                        BitCost: bitCost,
                                        RecipeBitSupplier: out GameObject recipeBitSupplier,
                                        BitSupplierBitLocker: out BitLocker bitSupplierBitLocker,
                                        Title: GetSupplierPickerTinkerDataTitle(tinkerRecipe),
                                        Context: tinkerRecipe.Type))
                                    {
                                        return false;
                                    }
                                    bool vendorSuppliesBits = recipeBitSupplier == vendor;

                                    tinkerInvoice = new(vendor, tinkerRecipe, bitCost, isVendorsRecipe, selectedObject)
                                    {
                                        VendorSuppliesIngredients = vendorSuppliesIngredients,
                                        VendorSuppliesBits = vendorSuppliesBits,
                                    };

                                    int totalDramsCost = tinkerInvoice.GetTotalCost();
                                    if (!CheckTooExpensive(
                                        Shopper: player,
                                        DramsCost: totalDramsCost,
                                        ToDoWhat: "mod this item.",
                                        TinkerInvoice: tinkerInvoice)
                                        && ConfirmTinkerService(
                                            Vendor: vendor,
                                            Shopper: player,
                                            DramsCost: totalDramsCost,
                                            DoWhat: "mod this item",
                                            TinkerInvoice: tinkerInvoice)
                                        && VendorDoMod(
                                            Vendor: vendor,
                                            Item: selectedObject,
                                            TinkerInvoice: tinkerInvoice,
                                            RecipeIngredientSupplier: recipeIngredientSupplier))
                                    {
                                        bitSupplierBitLocker.UseBits(bitCost);

                                        player.UseDrams(totalDramsCost);
                                        vendor.GiveDrams(totalDramsCost);

                                        player.UseEnergy(1000, "Trade Tinkering Mod");
                                        vendor.UseEnergy(1000, "Skill Tinkering Mod");

                                        return true;
                                    }
                                    return false;
                                }
                            }
                            finally
                            {
                                tinkerInvoice?.Clear();
                            }
                            return false;
                        }
                    }
                    else
                    if (E.Command == COMMAND_IDENTIFY_BY_DATADISK
                        && E.Item is GameObject dataDiskObject
                        && dataDiskObject.TryGetPart(out DataDisk dataDiskPart)
                        && dataDiskPart.Data is TinkerData diskDatum
                        && diskDatum.Type == "Build")
                    {
                        if (TinkerInvoice.CreateTinkerSample(diskDatum.Blueprint) is not GameObject sampleItem)
                        {
                            return false;
                        }
                        else
                        { 
                            try
                            {
                                if (!VendorHasSkillToIdentify("schematics on data disks")
                                    && !CheckItemNotUnderstoodByShopper(player, sampleItem, "the item on this data disk")
                                    && !CheckShopperCapableOfUnderstanding(player)
                                    && !VendorCanExplain(sampleItem, "The item on this data disk"))
                                {
                                    return false;
                                }
                                tinkerInvoice = new(Vendor: vendor, Service: TinkerInvoice.IDENTIFY, Item: sampleItem);
                                int dramsCost = vendor.IsPlayerLed() ? 0 : (int)tinkerInvoice.GetExamineCost();
                                string itemOnDataDisk = "the item on the data disk";
                                return !CheckTooExpensive(
                                    Shopper: player,
                                    DramsCost: dramsCost,
                                    ToDoWhat: "identify " + itemOnDataDisk + ".")
                                    && ConfirmTinkerService(
                                        Vendor: vendor,
                                        Shopper: player,
                                        DramsCost: dramsCost,
                                        DoWhat: "identify " + itemOnDataDisk)
                                    && VendorDoIdentify(
                                        Vendor: vendor,
                                        UnknownItem: sampleItem,
                                        DramsCost: dramsCost,
                                        IdentifyWhat: itemOnDataDisk);
                            }
                            finally
                            {
                                TinkerInvoice.ScrapTinkerSample(ref sampleItem);
                            }
                        }
                    }
                    else
                    if (E.Command == COMMAND_IDENTIFY_SCALING
                        && E.Item is GameObject unknownItem)
                    {
                        if (!VendorHasSkillToIdentify("items")
                            && !CheckItemNotUnderstoodByShopper(player, unknownItem, "this item")
                            && !CheckShopperCapableOfUnderstanding(player)
                            && !VendorCanExplain(unknownItem, "This =object.name="))
                        {
                            return false;
                        }
                        tinkerInvoice = new(Vendor: vendor, Service: TinkerInvoice.IDENTIFY, Item: unknownItem);
                        int dramsCost = vendor.IsPlayerLed() ? 0 : (int)tinkerInvoice.GetExamineCost();
                        string theItemName = "the =item.name=";
                        return !CheckTooExpensive(
                            Shopper: player,
                            DramsCost: dramsCost,
                            ToDoWhat: "identify " + theItemName + ".",
                            WithItem: unknownItem)
                            && ConfirmTinkerService(
                                Vendor: vendor,
                                Shopper: player,
                                DramsCost: dramsCost,
                                DoWhat: "identify " + theItemName,
                                WithItem: unknownItem)
                            && VendorDoIdentify(
                                Vendor: vendor,
                                UnknownItem: unknownItem,
                                DramsCost: dramsCost,
                                IdentifyWhat: theItemName);
                    }
                    else
                    if (E.Command == COMMAND_REPAIR_SCALING
                        && E.Item is GameObject repairableItem)
                    {
                        if (!IsVendorTinker() || !vendor.TryGetPart(out Tinkering_Repair vendorRepairSkill))
                        {
                            string notTinkerFailMsg = "=subject.Name= =subject.verb:don't= have the skill to repair =object.t=!"
                                .StartReplace()
                                .AddObject(vendor)
                                .AddObject(repairableItem)
                                .ToString();

                            Popup.ShowFail(notTinkerFailMsg);
                            return false;
                        }
                        if (!vendor.CanMoveExtremities("Repair", ShowMessage: true, AllowTelekinetic: true))
                        {
                            return false;
                        }
                        if (!IsRepairableEvent.Check(vendor, repairableItem, null, vendorRepairSkill, null))
                        {
                            string notBrokenFailMsg = "=subject.T= =subject.verb:are= not broken!"
                                .StartReplace()
                                .AddObject(repairableItem)
                                .ToString();

                            Popup.ShowFail(notBrokenFailMsg);
                            return false;
                        }
                        if (vendor.GetTotalConfusion() > 0)
                        {
                            string tooConfusedFailMsg = "=subject.Name= =subject.verb:are= too confused to repair =object.t=."
                                .StartReplace()
                                .AddObject(vendor)
                                .AddObject(repairableItem)
                                .ToString();

                            Popup.ShowFail(tooConfusedFailMsg);
                            return false;
                        }
                        if (vendor.AreHostilesNearby() && vendor.FireEvent("CombatPreventsRepair"))
                        {
                            string hostilesNearFailMsg = "=subject.Name= can't repair =object.t= with hostiles nearby."
                                .StartReplace()
                                .AddObject(vendor)
                                .AddObject(repairableItem)
                                .ToString();

                            Popup.ShowFail(hostilesNearFailMsg);
                            return false;
                        }

                        BitCost bitCost = new(Tinkering_Repair.GetRepairCost(repairableItem));

                        if (!Tinkering_Repair.IsRepairableBy(repairableItem, vendor, bitCost, vendorRepairSkill, null))
                        {
                            string tooComplexFailMsg = "=subject.T= =subject.verb:are= too complex for =object.name= to repair."
                                .StartReplace()
                                .AddObject(repairableItem)
                                .AddObject(vendor)
                                .ToString();

                            Popup.ShowFail(tooComplexFailMsg);

                            vendor.FireEvent(Event.New("UnableToRepair", "Object", repairableItem));
                            return false;
                        }
                        if (vendor.HasTagOrProperty("NoRepair"))
                        {
                            string irrepairableFailMsg = "=subject.T= can't be repaired."
                                .StartReplace()
                                .AddObject(repairableItem)
                                .ToString();

                            Popup.ShowFail(irrepairableFailMsg);
                            return false;
                        }

                        if (!PickBitsSupplier(
                            ForObject: repairableItem,
                            BitCost: bitCost,
                            RecipeBitSupplier: out GameObject bitSupplier,
                            BitSupplierBitLocker: out BitLocker bitSupplierBitLocker,
                            Title: "Repair =item.t=",
                            Context: "Repair"))
                        {
                            return false;
                        }
                        bool vendorSuppliesBits = bitSupplier == vendor;

                        tinkerInvoice = new(vendor, TinkerInvoice.REPAIR, null, bitCost, repairableItem)
                        {
                            VendorSuppliesBits = vendorSuppliesBits,
                        };
                        int totalDramsCost = tinkerInvoice.GetTotalCost();
                        string dramsCostString = totalDramsCost.Things("dram").Color("C");
                        if (!CheckTooExpensive(
                            Shopper: player,
                            DramsCost: totalDramsCost,
                            ToDoWhat: "repair this item.")
                            && ConfirmTinkerService(
                                Vendor: vendor,
                                Shopper: player,
                                DramsCost: totalDramsCost,
                                DoWhat: "repair this item",
                                TinkerInvoice: tinkerInvoice))
                        {
                            bitSupplierBitLocker.UseBits(bitCost);

                            player.UseDrams(totalDramsCost);
                            vendor.GiveDrams(totalDramsCost);

                            vendor.PlayWorldOrUISound("Sounds/Misc/sfx_interact_artifact_repair");

                            string repairedMsg = "=subject.Name= =subject.verb:repair= =object.t=."
                                .StartReplace()
                                .AddObject(vendor)
                                .AddObject(repairableItem)
                                .ToString();

                            Popup.Show(repairedMsg);

                            RepairedEvent.Send(vendor, repairableItem, null, vendorRepairSkill);

                            player.UseEnergy(1000, "Trade Tinkering Repair", null, null);
                            vendor.UseEnergy(1000, "Skill Tinkering Repair", null, null);

                            return true;
                        }
                        return false;
                    }
                    else
                    if (E.Command == COMMAND_RECHARGE_SCALING
                        && E.Item is GameObject rechargeableItem)
                    {
                        if (!IsVendorTinker() || !vendor.HasSkill(nameof(Tinkering_Tinker1)))
                        {
                            string notTinkerFailMsg = "=subject.Name= =subject.verb:don't= have the skill to recharge =object.t=!"
                                .StartReplace()
                                .AddObject(vendor)
                                .AddObject(rechargeableItem)
                                .ToString();

                            Popup.ShowFail(notTinkerFailMsg);
                            return false;
                        }
                        if (!vendor.CanMoveExtremities("Recharge", ShowMessage: true, AllowTelekinetic: true))
                        {
                            return false;
                        }
                        if (VendorDoRecharge(vendor, rechargeableItem))
                        {
                            return true;
                        }
                        return false;
                    }
                }
                finally
                {
                    tinkerInvoice?.Clear();
                }
            }
            return base.HandleEvent(E);
        }

        public override void Write(GameObject Basis, SerializationWriter Writer)
        {
            base.Write(Basis, Writer);

            Writer.Write(KnownRecipes);
            Writer.Write(TinkerInitialized);
        }
        public override void Read(GameObject Basis, SerializationReader Reader)
        {
            var modVersion = Reader.ModVersions[Utils.ThisMod.ID];
            if (modVersion < new Version("0.1.0"))
            {
                MigrateFrom = modVersion;
            }
            base.Read(Basis, Reader);

            KnownRecipes = Reader.ReadList<TinkerData>();
            
            if (modVersion >= new Version("0.1.0"))
            {
                TinkerInitialized = Reader.ReadBoolean();
            }
        }
        public override void FinalizeRead(SerializationReader Reader)
        {
            base.FinalizeRead(Reader);
            if (!ParentObject.IsPlayer())
            {
                KnowImplantedRecipes();
            }
        }

        public override IPart DeepCopy(GameObject Parent, Func<GameObject, GameObject> MapInv)
        {
            UD_VendorTinkering vendorTinkering = base.DeepCopy(Parent, MapInv) as UD_VendorTinkering;
            vendorTinkering.InstalledRecipes = null;
            vendorTinkering.KnownRecipes = null;
            return vendorTinkering;
        }

        public override void FinalizeCopy(GameObject Source, bool CopyEffects, bool CopyID, Func<GameObject, GameObject> MapInv)
        {
            base.FinalizeCopy(Source, CopyEffects, CopyID, MapInv);

            if (CopyID && Source.TryGetPart(out UD_VendorTinkering vendorTinkering))
            {
                CopyKnownRecipes(vendorTinkering);
                KnowImplantedRecipes();
            }
            else
            {
                InitializeVendorTinker();
            }
        }

        [WishCommand(Command = "UD_TB log recipe skills")]
        public static void LogRecipeSkillsWish()
        {
            UnityEngine.Debug.LogError("Outputting skill required for each recipe, and recipe tier...");
            foreach (TinkerData tinkerDatum in TinkerData.TinkerRecipes)
            {
                string recipeName = tinkerDatum.Blueprint ?? tinkerDatum.PartName;
                string recipeSkill = DataDisk.GetRequiredSkillHumanReadable(tinkerDatum.Tier);
                UnityEngine.Debug.LogError(recipeName + ", " + recipeSkill + " (" + tinkerDatum.Tier + ")");
            }
        }

        [WishCommand(Command = "UD_TB bestow random bits")]
        public static void BestowRandomBitsWish(string Count)
        {
            Cell playerCell = The.Player.CurrentCell;
            int playerX = playerCell.X;
            int playerY = playerCell.Y;

            if (PickTarget.ShowPicker(
                Style: PickTarget.PickStyle.EmptyCell,
                StartX: playerX,
                StartY: playerY,
                Label: "Pick a cell with objects") is not Cell pickedCell)
            {
                return;
            }
            if (Popup.PickGameObject(
                Title: "~ Pick target of Random Bit Bestowal ~",
                Objects: pickedCell?.GetObjects(GO => GO.IsCreature || GO.HasPart<AnimatedObject>() || GO.InheritsFrom("Fungus")),
                AllowEscape: true) is not GameObject pickedObject)
            {
                return;
            }
            int amount = 0;
            if (!Count.IsNullOrEmpty()
                && int.TryParse(Count, out int count))
            {
                amount = Math.Min(Math.Max(0, count), 100);
            }
            if (amount < 1)
            {
                if (Popup.AskNumber(
                    Message: "How many times?",
                    Start: 1,
                    Max: 100) is not int pickedNumber)
                {
                    return;
                }
                amount = Math.Min(Math.Max(0, pickedNumber), 100);
            }
            
            for (int i = 0; i < amount; i++)
            {
                 GiveRandomBits(pickedObject, false);
            }
            if (pickedObject?.RequirePart<BitLocker>() is BitLocker blitLocker)
            {
                Popup.Show(blitLocker?.GetBitsString());
            }
        }
        [WishCommand(Command = "UD_TB bestow random bits")]
        public static void BestowRandomBitsWish()
        {
            BestowRandomBitsWish(null);
        }

        [WishCommand(Command = "UD_TB give random bits")]
        public static void GiveRandomBitsWish()
        {
            BitLocker bitLocker = GiveRandomBits(The.Player, false);
            Popup.Show(bitLocker.GetBitsString());
        }

        [WishCommand(Command = "UD_TB give random new bits")]
        public static void GiveRandomNewBitsWish()
        {
            BitLocker bitLocker = GiveRandomBits(The.Player);
            Popup.Show(bitLocker.GetBitsString());
        }

        [WishCommand(Command = "UD_TB random bits summary")]
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
            Popup.Show("Average of 100 rolls for new bits:\n\n" + bitLocker.GetBitsString());
            The.Player.RemovePart(bitLocker);
            The.Player.AddPart(originalBitLocker);
        }

        [WishCommand(Command = "UD_TB random bits debug")]
        public static void RandomBitsDebug_Wish()
        {
            AllRandomBitsDebug();
        }
        [WishCommand(Command = "UD_TB random bits debug")]
        public static void Relevant_RandomBitsDebug_Wish(string Flags)
        {
            if (Flags.IsNullOrEmpty())
            {
                AllRandomBitsDebug();
            }
            string flags = Flags.ToLower();
            AllRandomBitsDebug(flags.Contains("-r"), flags.Contains("-l"));
        }

        public static void AllRandomBitsDebug(bool OnlyRelevant = false, bool OnlyLogical = false)
        {
            GameObject sampleBep = GameObject.CreateSample("Bep");
            List<PowerEntry> tinkeringPowers = SkillFactory.Factory.SkillByClass[nameof(Skill.Tinkering)]?.PowerList;
            if (OnlyRelevant)
            {
                tinkeringPowers = new()
                {
                    SkillFactory.Factory.PowersByClass[nameof(Tinkering_Disassemble)],
                    SkillFactory.Factory.PowersByClass[nameof(Tinkering_Scavenger)],
                    SkillFactory.Factory.PowersByClass[nameof(Tinkering_ReverseEngineer)],
                    SkillFactory.Factory.PowersByClass[nameof(Tinkering_Repair)],
                    SkillFactory.Factory.PowersByClass[nameof(Tinkering_Tinker1)],
                    SkillFactory.Factory.PowersByClass[nameof(Tinkering_Tinker2)],
                    SkillFactory.Factory.PowersByClass[nameof(Tinkering_Tinker3)],
                };
            }
            string finalDebugOutput = null;
            List<List<PowerEntry>> tinkeringSkillSets = new();

            int numSkillSets = (int)Math.Pow(2.0, tinkeringPowers.Count) -1;
            int binaryPermutationsPadding = Convert.ToString(numSkillSets, 2).Length;
            for (int i = 0; i < numSkillSets; i++)
            {
                string feedTape = Convert.ToString(i, 2).PadLeft(binaryPermutationsPadding, '0');
                // UnityEngine.Debug.LogError(feedTape);
                List<PowerEntry> tinkeringSkillSet = new();
                for (int j = 0; j < feedTape.Length; j++)
                {
                    if (feedTape[j] == '1')
                    {
                        continue;
                    }
                    if (!OnlyLogical
                        || tinkeringPowers[j].Requires.IsNullOrEmpty()
                        || tinkeringSkillSet.Any(p => p.Class == tinkeringPowers[j].Requires))
                    {
                        tinkeringSkillSet.Add(tinkeringPowers[j]);
                    }
                }
                if (!tinkeringSkillSet.IsNullOrEmpty()
                    && !tinkeringSkillSets.Any(p => 
                        p.All(q => tinkeringSkillSet.Contains(q)))
                    )
                {
                    tinkeringSkillSets.Add(tinkeringSkillSet);
                }
            }
            if (tinkeringSkillSets.IsNullOrEmpty())
            {
                Popup.Show(
                    "Didn't get any tinkering skills (" + 
                    nameof(OnlyRelevant) + ": " + OnlyRelevant + ", " + 
                    nameof(OnlyLogical) + ": " + OnlyRelevant + ")...");
                return;
            }
            int defaultRerolls = 5000;
            int maxRerolls = 25000;
            int benchmarkRerolls = 10000;

            int? askRerolls = Popup.AskNumber(
                Message: "How many rerolls do you want per set of skills?\n\n" +
                "There are " + tinkeringSkillSets.Count.ToString("N0") + " combinations that this will be multiplied by.\n\n" +
                "Default = {{W|" + defaultRerolls.ToString("N0") + "}} | " +
                "Max = {{c|" + maxRerolls.ToString("N0") + "}} | " +
                "Cancel = {{R|0}} or {{W|[esc]}}",
                Start: defaultRerolls, 
                Max: maxRerolls);

            if (askRerolls.IsNullOrZero())
            {
                Loading.SetLoadingStatus(null);
                return;
            }

            TimeSpan benchamrkDuration = DebugWishBenchmark;
            if (benchamrkDuration == TimeSpan.Zero)
            {
                var sw = new Stopwatch();
                int benchRerollCounter = 0;
                try
                {
                    int benchActuallyOutputLines = 0;
                    int benchSkillSetCounter = 0;
                    int benchIterationCounter = 0;
                    string benchFinalDebugOutput = "";
                    PerformSkillSetIteration(
                        SampleBep: sampleBep,
                        TinkeringSkillSet: tinkeringSkillSets[0],
                        TotalSkillSets: 1,
                        BitLockerRerolls: benchmarkRerolls,
                        TotalIterations: 1 * benchmarkRerolls,
                        IterationCounter: ref benchIterationCounter,
                        out benchRerollCounter,
                        SkillSetCounter: ref benchSkillSetCounter,
                        ActuallyOutputLines: ref benchActuallyOutputLines,
                        FinalDebugOutput: ref benchFinalDebugOutput,
                        Benchmark: sw);
                }
                catch (Exception x)
                {
                    MetricsManager.LogException(nameof(PerformSkillSetIteration) + " failed to benchmark properly", x, "game_mod_exception");
                }
                finally
                {
                    sw.Stop();
                    DebugWishBenchmark = TimeSpan.FromMilliseconds(sw.ElapsedMilliseconds);
                    benchamrkDuration = DebugWishBenchmark;
                    sw.Reset();
                }
            }

            UnityEngine.Debug.LogError("Expecting " + tinkeringSkillSets.Count + " lines of output...");
            int bitLockerRerolls = (int)askRerolls;
            int totalIterations = tinkeringSkillSets.Count * bitLockerRerolls;
            int iterationCounter = 1;
            int actuallyOutputLines = 0;
            try
            {
                double benchmarksPerMinute = TimeSpan.FromMinutes(1).TotalMilliseconds / benchamrkDuration.TotalMilliseconds;
                int iterationsPerMinute = (int)(benchmarkRerolls * benchmarksPerMinute);
                double minutesForTotalIterations = Math.Round((double)totalIterations / iterationsPerMinute, 1);

                if (minutesForTotalIterations > 2
                    && Popup.ShowYesNoCancel(
                        "This debug wish will attempt " + totalIterations.ToString("N0") + " random BitLocker rerolls over " +
                        tinkeringSkillSets.Count.Things("skill set combination") + ".\n\n" +
                        iterationsPerMinute.ToString("N0") + " total rerolls takes around 1 minute, so expect roughly " +
                        minutesForTotalIterations.Things("minute") + " for this number." + "\n\n" +
                        "Are you sure?") != DialogResult.Yes)
                {
                    return;
                }
                string debugHeader = "SkillSet";
                foreach (char bit in BitType.BitOrder)
                {
                    if (!debugHeader.IsNullOrEmpty())
                    {
                        debugHeader += ",";
                    }
                    debugHeader += BitType.TranslateBit(bit);
                }
                finalDebugOutput = debugHeader;

                int skillSetCounter = 1;
                foreach (List<PowerEntry> tinkeringPowerCombination in tinkeringSkillSets)
                {
                    if (!PerformSkillSetIteration(
                        SampleBep: sampleBep,
                        TinkeringSkillSet: tinkeringPowerCombination,
                        TotalSkillSets: tinkeringSkillSets.Count,
                        BitLockerRerolls: bitLockerRerolls,
                        TotalIterations: totalIterations,
                        IterationCounter: ref iterationCounter,
                        out int _,
                        SkillSetCounter: ref skillSetCounter,
                        ActuallyOutputLines: ref actuallyOutputLines,
                        FinalDebugOutput: ref finalDebugOutput))
                    {
                        break;
                    }
                }
            }
            finally
            {
                RemoveAllTinkeringPowers(sampleBep);
                sampleBep.Obliterate();
                if (actuallyOutputLines > 0)
                {
                    var baseFileName = "UD_TB.RandomBitDebug" + (OnlyRelevant ? ".R" : "") + (OnlyLogical ? ".L" : "");
                    var fileName = string.Format("{0}.{1:yyyyMMdd.HHmmss}.x{2:00000}.csv", baseFileName, DateTime.Now, bitLockerRerolls);
                    var fullFilePath = DataManager.SavePath(fileName);
                    string outputFileLocation = "";
                    if (new StreamWriter(fullFilePath) is StreamWriter writer)
                    {
                        writer.Write(finalDebugOutput);
                        writer.Flush();
                        writer.Dispose();
                        outputFileLocation = fileName + " (same folder as Player.log)";
                    }
                    else
                    {
                        outputFileLocation = "Player.log";
                    }
                    UnityEngine.Debug.LogError(fullFilePath);
                    UnityEngine.Debug.LogError(finalDebugOutput);
                    Popup.Show("Finished compiling " + actuallyOutputLines.Things("debug line") + ", go check " + outputFileLocation + " for the output.");
                }
                Loading.SetLoadingStatus(null);
            }
        }
        private static void RemoveAllTinkeringPowers(GameObject obj)
        {
            foreach (PowerEntry tinkeringPower in SkillFactory.Factory.SkillByClass[nameof(Skill.Tinkering)]?.PowerList)
            {
                if (obj.HasSkill(tinkeringPower.Class))
                {
                    obj.RemoveSkill(tinkeringPower.Class);
                }
            }
        }
        internal static bool PerformSkillSetIteration(
            GameObject SampleBep,
            List<PowerEntry> TinkeringSkillSet,
            int TotalSkillSets,
            int BitLockerRerolls,
            int TotalIterations,
            ref int IterationCounter,
            out int RerollCounter,
            ref int SkillSetCounter,
            ref int ActuallyOutputLines,
            ref string FinalDebugOutput,
            Stopwatch Benchmark = null)
        {
            bool doDebug = UD_Tinkering_Bytes.Options.doDebug;
            UD_Tinkering_Bytes.Options.doDebug = false;

            bool supressPopups = Popup.Suppress;
            Popup.Suppress = true;

            int totalIterationsPadding = TotalIterations.ToString().Length;
            int rerollsPadding = BitLockerRerolls.ToString().Length;
            int skillSetPadding = TotalSkillSets.ToString().Length;
            var bitLocker = SampleBep.RequirePart<BitLocker>();
            RerollCounter = 0;
            RemoveAllTinkeringPowers(SampleBep);

            string skillSet = "";
            foreach (PowerEntry tinkeringPower in TinkeringSkillSet)
            {
                SampleBep.AddSkill(tinkeringPower.Class);
                if (!skillSet.IsNullOrEmpty())
                {
                    skillSet += "/";
                }
                skillSet += tinkeringPower.Class.Replace(nameof(Skill.Tinkering) + "_", "");
            }
            string debugLine = skillSet + ",";

            try
            {
                string skillSetPaddingString = SkillSetCounter.ToString().PadLeft(skillSetPadding, ' ');
                for (int i = 0; i < Math.Max(1, BitLockerRerolls); i++)
                {
                    RerollCounter = i;
                    GiveRandomBits(SampleBep, false, suppressDebug: true);
                    string loadingString = "";

                    if (Benchmark != null)
                    {
                        Benchmark.Start();
                        loadingString += "Benchmarking load time... ";
                    }
                    string interationsPaddingString = IterationCounter.ToString().PadLeft(totalIterationsPadding, ' ');
                    string iPaddingString = (i + 1).ToString().PadLeft(rerollsPadding, ' ');
                    string iterationLoading = "Iteration " + interationsPaddingString + "/" + TotalIterations;
                    string rerollLoading = "Reroll: " + iPaddingString + "/" + BitLockerRerolls;
                    string skillLoading = "Skill Set: " + skillSetPaddingString + "/" + TotalSkillSets;
                    loadingString += iterationLoading + " | " + rerollLoading + " | " + skillLoading;
                    Loading.SetLoadingStatus(loadingString);
                    IterationCounter++;
                }
                List<double> granularBitLockerOutput = new();
                foreach (char bit in BitType.BitOrder)
                {
                    if (bitLocker.BitStorage.ContainsKey(bit))
                    {
                        double granularBits = Math.Round((double)bitLocker.BitStorage[bit] / Math.Max(1, BitLockerRerolls), 2);
                        granularBitLockerOutput.Add(granularBits);
                        bitLocker.BitStorage[bit] = (int)granularBits;
                    }
                }
                string granularBitsDebugString = "";
                if (!granularBitLockerOutput.All(count => count == 0))
                {
                    foreach (double count in granularBitLockerOutput)
                    {
                        if (!granularBitsDebugString.IsNullOrEmpty())
                        {
                            granularBitsDebugString += ",";
                        }
                        granularBitsDebugString += count;
                    }
                    FinalDebugOutput += "\n" + debugLine + granularBitsDebugString;
                    ActuallyOutputLines++;
                }
                else
                if (!bitLocker.BitStorage.Values.All(count => count == 0))
                {
                    FinalDebugOutput += "\n" + debugLine + bitLocker.GetBitDebugString();
                    ActuallyOutputLines++;
                }
                SkillSetCounter++;
                return true;
            }
            finally
            {
                if (Benchmark != null)
                {
                    Benchmark.Stop();
                    var benchmark = Benchmark.Elapsed;
                    double millisecs = Math.Round(benchmark.TotalMilliseconds, 2);
                    double seconds = Math.Round(benchmark.TotalSeconds, 2);

                    double elapsedTime = millisecs;
                    string elapsedUnit = "millisecond";
                    if (benchmark.Seconds > 0)
                    {
                        elapsedTime = seconds;
                        elapsedUnit = "second";
                    }
                    Loading.SetLoadingStatus(
                        "Benchmarking complete... " + IterationCounter.Things("teration") + 
                        " done in " + elapsedTime.Things(elapsedUnit));
                }
                UD_Tinkering_Bytes.Options.doDebug = doDebug;
                Popup.Suppress = supressPopups;
            }
        }
    }
}
