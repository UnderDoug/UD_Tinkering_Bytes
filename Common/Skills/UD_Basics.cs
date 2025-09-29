using System;
using UD_Tinkering_Bytes;
using UD_Vendor_Actions;

namespace XRL.World.Parts.Skill
{
    [AlwaysHandlesVendor_UD_VendorActions]
    [Serializable]
    public class UD_Basics : BaseSkill
    {
        // Given to literally everything so that the patch to DataDisk.GetRequiredSkill can return a non-null value when Tier is 0
        public UD_Basics()
        {
        }
    }
}
