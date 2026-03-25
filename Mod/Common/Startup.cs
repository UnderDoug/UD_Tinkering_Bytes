using System.Collections.Generic;
using System.Linq;
using System;

using Qud.UI;

using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;
using XRL.World.Parts.Skill;
using XRL.World.Tinkering;

using Version = XRL.Version;

using UD_Modding_Toolbox;
using UD_Vendor_Actions;

namespace UD_Tinkering_Bytes
{
    [HasModSensitiveStaticCache]
    [HasGameBasedStaticCache]
    [HasCallAfterGameLoaded]
    public static class Startup
    {
        private static string ThisModGameStatePrefix => Utils.ThisMod.ID + "::";

        public static bool SaveStartedWithVendorActions => UD_Vendor_Actions.Startup.SaveStartedWithVendorActions;
        public static bool SaveStartedWithTinkeringBytes
        {
            get => (The.Game?.GetBooleanGameState(ThisModGameStatePrefix + nameof(GameBasedCacheInit))).GetValueOrDefault();
            private set => The.Game?.SetBooleanGameState(ThisModGameStatePrefix + nameof(GameBasedCacheInit), value);
        }

        public static Version? LastModVersionSaved
        {
            get => The.Game?.GetObjectGameState(ThisModGameStatePrefix + nameof(Version)) as Version?;
            private set => The.Game?.SetObjectGameState(ThisModGameStatePrefix + nameof(Version), value);
        }

        public static bool LearnAllTheBytesGameState
        {
            get => (The.Game?.GetBooleanGameState(ThisModGameStatePrefix + nameof(LearnAllTheBytes))).GetValueOrDefault();
            set => The.Game?.SetBooleanGameState(ThisModGameStatePrefix + nameof(LearnAllTheBytes), value);
        }

        public static bool NeedVersionMismatchWarning => false;

        [GameBasedStaticCache]
        [ModSensitiveStaticCache]
        public static bool ModVersionWarningIssued = false;

        // Start-up calls in order that they happen.

        [ModSensitiveCacheInit]
        public static void ModSensitiveCacheInit()
        {
            // Called at game startup and whenever mod configuration changes
        }

        [GameBasedCacheInit]
        public static void GameBasedCacheInit()
        {
            // Called once when world is first generated.

            // The.Game registered events should go here.

            SaveStartedWithTinkeringBytes = true;
            LastModVersionSaved = Utils.ThisMod.Manifest.Version;

            Utils.ForceByteBitCost();
        }

        // [PlayerMutator]

        // The.Player.FireEvent("GameRestored");
        // AfterGameLoadedEvent.Send(Return);  // Return is the game.

        [CallAfterGameLoaded]
        public static void OnLoadGameCallback()
        {
            // Gets called every time the game is loaded but not during generation
            if (!SaveStartedWithVendorActions || !SaveStartedWithTinkeringBytes)
            {
                The.Player?.RequireSkill<UD_Basics>();
                if ((bool)!The.Game?.GetBooleanGameState(nameof(LearnAllTheBytes)))
                {
                    LearnAllTheBytes.AddByteBlueprints(The.Player);
                }
            }
            Utils.ForceByteBitCost();

            if (Options.EnableWarningsForBigJumpsInModVersion && Utils.ThisMod.Manifest.Version is Version newestVersion)
            {
                if (LastModVersionSaved is not Version savedVersion
                    || newestVersion.Minor > savedVersion.Minor
                    || newestVersion.Major > savedVersion.Major)
                {
                    if (NeedVersionMismatchWarning && !ModVersionWarningIssued)
                    {
                        ModManifest thisModManifest = Utils.ThisMod.Manifest;
                        savedVersion = LastModVersionSaved.GetValueOrDefault();
                        Popup.Show(thisModManifest.Title + " version mismatch:\n\n" +
                            "The version of " + thisModManifest.Title.Strip() + " used by this save is " +
                            "{{C|v" + savedVersion + "}} while the one currently enabled is {{C|v" + newestVersion + "}}." +
                            "\n\nSee this mod's {{C|\"Change Notes\"}} on the {{b|steam workshop}} for information on its backwards compatibility." +
                            "\n\nTo revert this save to its pre-migration state use {{hotkey|alt + F4}} to exit the game without saving " +
                            "(this should work in most circumstances)." +
                            "\n\nThere is an option to turn off these warnings.");
                        ModVersionWarningIssued = true;
                    }
                }
                else
                {
                    ModVersionWarningIssued = false;
                }
                LastModVersionSaved = Utils.ThisMod.Manifest.Version;
            }
        }
    }

    // [ModSensitiveCacheInit]

    // [GameBasedCacheInit]

    [PlayerMutator]
    public class UD_Tinkering_Bytes_OnPlayerLoad : IPlayerMutator
    {
        public void mutate(GameObject player)
        {
            // Gets called once when the player is first generated
        }
    }

    [PlayerMutator]
    public class LearnAllTheBytes : IPlayerMutator
    {
        public void mutate(GameObject player)
        {
            WorldCreationProgress.StepProgress("Converting bits to bytes...");

            Debug.Header(3, $"{nameof(LearnAllTheBytes)}", $"{nameof(mutate)}(GameObject player: {player.DebugName})");

            AddByteBlueprints(player);

            Debug.Footer(3, $"{nameof(LearnAllTheBytes)}", $"{nameof(mutate)}(GameObject player: {player.DebugName})");
        }

        public static void AddByteBlueprints(GameObject player)
        {
            if (player == null)
            {
                MetricsManager.LogModError(Utils.ThisMod,
                    $"{nameof(LearnAllTheBytes)}.{nameof(AddByteBlueprints)}:" +
                    $" supplied {nameof(player)} was null");
                return;
            }
            if (Startup.LearnAllTheBytesGameState)
            {
                return;
            }
            Debug.Entry(3, $"Spinning up data disks...", Indent: 1);
            List<string> byteBlueprints = new();
            List<GameObjectBlueprint> byteGameObjectBlueprints = new(UD_TinkeringByte.GetByteGameObjectBlueprints());
            if (!byteGameObjectBlueprints.IsNullOrEmpty())
            {
                foreach (GameObjectBlueprint byteBlueprint in byteGameObjectBlueprints)
                {
                    Debug.LoopItem(3, $"{byteBlueprint.DisplayName().Strip()}", Indent: 1);
                    TinkerData.LearnBlueprint(byteBlueprint.Name);
                }
            }
            Startup.LearnAllTheBytesGameState = true;
        }
    }

    // [CallAfterGameLoaded]
}