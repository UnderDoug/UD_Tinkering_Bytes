using System;
using System.Collections.Generic;
using System.Text;

using XRL.Language;
using XRL.UI;
using XRL.World.Capabilities;
using XRL.World.Effects;
using XRL.World.Parts.Mutation;
using XRL.World.Skills;
using XRL.World.Parts.Skill;
using XRL.World.Tinkering;

using static XRL.World.Parts.Skill.Tinkering;

using UD_Vendor_Actions;
using UD_Modding_Toolbox;
using static UD_Modding_Toolbox.Const;

using UD_Tinkering_Bytes;

namespace XRL.World.Parts
{
    [Serializable]
    public class UD_DataDisk : IPart
    {
        public TinkerData Data;

        public override bool WantEvent(int ID, int cascade)
        {
            return !base.WantEvent(ID, cascade)
                || ID == GetShortDescriptionEvent.ID;
        }

        public override bool HandleEvent(GetShortDescriptionEvent E)
        {
            if (Data != null)
            {
                if (Data.Type == "Mod")
                {
                    E.Postfix.Append("\nAdds item modification: ").Append(ItemModding.GetModificationDescription(Data.Blueprint, 0));
                }
                else
                {
                    GameObject gameObject = GameObject.CreateSample(Data.Blueprint);
                    if (gameObject != null 
                        && gameObject.Understood() 
                        && The.Player != null 
                        && (The.Player.HasSkill("Tinkering") || Scanning.HasScanningFor(The.Player, Scanning.Scan.Tech)))
                    {
                        TinkeringHelpers.StripForTinkering(gameObject);
                        TinkerItem part = gameObject.GetPart<TinkerItem>();
                        Description part2 = gameObject.GetPart<Description>();
                        E.Postfix.Append("\n{{rules|Creates:}} ");
                        if (part != null && part.NumberMade > 1)
                        {
                            E.Postfix.Append(Grammar.Cardinal(part.NumberMade)).Append(' ').Append(Grammar.Pluralize(gameObject.DisplayNameOnlyDirect));
                        }
                        else
                        {
                            E.Postfix.Append(gameObject.DisplayNameOnlyDirect);
                        }
                        E.Postfix.Append("\n");
                        if (part2 != null)
                        {
                            E.Postfix.Append('\n').Append(part2._Short);
                        }
                        E.Postfix.Append("\n\n{{rules|Requires:}} ").Append(GetRequiredSkillHumanReadable());
                        if (TinkerData.RecipeKnown(Data))
                        {
                            E.Postfix.Append("\n\n{{rules|You already know this recipe.}}");
                        }
                        gameObject.Obliterate();
                    }
                }
                if (Data.Type == "Mod")
                {
                    E.Postfix.Append("\n\n{{rules|Requires:}} ").Append(GetRequiredSkillHumanReadable());
                    if (TinkerData.RecipeKnown(Data))
                    {
                        E.Postfix.Append("\n\n{{rules|You already know this recipe.}}");
                    }
                }
            }
            return base.HandleEvent(E);
        }

        public static string GetRequiredSkill(int Tier)
        {
            if (Tier <= 3)
            {
                return "Tinkering_Tinker1";
            }
            if (Tier <= 6)
            {
                return "Tinkering_Tinker2";
            }
            return "Tinkering_Tinker3";
        }

        public string GetRequiredSkill()
        {
            return GetRequiredSkill(Data?.Tier ?? 0);
        }

        public static string GetRequiredSkillHumanReadable(int Tier)
        {
            if (Tier <= 3)
            {
                return "Tinker I";
            }
            if (Tier <= 6)
            {
                return "Tinker II";
            }
            return "Tinker III";
        }

        public string GetRequiredSkillHumanReadable()
        {
            return GetRequiredSkillHumanReadable(Data?.Tier ?? 0);
        }
    }
}
