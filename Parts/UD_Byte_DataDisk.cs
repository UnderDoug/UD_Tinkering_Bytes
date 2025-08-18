using System;
using System.Collections.Generic;

using XRL.World.Tinkering;

using UD_Modding_Toolbox;
using UD_Vendor_Actions;

using static UD_Modding_Toolbox.Const;

using UD_Tinkering_Bytes;

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
                List<string> byteBlueprints = new();
                foreach (GameObjectBlueprint byteBlueprint in GameObjectFactory.Factory.GetBlueprintsInheritingFrom("BaseByte"))
                {
                    byteBlueprints.Add(byteBlueprint.Name);
                }
                if (!byteBlueprints.IsNullOrEmpty() && byteBlueprints.Contains(dataDisk.Data.Blueprint))
                {
                    E.ReplacementObject = GameObjectFactory.Factory.CreateObject(ParentObject.Blueprint);
                    int indent = Debug.LastIndent;
                    Debug.Entry(4, 
                        $"{nameof(UD_Byte_DataDisk)}." +
                        $"{nameof(HandleEvent)}(" +
                        $"{nameof(AfterObjectCreatedEvent)} E)",
                        Indent: indent + 1, Toggle: true);
                    Debug.Entry(4, $"{nameof(E.ReplacementObject)}: {E.ReplacementObject.DebugName ?? NULL}",
                        Indent: indent + 2, Toggle: true);
                    if (E.ReplacementObject != null && E.ReplacementObject.TryGetPart(out DataDisk replacementDataDisk))
                    {
                        Debug.Entry(4, $"{nameof(replacementDataDisk)}: {replacementDataDisk?.Data?.Blueprint ?? NULL}",
                            Indent: indent + 2, Toggle: true);
                    }
                    Debug.LastIndent = indent;
                }
            }
            return base.HandleEvent(E);
        }
    }
}
