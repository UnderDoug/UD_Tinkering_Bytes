using System;
using System.Collections.Generic;
using System.Text;
using XRL.World;

namespace UD_Tinkering_Bytes
{
    public interface IVendorActionEventHandler
        : IModEventHandler<GetVendorActionsEvent>
        , IModEventHandler<VendorActionEvent>
        , IModEventHandler<AfterVendorActionEvent>
        , IModEventHandler<OwnerAfterVendorActionEvent>
    {
    }
}
