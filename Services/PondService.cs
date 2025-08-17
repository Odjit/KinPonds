using BepInEx.Configuration;
using BepInEx.Logging;
using ProjectM;
using ProjectM.CastleBuilding;
using ProjectM.Network;
using ProjectM.Shared;
using ProjectM.Terrain;
using Stunlock.Core;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using static KinPonds.Helper;

namespace KinPonds.Services;

class PondService
{
    static readonly PrefabGUID DT_Fish_Cursed_Standard_01 = new PrefabGUID(1553238108);
    static readonly PrefabGUID DT_Fish_Dunley_Standard_01 = new PrefabGUID(1612482091);
    static readonly PrefabGUID DT_Fish_Farbane_Standard_01 = new PrefabGUID(-47980789);
    static readonly PrefabGUID DT_Fish_General_Standard_01 = new PrefabGUID(-2110497587);
    static readonly PrefabGUID DT_Fish_Gloomrot_Standard_01 = new PrefabGUID(-1478565794);
    static readonly PrefabGUID DT_Fish_Silverlight_Standard_01 = new PrefabGUID(711437197);
    static readonly PrefabGUID DT_Fish_StrongBlade_Standard_01 = new PrefabGUID(1670470961);

    Dictionary<NetworkId, (Entity pond, int territoryId)> ponds = [];
    HashSet<Entity> pondsAdded = [];
    Dictionary<int, List<(Entity pond, NetworkId networkId)>> pondsPerTerritory = [];

    Dictionary<WorldRegionType, PrefabGUID> regionDropTables = new Dictionary<WorldRegionType, PrefabGUID>()
    {
        { WorldRegionType.FarbaneWoods, DT_Fish_Farbane_Standard_01 },
        { WorldRegionType.DunleyFarmlands, DT_Fish_Dunley_Standard_01 },
        { WorldRegionType.SilverlightHills, DT_Fish_Silverlight_Standard_01 },
        { WorldRegionType.Gloomrot_South, DT_Fish_Gloomrot_Standard_01 },
        { WorldRegionType.Gloomrot_North, DT_Fish_Gloomrot_Standard_01 },
        { WorldRegionType.CursedForest, DT_Fish_Cursed_Standard_01 },
        { WorldRegionType.Strongblade, DT_Fish_StrongBlade_Standard_01 }
    };

    Entity fishPrefab;

    PrefabGUID pondCostItem => new(PondCostItemGuid.Value);
    PrefabGUID dropTable => new(DropTable.Value);

    // Static configuration entries
    internal static ConfigEntry<int> PondCostItemGuid;
    internal static ConfigEntry<int> PondCostAmount;

    internal static ConfigEntry<float> RespawnTimeMin;
    internal static ConfigEntry<float> RespawnTimeMax;

    internal static ConfigEntry<int> DropTable;

    internal static ConfigEntry<int> TerritoryLimit;

    internal static void InitializeConfiguration(ConfigFile config, ManualLogSource log)
    {
        PondCostItemGuid = config.Bind("Pond Creation", "PondCostItemGuid", 0, 
            "PrefabGUID of the item required to create a pondEntry. Set to 0 to disable cost.");
        PondCostAmount = config.Bind("Pond Creation", "PondCostAmount", 0, 
            "Amount of the item required to create a pondEntry. Set to 0 to disable cost.");

        RespawnTimeMin = config.Bind("Fish Respawn", "RespawnTimeMin", 180f, 
            "Minimum time in seconds before a fish respawns after being caught");
        
        RespawnTimeMax = config.Bind("Fish Respawn", "RespawnTimeMax", 600f, 
            "Maximum time in seconds before a fish respawns after being caught");

        DropTable = config.Bind("Fish DropTable", "DropTable", 0);

        TerritoryLimit = config.Bind("Pond Limits", "TerritoryLimit", 3, "Maximum number of ponds allowed per territory. Set to -1 for unlimited.");

        // Validate configuration values
        if (RespawnTimeMin.Value < 0f)
        {
            log.LogWarning($"RespawnTimeMin cannot be negative. Setting to 0.");
            RespawnTimeMin.Value = 0f;
        }

        if (RespawnTimeMax.Value < RespawnTimeMin.Value)
        {
            log.LogWarning($"RespawnTimeMax cannot be less than RespawnTimeMin. Setting to RespawnTimeMin value.");
            RespawnTimeMax.Value = RespawnTimeMin.Value;
        }

        log.LogInfo($"PondService configuration loaded - RespawnTimeMin: {RespawnTimeMin.Value}s, RespawnTimeMax: {RespawnTimeMax.Value}s");
    }

