using KinPonds.Commands.Converters;
using KinPonds.Services;
using ProjectM;
using Stunlock.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using VampireCommandFramework;
using static KinPonds.Helper;

namespace KinPonds.Commands;
class PondCommands
{
    [Command("pond")]
    public static void Pond(ChatCommandContext ctx)
    {
        var aimPos = ctx.Event.SenderCharacterEntity.Read<EntityAimData>().AimPosition;
        var closestPool = FindClosestPool(aimPos);
        if (closestPool == Entity.Null)
        {
            ctx.Reply("<b><color=red>><(((*></b> <color=#DB8>No pool for a pond found nearby.");
            return;
        }

        var result = Core.Ponds.CreatePond(ctx.Event.SenderCharacterEntity, ctx.Event.SenderUserEntity, closestPool, ctx.IsAdmin);

        if (result == null)
        {
            ctx.Reply(RandomPondSuccess());
        }
        else
        {
            ctx.Reply("<b><color=red>><(((*> <color=#DB8>The fish gods deny your request: " + result);
        }
    }
    static readonly string[] PondCreatedMessages = new[]
{
    "Something now stirs in your pond.",
    "The pond waters ripple with dark promise.",
    "Home Fishin’: what bites here may bite back.",
    "Well, it’s not empty anymore.",
    "The pond accepts your offering."
};

    public static string RandomPondSuccess()
    {
        var msg = PondCreatedMessages[UnityEngine.Random.Range(0, PondCreatedMessages.Length)];
        return $"<size=10><b><color=#0CD>><(((*></b></size> <color=#DB8>{msg}";
    }

    [Command("pond respawn")]
    public static void PondRespawn(ChatCommandContext ctx)
    {
        ctx.Reply($"<color=#DB8>Current time between fish respawns is <color=#0CD>{PondService.RespawnTimeMin.Value}</color> to <color=#0CD>{PondService.RespawnTimeMax.Value}</color> seconds.");
    }

    [Command("pond respawn", adminOnly: true)]
    public static void PondRespawnSet(ChatCommandContext ctx, float minTime, float maxTime)
    {
        if (minTime < 0)
        {
            ctx.Reply("<color=#DB8>Minimum respawn time cannot be negative.");
            return;
        }
        if (maxTime < minTime)
        {
            ctx.Reply("<color=#DB8>Maximum respawn time cannot be less than minimum respawn time.");
            return;
        }

        PondService.RespawnTimeMin.Value = minTime;
        PondService.RespawnTimeMax.Value = maxTime;
        ctx.Reply($"<color=#DB8>Set time between fish respawns to <color=#0CD>{minTime}</color> to <color=#0CD>{maxTime}</color> seconds.");
    }

    [Command("pond cost", description: "Sets the cost of adding a fishing pond to a pool", adminOnly: true)]
    public static void PondCostSet(ChatCommandContext ctx, ItemParameter item, int amount)
    {
        PondService.PondCostItemGuid.Value = item.Value.GuidHash;
        PondService.PondCostAmount.Value = amount;
        ctx.Reply($"<color=#DB8>Set pond creation cost to {Format.Color(amount.ToString(), Color.White)}x {Format.Color(item.Value.PrefabName(), Color.Yellow)}. Set to 0 to disable cost.");
    }

    [Command("pond cost clear", description: "Clears the cost of adding a fishing pond to a pool", adminOnly: true)]
    public static void PondCostClear(ChatCommandContext ctx)
    {
        PondService.PondCostItemGuid.Value = PrefabGUID.Empty.GuidHash;
        PondService.PondCostAmount.Value = 0;
        ctx.Reply("<color=#DB8>Cleared pond creation cost.");
    }

    [Command("pond droptable", description: "Sets the drop table for fish in ponds", adminOnly: true)]
    public static void PondDropTableSet(ChatCommandContext ctx, DropTableParameter dropTable)
    {
        Core.Ponds.SetDropTable(dropTable.Value);
        if (dropTable.Value == PrefabGUID.Empty)
        {
            ctx.Reply("<color=#DB8>Cleared pond drop table.");
            return;
        }
        ctx.Reply($"<color=#DB8>Set pond drop table to {Format.Color(dropTable.Value.PrefabName(), Color.White)}.");
    }

