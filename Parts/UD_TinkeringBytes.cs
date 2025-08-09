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
    public class UD_TinkeringBytes : IScribedPart, IVendorActionEventHandler
    {
        public bool AllocatedBits;

        public UD_TinkeringBytes()
        {
            AllocatedBits = false;
        }

        /*
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
        */
    }
}
