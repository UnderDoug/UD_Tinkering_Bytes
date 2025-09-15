using System;
using System.Collections.Generic;
using System.Text;
using XRL.Language;
using XRL.UI;
using XRL.World.Capabilities;
using XRL.World.Effects;
using XRL.World.Parts.Mutation;
using XRL.World.Tinkering;

using static XRL.World.Parts.Skill.Tinkering;


using UD_Vendor_Actions;
using UD_Modding_Toolbox;

using static UD_Modding_Toolbox.Const;

using UD_Tinkering_Bytes;
using static UD_Tinkering_Bytes.Options;

namespace XRL.World.Parts
{
    [Serializable]
    public class UD_VendorKnownRecipe 
        : IScribedPart
        , I_UD_VendorActionEventHandler
        , IModEventHandler<UD_EndTradeEvent>
    {
        private static bool doDebug => false;

        public const string COMMAND_IDENTIFY_BY_RECIPE = "CmdVendorExamineRecipe";

        public TinkerData Data;

        public bool FromImplant;

        [NonSerialized]
        public string ObjectName;

        [NonSerialized]
        private static StringBuilder SB = new();

        [NonSerialized]
        private static BitCost Cost = new();

        public UD_VendorKnownRecipe()
        {
            Data = null;
            FromImplant = false;
        }

        public override bool CanGenerateStacked()
        {
            return false;
        }

        public TinkerData SetData(TinkerData KnownRecipe)
        {
            if (EnableKnownRecipeCategoryMirroring)
            {
                if (KnownRecipe.Type == "Build"
                    && GameObject.CreateSample(KnownRecipe.Blueprint) is GameObject sampleObject
                    && ParentObject.TryGetPart(out Physics recipePhysics)
                    && sampleObject.TryGetPart(out Physics samplePhysics))
                {
                    recipePhysics.Category = samplePhysics?.Category ?? "Able To Tinker";
                }
                else
                if (KnownRecipe.Type == "Mod"
                    && ParentObject.TryGetPart(out recipePhysics))
                {
                    recipePhysics.Category = "Data Disks";
                }
            }
            return Data = KnownRecipe;
        }

        public override bool AllowStaticRegistration()
        {
            return true;
        }

        public override bool WantTurnTick()
        {
            return base.WantTurnTick();
        }
        public override void TurnTick(long TimeTick, int Amount)
        {
            if (GameObject.Validate(ParentObject))
            {
                ParentObject.Obliterate();
            }
            base.TurnTick(TimeTick, Amount);
        }

        public override bool WantEvent(int ID, int Cascade)
        {
            return base.WantEvent(ID, Cascade)
                || ID == GetDisplayNameEvent.ID
                || ID == GetShortDescriptionEvent.ID
                || ID == GetInventoryCategoryEvent.ID
                || ID == UD_GetVendorActionsEvent.ID
                || ID == UD_VendorActionEvent.ID
                || ID == UD_EndTradeEvent.ID;
        }
        public override bool HandleEvent(GetDisplayNameEvent E)
        {
            SB.Clear();
            if (Data == null)
            {
                ObjectName = "invalid blueprint: " + ParentObject.Blueprint;
            }
            else
            if (Data.Type == "Build")
            {
                try
                {
                    if (ObjectName == null)
                    {
                        if (Data.Blueprint == null)
                        {
                            ObjectName = "invalid blueprint: " + Data.Blueprint;
                        }
                        else
                        {
                            ObjectName = TinkeringHelpers.TinkeredItemShortDisplayName(Data.Blueprint);
                        }
                    }
                }
                catch (Exception)
                {
                    ObjectName = "error:" + Data.Blueprint;
                }
                bool isUnderstood = false;
                if (GameObject.CreateSample(Data.Blueprint) is GameObject sampleObject)
                {
                    TinkeringHelpers.StripForTinkering(sampleObject);
                    isUnderstood = sampleObject.Understood() || (The.Player != null && The.Player.HasSkill(nameof(Skill.Tinkering)));
                    ObjectName = sampleObject.GetDisplayName(Short: true, Single: true, AsIfKnown: isUnderstood);

                    if (GameObject.Validate(ref sampleObject))
                    {
                        sampleObject.Obliterate();
                    }
                }
                SB.Append(": ").AppendColored("C", ObjectName); 
                if (isUnderstood || (The.Player != null && The.Player.HasSkill(nameof(Skill.Tinkering))))
                {
                    SB.Append(" <");
                    Cost.Clear();
                    Cost.Import(TinkerItem.GetBitCostFor(Data.Blueprint));
                    ModifyBitCostEvent.Process(ParentObject.InInventory ?? The.Player, Cost, "DataDisk");
                    Cost.ToStringBuilder(SB);
                    SB.Append('>');
                }
            }
            else
            if (Data.Type == "Mod")
            {
                string itemMod = "Item mod".Color("W");
                string recipeDisplayName = Data.DisplayName.Color("C");
                ObjectName = $"[{itemMod}] - {recipeDisplayName}";
                SB.Append(": ").Append(ObjectName);
            }
            if (SB.Length > 0)
            {
                E.AddBase(SB.ToString(), 5);
            }
            return base.HandleEvent(E);
        }
        public override bool HandleEvent(GetShortDescriptionEvent E)
        {
            if (Data != null)
            {
                bool isUnderstood = false;
                bool isPlayerTinker = The.Player != null && The.Player.HasSkill(nameof(Skill.Tinkering));
                bool isBuildItemPlural = false;
                if (Data.Type == "Mod")
                {
                    string modDesc = ItemModding.GetModificationDescription(Data.Blueprint, 0);
                    E.Postfix.AppendLine().Append("Adds item modification: ").Append(modDesc);
                }
                else
                {
                    if (GameObject.CreateSample(Data.Blueprint) is GameObject sampleObject 
                        && (sampleObject.Understood() || isPlayerTinker))
                    {
                        isUnderstood = sampleObject.Understood();
                        isBuildItemPlural = sampleObject.IsPlural;
                        TinkeringHelpers.StripForTinkering(sampleObject);
                        TinkerItem tinkerItem = sampleObject.GetPart<TinkerItem>();
                        Description description = sampleObject.GetPart<Description>();
                        E.Postfix.AppendRules("Creates: ");
                        if (tinkerItem != null && tinkerItem.NumberMade > 1)
                        {
                            isBuildItemPlural = true; 
                            E.Postfix.Append(Grammar.Cardinal(tinkerItem.NumberMade)).Append(' ');
                            E.Postfix.Append(Grammar.Pluralize(ObjectName ?? sampleObject.DisplayNameOnly));
                        }
                        else
                        {
                            E.Postfix.Append(ObjectName ?? sampleObject.DisplayNameOnly);
                        }
                        E.Postfix.AppendLine();
                        if (description != null)
                        {
                            E.Postfix.AppendLine().Append(description._Short);
                        }
                        sampleObject.Obliterate();
                    }
                }
                if (Data.Type == "Mod" || isUnderstood || isPlayerTinker)
                {
                    E.Postfix.AppendLine().AppendRules("Requires: ").Append(DataDisk.GetRequiredSkillHumanReadable(Data.Tier));
                    if (FromImplant)
                    {
                        E.Postfix.Append(" [").AppendColored("c", "implanted recipe").Append("]");
                    }
                    if (TinkerData.RecipeKnown(Data))
                    {
                        E.Postfix.AppendLine().AppendRules("You also know this recipe.");
                    }
                }
                if (Data.Type == "Build" && isPlayerTinker && !isUnderstood)
                {
                    string thisThese = !isBuildItemPlural ? "this" : "these";
                    string isAre = !isBuildItemPlural ? "is" : "are";
                    string itThem = !isBuildItemPlural ? "it" : "them";
                    E.Postfix.AppendLine().AppendRules(
                        $"You know approximately what {thisThese} {isAre} " +
                        $"but you do not {"understand".Color("y")} {itThem}.");
                }
            }
            return base.HandleEvent(E);
        }
        public override bool HandleEvent(GetInventoryCategoryEvent E)
        {
            if (EnableKnownRecipeCategoryMirroring
                && Data.Type == "Build"
                && GameObject.CreateSample(Data?.Blueprint) is GameObject sampleObject
                && sampleObject.TryGetPart(out Examiner sampleExaminer)
                && !sampleObject.Understood(sampleExaminer) && !E.AsIfKnown)
            {
                E.Category = "Artifacts";
            }
            return base.HandleEvent(E);
        }
        public virtual bool HandleEvent(UD_GetVendorActionsEvent E)
        {
            if (E.Item != null && E.Item == ParentObject)
            {
                if (Data?.Type == "Mod")
                {
                    E.AddAction("Mod From Recipe", "mod an item with tinkering", UD_VendorTinkering.COMMAND_MOD, "tinkering", Key: 'T', Priority: -4, DramsCost: 100, ClearAndSetUpTradeUI: true);
                }
                else
                if (Data?.Type == "Build" 
                    && GameObject.CreateSample(Data.Blueprint) is GameObject sampleObject)
                {
                    if (sampleObject.Understood() || (The.Player != null && The.Player.HasSkill(nameof(Skill.Tinkering))))
                    {
                        E.AddAction(
                            Name: "Build From Recipe", 
                            Display: "tinker item", 
                            Command: UD_VendorTinkering.COMMAND_BUILD, 
                            PreferToHighlight: "tinker", 
                            Key: 'T', 
                            Priority: -4, 
                            DramsCost: 100, 
                            ClearAndSetUpTradeUI: true);
                    }
                    if (!sampleObject.Understood() && GetIdentifyLevel(E.Vendor) > 0)
                    {
                        E.AddAction(
                            Name: "Identify Recipe",
                            Display: "identify recipe",
                            Command: COMMAND_IDENTIFY_BY_RECIPE,
                            Key: 'i',
                            Priority: 8,
                            ClearAndSetUpTradeUI: true,
                            FireOnItem: true);
                    }
                    if (GameObject.Validate(ref sampleObject))
                    {
                        sampleObject.Obliterate();
                    }
                }
            }
            return base.HandleEvent(E);
        }
        public virtual bool HandleEvent(UD_VendorActionEvent E)
        {
            if (E.Command == COMMAND_IDENTIFY_BY_RECIPE 
                && E.Item != null && E.Item == ParentObject 
                && E.Vendor != null)
            {
                GameObject vendor = E.Vendor;
                GameObject player = The.Player;
                int identifyLevel = GetIdentifyLevel(vendor);
                if (identifyLevel > 0 
                    && Data.Type == "Build"
                    && GameObject.CreateSample(Data.Blueprint) is GameObject item)
                {
                    try
                    {
                        if (!item.Understood())
                        {
                            int complexity = item.GetComplexity();
                            int examineDifficulty = item.GetExamineDifficulty();
                            if (player.HasPart<Dystechnia>())
                            {
                                Popup.ShowFail($"You can't understand {Grammar.MakePossessive(vendor.t(Stripped: true))} explanation.");
                                return false;
                            }
                            if (identifyLevel < complexity)
                            {
                                Popup.ShowFail($"This tinker recipe is too complex for {vendor.t(Stripped: true)} to explain.");
                                return false;
                            }
                            int dramsCost = vendor.IsPlayerLed() ? 0 : (int)TinkerInvoice.GetExamineCost(item, GetTradePerformanceEvent.GetFor(player, vendor));
                            if (dramsCost > 0 && player.GetFreeDrams() < dramsCost)
                            {
                                Popup.ShowFail(
                                    $"You do not have the required {dramsCost.Things("dram").Color("C")} " +
                                    $"to have {vendor.t(Stripped: true)} identify this tinker recipe.");
                            }
                            else
                            if (Popup.ShowYesNo(
                                $"You may have {vendor.t(Stripped: true)} identify this tinker recipe for " +
                                $"{dramsCost.Things("dram").Color("C")} of fresh water.") == DialogResult.Yes)
                            {
                                player.UseDrams(dramsCost);
                                vendor.GiveDrams(dramsCost);
                                Popup.Show(
                                    $"{vendor.does("identify", Stripped: true)} {item.the} {item.GetDisplayName()}" +
                                    $" as {item.an(AsIfKnown: true)}.");
                                item.MakeUnderstood();
                                return true;
                            }
                        }
                        else
                        {
                            Popup.ShowFail("You already understand what this tinker recipe creates.");
                            return false;
                        }
                    }
                    finally
                    {
                        if (GameObject.Validate(ref item))
                        {
                            item.Obliterate();
                        }
                    }
                }
                else
                {
                    Popup.Show($"{vendor.Does("don't", Stripped: true)} have the skill to identify tinker recipes.");
                    return false;
                }
            }
            return base.HandleEvent(E);
        }
        public virtual bool HandleEvent(UD_EndTradeEvent E)
        {
            if (GameObject.Validate(ParentObject))
            {
                ParentObject.Obliterate();
            }
            return base.HandleEvent(E);
        }
    }
}
