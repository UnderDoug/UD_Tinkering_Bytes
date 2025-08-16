using System.Collections.Generic;

using XRL;
using XRL.UI;
using XRL.World;
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
            List<GameObjectBlueprint> byteGameObjectBlueprints = GameObjectFactory.Factory.GetBlueprintsInheritingFrom("BaseByte");
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
    public static class UD_Blink_Mutation_ModBasedInitialiser
    {
        [ModSensitiveCacheInit]
        public static void AdditionalSetup()
        {
            // Called at game startup and whenever mod configuration changes
        }
    } //!-- public static class UD_Blink_Mutation_ModBasedInitialiser

    [HasGameBasedStaticCache]
    public static class UD_Blink_Mutation_GameBasedInitialiser
    {
        [GameBasedCacheInit]
        public static void AdditionalSetup()
        {
            // Called once when world is first generated.

            // The.Game registered events should go here.
        }
    } //!-- public static class UD_Blink_Mutation_GameBasedInitialiser

    [PlayerMutator]
    public class UD_Blink_Mutation_OnPlayerLoad : IPlayerMutator
    {
        public void mutate(GameObject player)
        {
            // Gets called once when the player is first generated
        }
    } //!-- public class UD_Blink_Mutation_OnPlayerLoad : IPlayerMutator

    [HasCallAfterGameLoaded]
    public class UD_Blink_Mutation_OnLoadGameHandler
    {
        [CallAfterGameLoaded]
        public static void OnLoadGameCallback()
        {
            // Gets called every time the game is loaded but not during generation
        }
    } //!-- public class UD_Blink_Mutation_OnLoadGameHandler
}