    [Command("pond droptable clear", description: "Clears the drop table for fish in ponds", adminOnly: true)]
    public static void PondDropTableClear(ChatCommandContext ctx)
    {
        Core.Ponds.SetDropTable(PrefabGUID.Empty);
        ctx.Reply("<color=#DB8>Cleared pond drop table.");
    }


    [Command("pond setdrop", description: "Sets the drop table for fish in the target pond", adminOnly: true)]
    public static void PondSetDropTable(ChatCommandContext ctx, DropTableParameter dropTable)
    {
        var aimPos = ctx.Event.SenderCharacterEntity.Read<EntityAimData>().AimPosition;
        var pond = FindClosestPool(aimPos);
        if (pond == Entity.Null)
        {
            ctx.Reply("<color=#DB8>No pond found");
            return;
        }

        Core.Ponds.SetPondOverrideDropTable(pond, dropTable.Value);
        if (dropTable.Value == PrefabGUID.Empty)
        {
            ctx.Reply("<color=#DB8>Cleared pond drop table.");
            return;
        }
        ctx.Reply($"<color=#DB8>Set pond drop table to {Format.Color(dropTable.Value.PrefabName(), Color.White)}.");
    }

    [Command("pond limit", description: "Get the current pond limit.")]
    public static void PondLimitSet(ChatCommandContext ctx)
    {
        ctx.Reply($"<color=#DB8>Maximum of <color=#fff>{PondService.TerritoryLimit.Value}</color> per territory.");
    }

    [Command("pond limit", description: "Sets the maximum number of ponds allowed per territory. -1 for unlimited.", adminOnly: true)]
    public static void PondLimitSet(ChatCommandContext ctx, int limit)
    {
        PondService.TerritoryLimit.Value = limit;

        ctx.Reply($"<color=#DB8>Set maximum ponds per territory to <color=#0CD>{(limit<0 ? "unlimited" : limit)}</color>.");
    }

    //[Command("pond droptable list", description: "Lists all available drop tables", adminOnly: true)]
    public static void PondDropTableList(ChatCommandContext ctx)
    {
        ctx.Reply("<color=#DB8>Generating markdown droptables!");
        var dropTables = new List<string>();
        var eqb = new EntityQueryBuilder(Allocator.Temp).
                  AddAll(ComponentType.ReadOnly<DropTableDataBuffer>()).
                  AddAll(ComponentType.ReadOnly<Prefab>()).
                  WithOptions(EntityQueryOptions.IncludePrefab);
        var eq = Core.EntityManager.CreateEntityQuery(ref eqb);
        eqb.Dispose();
        var dropTableEntities = eq.ToEntityArray(Allocator.Temp);

        foreach (var dropTableEntity in dropTableEntities)
        {
            var sb = new StringBuilder();
            var tableName = dropTableEntity.Read<PrefabGUID>().LookupName();
            var prefabGuid = dropTableEntity.Read<PrefabGUID>().GuidHash;

            // Main drop table header with collapsible section
            sb.AppendLine($"<details>");
            sb.AppendLine($"<summary><strong>{tableName}</strong> <code>PrefabGuid({prefabGuid})</code></summary>");
            sb.AppendLine();

            var dropTableDataBuffer = Core.EntityManager.GetBuffer<DropTableDataBuffer>(dropTableEntity);
            foreach (var entry in dropTableDataBuffer)
            {
                if (entry.ItemType == DropItemType.Group)
                {
                    sb.AppendLine($"- **{(entry.DropRate * 100):F1}%** 🎲 **{entry.Quantity}x Group** - `{entry.ItemGuid.LookupName()}`");
                    ProcessDropGroupMarkdown(sb, entry.ItemGuid, 1);
                }
                else if (entry.ItemType == DropItemType.Unit)
                {
                    sb.AppendLine($"- **{(entry.DropRate * 100):F1}%** 👤 **{entry.Quantity}x Unit** - *'{entry.ItemGuid.PrefabName()}'* `{entry.ItemGuid.LookupName()}`");
                }
                else
                {
                    sb.AppendLine($"- **{(entry.DropRate * 100):F1}%** 📦 **{entry.Quantity}x Item** - *'{entry.ItemGuid.PrefabName()}'* `{entry.ItemGuid.LookupName()}`");
                }
            }

            sb.AppendLine();
            sb.AppendLine("</details>");
            sb.AppendLine();

            dropTables.Add(sb.ToString());
        }

        dropTableEntities.Dispose();
        dropTables.Sort();

        // Create the full markdown document
        var markdownContent = new StringBuilder();
        markdownContent.AppendLine("# VRising Drop Tables");
        markdownContent.AppendLine();
        markdownContent.AppendLine("This document contains all drop tables and their hierarchical structures in VRising.");
        markdownContent.AppendLine();
        markdownContent.AppendLine("## Legend");
        markdownContent.AppendLine("- 🎲 **Drop Group** - Contains nested drop tables");
        markdownContent.AppendLine("- 📦 **Item** - Individual items that can be dropped");
        markdownContent.AppendLine("- 👤 **Unit** - Creatures/NPCs that can spawn");
        markdownContent.AppendLine("- **Percentage** - Drop chance/probability");
        markdownContent.AppendLine("- **Quantity** - Number of items/units");
        markdownContent.AppendLine();
        markdownContent.AppendLine("---");
        markdownContent.AppendLine();

        markdownContent.Append(string.Join("", dropTables));

        try
        {
            File.WriteAllText("dropTables.md", markdownContent.ToString());
            ctx.Reply($"Drop tables written to dropTables.md ({dropTables.Count} tables processed)");
        }
        catch (Exception ex)
        {
            ctx.Reply($"Failed to write file: {ex.Message}");
        }
    }

