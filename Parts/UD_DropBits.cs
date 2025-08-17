using System;
using System.Collections.Generic;

using XRL.Names;
using XRL.World.Capabilities;
using XRL.World.Tinkering;

using UD_Modding_Toolbox;
using static UD_Modding_Toolbox.Const;
using UD_Vendor_Actions;

using UD_Tinkering_Bytes;

using static UD_Tinkering_Bytes.Utils;

namespace XRL.World.Parts
{
    [Serializable]
    public class UD_DropBits : IScribedPart
    {
        public int BitsChance = 100; // chance this creature will drop the contents of their BitLocker

        public int BitChance = 25; // chance per bit that an indifidual bit will be dropped.

        public int BurntChanceModifier = 50; // chance a burnt creature will drop an individual bit
        // a bit of a creature that has a BitChance of 70 and BurntChanceModifier of 50 will frop 35% of the time.

        public int VaporizedChanceModifier = 10; // chance a vaporized creature will drop an individual bit
        // a bit of a creature that has a BitChance of 70 and VaporizedChanceModifier of 10 will frop 7% of the time.

        public int BuildBitsChance = 100;

        public UD_DropBits()
        {
        }

        private void ProcessBitsDrop(BeforeDeathRemovalEvent E)
        {
            if (ParentObject != null && ParentObject.TryGetPart(out BitLocker bitLocker) && BitsChance.in100())
            {
                IInventory dropInventory = ParentObject.GetDropInventory();
                if (dropInventory == null)
                {
                    return;
                }
                Zone inventoryZone = dropInventory.GetInventoryZone();
                if (inventoryZone != null && !inventoryZone.Built && !BuildBitsChance.in100())
                {
                    return;
                }

                int indent = Debug.LastIndent;
                bool doDebug = true;
                Debug.Entry(4, 
                    $"{nameof(UD_DropBits)}." +
                    $"{nameof(ProcessBitsDrop)}(" +
                    $"{nameof(BeforeDeathRemovalEvent)} E) for " +
                    $"{nameof(E.Dying)}: {E.Dying?.DebugName ?? NULL}",
                    Indent: indent + 1, Toggle: doDebug);

                bool wasBurnt = ParentObject.Physics?.LastDamagedByType == "Fire" || ParentObject.Physics?.LastDamagedByType == "Light";
                bool wasVaporised = ParentObject.Physics.LastDamagedByType == "Vaporized";

                Debug.LoopItem(4, $"{nameof(wasBurnt)}: {wasBurnt}", Good: wasBurnt, Indent: indent + 2, Toggle: doDebug);
                Debug.LoopItem(4, $"{nameof(wasVaporised)}: {wasVaporised}", Good: wasBurnt, Indent: indent + 2, Toggle: doDebug);

                if ((wasBurnt && BurntChanceModifier < 1) || (wasVaporised && VaporizedChanceModifier < 1))
                {
                    return;
                }

                Dictionary<char, int> droppableBits = new();

                Debug.Entry(4, $"Getting bits from {nameof(bitLocker.BitStorage)}...", Indent: indent + 2, Toggle: doDebug);
                foreach ((char bit, int count) in bitLocker.BitStorage)
                {
                    try
                    {
                        Debug.LoopItem(4, $"{nameof(bit)}: {bit}, {nameof(count)}: {count}", Indent: indent + 3, Toggle: doDebug);
                        int amountToDrop = count * BitChance / 100;
                        if (wasBurnt)
                        {
                            amountToDrop *= BurntChanceModifier / 100;
                        }
                        if (wasVaporised)
                        {
                            amountToDrop *= VaporizedChanceModifier / 100;
                        }
                        if (amountToDrop > 0)
                        {
                            Debug.LoopItem(4, $"{nameof(amountToDrop)}: {amountToDrop}", Indent: indent + 4, Toggle: doDebug);
                            droppableBits.Add(bit, amountToDrop);
                        }
                    }
                    catch (Exception x)
                    {
                        MetricsManager.LogException(nameof(bitLocker.BitStorage), x);
                    }
                }

                if (droppableBits.IsNullOrEmpty())
                {
                    Debug.CheckNah(4, $"{nameof(droppableBits)}: IsNullOrEmpty", Indent: indent + 4, Toggle: doDebug);
                    Debug.LastIndent = indent;
                    return;
                }


                Dictionary<char, string> byteBlueprints = new();
                Debug.Entry(4, $"Getting {nameof(byteBlueprints)}...", Indent: indent + 2, Toggle: doDebug);
                foreach (GameObjectBlueprint byteBlueprint in GameObjectFactory.Factory.GetBlueprintsInheritingFrom("BaseByte"))
                {
                    try
                    {
                        string blueprintName = byteBlueprint.Name;
                        string byteBits = byteBlueprint.GetPartParameter<string>(nameof(TinkerItem), "Bits");
                        Debug.LoopItem(4,
                            $"{nameof(blueprintName)}: {blueprintName}, " +
                            $"{nameof(byteBits)}: {byteBits ?? NULL}",
                            Indent: indent + 3, Toggle: doDebug);
                        if (!byteBits.IsNullOrEmpty())
                        {
                            char byteBit = byteBits[0];
                            if (int.TryParse(byteBits[0].ToString(), out _))
                            {
                                byteBit = BitType.ReverseCharTranslateBit(byteBits[0]);
                            }

                            Debug.LoopItem(4, $"{nameof(byteBit)}: {byteBit}", Indent: indent + 4, Toggle: doDebug);

                            if (byteBit != '?' && !byteBlueprints.ContainsKey(byteBit))
                            {
                                byteBlueprints.Add(byteBit, blueprintName);
                            }
                        }
                    }
                    catch (Exception x)
                    {
                        MetricsManager.LogException(nameof(byteBlueprints), x);
                    }
                }

                Dictionary<char, string> bytePunnetBlueprints = new();
                Debug.Entry(4, $"Getting {nameof(bytePunnetBlueprints)}...", Indent: indent + 2, Toggle: doDebug);
                foreach (GameObjectBlueprint bytePunnetBlueprint in GameObjectFactory.Factory.GetBlueprintsInheritingFrom("BaseBytePunnet"))
                {
                    try
                    {
                        string blueprintName = bytePunnetBlueprint.Name;
                        string bytePunnetBytes = bytePunnetBlueprint.GetPartParameter<string>(nameof(UD_BytePunnet), "Bytes");
                        Debug.LoopItem(4,
                            $"{nameof(blueprintName)}: {blueprintName}, " +
                            $"{nameof(bytePunnetBytes)}: {bytePunnetBytes ?? NULL}",
                            Indent: indent + 3, Toggle: doDebug);
                        if (!bytePunnetBytes.IsNullOrEmpty())
                        {
                            char bytePunnetBit = BitType.ReverseCharTranslateBit(bytePunnetBytes[0]);

                            Debug.LoopItem(4, $"{nameof(bytePunnetBit)}: {bytePunnetBit}", Indent: indent + 4, Toggle: doDebug);

                            if (bytePunnetBit != '?' && !bytePunnetBlueprints.ContainsKey(bytePunnetBit))
                            {
                                bytePunnetBlueprints.Add(bytePunnetBit, blueprintName);
                            }
                        }
                    }
                    catch (Exception x)
                    {
                        MetricsManager.LogException(nameof(bytePunnetBlueprints), x);
                    }
                }

                Dictionary<string, int> bitBlueprintsToDrop = new();

                int bitsPerPunnet = UD_BytePunnet.BitsPerPunnet; // 256
                int bitsPerByte = UD_TinkeringByte.BitsPerByte; // 8

                List<char> droppableBitsList = new(droppableBits.Keys);
                Debug.Entry(4, $"Compiling {nameof(bitBlueprintsToDrop)}...", Indent: indent + 2, Toggle: doDebug);
                foreach (char bit in droppableBitsList)
                {
                    try
                    {
                        int totalDroppableBits = droppableBits[bit];
                        int currentDroppableBits = totalDroppableBits;
                        Debug.LoopItem(4, $"{nameof(bit)}: {bit}", Indent: indent + 3, Toggle: doDebug);
                        if (currentDroppableBits > bitsPerPunnet + 1)
                        {
                            int punnetDropCount = (int)Math.Floor((double)currentDroppableBits / bitsPerPunnet);

                            Debug.LoopItem(4,
                                $"{nameof(bytePunnetBlueprints)}, " +
                                $"{nameof(punnetDropCount)}: {punnetDropCount}",
                                Indent: indent + 4, Toggle: doDebug);

                            bitBlueprintsToDrop.Add(bytePunnetBlueprints[bit], punnetDropCount);
                            droppableBits[bit] -= punnetDropCount;
                            currentDroppableBits = droppableBits[bit];
                        }
                        if (currentDroppableBits > bitsPerByte + 1)
                        {
                            int byteDropCount = (int)Math.Floor((double)currentDroppableBits / bitsPerByte);

                            Debug.LoopItem(4,
                                $"{nameof(byteBlueprints)}, " +
                                $"{nameof(byteDropCount)}: {byteDropCount}",
                                Indent: indent + 4, Toggle: doDebug);

                            bitBlueprintsToDrop.Add(byteBlueprints[bit], byteDropCount);
                            droppableBits[bit] -= byteDropCount;
                            currentDroppableBits = droppableBits[bit];
                        }
                        if (currentDroppableBits > 1)
                        {
                            string scrapBlueprint = GetScrapBlueprintFromColor(bit);

                            Debug.LoopItem(4,
                                $"{nameof(scrapBlueprint)}, " +
                                $"{nameof(currentDroppableBits)}: {currentDroppableBits}",
                                Indent: indent + 4, Toggle: doDebug);

                            bitBlueprintsToDrop.Add(scrapBlueprint, currentDroppableBits);
                            droppableBits[bit] -= currentDroppableBits;
                            currentDroppableBits = droppableBits[bit];
                        }
                    }
                    catch (Exception x)
                    {
                        MetricsManager.LogException(nameof(droppableBitsList), x);
                    }
                }

                if (bitBlueprintsToDrop.IsNullOrEmpty())
                {
                    Debug.CheckNah(4, $"{nameof(bitBlueprintsToDrop)}: IsNullOrEmpty", Indent: indent + 4, Toggle: doDebug);
                    Debug.LastIndent = indent;
                    return;
                }

                Debug.Entry(4, $"Running through {nameof(bitBlueprintsToDrop)}...", Indent: indent + 2, Toggle: doDebug);
                foreach ((string bitBlueprintToDrop, int count) in bitBlueprintsToDrop)
                {
                    try
                    {
                        Debug.LoopItem(4,
                            $"{nameof(bitBlueprintToDrop)}: {bitBlueprintToDrop}, " +
                            $"{nameof(count)}: {count}",
                            Indent: indent + 2, Toggle: doDebug);
                        GameObject bitItemToDrop = GameObjectFactory.Factory.CreateObject(bitBlueprintToDrop, Context: nameof(UD_DropBits));
                        if (bitItemToDrop != null)
                        {
                            if (count > 1)
                            {
                                bitItemToDrop.Stacker.StackCount = count;
                            }
                            Temporary.CarryOver(ParentObject, bitItemToDrop);
                            Phase.carryOver(ParentObject, bitItemToDrop);
                            if (ParentObject.HasProperName)
                            {
                                bitItemToDrop.SetStringProperty("CreatureName", ParentObject.BaseDisplayName);
                            }
                            else
                            {
                                string creatureName = NameMaker.MakeName(ParentObject, FailureOkay: true);
                                if (creatureName != null)
                                {
                                    bitItemToDrop.SetStringProperty("CreatureName", creatureName);
                                }
                            }
                            if (ParentObject.HasID)
                            {
                                bitItemToDrop.SetStringProperty("SourceID", ParentObject.ID);
                            }
                            bitItemToDrop.SetStringProperty("SourceBlueprint", ParentObject.Blueprint);
                            if (E.Killer != null && E.Killer != ParentObject)
                            {
                                if (E.Killer.HasID)
                                {
                                    bitItemToDrop.SetStringProperty("KillerID", E.Killer.ID);
                                }
                                bitItemToDrop.SetStringProperty("KillerBlueprint", E.Killer.Blueprint);
                            }
                            if (!E.ThirdPersonReason.IsNullOrEmpty())
                            {
                                bitItemToDrop.SetStringProperty("DeathReason", E.ThirdPersonReason);
                            }
                            if (ParentObject.HasProperty("StoredByPlayer") || ParentObject.HasProperty("FromStoredByPlayer"))
                            {
                                bitItemToDrop.SetIntProperty("FromStoredByPlayer", 1);
                            }

                            dropInventory.AddObjectToInventory(
                                Object: bitItemToDrop,
                                Context: nameof(UD_DropBits),
                                ParentEvent: E);

                            DroppedEvent.Send(ParentObject, bitItemToDrop);

                            Debug.Entry(4, $"{nameof(DroppedEvent)} sent.", Indent: indent + 3, Toggle: doDebug);
                        }
                    }
                    catch (Exception x)
                    {
                        MetricsManager.LogException(nameof(bitBlueprintsToDrop), x);
                    }
                }
                Debug.LastIndent = indent;
            }
        }

        public override bool AllowStaticRegistration()
        {
            return true;
        }
        public override void Register(GameObject Object, IEventRegistrar Registrar)
        {
            Registrar.Register(BeforeDeathRemovalEvent.ID, EventOrder.VERY_EARLY);
            base.Register(Object, Registrar);
        }
        public override bool WantEvent(int ID, int Cascade)
        {
            return base.WantEvent(ID, Cascade);
        }
        public override bool HandleEvent(BeforeDeathRemovalEvent E)
        {
            if (ParentObject.GetIntProperty("SuppressCorpseDrops") < 1 || ParentObject.GetIntProperty("SuppressBitsDrops") < 1)
            {
                ProcessBitsDrop(E);
            }
            return base.HandleEvent(E);
        }
    }
}
