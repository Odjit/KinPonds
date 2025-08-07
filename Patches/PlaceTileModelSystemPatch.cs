using ProjectM;
using ProjectM.Shared;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace KinPonds.Patches;
[HarmonyLib.HarmonyPatch(typeof(PlaceTileModelSystem), nameof(PlaceTileModelSystem.OnUpdate))]
static class PlaceTileModelSystemPatch
{
    static Dictionary<Entity, float3> offsets = [];

    static void Prefix(PlaceTileModelSystem __instance)
    {
        if (Core.Ponds == null) return;

        var moveEvents = __instance._MoveTileQuery.ToEntityArray(Allocator.Temp);
        foreach (var moveEvent in moveEvents)
        {
            var mtme = moveEvent.Read<MoveTileModelEvent>();

            var pondEntity = Core.Ponds.GetPond(mtme.Target);
            if (pondEntity == Entity.Null) continue;

            var poolPos = pondEntity.Read<Translation>().Value;

            var attachedBuffer = Core.EntityManager.GetBuffer<AttachedBuffer>(pondEntity);
            foreach (var attached in attachedBuffer)
            {
                if (attached.PrefabGuid != Helper.Char_Fish_General) continue;

                var fishEntity = attached.Entity;
                var translation = fishEntity.Read<Translation>();
                offsets[fishEntity] = translation.Value - poolPos;
            }
        }
        moveEvents.Dispose();

        var dismantleEvents = __instance._DismantleTileQuery.ToEntityArray(Allocator.Temp);
        foreach (var dismantleEvent in dismantleEvents)
        {
            var dtme = dismantleEvent.Read<DismantleTileModelEvent>();

            var pondEntity = Core.Ponds.GetPond(dtme.Target);
            if (pondEntity == Entity.Null) continue;
            if (!pondEntity.Has<NameableInteractable>()) continue;
            if (pondEntity.Read<NameableInteractable>().Name.Value != MyPluginInfo.PLUGIN_NAME) continue;

            var attachedBuffer = Core.EntityManager.GetBuffer<AttachedBuffer>(pondEntity);
            foreach (var attached in attachedBuffer)
            {
                if (attached.PrefabGuid != Helper.Char_Fish_General) continue;
                DestroyUtility.Destroy(Core.EntityManager, attached.Entity);
            }

            pondEntity.Remove<NameableInteractable>();
            Core.Ponds.RemovePond(dtme.Target);
        }
        dismantleEvents.Dispose();
    }

    static void Postfix(PlaceTileModelSystem __instance)
    {
        if (Core.Ponds == null) return;
        foreach (var (fishEntity, offset) in offsets)
        {
            var attached = fishEntity.Read<Attached>();
            var parentPos = attached.Parent.Read<Translation>().Value;
            fishEntity.Write(new Translation
            {
                Value = parentPos + offset
            });
        }
        offsets.Clear();
    }

}
