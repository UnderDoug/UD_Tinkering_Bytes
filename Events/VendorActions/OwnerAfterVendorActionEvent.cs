using System;
using System.Collections.Generic;
using System.Text;

using XRL.World;

namespace UD_Tinkering_Bytes
{
    [GameEvent(Cascade = CASCADE_NONE, Cache = Cache.Pool)]
    public class OwnerAfterVendorActionEvent : IVendorActionEvent<OwnerAfterVendorActionEvent>
    {
        public OwnerAfterVendorActionEvent()
        {
        }

        public override void Reset()
        {
            base.Reset();
        }
    }
}