    public PondService()
    {
        var eqb = new EntityQueryBuilder(Allocator.Temp).
                  AddAll(ComponentType.ReadOnly<NameableInteractable>()).
                  AddAll(ComponentType.ReadOnly<PrefabGUID>()).
                  WithOptions(EntityQueryOptions.IncludeDisabledEntities);

        var query = Core.Server.EntityManager.CreateEntityQuery(ref eqb);
        eqb.Dispose();

        var entities = query.ToEntityArray(Allocator.Temp);
        foreach (var entity in entities)
        {
            var prefabGuid = entity.Read<PrefabGUID>();
            if (prefabGuid != Helper.TM_Castle_ObjectDecor_Pool_StrongbladeDLC01 &&
                prefabGuid != Helper.TM_Castle_ObjectDecor_Pool_StrongbladeDLC02 &&
                prefabGuid != Helper.TM_LiquidStation_Water_Well01 &&
                prefabGuid != Helper.TM_LiquidStation_Water_Well03)
            {
                continue;
            }

            var nameableInteractable = entity.Read<NameableInteractable>();
            if (nameableInteractable.Name.Value != MyPluginInfo.PLUGIN_NAME) continue;

            AddPond(entity);
        }
        entities.Dispose();

        if (!Core.PrefabCollectionSystem._PrefabLookupMap.TryGetValue(Char_Fish_General, out fishPrefab))
        {
            Core.Log.LogError("PondService: Fish prefab missing! " + Char_Fish_General.PrefabName());
        }

        Core.StartCoroutine(DelayCheckForSpawning());

        Core.Log.LogInfo("PondService initialized with " + ponds.Count + " ponds.");
    }

    IEnumerator DelayCheckForSpawning()
    {
        yield return new WaitForSeconds(1);
        foreach (var pondEntry in ponds.Values)
        {
            if (!pondEntry.pond.Has<AttachedBuffer>())
            {
                Core.Log.LogInfo("Missing AttachedBuffer?");
                continue;
            }
            var hasFish = false;
            var attachedBuffer = Core.EntityManager.GetBuffer<AttachedBuffer>(pondEntry.pond);
            foreach (var attached in attachedBuffer)
            {
                if (attached.PrefabGuid == Char_Fish_General)
                {
                    hasFish = true;
                    break;
                }
            }

            if (!hasFish)
            {
                SpawnFish(pondEntry.pond);
            }
        }
    }

    public int GetPondCountForTerritory(Entity pondEntity)
    {
        if (pondEntity == Entity.Null) return 0;

        var castleHeart = pondEntity.Read<CastleHeartConnection>().CastleHeartEntity.GetEntityOnServer();
        if (castleHeart == Entity.Null) return 0;

        var territoryEntity = castleHeart.Read<CastleHeart>().CastleTerritoryEntity;
        if (territoryEntity == Entity.Null) return 0;
        
        var territoryId = territoryEntity.Read<CastleTerritory>().CastleTerritoryIndex;
        if (!pondsPerTerritory.TryGetValue(territoryId, out var pondsInTerritory)) return 0;

        var count = 0;
        foreach (var pondEntry in pondsInTerritory)
        {
            if (!Core.EntityManager.Exists(pondEntry.pond))
            {
                RemovePond(pondEntry.networkId);
                continue;
            }

            count += 1;
        }

        return count;
    }

    public void AddPond(Entity pondEntity)
    {
        var networkId = pondEntity.Read<NetworkId>();

        var castleHeart = pondEntity.Read<CastleHeartConnection>().CastleHeartEntity.GetEntityOnServer();
        if (castleHeart == Entity.Null) return;

        var territoryEntity = castleHeart.Read<CastleHeart>().CastleTerritoryEntity;
        if (territoryEntity == Entity.Null) return;
        
        var territoryId = territoryEntity.Read<CastleTerritory>().CastleTerritoryIndex;


        ponds[networkId] = (pondEntity, territoryId);
        pondsAdded.Add(pondEntity);

        if (!pondsPerTerritory.TryGetValue(territoryId, out var pondsInTerritory))
        {
            pondsInTerritory = [];
            pondsPerTerritory[territoryId] = pondsInTerritory;
        }
        pondsInTerritory.Add((pondEntity, networkId));
    }

    public void RemovePond(NetworkId networkId)
    {
        if (!ponds.TryGetValue(networkId, out var pondEntry)) return;
        pondsAdded.Remove(pondEntry.pond);
        ponds.Remove(networkId);

        var castleHeart = pondEntry.pond.Read<CastleHeartConnection>().CastleHeartEntity.GetEntityOnServer();
        if (castleHeart == Entity.Null) return;

        var territoryEntity = castleHeart.Read<CastleHeart>().CastleTerritoryEntity;
        if (territoryEntity == Entity.Null) return;

        var territoryId = territoryEntity.Read<CastleTerritory>().CastleTerritoryIndex;

        if (!pondsPerTerritory.TryGetValue(territoryId, out var pondsInTerritory)) return;

        pondsInTerritory.RemoveAll(p => p.networkId == networkId);
    }

