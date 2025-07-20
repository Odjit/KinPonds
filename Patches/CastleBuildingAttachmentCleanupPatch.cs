using HarmonyLib;
using ProjectM;
using ProjectM.CastleBuilding;
using ProjectM.Network;
using ProjectM.Shared;
using Stunlock.Core;
using Unity.Collections;

namespace KinPonds.Patches;

[HarmonyPatch(typeof(CastleBuildingAttachmentCleanup), nameof(CastleBuildingAttachmentCleanup.OnUpdate))]
static class CastleBuildingAttachmentCleanupPatch
{
    static void Prefix(CastleBuildingAttachmentCleanup __instance)
    {
        if (Core.Ponds == null) return;

        var destroyingEntities = __instance.__query_475332371_0.ToEntityArray(Allocator.Temp);
        foreach (var entity in destroyingEntities)
        {
            var prefabGuid = entity.Read<PrefabGUID>();
            if (prefabGuid != Helper.TM_Castle_ObjectDecor_Pool_StrongbladeDLC01 &&
                prefabGuid != Helper.TM_Castle_ObjectDecor_Pool_StrongbladeDLC02)
                continue;
            if (!entity.Has<NameableInteractable>()) continue;
            if (entity.Read<NameableInteractable>().Name.Value != MyPluginInfo.PLUGIN_NAME) continue;

            var attachedBuffer = Core.EntityManager.GetBuffer<AttachedBuffer>(entity);
            foreach (var attached in attachedBuffer)
            {
                if (attached.PrefabGuid != Helper.Char_Fish_General) continue;
                DestroyUtility.Destroy(Core.EntityManager, attached.Entity);
            }
            entity.Remove<NameableInteractable>();
            var networkId = entity.Read<NetworkId>();
            Core.Ponds.RemovePond(networkId);
        }
    }
}
