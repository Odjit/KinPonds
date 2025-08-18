using ProjectM;
using Stunlock.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

namespace KinPonds;
class Helper
{
    internal static PrefabGUID TM_Castle_ObjectDecor_Pool_StrongbladeDLC01 = new PrefabGUID(558668025);
    internal static PrefabGUID TM_Castle_ObjectDecor_Pool_StrongbladeDLC02 = new PrefabGUID(-1963794511);
    internal static PrefabGUID Char_Fish_General = new PrefabGUID(1559481073);
    internal static PrefabGUID TM_LiquidStation_Water_Well01 = new PrefabGUID(986517450);
    internal static PrefabGUID TM_LiquidStation_Water_Well03 = new PrefabGUID(1742891933);

    public static Entity FindClosestPool(Vector3 pos)
    {
        var generateCastle = Core.GenerateCastle;
        var spatialData = generateCastle._TileModelLookupSystemData;
        var tileModelSpatialLookupRO = spatialData.GetSpatialLookupReadOnlyAndComplete(generateCastle);

        var gridPos = ConvertPosToTileGrid(pos);
        var bounds = new BoundsMinMax((int)(gridPos.x - 2.5), (int)(gridPos.z - 2.5),
                                      (int)(gridPos.x + 2.5), (int)(gridPos.z + 2.5));

        var closestEntity = Entity.Null;
        var closestDistance = float.MaxValue;
        var entities = tileModelSpatialLookupRO.GetEntities(ref bounds, TileType.All);
        for (var i = 0; i < entities.Length; ++i)
        {
            var entity = entities[i];
            if (!entity.Has<TilePosition>()) continue;
            if (!entity.Has<Translation>()) continue;
            var prefabGuid = entity.Read<PrefabGUID>();
            if (prefabGuid != TM_Castle_ObjectDecor_Pool_StrongbladeDLC01 &&
                prefabGuid != TM_Castle_ObjectDecor_Pool_StrongbladeDLC02 &&
                prefabGuid != TM_LiquidStation_Water_Well01 &&
                prefabGuid != TM_LiquidStation_Water_Well03) 
            {
                continue;
            }

            var entityPos = entity.Read<Translation>().Value;
            var distance = math.distancesq(pos, entityPos);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestEntity = entity;
            }
        }
        entities.Dispose();

        return closestEntity;
    }

    public static float3 ConvertPosToTileGrid(float3 pos)
    {
        return new float3(Mathf.FloorToInt(pos.x * 2) + 6400, pos.y, Mathf.FloorToInt(pos.z * 2) + 6400);
    }

    public static Entity SpawnEntity(Entity userEntity, Vector3 translation, Vector3 pos, Quaternion rot, Entity prefab)
    {
        var entity = Core.EntityManager.Instantiate(prefab);

        entity.Add<PhysicsCustomTags>();

        if (!entity.Has<Translation>())
            entity.Add<Translation>();
        entity.Write(new Translation { Value = pos + translation });
        if (entity.Has<LastTranslation>())
            entity.Write(new LastTranslation { Value = pos + translation });

        if (!entity.Has<Rotation>())
            entity.Add<Rotation>();
        entity.Write(new Rotation { Value = rot });

        return entity;
    }
}
