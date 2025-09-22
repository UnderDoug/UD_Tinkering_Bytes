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

        public override void Attach()
        {
            base.Attach();
            /*
            if (!ParentObject.IsPlayer() 
                && ParentObject.RequirePart<UD_VendorTinkering>() is UD_VendorTinkering uD_VendorTinkering)
            {
                UD_VendorTinkering.LearnByteRecipes(ParentObject, uD_VendorTinkering.KnownRecipes);
            }
            else
            if (ParentObject.IsPlayer()
                && (bool)!The.Game?.GetBooleanGameState(nameof(LearnAllTheBytes)))
            {
                LearnAllTheBytes.AddByteBlueprints(ParentObject);
            }
            */
        }
    }
}
