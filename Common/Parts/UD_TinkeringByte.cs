using System;

using UD_Tinkering_Bytes;

using UD_Modding_Toolbox;
using XRL.World.Tinkering;
using System.Collections.Generic;
using System.Linq;

namespace XRL.World.Parts
{
    [Serializable]
    public class UD_TinkeringByte : IScribedPart, IModEventHandler<GetVendorTinkeringBonusEvent>
    {
        public static bool WantTinkerBonusMax = true;

        public static int BitsPerByte => 8;

        private char _Bit;
        public char Bit
        { 
            get
            {
                if (_Bit == default
                    && ParentObject is GameObject byteObject
                    && byteObject.TryGetPart(out TinkerItem tinkerItem)
                    && tinkerItem.Bits.Length > 0)
                {
                    _Bit = tinkerItem.Bits[^1];
                }
                return _Bit;
            }
        }

        public UD_TinkeringByte()
        {
            _Bit = default;
        }

        public static IEnumerable<GameObjectBlueprint> GetByteGameObjectBlueprints()
        {
            return GameObjectFactory.Factory.SafelyGetBlueprintsInheritingFrom("BaseByte").AsEnumerable();
        }
        public static IEnumerable<string> GetByteBlueprints()
        {
            foreach (GameObjectBlueprint byteBlueprint in GetByteGameObjectBlueprints())
            {
                yield return byteBlueprint.Name;
            }
            yield break;
        }
        public static bool IsByteBlueprint(string Blueprint)
        {
            return GetByteBlueprints().Contains(Blueprint);
        }
        public static bool IsByteBlueprint(GameObjectBlueprint GameObjectBlueprint)
        {
            return IsByteBlueprint(GameObjectBlueprint.Name);
        }

        public override bool AllowStaticRegistration()
        {
            return true;
        }
        public override void Register(GameObject Object, IEventRegistrar Registrar)
        {
            Registrar.Register(GetVendorTinkeringBonusEvent.ID, EventOrder.EXTREMELY_EARLY);
            Registrar.Register(GetTinkeringBonusEvent.ID, EventOrder.EXTREMELY_EARLY);
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
                    Debug.CheckYeh(4, $"{E.Item.ShortDisplayNameSingle}{E.Item.GetVerb("have")} a tinkering bonus of {9999.Signed()}!",
                        Indent: indent + 1, Toggle: true);
                    Debug.LastIndent = indent;
                    return true;
                }
            }
            return base.HandleEvent(E);
        }
        public override bool HandleEvent(GetTinkeringBonusEvent E)
        {
            if (WantTinkerBonusMax)
            {
                if (E.Item != null && E.Item == ParentObject && (E.Type == "Disassemble" || E.Type == "ReverseEngineer"))
                {
                    int indent = Debug.LastIndent;
                    E.Bonus = 9999;
                    E.SecondaryBonus = 9999;
                    Debug.CheckYeh(4, $"{E.Item.ShortDisplayNameSingle}{E.Item.GetVerb("have")} a tinkering bonus of {9999.Signed()}!",
                        Indent: indent + 1, Toggle: true);
                    Debug.LastIndent = indent;
                    return true;
                }
            }
            return base.HandleEvent(E);
        }
        public override bool HandleEvent(AfterObjectCreatedEvent E)
        {
            if (Bit != default 
                && E.Object != null
                && E.Object == ParentObject
                && E.Object.TryGetPart(out Description description))
            {
                string bits = "bit";
                bool haveBits = false;
                if (BitType.BitMap.ContainsKey(Bit))
                {
                    BitType bitType = BitType.BitMap[Bit];
                    bits = bitType.Description;
                    haveBits = true;
                }
                string pluralBits = haveBits ? bits : null;
                description._Short = description._Short.Replace("*8 bits*", BitsPerByte.Things(bits, pluralBits));
            }
            return base.HandleEvent(E);
        }
    }
}
