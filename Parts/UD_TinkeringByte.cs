using System;

using UD_Tinkering_Bytes;

using UD_Modding_Toolbox;
using XRL.World.Tinkering;

namespace XRL.World.Parts
{
    [Serializable]
    public class UD_TinkeringByte : IScribedPart, IModEventHandler<GetVendorTinkeringBonusEvent>
    {
        public static bool WantTinkerBonusMax = true;

        public static int BitsPerByte => 8;

        public UD_TinkeringByte()
        {
        }

        public override bool AllowStaticRegistration()
        {
            return true;
        }
        public override void Register(GameObject Object, IEventRegistrar Registrar)
        {
            Registrar.Register(GetVendorTinkeringBonusEvent.ID, EventOrder.EXTREMELY_EARLY);
            base.Register(Object, Registrar);
        }
        public override bool WantEvent(int ID, int Cascade)
        {
            return base.WantEvent(ID, Cascade)
                || ID == AfterObjectCreatedEvent.ID;
        }
        public virtual bool HandleEvent(GetVendorTinkeringBonusEvent E)
        {
            if (WantTinkerBonusMax)
            {
                if (E.Item != null && E.Item == ParentObject && (E.Type == "Disassemble" || E.Type == "ReverseEngineer"))
                {
                    int indent = Debug.LastIndent;
                    E.Bonus = 9999;
                    E.SecondaryBonus = 9999;
                    Debug.CheckYeh(4, $"{E.Item.T(Single: true)}{E.Item.GetVerb("have")} a tinkering bonus of {9999.Signed()}!",
                        Indent: indent + 1, Toggle: true);
                    Debug.LastIndent = indent;
                    return true;
                }
            }
            return base.HandleEvent(E);
        }
        public override bool HandleEvent(AfterObjectCreatedEvent E)
        {
            if (E.Object != null && E.Object == ParentObject && E.Object.TryGetPart(out Description description))
            {
                TinkerItem tinkerItem = E.Object.GetPart<TinkerItem>();
                string bits = "bit";
                if (tinkerItem != null)
                {
                    char bit = tinkerItem.Bits[0];
                    if (BitType.BitMap.ContainsKey(bit))
                    {
                        BitType bitType = BitType.BitMap[bit];
                        bits = bitType.Description;
                    }
                }
                description._Short = description.Short.Replace("*8 bits*", BitsPerByte.Things(bits, bits));
            }
            return base.HandleEvent(E);
        }
    }
}