    static void ProcessDropGroupMarkdown(StringBuilder sb, PrefabGUID dropGroupPrefab, int level)
    {
        if (!Core.PrefabCollectionSystem._PrefabLookupMap.TryGetValue(dropGroupPrefab, out var dropGroupEntity)) return;

        var dropGroupBuffer = Core.EntityManager.GetBuffer<ItemDataDropGroupBuffer>(dropGroupEntity);
        var totalWeight = 0f;
        foreach (var entry in dropGroupBuffer)
        {
            totalWeight += entry.Weight;
        }

        // Create nested collapsible section for drop groups
        var indent = new string(' ', level * 2);
        var hasNestedGroups = dropGroupBuffer.AsNativeArray().ToArray().Any(entry => entry.Type == DropItemType.Group);

        if (hasNestedGroups)
        {
            sb.AppendLine($"{indent}<details>");
            sb.AppendLine($"{indent}<summary>📂 <strong>Nested Drop Groups</strong></summary>");
            sb.AppendLine();
        }

        foreach (var entry in dropGroupBuffer)
        {
            var percentage = (entry.Weight / totalWeight * 100);

            if (entry.Type == DropItemType.Group)
            {
                sb.AppendLine($"{indent}- **{percentage:F1}%** 🎲 **{entry.Quantity}x Group** - `{entry.DropItemPrefab.LookupName()}`");
                ProcessDropGroupMarkdown(sb, entry.DropItemPrefab, level + 1);
            }
            else if (entry.Type == DropItemType.Unit)
            {
                sb.AppendLine($"{indent}- **{percentage:F1}%** 👤 **{entry.Quantity}x Unit** - *'{entry.DropItemPrefab.PrefabName()}'* `{entry.DropItemPrefab.LookupName()}`");
            }
            else
            {
                sb.AppendLine($"{indent}- **{percentage:F1}%** 📦 **{entry.Quantity}x Item** - *'{entry.DropItemPrefab.PrefabName()}'* `{entry.DropItemPrefab.LookupName()}`");
            }
        }

        if (hasNestedGroups)
        {
            sb.AppendLine();
            sb.AppendLine($"{indent}</details>");
            sb.AppendLine();
        }
    }
}
