using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.Messages;
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

        public int DramsCostPer;

        public int EnergyCostPer;

        public UD_SyncedDisassembly(Disassembly Disassembly, GameObject Disassembler, List<TinkerData> KnownRecipes, int DramsCostPer = 0, int EnergyCostPer = 1000)
        {
            this.Disassembly = Disassembly;
            this.Disassembler = Disassembler;
            this.KnownRecipes = KnownRecipes;
            this.DramsCostPer = DramsCostPer;
            this.EnergyCostPer = EnergyCostPer;
            this.Disassembly.EnergyCostPer = 0;
        }

        public override string GetDescription()
        {
            return "waiting for disassembling";
        }

        public override bool ShouldHostilesInterrupt()
        {
            return true;
        }

        public override bool Continue()
        {
            Disassembler.Brain.Goals.Clear();
            bool vendorDisassemblyContinue = UD_VendorDisassembly.VendorDisassemblyContinue(Disassembler, Disassembly, this, ref KnownRecipes);
            if (vendorDisassemblyContinue)
            {
                if (GameObject.Validate(ref Disassembler))
                {
                    The.Player?.UseDrams(DramsCostPer);
                    Disassembler?.GiveDrams(DramsCostPer);

                    Disassembler?.UseEnergy(EnergyCostPer, "Skill Tinkering Disassemble");
                }
                The.Player.UseEnergy(EnergyCostPer, "Vendor Tinkering Disassemble");
            }
            return vendorDisassemblyContinue;
        }

        public override bool CanComplete()
        {
            return Disassembly.CanComplete();
        }

        public override void Interrupt()
        {
            Disassembly.Interrupt();
            MessageQueue.AddPlayerMessage(Event.NewStringBuilder()
                .Append(Disassembler.T())
                .Append(Disassembler.GetVerb("stop"))
                .Append(" ")
                .Append(Disassembly.GetDescription())
                .Append(" because ")
                .Append(Disassembly.GetInterruptBecause())
                .Append(".")
                .ToString());
            Loading.SetLoadingStatus($"Interrupted!");
            base.Interrupt();

            if (Disassembler.TryGetPart(out UD_VendorDisassembly vendorDisassembly))
            {
                vendorDisassembly.ResetDisassembly();
            }
        }

        public override void Complete()
        {
            Disassembly.Complete();
            base.Complete();
        }

        public override void End()
        {
            UD_VendorDisassembly.VendorDisassemblyEnd(Disassembler, Disassembly);
            if (Disassembler.TryGetPart(out UD_VendorDisassembly vendorDisassembly))
            {
                vendorDisassembly.ResetDisassembly();
            }
            base.End();
        }
    }
}
