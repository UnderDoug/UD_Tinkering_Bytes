using System;
using System.Collections.Generic;
using System.Text;
using XRL.World.Tinkering;

namespace UD_Tinkering_Bytes
{
    public static class Extensions
    {

        public static void ProcessVendorDisassemblyWhat(this Disassembly Disassembly)
        {
            if (!Disassembly.DisassemblingWhat.IsNullOrEmpty())
            {
                string disassemblingWhat = Disassembly.DisassemblingWhat;
                if (Disassembly.NumberDone > 1 || Disassembly.NumberWanted > 1)
                {
                    disassemblingWhat = disassemblingWhat + " x" + Disassembly.NumberDone;
                }
                if (Disassembly.DisassembledWhat == null)
                {
                    Disassembly.DisassembledWhat = disassemblingWhat;
                    Disassembly.DisassembledWhere = Disassembly.DisassemblingWhere;
                }
                else
                {
                    if (Disassembly.DisassembledWhats.IsNullOrEmpty())
                    {
                        if (Disassembly.DisassembledWhats == null)
                        {
                            Disassembly.DisassembledWhats = new List<string>();
                        }
                        if (Disassembly.DisassembledWhatsWhere == null)
                        {
                            Disassembly.DisassembledWhatsWhere = new Dictionary<string, string>();
                        }
                        Disassembly.DisassembledWhats.Add(Disassembly.DisassembledWhat);
                        Disassembly.DisassembledWhatsWhere[Disassembly.DisassembledWhat] = Disassembly.DisassembledWhere;
                    }
                    Disassembly.DisassembledWhats.Add(disassemblingWhat);
                    Disassembly.DisassembledWhatsWhere[disassemblingWhat] = Disassembly.DisassemblingWhere;
                }
                Disassembly.DisassemblingWhat = null;
                Disassembly.DisassemblingWhere = null;
            }
        }
    }
}
