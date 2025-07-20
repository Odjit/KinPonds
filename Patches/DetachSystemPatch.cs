using ProjectM;
using Stunlock.Core;
using Unity.Collections;

namespace KinPonds.Patches;

[HarmonyLib.HarmonyPatch(typeof(DetachSystem), nameof(DetachSystem.OnUpdate))]
static class DetachSystemPatch
{
    static void Prefix(DetachSystem __instance)
    {
        var detachingEntities = __instance.__query_1229206336_1.ToEntityArray(Allocator.TempJob);
        foreach (var entity in detachingEntities)
        {
            if (!entity.Has<PrefabGUID>()) continue;

            var prefabGuid = entity.Read<PrefabGUID>();
            if (prefabGuid != Helper.Char_Fish_General) continue;

            Core.Ponds.CheckForRespawn(entity);
        }
        detachingEntities.Dispose();
    }
}
