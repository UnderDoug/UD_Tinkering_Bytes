using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UD_Modding_Toolbox;
using UD_Vendor_Actions;
using XRL;
using XRL.World;
using XRL.World.Parts;
using XRL.World.Tinkering;

namespace UD_Tinkering_Bytes
{
    public static class Utils
    {
        public static ModInfo ThisMod => ModManager.GetMod("UD_Tinkering_Bytes");

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
                    char bit = byteBlueprint.GetPartParameter<char>(nameof(UD_TinkeringByte), nameof(UD_TinkeringByte.Bit));
                    if (TinkerItem.BitCostMap.ContainsKey(byteBlueprint.Name)
                        && TinkerItem.BitCostMap[byteBlueprint.Name].Any(c => c != bit))
                    {
                        string bitCost = "";
                        for (int i = 0; i < UD_TinkeringByte.BitsPerByte; i++)
                        {
                            bitCost += bit;
                        }
                        TinkerItem.BitCostMap[byteBlueprint.Name] = bitCost;
                    }
                }
            }
        }
    }
}
