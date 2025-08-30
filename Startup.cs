using System.Collections.Generic;

using Qud.UI;

using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;
using XRL.World.Tinkering;

using UD_Modding_Toolbox;

namespace UD_Tinkering_Bytes
{
    [PlayerMutator]
    public class LearnAllTheBytes : IPlayerMutator
    {
        public void mutate(GameObject player)
        {
            
            WorldCreationProgress.StepProgress("Converting bits to bytes...");

            Debug.Header(3, $"{nameof(LearnAllTheBytes)}", $"{nameof(mutate)}(GameObject player: {player.DebugName})");
            
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
            Debug.Footer(3, $"{nameof(LearnAllTheBytes)}", $"{nameof(mutate)}(GameObject player: {player.DebugName})");
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

            FilterBarCategoryButton.categoryImageMap.Add("Able To Tinker", "Items/sw_unfurled_scroll1.bmp");
            FilterBarCategoryButton.categoryImageMap.Add("Bytes", "4_byte_cleo_inverted.png");
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
        }
    }
}