    public Entity GetPond(NetworkId networkId)
    {
        if (ponds.TryGetValue(networkId, out var entry))
        {
            return entry.pond;
        }
        return Entity.Null;
    }

    public string CreatePond(Entity charEntity, Entity userEntity, Entity pondEntity, bool isAdmin)
    {
        if (pondEntity == Entity.Null) return "Invalid pool";
        if (!pondEntity.Has<NetworkId>()) return "Pool has no NetworkId";

        var networkId = pondEntity.Read<NetworkId>();
        if (ponds.ContainsKey(networkId)) return "Pond already exists in the pool.";

        var prefabGuid = pondEntity.Read<PrefabGUID>();
        if (prefabGuid != Helper.TM_Castle_ObjectDecor_Pool_StrongbladeDLC01 &&
            prefabGuid != Helper.TM_Castle_ObjectDecor_Pool_StrongbladeDLC02 &&
                prefabGuid != Helper.TM_LiquidStation_Water_Well01 &&
                prefabGuid != Helper.TM_LiquidStation_Water_Well03)
        {
            return "Not a pool";
        }

        if (pondEntity.Has<NameableInteractable>())
        {
            return "Pool is already being used by " + pondEntity.Read<NameableInteractable>().Name.Value;
        }

        if (!isAdmin)
        {
            var castleHeart = pondEntity.Read<CastleHeartConnection>().CastleHeartEntity.GetEntityOnServer();
            var castleHeartOwner = castleHeart.Read<UserOwner>().Owner.GetEntityOnServer();
            if (castleHeartOwner != userEntity) return "You are not the owner of this pool";

            var poolCount = GetPondCountForTerritory(pondEntity);
            if (TerritoryLimit.Value >= 0 && poolCount >= TerritoryLimit.Value)
            {
                return $"You have reached the limit of {TerritoryLimit.Value} ponds for this territory.";
            }
        }

        if (pondCostItem != PrefabGUID.Empty && PondCostAmount.Value > 0)
        {
            var inventories = Core.EntityManager.GetBuffer<InventoryInstanceElement>(charEntity);
            var inventoryEntity = Entity.Null;
            foreach (var inventory in inventories)
            {
                inventoryEntity = inventory.ExternalInventoryEntity.GetEntityOnServer();
                break;
            }

            if (inventoryEntity == Entity.Null || !InventoryUtilitiesServer.TryRemoveItem(Core.EntityManager, inventoryEntity, pondCostItem, PondCostAmount.Value))
            {
                return $"You must have {Format.Color(PondCostAmount.Value.ToString(), Color.White)}x {Format.Color(pondCostItem.PrefabName(),Color.Yellow)} in your inventory to create a pond.";
            }
        }


        pondEntity.Add<NameableInteractable>();
        pondEntity.Write(new NameableInteractable
        {
            Name = new FixedString64Bytes(MyPluginInfo.PLUGIN_NAME)
        });

        AddPond(pondEntity);
        SpawnFish(pondEntity);

        return null;
    }

    void SpawnFish(Entity pondEntity)
    {
        var castleHeart = pondEntity.Read<CastleHeartConnection>().CastleHeartEntity.GetEntityOnServer();
        if (castleHeart == Entity.Null) return;
        var castleHeartOwner = castleHeart.Read<UserOwner>().Owner.GetEntityOnServer();

        if (fishPrefab == Entity.Null)
        {
            Core.Log.LogError("PondService: Fish prefab missing! " + Char_Fish_General.PrefabName());
            return;
        }

        var poolPos = pondEntity.Read<Translation>().Value;
        poolPos.y += 0.5f;
        var fishEntity = SpawnEntity(castleHeartOwner, poolPos, Vector3.zero, Quaternion.identity, fishPrefab);

        var attach = new Attach(pondEntity);
        fishEntity.Add<Attach>()
                  .Write(attach);

        var dad = fishEntity.Read<DestroyAfterDuration>();
        dad.Duration = float.MaxValue;
        fishEntity.Write(dad);

        PrefabGUID dropTableToUse;
        if (pondEntity.Has<DropTableBuffer>())
        {
            var dropTableBuffer = Core.EntityManager.GetBuffer<DropTableBuffer>(pondEntity);
            dropTableToUse = dropTableBuffer[0].DropTableGuid;
        }
        else
        {
            dropTableToUse = GetDropTableToUse(poolPos);
        }

        if (dropTableToUse != DT_Fish_General_Standard_01)
        {
            var dropTableBuffer = Core.EntityManager.GetBuffer<DropTableBuffer>(fishEntity);
            var entry = dropTableBuffer[0];
            entry.DropTableGuid = dropTableToUse;
            dropTableBuffer[0] = entry;
        }
    }

