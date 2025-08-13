using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;

using XRL.Language;
using XRL.Messages;
using XRL.Rules;
using XRL.UI;
using XRL.World.Capabilities;
using XRL.World.Tinkering;

using UD_Tinkering_Bytes;

using UD_Blink_Mutation;

namespace XRL.World.Parts
{
    [Serializable]
    public class UD_BytePunnet : IScribedPart
    {
        public const string COMMAND_UNPACK = "UnpackBytePunnet";
        public const string COMMAND_UNPACK_ALL = "UnpackAllBytePunnets";
        public const string BYTE_BLUEPRINT_END = " Byte";

        public static int BytesPerPunnet => 32;

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

        public static bool UnpackPunnet(UD_BytePunnet BytePunnet, Dictionary<string, GameObject> BytesBucket)
        {
            BytePunnet.BytesBlueprintList = GetByteBlueprints(BytePunnet.Bytes);
            BytesBucket ??= new();
            if (!BytePunnet.BytesBlueprintList.IsNullOrEmpty())
            {
                for (int i = 0; i < BytesPerPunnet; i++)
                {
                    int seededIndex = Stat.SeededRandom($"{BytePunnet.ParentObject.ID}:{i}", 0, BytePunnet.BytesBlueprintList.Count - 1);
                    string blueprint = BytePunnet.BytesBlueprintList[seededIndex];
                    if (!blueprint.IsNullOrEmpty())
                    {
                        if (!BytesBucket.ContainsKey(blueprint))
                        {
                            BytesBucket.Add(blueprint, GameObjectFactory.Factory.CreateObject(blueprint));
                        }
                        else
                        {
                            BytesBucket[blueprint].Count++;
                        }
                    }
                }
                if (BytePunnet.ParentObject.Count > 0)
                {
                    BytePunnet.ParentObject.Count--;
                }
                else
                {
                    BytePunnet.ParentObject.Destroy();
                }
            }
            return false;
        }

        public override bool AllowStaticRegistration()
        {
            return true;
        }
        public override bool WantEvent(int ID, int Cascade)
        {
            return base.WantEvent(ID, Cascade)
                || ID == AfterObjectCreatedEvent.ID
                || ID == GetInventoryActionsEvent.ID
                || ID == InventoryActionEvent.ID;
        }
        public override bool HandleEvent(AfterObjectCreatedEvent E)
        {
            if (E.Object != null && E.Object == ParentObject && E.Object.TryGetPart(out Description description))
            {
                Render render = E.Object.GetPart<Render>();
                string bytes = "byte";
                string bits = "bit";
                if (render != null)
                {
                    if (Bytes.Length > 1)
                    {
                        bytes = render.DisplayName.Replace("punnet of ", "");
                    }
                    else
                    {
                        GameObjectBlueprint byteBlueprint = GameObjectFactory.Factory.GetBlueprint(Bytes[0] + BYTE_BLUEPRINT_END);
                        if (byteBlueprint != null)
                        {
                            bytes = byteBlueprint.DisplayName();
                        }
                        render.DisplayName.Replace("*bytes*", bytes.Pluralize());
                    }
                    if (BitType.BitMap.ContainsKey(render.TileColor[1]))
                    {
                        BitType bitType = BitType.BitMap[render.TileColor[1]];
                        bits = bitType.Description;
                    }
                }
                description.Short = description.Short.Replace("*32 bytes*", 32.Things(bytes));
                description.Short = description.Short.Replace("*256 bits*", 256.Things(bits));
            }
            return base.HandleEvent(E);
        }
        public override bool HandleEvent(GetInventoryActionsEvent E)
        {
            E.AddAction("Unpack", "unpack", COMMAND_UNPACK, Key: 'u', Default: 5, WorksTelekinetically: true);
            if (E.Object.Count > 1)
            {
                E.AddAction("Unpack All", "unpack all", COMMAND_UNPACK_ALL, Key: 'U', Default: 4, WorksTelekinetically: true);
            }
            return base.HandleEvent(E);
        }
        public override bool HandleEvent(InventoryActionEvent E)
        {
            if (E.Command == COMMAND_UNPACK || E.Command == COMMAND_UNPACK_ALL)
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
                if (E.Actor.AreHostilesNearby() && E.Actor.FireEvent("CombatPreventsTinkering"))
                {
                    Popup.ShowFail("You can't unpack with hostiles nearby.");
                    return false;
                }
                Dictionary<string, GameObject> bytesBucket = new();
                int amountToUnpack = ParentObject.Count;
                if (E.Command == COMMAND_UNPACK)
                {
                    UnpackPunnet(this, bytesBucket);
                }
                else
                {
                    int attempts = 0;
                    while (ParentObject.Count > 0 && attempts < 25)
                    {
                        if (!UnpackPunnet(this, bytesBucket))
                        {
                            attempts++;
                        }
                    }
                }

                if (!bytesBucket.IsNullOrEmpty())
                {
                    if (!bytesBucket.IsNullOrEmpty())
                    {
                        Dictionary<string, string> receivedBytesSortable = new();
                        List<string> byteBlueprints = new();
                        List<string> receivedBytesList = new();
                        foreach ((string _, GameObject byteStack) in bytesBucket)
                        {
                            byteBlueprints.Add(byteStack.Blueprint);
                            receivedBytesSortable.Add(byteStack.Blueprint, byteStack.Count.Things(byteStack.Render.DisplayName));
                        }
                        byteBlueprints.Sort((s, o) => GetByteIndex(s.Strip()[0]).CompareTo(GetByteIndex(o.Strip()[0])));
                        foreach (string byteBlueprint in byteBlueprints)
                        {
                            receivedBytesList.Add(receivedBytesSortable[byteBlueprint]);
                        }
                        string thisPunnet = E.Command == COMMAND_UNPACK ? ParentObject.DisplayName : amountToUnpack.Things(ParentObject.DisplayName);
                        string receivedString = Grammar.MakeAndList(receivedBytesList);
                        E.Actor.ShowSuccess($"{E.Actor.GetVerb("unpack")} {E.Actor.Poss(thisPunnet)} into {receivedString}");
                        return true;
                    }
                    E.RequestInterfaceExit();
                }
            }
            return base.HandleEvent(E);
        }
    }
}
