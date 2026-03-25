using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using XRL;
using XRL.World;
using XRL.World.Parts;
using XRL.World.Tinkering;

using UD_Modding_Toolbox;
using UD_Vendor_Actions;

namespace UD_Tinkering_Bytes
{
    public static class Utils
    {
        public static ModInfo ThisMod => ModManager.GetMod("UD_Tinkering_Bytes");

        public static string ModAuthor => ThisMod?.Manifest?.Author;
        public static string ModAuthorStripped => ModAuthor?.Strip();

        public static string TellModAuthor => ModAuthor.IsNullOrEmpty() ? null : "Let " + ModAuthor + " know on the steam workshop discussion for this mod.";
        public static string TellModAuthorStripped => ModAuthorStripped.IsNullOrEmpty() ? null : "Let " + ModAuthorStripped + " know on the steam workshop discussion for this mod.";

        public static GameObjectBlueprint GetGameObjectBlueprint(string Blueprint)
        {
            return GameObjectFactory.Factory.GetBlueprintIfExists(Blueprint);
        }

        public static string GetScrapBlueprintFromBit(char Bit)
        {
            return Bit switch
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
        }
        public static string GetScrapBlueprintFromColor(char Color)
        {
            return Color switch
            {
                'R' => "Scrap Metal",
                'G' => "Scrap Crystal",
                'B' => "Scrap Electronics",
                'C' => "Scrap Energy",
                'r' => "Scrap 1",
                'g' => "Scrap 2",
                'b' => "Scrap 3",
                'c' => "Scrap 4",
                'K' => "Scrap 5",
                'W' => "Scrap 6",
                'Y' => "Scrap 7",
                'M' => "Scrap 8",
                _ => null,
            };
        }

        public static void ForceByteBitCost()
        {
            List<GameObjectBlueprint> byteGameObjectBlueprints = new(UD_TinkeringByte.GetByteGameObjectBlueprints());
            if (!byteGameObjectBlueprints.IsNullOrEmpty())
            {
                foreach (GameObjectBlueprint byteBlueprint in UD_TinkeringByte.GetByteGameObjectBlueprints())
                {
                    string tinkerItemBits = byteBlueprint.GetPartParameter<string>(nameof(TinkerItem), nameof(TinkerItem.Bits));
                    if (!tinkerItemBits.IsNullOrEmpty())
                    {
                        char bit = tinkerItemBits[^1];
                        if (int.TryParse(bit.ToString(), out int bitLevel))
                        {
                            bit = BitType.LevelMap[bitLevel].FirstOrDefault().Color;
                        }
                        if (BitType.BitOrder.Contains(bit)
                            && TinkerItem.BitCostMap.ContainsKey(byteBlueprint.Name)
                            && TinkerItem.BitCostMap[byteBlueprint.Name].Any(c => c != bit))
                        {
                            string bitCost = "";
                            for (int i = 0; i < UD_TinkeringByte.BitsPerByte; i++)
                            {
                                bitCost += bit;
                            }
                            if (!bitCost.IsNullOrEmpty())
                            {
                                TinkerItem.BitCostMap[byteBlueprint.Name] = bitCost;
                            }
                        }
                    }
                }
            }
        }
    }
}
