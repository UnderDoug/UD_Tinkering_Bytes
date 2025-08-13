using System;
using System.Collections.Generic;
using UD_Blink_Mutation;
using UD_Tinkering_Bytes;
using XRL.World.Tinkering;

namespace XRL.World.Parts
{
    [Serializable]
    public class UD_Byte_DataDisk : IScribedPart, IModEventHandler<GetVendorTinkeringBonusEvent>
    {
        public UD_Byte_DataDisk()
        {
        }

        public override bool AllowStaticRegistration()
        {
            return true;
        }
        public override bool WantEvent(int ID, int Cascade)
        {
            return base.WantEvent(ID, Cascade)
                || ID == AfterObjectCreatedEvent.ID;
        }
        public override bool HandleEvent(AfterObjectCreatedEvent E)
        {
            if (E.Object != null && E.Object == ParentObject && ParentObject.TryGetPart(out DataDisk dataDisk))
            {
                List<GameObjectBlueprint> byteGameObjectBlueprints = GameObjectFactory.Factory.GetBlueprintsInheritingFrom("BaseByte");
                List<string> byteBlueprints = new();
                if (!byteGameObjectBlueprints.IsNullOrEmpty())
                {
                    foreach (GameObjectBlueprint byteBlueprint in byteGameObjectBlueprints)
                    {
                        byteBlueprints.Add(byteBlueprint.Name);
                    }
                }
                if (!byteBlueprints.IsNullOrEmpty() && byteBlueprints.Contains(dataDisk.Data.Blueprint))
                {
                    E.ReplacementObject = GameObjectFactory.Factory.CreateObject(ParentObject.Blueprint);
                }
            }
            return base.HandleEvent(E);
        }
    }
}
