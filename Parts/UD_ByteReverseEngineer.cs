using System;

using UD_Tinkering_Bytes;

using UD_Blink_Mutation;

namespace XRL.World.Parts
{
    [Serializable]
    public class UD_ByteReverseEngineer : IScribedPart, IModEventHandler<GetVendorTinkeringBonusEvent>
    {
        public static bool WantTinkerBonusMax = true;

        public UD_ByteReverseEngineer()
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
            return base.WantEvent(ID, Cascade);
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
                    Debug.CheckYeh(4, $"{E.Item.T(Single: true)}{E.Item.GetVerb("have")} a tinkering bonus of {9999.Signed()}!", Indent: indent + 1, Toggle: true);
                    Debug.LastIndent = indent;
                    return true;
                }
            }
            return base.HandleEvent(E);
        }
    }
}
