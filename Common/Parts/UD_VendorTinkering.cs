using ConsoleLib.Console;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using XRL.UI;
using XRL.Wish;
using XRL.Rules;
using XRL.Language;
using XRL.World.Tinkering;
using XRL.World.Anatomy;
using XRL.World.Capabilities;
using XRL.World.Parts.Mutation;
using XRL.World.Parts.Skill;

using static XRL.World.Parts.Skill.Tinkering;

using UD_Modding_Toolbox;
using UD_Vendor_Actions;

using UD_Tinkering_Bytes;

using static UD_Modding_Toolbox.Const;

using static UD_Tinkering_Bytes.Options;
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
        private static bool doDebug = true;

        private static bool SaveStartedWithVendorActions => UD_Vendor_Actions.Startup.SaveStartedWithVendorActions;

        public const string COMMAND_BUILD = "CmdVendorBuild";
        public const string COMMAND_MOD = "CmdVendorMod";
        public const string COMMAND_IDENTIFY_BY_DATADISK = "CmdVendorExamineDataDisk";
        public const string COMMAND_IDENTIFY_SCALING = "CmdVendorExamineScaling";
        public const string COMMAND_RECHARGE_SCALING = "CmdVendorRechargeScaling";
        public const string COMMAND_REPAIR_SCALING = "CmdVendorRepairScaling";

        public const string BITLOCK_DISPLAY = "UD_BitLocker_Display";

        public bool WantVendorActions => (bool)ParentObject?.HasSkill(nameof(Skill.Tinkering)) && (bool)!ParentObject?.IsPlayer();

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

        public UD_VendorTinkering()
        {
            ScribesKnownRecipesOnRestock = true;
            RestockScribeChance = 15;
        }

        public override void AddedAfterCreation()
        {
            base.AddedAfterCreation();
            if (WantVendorActions && !SaveStartedWithVendorActions)
            {
                int indent = Debug.LastIndent;
                Debug.Entry(4, $"{nameof(UD_VendorTinkering)}.{nameof(AddedAfterCreation)}()", Indent: indent + 1, Toggle: doDebug);

                GiveRandomBits(ParentObject);

                LearnByteRecipes(ParentObject, KnownRecipes);

                LearnGiganticRecipe(ParentObject, KnownRecipes);

                LearnSkillRecipes(ParentObject, KnownRecipes);

                KnowImplantedRecipes(ParentObject, InstalledRecipes);

                Debug.LastIndent = indent;
            }
        }

        public bool LearnFromOneDataDisk()
        {
            List<GameObject> dataDiskObjects = ParentObject?.Inventory?.GetObjectsViaEventList(GO => GO.HasPart<DataDisk>());
            if (!dataDiskObjects.IsNullOrEmpty())
            {
                dataDiskObjects.ShuffleInPlace();
                foreach (GameObject dataDiskObject in dataDiskObjects)
                {
                    return LearnFromDataDisk(ParentObject, dataDiskObject, KnownRecipes, true);
                }
            }
            return false;
        }

        public static bool LearnTinkerData(
            GameObject Vendor,
            TinkerData TinkerData,
            List<TinkerData> KnownRecipes,
            bool CreateDisk = false)
        {
            KnownRecipes ??= new();
            if (Vendor.HasSkill(DataDisk.GetRequiredSkill(TinkerData.Tier)) 
                && !KnownRecipes.Any(datum => datum.IsSameDatumAs(TinkerData)))
            {
                KnownRecipes.Add(TinkerData);
            }
            return KnownRecipes.Contains(TinkerData) && (!CreateDisk || ScribeDisk(Vendor, TinkerData));
        }
        public bool LearnTinkerData(TinkerData TinkerData, bool CreateDisk = false)
        {
            return LearnTinkerData(
                Vendor: ParentObject,
                TinkerData: TinkerData,
                KnownRecipes: KnownRecipes,
                CreateDisk: CreateDisk);
        }
        public static bool LearnDataDisk(GameObject Vendor, DataDisk DataDisk, List<TinkerData> KnownRecipes)
        {
            return LearnTinkerData(
                Vendor: Vendor,
                TinkerData: DataDisk.Data,
                KnownRecipes: KnownRecipes,
                CreateDisk: false);
        }
        public bool LearnDataDisk(DataDisk DataDisk)
        {
            return LearnDataDisk(
                Vendor: ParentObject,
                DataDisk: DataDisk,
                KnownRecipes: KnownRecipes);
        }
        public static bool LearnFromDataDisk(
            GameObject Vendor,
            GameObject DataDiskObject,
            List<TinkerData> KnownRecipes,
            bool ConsumeDisk = false)
        {
            if (DataDiskObject.TryGetPart(out DataDisk dataDisk)
                && LearnDataDisk(Vendor, dataDisk, KnownRecipes))
            {
                if (ConsumeDisk)
                {
                    DataDiskObject.Destroy();
                }
                return true;
            }
            return false;
        }
        public bool LearnFromDataDisk(GameObject DataDiskObject, bool ConsumeDisk = false)
        {
            return LearnFromDataDisk(
                Vendor: ParentObject,
                DataDiskObject: DataDiskObject,
                KnownRecipes: KnownRecipes,
                ConsumeDisk: ConsumeDisk);
        }

        public static IEnumerable<TinkerData> FindTinkerableDataDiskData(GameObject Vendor, Predicate<TinkerData> Filter = null)
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

        public IEnumerable<TinkerData> GetKnownRecipes(Predicate<TinkerData> Filter = null, bool IncludeInstalled = true)
        {
            int indent = Debug.LastIndent;
            bool doDebug = false;
            Debug.Entry(3, 
                $"{nameof(UD_VendorTinkering)}.{nameof(GetKnownRecipes)}(" +
                $"{nameof(Filter)}? {Filter != null}, " +
                $"{nameof(IncludeInstalled)}: {IncludeInstalled})", 
                Indent: indent + 1, Toggle: doDebug);
            Debug.CheckYeh(3, $"KnownRecipes", Indent: indent + 2, Toggle: doDebug);
            foreach (TinkerData tinkerData in KnownRecipes)
            {
                bool includeRecipe = (Filter == null || Filter(tinkerData)) && (!IncludeInstalled || !InstalledRecipes.Contains(tinkerData));
                Debug.CheckYeh(3, $"{tinkerData.Blueprint ?? tinkerData.PartName ?? tinkerData.DisplayName.Strip()}",
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
                    Debug.CheckYeh(3, $"{installedRecipe.Blueprint ?? installedRecipe.PartName ?? installedRecipe.DisplayName.Strip()}", 
                        Good: includeRecipe, 
                        Indent: indent + 3, Toggle: doDebug);
                    if (includeRecipe)
                    {
                        yield return installedRecipe;
                    }
                }
            }
            Debug.LastIndent = indent;
            yield break;
        }

        public static bool ScribeDisk(GameObject Vendor, TinkerData TinkerData, bool IsStock = true)
        {
            if (Vendor == null || TinkerData == null)
            {
                return false;
            }
            GameObject newDataDisk = TinkerData.createDataDisk(TinkerData);
            if (IsStock)
            {
                newDataDisk.SetIntProperty("_stock", 1);
            }
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

                    int bitTier = DataDisk.GetRequiredSkill((int)Math.Max(0, i + 1.0 - 4.0)) switch
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
                        $"{nameof(bitTier)}: {bitTier}, " +
                        $"Skill: {DataDisk.GetRequiredSkillHumanReadable((int)Math.Max(0, i + 1.0 - 4.0))}",
                        Indent: indent + 3, Toggle: doDebug);

                    if (hasDisassemble || hasReverseEngineer)
                    {
                        if (i < firstBreakPoint)
                        {
                            currentRange.low += 4;
                            currentRange.high += 8;
                        }
                        if (i < secondBreakPoint)
                        {
                            currentRange.low += 2;
                            currentRange.high += 4;
                        }
                        if (i == upperLimit && !Math.Max(0, i - 4).ChanceIn(upperLimit - 3))
                        {
                            currentRange.high += 1;
                        }
                        Debug.CheckYeh(4, $"Have Ancillary Skill, {nameof(currentRange)}: {currentRange}",
                            Indent: indent + 4, Toggle: doDebug);
                    }
                    if (hasScavenger && hasDisassemble)
                    {
                        if (i < firstBreakPoint)
                        {
                            currentRange.low += 4;
                            currentRange.high += 8;
                        }
                        if (i < secondBreakPoint)
                        {
                            currentRange.low += 2;
                            currentRange.high += 4;
                        }
                        Debug.CheckYeh(4, $"Have Scavenger, {nameof(currentRange)}: {currentRange}",
                            Indent: indent + 4, Toggle: doDebug);
                    }
                    if (hasReverseEngineer)
                    {
                        if (i == upperLimit && !Math.Max(0, i - 4).ChanceIn(upperLimit - 3))
                        {
                            currentRange.high += 1;
                        }
                        Debug.CheckYeh(4, $"Have Reverse Engineering, {nameof(currentRange)}: {currentRange}",
                            Indent: indent + 4, Toggle: doDebug);
                    }
                    if (bitTier < tinkeringSkill)
                    {
                        currentRange.low += 8;
                        currentRange.high += 16;
                        Debug.CheckYeh(4, 
                            $"{nameof(bitTier)} < {nameof(tinkeringSkill)}, " +
                            $"{nameof(currentRange)}: {currentRange}",
                            Indent: indent + 4, Toggle: doDebug);
                    }
                    if (bitTier == tinkeringSkill)
                    {
                        if (i < secondBreakPoint)
                        {
                            currentRange.low += 2;
                            currentRange.high += 4;
                        }
                        else if (i < upperLimit)
                        {
                            currentRange.low += 1;
                            currentRange.high += 2;
                        }
                        else
                        {
                            if (3.in10())
                            {
                                currentRange.high += 1;
                            }
                        }
                        Debug.CheckYeh(4,
                            $"{nameof(bitTier)} == {nameof(tinkeringSkill)}, " +
                            $"{nameof(currentRange)}: {currentRange}",
                            Indent: indent + 4, Toggle: doDebug);
                    }

                    bitRanges[bitIndex] = currentRange;

                    Debug.Entry(4, $"{nameof(bitRanges)}[{nameof(bitIndex)}]: {bitRanges[bitIndex]}",
                        Indent: indent + 3, Toggle: doDebug);
                }
                Debug.Divider(4, HONLY, Count: 40, Indent: indent + 3, Toggle: doDebug);

                Debug.Entry(4, $"Disassembling bits and throwing in Locker...", Indent: indent + 3, Toggle: doDebug);
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
                        bitLocker.AddBits(bits);
                    }
                    Debug.LoopItem(4, $"{BitType.CharTranslateBit(bit)}] " +
                        $"{nameof(low)}: {low,2}, " +
                        $"{nameof(high)}: {high,3} | " +
                        $"Rolled: {amountToAdd,4}", Indent: indent + 4, Toggle: doDebug);
                }
                Debug.Entry(4, $"Scrap found, disassembled, and stored...", Indent: indent + 2, Toggle: doDebug);
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
            Debug.Entry(3, $"Spinning up byte data disks for {Vendor?.DebugName ?? NULL}...", Indent: 0, Toggle: doDebug);
            bool learned = false;
            if (!byteBlueprints.IsNullOrEmpty())
            {
                foreach (TinkerData tinkerDatum in TinkerData.TinkerRecipes)
                {
                    if (byteBlueprints.Contains(tinkerDatum.Blueprint))
                    {
                        Debug.LoopItem(3, $"{tinkerDatum.DisplayName.Strip()}", Indent: 1, Toggle: doDebug);
                        learned = LearnTinkerData(Vendor, tinkerDatum, KnownRecipes) || learned;
                    }
                }
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
        public static bool LearnSkillRecipes(GameObject Vendor, List<TinkerData> KnownRecipes)
        {
            if (Vendor == null)
            {
                return false;
            }    
            KnownRecipes ??= new();
            bool learned = false; 
            List<TinkerData> avaialableRecipes = new(TinkerData.TinkerRecipes);
            avaialableRecipes.RemoveAll(r => !Vendor.HasSkill(DataDisk.GetRequiredSkill(r.Tier)) && !KnownRecipes.Contains(r));

            if (!avaialableRecipes.IsNullOrEmpty())
            {
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
                    TinkerData recipeToKnow = avaialableRecipes.DrawSeededToken(vendorRecipeSeed, i, nameof(LearnSkillRecipes));
                    learned = LearnTinkerData(Vendor, recipeToKnow, KnownRecipes) || learned;
                }
            }
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
                    Debug.Entry(3, $"Spinning up installed shemasoft for {Vendor?.DebugName ?? NULL}...", Indent: 0, Toggle: doDebug);
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
                                    Debug.LoopItem(3, $"{availableRecipe.Blueprint ?? availableRecipe.PartName ?? availableRecipe.DisplayName.Strip()}", Indent: 2);
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
            if (!Vendor.HasSkill(DataDisk.GetRequiredSkill(Recipe.Tier)) && !(InstalledRecipes != null && InstalledRecipes.Contains(Recipe)))
            {
                if (!Silent)
                {
                    string requiredSkillName = DataDisk.GetRequiredSkillHumanReadable(Recipe.Tier);
                    string skillFailMsg = ("=subject.T= =verb:don't= have the required skill: " + requiredSkillName + "!")
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
                (askPay + " =subject.t= to supply " + vendorBitCostString + ".")
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
                string modPrefix = TinkerData.Type == "Mod" ? "[{{W|Mod}}] " : "";

                string numberMadePrefix = null;
                if (TinkerData.Type == "Build"
                    && ForObject != null
                    && ForObject.TryGetPart(out TinkerItem tinkerItem)
                    && tinkerItem.NumberMade > 1)
                {
                    numberMadePrefix = $"{Grammar.Cardinal(tinkerItem.NumberMade)} ";
                }

                string title = modPrefix + numberMadePrefix + TinkerData.DisplayName + "\n";
                title += "| 1 Ingredient Required |".Color("Y") + "\n";

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
                        string tempObjectFailMsg = "=subject.t= =verb:are:afterpronoun= too unstable to craft with."
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
                        string noIngredientMsg = ("=subject.T= =verb:don't:afterpronoun= have the required ingredient: " + ingredientName)
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

        public static bool PickRecipeBitsSupplier(
            GameObject Vendor,
            GameObject ForObject,
            TinkerData TinkerData,
            BitCost BitCost,
            out GameObject RecipeBitSupplier,
            out BitLocker BitSupplierBitLocker)
        {
            BitSupplierBitLocker = null;

            string modPrefix = TinkerData.Type == "Mod" ? "[{{W|Mod}}] " : "";

            string numberMadePrefix = null;
            if (TinkerData.Type == "Build"
                && ForObject != null
                && ForObject.TryGetPart(out TinkerItem tinkerItem)
                && tinkerItem.NumberMade > 1)
            {
                numberMadePrefix = Grammar.Cardinal(tinkerItem.NumberMade) + " ";
            }

            string title = modPrefix + numberMadePrefix + TinkerData.DisplayName + "\n"
                + "| Bit Cost |".Color("y") + "\n"
                + "<" + BitCost + ">" + "\n";

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

            ModifyBitCostEvent.Process(RecipeBitSupplier, BitCost, "Build");

            if (!BitSupplierBitLocker.HasBits(BitCost))
            {
                string missingRequiredBitsMsg = 
                    ("=subject.T= =verb:do:afterpronoun= not have the required <" + BitCost.ToString() + "> bits!\n\n" +
                    "=subject.Subjective= =verb:have:afterpronoun=:\n" + BitSupplierBitLocker.GetBitsString())
                        .StartReplace()
                        .AddObject(RecipeBitSupplier)
                        .ToString();

                Popup.ShowFail(Message: missingRequiredBitsMsg);
                return false;
            }

            return true;
        }
        public bool PickRecipeBitsSupplier(
            GameObject ForObject,
            TinkerData TinkerData,
            BitCost BitCost,
            out GameObject RecipeBitSupplier,
            out BitLocker BitSupplierBitLocker)
        {
            return PickRecipeBitsSupplier(
                Vendor: ParentObject,
                ForObject: ForObject,
                TinkerData: TinkerData,
                BitCost: BitCost,
                RecipeBitSupplier: out RecipeBitSupplier,
                BitSupplierBitLocker: out BitSupplierBitLocker);
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
                    ("=subject.t= =verb:don't:afterpronoun= have any items " +
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
            List<GameObject> vendorHeldRecipeObjects = Vendor?.Inventory?.GetObjectsViaEventList(
                GO => GO.TryGetPart(out UD_VendorKnownRecipe knownRecipe)
                && knownRecipe.Data.Type == "Mod");

            List<GameObject> vendorHeldDataDiskObjects = Vendor?.Inventory?.GetObjectsViaEventList(
                GO => GO.TryGetPart(out DataDisk D)
                && D.Data.Type == "Mod");

            List<GameObject> playerHeldDataDiskObjects = player?.Inventory?.GetObjectsViaEventList(
                GO => GO.TryGetPart(out DataDisk D)
                && D.Data.Type == "Mod");

            if (vendorHeldRecipeObjects.IsNullOrEmpty()
                && vendorHeldDataDiskObjects.IsNullOrEmpty()
                && playerHeldDataDiskObjects.IsNullOrEmpty())
            {
                string noModsKnownMsg = "=subject.T= =verb:do:afterpronoun= not know any item modifications."
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
                        string shopperOwnedTag = "=subject.t's= inventory"
                            .StartReplace()
                            .AddObject(player)
                            .ToString();

                        applicableRecipes.Add(playerHeldDataDisk.Data, shopperOwnedTag);
                        IsVendorOwnedRecipe.Add(false);
                    }
                }
            }
            if (!vendorHeldRecipeObjects.IsNullOrEmpty())
            {
                foreach (GameObject vendorHeldRecipeObject in vendorHeldRecipeObjects)
                {
                    if (vendorHeldRecipeObject.TryGetPart(out UD_VendorKnownRecipe vendorHeldRecipePart)
                        && IsModApplicableAndNotAlreadyInDictionary(
                            Vendor: Vendor,
                            ApplicableItem: ApplicableItem,
                            ModData: vendorHeldRecipePart.Data,
                            ExistingRecipes: applicableRecipes,
                            InstalledRecipes: InstalledRecipes))
                    {
                        applicableRecipes.Add(vendorHeldRecipePart.Data, "known by trader");
                        IsVendorOwnedRecipe.Add(true);
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
                    }
                }
            }
            if (applicableRecipes.IsNullOrEmpty())
            {
                string noModsKnownForItemMsg = "=subject.T= =verb:do:afterpronoun= not know any item modifications for =object.t=."
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
            GameObject sampleDiskObject = null;
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

                sampleDiskObject = TinkerData.createDataDisk(applicableRecipe);

                LineIcons.Add(sampleDiskObject.RenderForUI());
            }
            TinkerInvoice.ScrapTinkerSample(ref sampleDiskObject);
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

        public static bool VendorDoBuild(GameObject Vendor, TinkerInvoice TinkerInvoice, GameObject RecipeIngredientSupplier, bool VendorKeepsItem)
        {
            if (Vendor == null || TinkerInvoice == null || TinkerInvoice.Recipe is not TinkerData tinkerDatum)
            {
                Popup.ShowFail("That trader or recipe doesn't exist (this is an error).");
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
                        string invalidIngredientMsg = "=subject.T= cannot use =object.t= as an ingredient!="
                            .StartReplace()
                            .AddObject(Vendor)
                            .AddObject(ingredientObject)
                            .ToString();
                        Vendor.Fail(invalidIngredientMsg);
                        return false;
                    }
                }
                Inventory inventory = VendorKeepsItem ? Vendor.Inventory : player.Inventory;
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

                    if (VendorKeepsItem)
                    {
                        // tinkeredItem.SetIntProperty(HELD_FOR_PLAYER, 2);
                        var heldForPlayer = tinkeredItem.RequirePart<UD_HeldForPlayer>();
                        heldForPlayer.HeldFor = player;
                        heldForPlayer.DepositPaid = TinkerInvoice.GetDepositCost();
                        heldForPlayer.RestocksLeft = UD_HeldForPlayer.GUARANTEED_RESTOCKS;
                        Vendor.RegisterEvent(heldForPlayer, StartTradeEvent.ID, Serialize: true);
                    }

                    inventory.AddObject(tinkeredItem);
                    if (!VendorKeepsItem)
                    {
                        tinkeredItem?.CheckStack();
                    }
                }

                string singleShortKnownDisplayName = tinkerSampleItem.GetDisplayName(AsIfKnown: true, Single: true, Short: true);
                string whatWasTinkeredUp = tinkerItem.NumberMade > 1
                    ? (Grammar.Cardinal(tinkerItem.NumberMade) + " " + Grammar.Pluralize(singleShortKnownDisplayName))
                    : "=object.a= " + singleShortKnownDisplayName;


                string itemTinkeredMsg = ("=subject.T= =verb:tinker:afterpronoun= up " + whatWasTinkeredUp + "!")
                    .StartReplace()
                    .AddObject(Vendor)
                    .AddObject(tinkerSampleItem)
                    .ToString();

                string comeBackToPickItUp = "";
                if (VendorKeepsItem)
                {
                    string themIt = tinkerSampleItem.themIt();
                    comeBackToPickItUp += 
                        ("\n\n" + "Once =subject.t= =verb:have:afterpronoun= the drams for " 
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
                        string noUnequipMsg = "=subject.T= can't unequip =object.t=."
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
                        ("=subject.T= =verb:mod:afterpronoun= " + itemNameBeforeMod + " to be " +
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
        public static bool VendorDoRecharge(GameObject Vendor, GameObject Item)
        {
            if (Vendor == null || Item == null)
            {
                Popup.ShowFail($"That trader or item doesn't exist (this is an error).");
                return false;
            }
            GameObject player = The.Player;
            bool AnyParts = false;
            bool AnyRechargeable = false;
            bool AnyRecharged = false;
            foreach (var rechargablePart in Item.GetPartsDescendedFrom<IRechargeable>())
            {
                AnyParts = true;
                if (!rechargablePart.CanBeRecharged())
                {
                    continue;
                }

                AnyRechargeable = true;
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

                string title =
                    ("Recharge =subject.t= \n" +
                    "| Bit Cost |".Color("y") + "\n" +
                    "<" + bitCost + ">")
                        .StartReplace()
                        .AddObject(Item)
                        .ToString();
                GameObject rechargeBitSupplier = PickASupplier(
                    Vendor: Vendor,
                    ForObject: Item,
                    Title: title,
                    CenterIntro: true,
                    BitCost: bitCost);

                if (rechargeBitSupplier == null)
                {
                    AnyParts = false;
                    AnyRechargeable = false;
                    continue;
                }
                bool vendorSuppliesBits = rechargeBitSupplier == Vendor;

                BitLocker bitSupplierBitLocker = rechargeBitSupplier.RequirePart<BitLocker>();

                int bitCount = BitLocker.GetBitCount(rechargeBitSupplier, rechargeBit);

                if (bitCount == 0)
                {
                    string noBitsMsg =
                        ("=subject.T= =verb:don't:afterpronoun= have any " + BitType.GetString(bits) + " bits," +
                        " which are required for recharging =object.t=.\n\n" +
                        "=subject.Subjective= =verb:have:afterpronoun=:\n" +
                        bitSupplierBitLocker.GetBitsString())
                            .StartReplace()
                            .AddObject(rechargeBitSupplier)
                            .AddObject(Item)
                            .ToString();

                    Popup.ShowFail(Message: noBitsMsg);

                    AnyParts = false;
                    AnyRechargeable = false;
                    continue;
                }

                int availableBitsToRecharge = Math.Min(bitCount, bitsToRechargeFully);

                string howManyBitsMsg =
                    ("It would take " + bitsToRechargeFully.Things($"{BitType.GetString(bits)} bit").Color("C") +
                    " to fully recharge =object.t=. =subject.T= =verb:have:afterpronoun=" + bitCount.Color("C") + ". " +
                    "How many do you want to use?")
                        .StartReplace()
                        .AddObject(rechargeBitSupplier)
                        .AddObject(Item)
                        .ToString();

                int chosenBitsToUse = Popup.AskNumber(Message: howManyBitsMsg, "Sounds/UI/ui_notification", "", availableBitsToRecharge, 0, availableBitsToRecharge).GetValueOrDefault();

                if (chosenBitsToUse < 1)
                {
                    AnyParts = false;
                    AnyRechargeable = false;
                    continue;
                }

                bitCost.Clear();
                bitCost.Add(rechargeBit, chosenBitsToUse);

                TinkerInvoice tinkerInvoice = new(Vendor, TinkerInvoice.RECHARGE, null, bitCost, Item)
                {
                    VendorSuppliesBits = vendorSuppliesBits,
                };
                int totalDramsCost = tinkerInvoice.GetTotalCost();

                if (player.GetFreeDrams() < totalDramsCost)
                {
                    string tooExpensiveMsg =
                        ("=subject.T= =verb:don't:afterpronoun= have the required " +
                        totalDramsCost.Things("dram").Color("C") +
                        " to have =object.t= recharge this item.")
                            .StartReplace()
                            .AddObject(player)
                            .AddObject(Vendor)
                            .ToString();

                    Popup.ShowFail(tooExpensiveMsg);
                    Popup.Show(tinkerInvoice, "Invoice");
                    AnyParts = false;
                    AnyRechargeable = false;
                    continue;
                }
                string wantToRechargeMsg =
                    ("=object.T= will recharge this item for " +
                    totalDramsCost.Things("dram").Color("C") +
                    " of fresh water." +
                    "\n\n" + tinkerInvoice)
                        .StartReplace()
                        .AddObject(player)
                        .AddObject(Vendor)
                        .ToString();

                if (Popup.ShowYesNo(wantToRechargeMsg) == DialogResult.Yes)
                {
                    bitSupplierBitLocker.UseBits(bitCost);

                    player.UseDrams(totalDramsCost);
                    Vendor.GiveDrams(totalDramsCost);

                    rechargablePart.AddCharge((chosenBitsToUse < bitsToRechargeFully) ? (chosenBitsToUse * rechargeValue) : rechargeAmount);

                    PlayUISound("Sounds/Abilities/sfx_ability_energyCell_recharge");

                    string partiallyOrFully = ((chosenBitsToUse < bitsToRechargeFully) ? "partially " : "");
                    string endMark = (!partiallyOrFully.IsNullOrEmpty() ? "!" : ".");
                    string rechargedMsg = ("=subject.T= " + partiallyOrFully + "recharged =object.t=" + endMark)
                        .StartReplace()
                        .AddObject(Vendor)
                        .AddObject(Item)
                        .ToString();

                    Popup.Show(rechargedMsg);

                    AnyRecharged = true;
                    break;
                }
            }
            if (AnyRecharged)
            {
                player.UseEnergy(1000, "Trade Tinkering Recharge");
                Vendor.UseEnergy(1000, "Skill Tinkering Recharge");
            }
            else
            {
                if (!AnyParts)
                {
                    Popup.ShowFail("=subject.T= =verb:isn't:afterpronoun= an energy cell and does not have a rechargeable capacitor."
                        .StartReplace()
                        .AddObject(Item)
                        .ToString());
                }
                else
                if (!AnyRechargeable)
                {
                    Popup.ShowFail("=subject.T= can't be recharged that way."
                        .StartReplace()
                        .AddObject(Item)
                        .ToString());
                }
            }
            Item.CheckStack();
            Item.InInventory.CheckStacks();
            return AnyRecharged;
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
                || ID == ImplantAddedEvent.ID
                || ID == ImplantRemovedEvent.ID
                || ID == GetShortDescriptionEvent.ID
                || (WantVendorActions && ID == StockedEvent.ID)
                || (WantVendorActions && ID == StartTradeEvent.ID)
                || (WantVendorActions && ID == UD_AfterVendorActionEvent.ID)
                || (WantVendorActions && ID == UD_EndTradeEvent.ID)
                || (WantVendorActions && ID == UD_GetVendorActionsEvent.ID)
                || (WantVendorActions && ID == UD_VendorActionEvent.ID);
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

                KnowImplantedRecipes(E.Object, InstalledRecipes);
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

                KnowImplantedRecipes(E.Object, InstalledRecipes);
            }
            return base.HandleEvent(E);
        }
        public override bool HandleEvent(ImplantAddedEvent E)
        {
            if (E.Implantee != null && ParentObject == E.Implantee && !E.Implantee.IsPlayer())
            {
                KnowImplantedRecipes(E.Implantee, InstalledRecipes);
            }
            return base.HandleEvent(E);
        }
        public override bool HandleEvent(ImplantRemovedEvent E) 
        {
            if (E.Implantee != null && ParentObject == E.Implantee && !E.Implantee.IsPlayer())
            {
                KnowImplantedRecipes(E.Implantee, InstalledRecipes);
            }
            return base.HandleEvent(E);
        }
        public override bool HandleEvent(GetShortDescriptionEvent E)
        {
            if (E.Object != null && ParentObject == E.Object)
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
                string recipeBullet = "\u0007 ".Color("K");
                string creditBullet = "\u009B ".Color("c");
                if (DebugKnownRecipesDebugDescriptions)
                {
                    List<TinkerData> knownRecipes = new(GetKnownRecipes());
                    if (!knownRecipes.IsNullOrEmpty())
                    {
                        List<string> byteBlueprints = new(UD_TinkeringByte.GetByteBlueprints());
                        List<string> tierIRecipes = new();
                        List<string> tierIIRecipes = new();
                        List<string> tierIIIRecipes = new();

                        foreach (TinkerData knownRecipe in knownRecipes)
                        {
                            string recipeDisplayName = knownRecipe.DisplayName;
                            bool isInstalledRecipe = InstalledRecipes.Contains(knownRecipe);
                            if (knownRecipe.Type == "Mod")
                            {
                                recipeDisplayName = $"[{"Mod".Color("W")}]{recipeDisplayName}";
                            }
                            recipeDisplayName = $"{(isInstalledRecipe ? creditBullet : recipeBullet)}{recipeDisplayName}";
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

                        string noRecipe = recipeBullet + "none";

                        E.Infix.AppendRules("Known Recipes".Color("M") + ":");

                        E.Infix.AppendRules("Tier I".Color("W") + ":");
                        if (!tierIRecipes.IsNullOrEmpty())
                        {
                            foreach (string tier1IRecipe in tierIRecipes)
                            {
                                E.Infix.AppendRules(tier1IRecipe);
                            }
                        }
                        else
                        {
                            E.Infix.AppendRules(noRecipe);
                        }

                        E.Infix.AppendRules("Tier II".Color("W") + ":");
                        if (!tierIIRecipes.IsNullOrEmpty())
                        {
                            foreach (string tier1IIRecipe in tierIIRecipes)
                            {
                                E.Infix.AppendRules(tier1IIRecipe);
                            }
                        }
                        else
                        {
                            E.Infix.AppendRules(noRecipe);
                        }

                        E.Infix.AppendRules("Tier III".Color("W") + ":");
                        if (!tierIIIRecipes.IsNullOrEmpty())
                        {
                            foreach (string tier1IIIRecipe in tierIIIRecipes)
                            {
                                E.Infix.AppendRules(tier1IIIRecipe);
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
                    List<BaseSkill> tinkersSkills = ParentObject.GetPartsDescendedFrom<BaseSkill>();
                    if (!tinkersSkills.IsNullOrEmpty())
                    {
                        E.Infix.AppendRules("Tinkering Skills".Color("M") + ":");
                        foreach (BaseSkill skill in ParentObject.GetPartsDescendedFrom<BaseSkill>())
                        {
                            if (skill.GetType().Name.StartsWith(nameof(Skill.Tinkering))) // || skill.GetType().Name == nameof(UD_Basics))
                            {
                                E.Infix.AppendRules(recipeBullet + skill.DisplayName.Color("y"));
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
                LearnFromOneDataDisk();
                if (ScribesKnownRecipesOnRestock)
                {
                    List<TinkerData> knownRecipes = new(GetKnownRecipes());
                    if (!knownRecipes.IsNullOrEmpty())
                    {
                        List<GameObject> knownDataDiskObjects = Vendor?.Inventory?.GetObjectsViaEventList(GO => GO.TryGetPart(out DataDisk dataDisk) && knownRecipes.Contains(dataDisk.Data));
                        List<TinkerData> inventoryTinkerData = new();
                        foreach (GameObject knownDataDiskObject in knownDataDiskObjects)
                        {
                            if (knownDataDiskObject.TryGetPart(out DataDisk knownDataDisk))
                            {
                                inventoryTinkerData.Add(knownDataDisk.Data);
                            }
                        }
                        List<string> byteBlueprints = new(UD_TinkeringByte.GetByteBlueprints());
                        foreach (TinkerData knownRecipe in knownRecipes)
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

                foreach (GameObject item in Vendor.Inventory.GetObjectsViaEventList(GO => GO.HasPart<UD_HeldForPlayer>()))
                {
                    if (item.TryGetPart(out UD_HeldForPlayer heldForPlayer))
                    {
                        if (heldForPlayer.RestocksLeft == 1 && Stat.RollCached("1d3") == 1)
                        {
                            continue;
                        }
                        heldForPlayer.Restocked(Vendor);
                    }
                }
            }
            return base.HandleEvent(E);
        }
        public override bool HandleEvent(StartTradeEvent E)
        {
            if (E.Trader == ParentObject && WantVendorActions)
            {
                List<TinkerData> knownRecipes = GetKnownRecipes(IncludeInstalled: false).ToList();
                KnownRecipes.Clear();
                foreach (TinkerData knownRecipe in knownRecipes)
                {
                    LearnTinkerData(knownRecipe);
                }
                ReceiveKnownRecipeDisplayItems();
                ReceiveBitLockerDisplayItem();
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
            if (E.Vendor != null && ParentObject == E.Vendor && E.Item != null && WantVendorActions)
            {
                int vendorIdentifyLevel = GetIdentifyLevel(E.Vendor);
                bool itemUnderstood = E.Item.Understood();

                Tinkering_Repair vendorRepairSkill = E.Vendor.GetPart<Tinkering_Repair>();

                if (E.Item.TryGetPart(out DataDisk dataDisk))
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
                        && The.Player != null
                        && TinkerInvoice.CreateTinkerSample(dataDisk?.Data.Blueprint) is GameObject sampleObject)
                    {
                        if (sampleObject.Understood()
                            || The.Player.HasSkill(nameof(Skill.Tinkering))
                            || Scanning.HasScanningFor(The.Player, Scanning.Scan.Tech))
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
                    if (E.Item.InInventory != E.Vendor && !ItemModding.ModKey(E.Item).IsNullOrEmpty() && itemUnderstood)
                    {
                        E.AddAction("Mod This Item", "mod with tinkering", COMMAND_MOD, "tinkering", Key: 'T', Priority: -2, ClearAndSetUpTradeUI: true);
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
                        && E.Item.InInventory != E.Vendor 
                        && vendorRepairSkill != null
                        && IsRepairableEvent.Check(E.Vendor, E.Item, null, vendorRepairSkill, null))
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
                        && E.Vendor.HasSkill(nameof(Tinkering_Tinker1))
                        && (itemUnderstood || vendorIdentifyLevel > E.Item.GetComplexity()) 
                        && E.Item.NeedsRecharge())
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
                if (E.Command == COMMAND_MOD || E.Command == COMMAND_BUILD)
                {
                    if (vendor.AreHostilesNearby() && vendor.FireEvent("CombatPreventsTinkering"))
                    {
                        string hostileFailMsg = "=subject.T= can't tinker with hostiles nearby!"
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

                            if (!PickRecipeBitsSupplier(
                                ForObject: sampleItem, 
                                TinkerData: tinkerRecipe,
                                BitCost: bitCost,
                                RecipeBitSupplier: out GameObject recipeBitSupplier,
                                BitSupplierBitLocker: out BitLocker bitSupplierBitLocker))
                            {
                                return false;
                            }
                            bool vendorSuppliesBits = recipeBitSupplier == vendor;

                            tinkerInvoice = new(vendor, tinkerRecipe, selectedIngredient, bitCost, vendorsRecipe)
                            {
                                VendorSuppliesIngredients = vendorSuppliesIngredients,
                                VendorSuppliesBits = vendorSuppliesBits,
                            };

                            int totalDramsCost = tinkerInvoice.GetTotalCost();
                            int depositDramCost = tinkerInvoice.GetDepositCost();
                            double itemDramValue = tinkerInvoice.GetItemValue();

                            bool vendorHoldsItem = false;

                            string dramsCostString = totalDramsCost.Things("dram").Color("C");

                            if (player.GetFreeDrams() < totalDramsCost 
                                && (depositDramCost == 0 || player.GetFreeDrams() < depositDramCost))
                            {
                                int numberMade = tinkerInvoice.NumberMade;
                                string tooExpensiveMsg =
                                    ("=subject.T= =verb:don't:afterpronoun= have the required " +
                                    dramsCostString + " to have =object.t= tinker " + "item".ThisTheseN(numberMade) + ".")
                                        .StartReplace()
                                        .AddObject(player)
                                        .AddObject(vendor)
                                        .ToString();

                                Popup.ShowFail(tooExpensiveMsg);
                                Popup.Show(tinkerInvoice, "Invoice");
                                return false;
                            }
                            if (player.GetFreeDrams() < totalDramsCost
                                && (depositDramCost > 0 && player.GetFreeDrams() > depositDramCost))
                            {
                                vendorHoldsItem = Popup.ShowYesNo(tinkerInvoice.GetDepositMessage()) == DialogResult.Yes;
                                if (!vendorHoldsItem)
                                {
                                    return false;
                                }
                            }

                            string confirmTinkerMsg = 
                                ("=subject.T= will tinker this item for " + dramsCostString +
                                " of fresh water." + "\n\n" + tinkerInvoice)
                                .StartReplace()
                                .AddObject(vendor)
                                .ToString();

                            if (!vendorHoldsItem && Popup.ShowYesNo(confirmTinkerMsg) != DialogResult.Yes)
                            {
                                return false;
                            }
                            if (VendorDoBuild(vendor, tinkerInvoice, recipeIngredientSupplier, vendorHoldsItem))
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
                                    Title: $"select which item mod to apply",
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
                                    Title: $"select an item to apply " + modName + " to",
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
                                
                                if (!PickRecipeBitsSupplier(
                                    ForObject: selectedObject, 
                                    TinkerData: tinkerRecipe, 
                                    BitCost: bitCost,
                                    RecipeBitSupplier: out GameObject recipeBitSupplier,
                                    BitSupplierBitLocker: out BitLocker bitSupplierBitLocker))
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
                                string dramsCostString = totalDramsCost.Things("dram").Color("C");

                                if (player.GetFreeDrams() < totalDramsCost)
                                {
                                    string tooExpensiveMsg =
                                    ("=subject.T= =verb:don't:afterpronoun= have the required " +
                                    dramsCostString + " to have =object.t= mod this item.")
                                        .StartReplace()
                                        .AddObject(player)
                                        .AddObject(vendor)
                                        .ToString();

                                    Popup.ShowFail(tooExpensiveMsg);
                                    Popup.Show(tinkerInvoice, "Invoice");
                                    return false;
                                }

                                string confirmModMsg = 
                                    ("=subject.T= will mod this item for " + dramsCostString + 
                                    " of fresh water" + "\n\n" + tinkerInvoice)
                                        .StartReplace()
                                        .AddObject(vendor)
                                        .ToString();

                                if (Popup.ShowYesNo(confirmModMsg) == DialogResult.Yes)
                                {
                                    if (VendorDoMod(vendor, selectedObject, tinkerInvoice, recipeIngredientSupplier))
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
                else
                if (E.Command == COMMAND_IDENTIFY_BY_DATADISK
                    && E.Item is GameObject dataDiskObject
                    && dataDiskObject.TryGetPart(out DataDisk dataDiskPart)
                    && dataDiskPart.Data is TinkerData diskDatum)
                {
                    int identifyLevel = GetIdentifyLevel(vendor);
                    bool diskIsBuild = diskDatum.Type == "Build";
                    if (identifyLevel > 0 
                        && diskIsBuild
                        && TinkerInvoice.CreateTinkerSample(diskDatum.Blueprint) is GameObject sampleItem)
                    {
                        try
                        {
                            if (!sampleItem.Understood())
                            {
                                int complexity = sampleItem.GetComplexity();
                                int examineDifficulty = sampleItem.GetExamineDifficulty();
                                if (player.HasPart<Dystechnia>())
                                {
                                    Popup.ShowFail($"You can't understand {Grammar.MakePossessive(vendor.t(Stripped: true))} explanation.");
                                    return false;
                                }
                                if (identifyLevel < complexity)
                                {
                                    Popup.ShowFail($"The tinker recipe on this data disk is too complex for {vendor.t(Stripped: true)} to explain.");
                                    return false;
                                }
                                int dramsCost = vendor.IsPlayerLed() ? 0 : (int)tinkerInvoice.GetExamineCost();
                                if (dramsCost > 0 && player.GetFreeDrams() < dramsCost)
                                {
                                    Popup.ShowFail(
                                        $"You do not have the required {dramsCost.Things("dram").Color("C")} " +
                                        $"to have {vendor.t(Stripped: true)} identify the tinker recipe on this data disk.");
                                }
                                else
                                if (Popup.ShowYesNo(
                                    $"You may have {vendor.t(Stripped: true)} identify the tinker recipe on this data disk for " +
                                    $"{dramsCost.Things("dram").Color("C")} of fresh water.") == DialogResult.Yes)
                                {
                                    player.UseDrams(dramsCost);
                                    vendor.GiveDrams(dramsCost);
                                    Popup.Show(
                                        $"{vendor.does("identify", Stripped: true)} {sampleItem.the} {sampleItem.GetDisplayName()}" +
                                        $" as {sampleItem.an(AsIfKnown: true)}.");
                                    sampleItem.MakeUnderstood();
                                    return true;
                                }
                            }
                            else
                            {
                                Popup.ShowFail("You already understand what the tinker recipe on this data disk creates.");
                                return false;
                            }
                        }
                        finally
                        {
                            TinkerInvoice.ScrapTinkerSample(ref sampleItem);
                        }
                    }
                    else
                    {
                        Popup.Show($"{vendor.Does("don't", Stripped: true)} have the skill to identify tinker recipes on data disks.");
                        return false;
                    }
                }
                else
                if (E.Command == COMMAND_IDENTIFY_SCALING
                    && E.Item is GameObject unknownItem)
                {
                    int identifyLevel = GetIdentifyLevel(vendor);
                    if (identifyLevel > 0)
                    {
                        if (!unknownItem.Understood())
                        {
                            int complexity = unknownItem.GetComplexity();
                            int examineDifficulty = unknownItem.GetExamineDifficulty();
                            if (player.HasPart<Dystechnia>())
                            {
                                Popup.ShowFail($"You can't understand {Grammar.MakePossessive(vendor.t(Stripped: true))} explanation.");
                                return false;
                            }
                            if (identifyLevel < complexity)
                            {
                                Popup.ShowFail($"This item is too complex for {vendor.t(Stripped: true)} to identify.");
                                return false;
                            }
                            int dramsCost = vendor.IsPlayerLed() ? 0 : (int)tinkerInvoice.GetExamineCost();
                            if (player.GetFreeDrams() < dramsCost)
                            {
                                Popup.ShowFail(
                                    $"You do not have the required {dramsCost.Things("dram").Color("C")} " +
                                    $"to have {vendor.t(Stripped: true)} identify this item.");
                            }
                            else
                            if (Popup.ShowYesNo(
                                $"You may have {vendor.t(Stripped: true)} identify this for " +
                                $"{dramsCost.Things("dram").Color("C")} of fresh water.") == DialogResult.Yes)
                            {
                                player.UseDrams(dramsCost);
                                vendor.GiveDrams(dramsCost);
                                Popup.Show($"{vendor.does("identify", Stripped: true)} {unknownItem.the} {unknownItem.GetDisplayName()} {unknownItem.an(AsIfKnown: true)}.");
                                unknownItem.MakeUnderstood();
                                return true;
                            }
                        }
                        else
                        {
                            Popup.ShowFail("You already understand this item.");
                            return false;
                        }
                    }
                    else
                    {
                        Popup.Show($"{vendor.Does("don't", Stripped: true)} have the skill to identify tinker recipes on data disks.");
                        return false;
                    }
                }
                else
                if (E.Command == COMMAND_REPAIR_SCALING
                    && E.Vendor.TryGetPart(out Tinkering_Repair vendorRepairSkill)
                    && E.Item is GameObject repairableItem)
                {
                    if (!IsRepairableEvent.Check(vendor, repairableItem, null, vendorRepairSkill, null))
                    {
                        Popup.Show($"{repairableItem.T(Single: true)}{repairableItem.GetVerb("are")} not broken!");
                        return false;
                    }
                    if (vendor.GetTotalConfusion() > 0)
                    {
                        Popup.ShowFail($"{vendor.T()}{vendor.GetVerb("'re")} too confused to repair {repairableItem.t(Single: true)}.");
                        return false;
                    }
                    if (vendor.AreHostilesNearby() && vendor.FireEvent("CombatPreventsRepair"))
                    {
                        Popup.ShowFail($"{vendor.T()} cannot repair {repairableItem.t()} with hostiles nearby.");
                        return false;
                    }

                    BitCost bitCost = new(Tinkering_Repair.GetRepairCost(repairableItem));

                    if (!Tinkering_Repair.IsRepairableBy(repairableItem, vendor, bitCost, vendorRepairSkill, null))
                    {
                        Popup.ShowFail($"{repairableItem.T(Single: true)}{repairableItem.GetVerb("are")} too complex for {vendor.t(Stripped: true)} to repair.");
                        vendor.FireEvent(Event.New("UnableToRepair", "Object", repairableItem));
                        return false;
                    }
                    if (vendor.HasTagOrProperty("NoRepair"))
                    {
                        Popup.ShowFail($"{repairableItem.T(Single: true)} cannot be repaired.");
                        return false;
                    }

                    GameObject repairBitSupplier = PickASupplier(
                        Vendor: vendor,
                        ForObject: repairableItem,
                        Title: $"Repair {repairableItem.t()}" + "\n"
                        + $"| Bit Cost |".Color("y") + "\n"
                        + $"<{bitCost}>" + "\n",
                        CenterIntro: true,
                        BitCost: bitCost);

                    if (repairBitSupplier == null)
                    {
                        return false;
                    }
                    bool vendorSuppliesBits = repairBitSupplier == vendor;

                    BitLocker bitSupplierBitLocker = repairBitSupplier.RequirePart<BitLocker>();

                    ModifyBitCostEvent.Process(vendor, bitCost, "Repair");

                    if (!bitSupplierBitLocker.HasBits(bitCost))
                    {
                        string message = GameText.VariableReplace(
                            $"{repairBitSupplier.T()}{repairBitSupplier.GetVerb("do")} not have the required <{bitCost}> bits!\n\n" +
                            $"{repairBitSupplier.It} =verb:have:afterpronoun=:\n" +
                            $"{bitSupplierBitLocker.GetBitsString()}", repairBitSupplier);
                        Popup.ShowFail(Message: message);
                        return false;
                    }

                    tinkerInvoice = new(vendor, TinkerInvoice.REPAIR, null, bitCost, repairableItem)
                    {
                        VendorSuppliesBits = vendorSuppliesBits,
                    };
                    int totalDramsCost = tinkerInvoice.GetTotalCost();

                    if (player.GetFreeDrams() < totalDramsCost)
                    {
                        Popup.ShowFail(
                            $"You do not have the required {totalDramsCost.Things("dram").Color("C")} " +
                            $"to have {vendor.t()} repair this item.");
                        Popup.Show(tinkerInvoice, "Invoice");
                        return false;
                    }
                    if (Popup.ShowYesNo($"{vendor.T()} will repair this item for " +
                        $"{totalDramsCost.Things("dram").Color("C")} of fresh water." +
                        $"\n\n{tinkerInvoice}") == DialogResult.Yes)
                    {
                        bitSupplierBitLocker.UseBits(bitCost);

                        player.UseDrams(totalDramsCost);
                        vendor.GiveDrams(totalDramsCost);

                        vendor.PlayWorldOrUISound("Sounds/Misc/sfx_interact_artifact_repair");

                        RepairedEvent.Send(vendor, repairableItem, null, vendorRepairSkill);

                        string repairedMessage = GameText.VariableReplace($"{vendor.T()} repaired {repairableItem.t()}.", vendor);
                        Popup.Show(repairedMessage);

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
                    if (!vendor.CanMoveExtremities("Recharge", ShowMessage: true))
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
            return base.HandleEvent(E);
        }

        public override void Write(GameObject Basis, SerializationWriter Writer)
        {
            base.Write(Basis, Writer);

            Writer.Write(KnownRecipes);
        }
        public override void Read(GameObject Basis, SerializationReader Reader)
        {
            base.Read(Basis, Reader);

            KnownRecipes = Reader.ReadList<TinkerData>();
        }
        public override void FinalizeRead(SerializationReader Reader)
        {
            base.FinalizeRead(Reader);
            if (!ParentObject.IsPlayer())
            {
                KnowImplantedRecipes(ParentObject, InstalledRecipes);
            }
        }

        public override void FinalizeCopy(GameObject Source, bool CopyEffects, bool CopyID, Func<GameObject, GameObject> MapInv)
        {
            base.FinalizeCopy(Source, CopyEffects, CopyID, MapInv);

            if (CopyID && Source.TryGetPart(out UD_VendorTinkering vendorTinkering))
            {
                KnownRecipes = vendorTinkering.GetKnownRecipes(IncludeInstalled: false).ToList();
            }
            else
            {
                if (ParentObject is GameObject vendor
                    && vendor.TryGetPart(out BitLocker bitLocker))
                {
                    Dictionary<char, int> bitStorage = new(bitLocker?.BitStorage);
                    bitLocker?.UseBits(bitStorage);
                }
                LearnByteRecipes(ParentObject, KnownRecipes);
                LearnGiganticRecipe(ParentObject, KnownRecipes);
                KnowImplantedRecipes(ParentObject, InstalledRecipes);
            }
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
