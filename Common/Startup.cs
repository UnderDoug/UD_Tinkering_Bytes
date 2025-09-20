using System.Collections.Generic;

using Qud.UI;

using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;
using XRL.World.Tinkering;

using UD_Modding_Toolbox;
using UD_Vendor_Actions;
using XRL.World.Parts.Skill;

namespace UD_Tinkering_Bytes
{
    public static class Startup
    {
        public static bool SaveStartedWithVendorActions => UD_Vendor_Actions.Startup.SaveStartedWithVendorActions;
        public static bool SaveStartedWithTinkeringBytes => (bool)The.Game?.GetBooleanGameState(nameof(UD_Tinkering_Bytes_GameBasedInitialiser));
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
            if ((bool)The.Game?.GetBooleanGameState(nameof(LearnAllTheBytes)))
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
            The.Game?.SetBooleanGameState(nameof(LearnAllTheBytes), true);
        }
    }

    // Start-up calls in order that they happen.

    [HasModSensitiveStaticCache]
    public static class UD_Tinkering_Bytes_ModBasedInitialiser
    {
        [ModSensitiveCacheInit]
        public static void AdditionalSetup()
        {
            // Called at game startup and whenever mod configuration changes
        }
    }

    [HasGameBasedStaticCache]
    public static class UD_Tinkering_Bytes_GameBasedInitialiser
    {
        [GameBasedCacheInit]
        public static void AdditionalSetup()
        {
            // Called once when world is first generated.

            // The.Game registered events should go here.
        }
    }

    [PlayerMutator]
    public class UD_Tinkering_Bytes_OnPlayerLoad : IPlayerMutator
    {
        public void mutate(GameObject player)
        {
            // Gets called once when the player is first generated
        }
    }

    [HasCallAfterGameLoaded]
    public class UD_Tinkering_Bytes_OnLoadGameHandler
    {
        [CallAfterGameLoaded]
        public static void OnLoadGameCallback()
        {
            // Gets called every time the game is loaded but not during generation
            if (!Startup.SaveStartedWithVendorActions || !Startup.SaveStartedWithTinkeringBytes)
            {
                The.Player?.RequireSkill<UD_Basics>();
                if ((bool)!The.Game?.GetBooleanGameState(nameof(LearnAllTheBytes)))
                {
                    LearnAllTheBytes.AddByteBlueprints(The.Player);
                }
            }
        }
    }
}