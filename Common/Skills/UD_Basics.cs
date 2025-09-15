using System;
using System.Collections.Generic;
using System.Text;

namespace XRL.World.Parts.Skill
{
    [Serializable]
    public class UD_Basics : BaseSkill
    {
        // Given to literally everything so that the patch to DataDisk.GetRequiredSkill can return a non-null value when Tier is 0
        public UD_Basics()
        {
        }
    }
}
