using System;
using System.Collections.Generic;
using System.Text;

using XRL.World;

namespace UD_Tinkering_Bytes
{
    [GameEvent(Cascade = CASCADE_NONE, Cache = Cache.Pool)]
    public class AfterVendorActionEvent : IVendorActionEvent<AfterVendorActionEvent>
    {
        public AfterVendorActionEvent()
        {
        }

        public override void Reset()
        {
            base.Reset();
        }
    }
}