    internal void SetDropTable(PrefabGUID newDropTable)
    {
        DropTable.Value = newDropTable.GuidHash;

        // Change existing fish drop tables
        foreach (var pond in pondsAdded)
        {
            // Ignore overridden ponds
            if (pond.Has<DropTableBuffer>()) continue;
            SetPondDropTable(newDropTable, pond);
        }
    }

    internal PrefabGUID GetOverrideDropTable(Entity pondEntity)
    {
        if (pondEntity == Entity.Null) return PrefabGUID.Empty;
        if (pondEntity.Has<DropTableBuffer>())
        {
            var dropTableBuffer = Core.EntityManager.GetBuffer<DropTableBuffer>(pondEntity);
            if (dropTableBuffer.Length > 0)
            {
                return dropTableBuffer[0].DropTableGuid;
            }
        }
        return GetDropTableToUse(pondEntity.Read<Translation>().Value);
    }

    internal PrefabGUID GetGlobalDropTable()
    {
        if (dropTable != PrefabGUID.Empty)
        {
            return dropTable;
        }
        return PrefabGUID.Empty;
    }

    internal bool HasPondOverrideDropTable(Entity pondEntity)
    {
        if (pondEntity == Entity.Null) return false;
        if (pondEntity.Has<DropTableBuffer>())
        {
            var dropTableBuffer = Core.EntityManager.GetBuffer<DropTableBuffer>(pondEntity);
            return dropTableBuffer.Length > 0 && dropTableBuffer[0].DropTableGuid != PrefabGUID.Empty;
        }
        return false;
    }

    internal void SetPondOverrideDropTable(Entity pondEntity, PrefabGUID newDropTable)
    {
        if (newDropTable == PrefabGUID.Empty)
        {
            if (pondEntity.Has<DropTableBuffer>())
            {
                pondEntity.Remove<DropTableBuffer>();
                SetPondDropTable(GetDropTableToUse(pondEntity.Read<Translation>().Value), pondEntity);
            }
            return;
        }

        if (!pondEntity.Has<DropTableBuffer>())
            pondEntity.Add<DropTableBuffer>();
        var dropTableBuffer = Core.EntityManager.GetBuffer<DropTableBuffer>(pondEntity);
        dropTableBuffer.Clear();
        dropTableBuffer.Add(new DropTableBuffer { DropTableGuid = newDropTable });

        SetPondDropTable(newDropTable, pondEntity);
    }

    static void SetPondDropTable(PrefabGUID newDropTable, Entity pondEntity)
    {
        if (!pondEntity.Has<AttachedBuffer>()) return;
        var attachedBuffer = Core.EntityManager.GetBuffer<AttachedBuffer>(pondEntity);
        foreach (var attached in attachedBuffer)
        {
            if (attached.PrefabGuid != Char_Fish_General) continue;
            var fishEntity = attached.Entity;

            if (!fishEntity.Has<DropTableBuffer>()) continue;
            var dropTableBuffer = Core.EntityManager.GetBuffer<DropTableBuffer>(fishEntity);
            var entry = dropTableBuffer[0];
            entry.DropTableGuid = newDropTable;
            dropTableBuffer[0] = entry;
        }
    }

    PrefabGUID GetDropTableToUse(float3 pondPos)
    {
        var dropTableToUse = DT_Fish_General_Standard_01;
        if (dropTable != PrefabGUID.Empty)
        {
            dropTableToUse = dropTable;
        }
        else
        {
            var region = Core.Region.GetRegion(pondPos);
            if (region != WorldRegionType.None)
                regionDropTables.TryGetValue(region, out dropTableToUse);
        }

        return dropTableToUse;
    }

    internal void CheckForRespawn(Entity fishEntity)
    {
        if (!fishEntity.Has<Attached>()) return;

        var pondEntity = fishEntity.Read<Attached>().Parent;
        if (!pondsAdded.Contains(pondEntity)) return;

        var waitFor = UnityEngine.Random.RandomRange(RespawnTimeMin.Value, RespawnTimeMax.Value);

        Core.StartCoroutine(SpawnFishIn(pondEntity, waitFor));
    }

    IEnumerator SpawnFishIn(Entity pondEntity, float timeToWait)
    {
        yield return new WaitForSeconds(timeToWait);

        if (!pondsAdded.Contains(pondEntity)) yield break;
        if (!Core.EntityManager.Exists(pondEntity))
        {
            // Find the pond networkId
            var networkId = ponds.Where(p => p.Value.pond == pondEntity)
                                 .Select(p => p.Key)
                                 .FirstOrDefault();
            RemovePond(networkId);
            yield break;
        }

        SpawnFish(pondEntity);
    }


}