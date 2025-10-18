using System;
using System.Text;

using XRL.UI;
using XRL.Language;
using XRL.World.Parts.Mutation;
using XRL.World.Tinkering;

using static XRL.World.Parts.Skill.Tinkering;
using static XRL.World.Parts.UD_VendorTinkering;

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
                GameObject sampleObject = null;
                if (KnownRecipe.Type == "Build"
                    && ParentObject.TryGetPart(out Physics recipePhysics))
                {
                    sampleObject = TinkerInvoice.CreateTinkerSample(KnownRecipe.Blueprint);
                    if (sampleObject.TryGetPart(out Physics samplePhysics)
                        && samplePhysics?.Category is string sampleCategory)
                    {
                        recipePhysics.Category = sampleCategory;
                    }
                }
                else
                if (KnownRecipe.Type == "Mod"
                    && ParentObject.TryGetPart(out recipePhysics))
                {
                    recipePhysics.Category = "Data Disks";
                }
                TinkerInvoice.ScrapTinkerSample(ref sampleObject);
            }
            return Data = KnownRecipe;
        }

        public override bool AllowStaticRegistration()
        {
            return true;
        }

        public override bool WantEvent(int ID, int Cascade)
        {
            return base.WantEvent(ID, Cascade)
                || ID == GetDisplayNameEvent.ID
                || ID == GetShortDescriptionEvent.ID
                || ID == GetInventoryCategoryEvent.ID
                || ID == UD_GetVendorActionsEvent.ID
                || ID == UD_VendorActionEvent.ID;
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
                    E.AddAction(
                        Name: "Mod Recipe",
                        Display: "mod an item with tinkering",
                        Command: COMMAND_MOD,
                        PreferToHighlight: "tinkering",
                        Key: 'T',
                        Priority: -4,
                        DramsCost: 100,
                        ClearAndSetUpTradeUI: true);
                }
                else
                if (Data?.Type == "Build" 
                    && TinkerInvoice.CreateTinkerSample(Data.Blueprint) is GameObject sampleObject)
                {
                    if (sampleObject.Understood() || (The.Player != null && The.Player.HasSkill(nameof(Skill.Tinkering))))
                    {
                        E.AddAction(
                            Name: "Build Recipe",
                            Display: "tinker item",
                            Command: COMMAND_BUILD,
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
                    TinkerInvoice.ScrapTinkerSample(ref sampleObject);
                }
            }
            return base.HandleEvent(E);
        }
        public virtual bool HandleEvent(UD_VendorActionEvent E)
        {
            if (E.Command == COMMAND_IDENTIFY_BY_RECIPE
                && E.Item is GameObject knownRecipeObject
                && E.Vendor is GameObject vendor
                && The.Player is GameObject player
                && Data is TinkerData recipeDatum
                && recipeDatum.Type == "Build")
            {
                int identifyLevel = GetIdentifyLevel(vendor);
                if (identifyLevel > 0
                    && TinkerInvoice.CreateTinkerSample(recipeDatum.Blueprint) is GameObject sampleItem)
                {
                    TinkerInvoice tinkerInvoice = null;
                    try
                    {
                        if (!sampleItem.Understood())
                        {
                            int complexity = sampleItem.GetComplexity();
                            int examineDifficulty = sampleItem.GetExamineDifficulty();
                            if (player.HasPart<Dystechnia>())
                            {
                                string dystechniaFailMsg = "=subject.Name= can't understand =object.name's= explanation."
                                    .StartReplace()
                                    .AddObject(player)
                                    .AddObject(vendor)
                                    .ToString();

                                Popup.ShowFail(dystechniaFailMsg);
                                return false;
                            }
                            if (identifyLevel < complexity)
                            {
                                string tooComplexForTinkerFailMsg = "This tinker recipe is too complex for =subject.name= to explain."
                                    .StartReplace()
                                    .AddObject(vendor)
                                    .ToString();

                                Popup.ShowFail(tooComplexForTinkerFailMsg);
                                return false;
                            }
                            tinkerInvoice = new(Vendor: vendor, Service: TinkerInvoice.IDENTIFY, Item: sampleItem);
                            int dramsCost = vendor.IsPlayerLed() ? 0 : (int)tinkerInvoice.GetExamineCost();
                            if (!IsTooExpensive(
                                Vendor: vendor,
                                Shopper: player,
                                DramsCost: dramsCost,
                                ToDoWhat: "identify this tinker recipe.")
                                && ConfirmTinkerService(
                                    Vendor: vendor,
                                    Shopper: player,
                                    DramsCost: dramsCost,
                                    DoWhat: "identify this tinker recipe"))
                            {
                                player.UseDrams(dramsCost);
                                vendor.GiveDrams(dramsCost);

                                // this is necessary until lang hits main, =object.a= respects examiner obfuscation
                                string anIdentifiedItem = Grammar.A(sampleItem.GetReferenceDisplayName(WithoutTitles: true, Short: true));

                                string identifiedMsg =
                                    ("=subject.Name= =verb:identify:afterpronoun= this tinker recipe " +
                                    "as " + anIdentifiedItem + ".")
                                        .StartReplace()
                                        .AddObject(vendor)
                                        .AddObject(sampleItem)
                                        .ToString();

                                Popup.Show(identifiedMsg);

                                sampleItem.MakeUnderstood();

                                return true;
                            }
                            return false;
                        }
                        else
                        {
                            string alreadyKnowItemMsg = "=subject.Name= already =verb:understand:afterpronoun= what this tinker recipe creates."
                                .StartReplace()
                                .AddObject(player)
                                .ToString();

                            Popup.ShowFail(alreadyKnowItemMsg);
                            return false;
                        }
                    }
                    finally
                    {
                        tinkerInvoice?.Clear();
                        TinkerInvoice.ScrapTinkerSample(ref sampleItem);
                    }
                }
                else
                {
                    string tinkerUnableIdentifyMsg =
                        ("=subject.Name= =verb:don't:afterpronoun= have the skill " +
                        "to identify tinkering recipes.")
                        .StartReplace()
                        .AddObject(vendor)
                        .ToString();

                    Popup.Show(tinkerUnableIdentifyMsg);
                    return false;
                }
            }
            return base.HandleEvent(E);
        }
    }
}
