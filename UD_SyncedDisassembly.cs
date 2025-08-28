using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;
using XRL.World.Tinkering;

namespace UD_Tinkering_Bytes
{
    public class UD_SyncedDisassembly : OngoingAction 
    {
        public Disassembly Disassembly;

        public GameObject Disassembler;

        public List<TinkerData> KnownRecipes;

        public UD_SyncedDisassembly(Disassembly Disassembly, GameObject Disassembler, List<TinkerData> KnownRecipes)
        {
            this.Disassembly = Disassembly;
            this.Disassembler = Disassembler;
            this.KnownRecipes = KnownRecipes;
        }

        public override bool ShouldHostilesInterrupt()
        {
            return true;
        }

        public override bool Continue()
        {
            return UD_VendorDisassembly.VendorDisassemblyContinue(Disassembler, Disassembly, ref KnownRecipes);
        }

        public override bool CanComplete()
        {
            return Disassembly.CanComplete();
        }

        public override void Interrupt()
        {
            Disassembly.Interrupt();
            base.Interrupt();
        }

        public override void Complete()
        {
            Disassembly.Complete();
            base.Complete();
        }

        public override void End()
        {
            Disassembly.End();
            base.End();
        }
    }
}
