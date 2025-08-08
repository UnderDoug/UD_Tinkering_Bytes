using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UD_Tinkering_Bytes;
using XRL.Language;
using XRL.Rules;
using XRL.World.Capabilities;
using XRL.World.Tinkering;

namespace XRL.World.Parts
{
    [Serializable]
    public class UD_BytePunnet : IScribedPart
    {
        public const string COMMAND_UNPACK = "UnpackBytePunnet";
        public const string BYTE_BLUEPRINT_END = " Byte";

        private static int BytesPerPunnet => 32;

        private static int MaxByte => BytesOrder.Count - 1;

        private static Dictionary<string, string> BytesMap => new()
        {
            { "*", $"0-{MaxByte}" },
            { "0", $"0-3" },
        };

        private static List<char> BytesOrder => new(GetByteChars());

        private List<string> BytesBlueprintList = new();

        public string Bytes;

        public UD_BytePunnet()
        {
            Bytes = null;
        }

        public static bool IsWildCardByte(string Bytes)
        {
            return !Bytes.IsNullOrEmpty()
                && (Bytes == "*"
                    || Bytes == "0"
                    || Bytes.Contains("-")
                    || Bytes.Contains("<")
                    || Bytes.Contains(">"));
        }

        public static IEnumerable<char> GetByteChars()
        {
            if (BitType.BitSortOrder.IsNullOrEmpty())
            {
                yield break;
            }
            foreach (BitType bitType in BitType.BitTypes)
            {
                yield return BitType.CharTranslateBit(bitType.Color);
            }
        }

        public static int GetByteIndex(string Byte)
        {
            if (Byte.IsNullOrEmpty())
            {
                return -1;
            }
            return BitType.GetBitSortOrder(BitType.ReverseCharTranslateBit(Byte[0]));
        }
        public static bool TryGetByteIndex(string Byte, out int Index)
        {
            return (Index = GetByteIndex(Byte)) > -1;
        }
        public static int GetByteIndex(char Byte)
        {
            return GetByteIndex(Byte.ToString());
        }
        public static bool TryGetByteIndex(char Byte, out int Index)
        {
            return (Index = GetByteIndex(Byte)) > -1;
        }

        public static string GetByteRange(string Bytes)
        {
            if (!Bytes.IsNullOrEmpty() && (Bytes[0] == '>' || Bytes[0] == '<'))
            {
                if (TryGetByteIndex(Bytes[1..], out int output))
                {
                    output = Math.Max(0, Math.Min(output, MaxByte));
                    if (Bytes[0] == '>')
                    {
                        return $"{output + 1}-{MaxByte}";
                    }
                    if (Bytes[0] == '<')
                    {
                        return $"0-{output - 1}";
                    }
                }
            }
            return null;
        }

        public static List<string> GetByteBlueprints(string Bytes)
        {
            List<string> bytesBlueprintList = new();

            if (!Bytes.IsNullOrEmpty())
            {
                if (IsWildCardByte(Bytes))
                {
                    string bytesRange;
                    if (BytesMap.ContainsKey(Bytes))
                    {
                        bytesRange = BytesMap[Bytes];
                    }
                    else
                    {
                        bytesRange = GetByteRange(Bytes);
                    }
                    // AddPlayerMessage(nameof(bytesRange)+": "+bytesRange);
                    string[] bytesHighLow = bytesRange.Split('-');
                    if (!int.TryParse(bytesHighLow[0], out int low))
                    {
                        low = 0;
                    }
                    if (!int.TryParse(bytesHighLow[1], out int high))
                    {
                        high = MaxByte;
                    }
                    for (int i = low; i < high + 1; i++)
                    {
                        bytesBlueprintList.Add($"{BytesOrder[i]}{BYTE_BLUEPRINT_END}");
                    }
                }
                else
                {
                    foreach (char c in Bytes)
                    {
                        if (TryGetByteIndex(c, out int byteIndex))
                        {
                            bytesBlueprintList.Add($"{BytesOrder[byteIndex]}{BYTE_BLUEPRINT_END}");
                        }
                    }
                }
            }
            return bytesBlueprintList;
        }

        public override bool WantEvent(int ID, int Cascade)
        {
            return base.WantEvent(ID, Cascade)
                || ID == GetInventoryActionsEvent.ID
                || ID == InventoryActionEvent.ID;
        }
        public override bool HandleEvent(GetInventoryActionsEvent E)
        {
            E.AddAction("Unpack", "unpack", COMMAND_UNPACK, Key: 'u');
            return base.HandleEvent(E);
        }
        public override bool HandleEvent(InventoryActionEvent E)
        {
            if (E.Command == COMMAND_UNPACK)
            {
                if (!E.Actor.CheckFrozen(Telepathic: false, Telekinetic: true))
                {
                    return false;
                }
                if (E.Item.IsBroken() || E.Item.IsRusted() || E.Item.IsEMPed())
                {
                    E.Actor.Fail(ParentObject.Does("do") + " nothing.");
                    return false;
                }

                BytesBlueprintList = GetByteBlueprints(Bytes);
                Dictionary<string, int> receivedBytes = new();
                if (!BytesBlueprintList.IsNullOrEmpty())
                {
                    string backUpByte = null;
                    for (int i = 0; i < BytesPerPunnet; i++)
                    {
                        int seededIndex = Stat.SeededRandom($"{ParentObject.ID}:{i}", 0, BytesBlueprintList.Count - 1);
                        string blueprint = BytesBlueprintList[seededIndex];
                        GameObject byteObject = GameObjectFactory.Factory.CreateObject(blueprint);
                        string byteName = byteObject.Render.DisplayName;
                        backUpByte = byteName;
                        if (E.Actor.ReceiveObject(byteObject))
                        {
                            if (receivedBytes.Keys.Contains(byteName))
                            {
                                receivedBytes[byteName]++;
                            }
                            else
                            {
                                receivedBytes.Add(byteName, 1);
                            }
                        }
                    }
                    if (!receivedBytes.IsNullOrEmpty())
                    {
                        List<string> receivedBytesList = new();
                        foreach ((string byteName, int count) in receivedBytes)
                        {
                            receivedBytesList.Add(count.Things(byteName));
                        }
                        receivedBytesList.Sort((s,o) => GetByteIndex(s.Strip()).CompareTo(GetByteIndex(o.Strip())));
                        string receivedString = Grammar.MakeAndList(receivedBytesList) ?? BytesPerPunnet.Things(backUpByte);
                        E.Actor.ShowSuccess($"{ParentObject.Does("unpack")} into {receivedString}");
                        ParentObject.Destroy();
                        E.RequestInterfaceExit();
                    }
                }
            }
            return base.HandleEvent(E);
        }
    }
}
