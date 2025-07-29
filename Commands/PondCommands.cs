using KinPonds.Commands.Converters;
using KinPonds.Services;
using ProjectM;
using Stunlock.Core;
using System;
using System.Collections.Generic;
using System.IO;
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
            ctx.Reply("<b><color=red>><(((*> <color=#DB8>The invocation was flawed: " + result);
        }
    }
    static readonly string[] PondCreatedMessages = new[]
{
    "Something now stirs in your pond.",
    "The pond waters ripple with dark promise.",
    "Home fishin’: what bites here may bite back.",
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

    [Command("pond droptable", description: "Sets the drop table for fish in ponds (use clear to reset to defaults)", adminOnly: true)]
    public static void PondDropTableSet(ChatCommandContext ctx, DropTableParameter dropTable)
    {
        Core.Ponds.SetDropTable(dropTable.Value);
        if (dropTable.Value == PrefabGUID.Empty)
        {
            ctx.Reply("<color=#DB8>Cleared pond drop table and restored to defaults.");
            return;
        }
        ctx.Reply($"<color=#DB8>Set pond drop table to {Format.Color(dropTable.Value.PrefabName(), Color.White)}.");
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
            ctx.Reply("<color=#DB8>Cleared this pond's drop table and restored it to defaults.");
            return;
        }
        ctx.Reply($"<color=#DB8>Set this pond's drop table to {Format.Color(dropTable.Value.PrefabName(), Color.White)}.");
    }

    [Command("pond limit", description: "Get the current pond limit.")]
    public static void PondLimitSet(ChatCommandContext ctx)
    {
        ctx.Reply($"<color=#DB8>Maximum of <color=#fff>{PondService.TerritoryLimit.Value}</color> pond{(PondService.TerritoryLimit.Value > 1 ? "s" : "")} per territory.");
    }

    [Command("pond limit", description: "Sets the maximum number of ponds allowed per territory. -1 for unlimited.", adminOnly: true)]
    public static void PondLimitSet(ChatCommandContext ctx, int limit)
    {
        PondService.TerritoryLimit.Value = limit;

        ctx.Reply($"<color=#DB8>Set maximum ponds per territory to <color=#0CD>{(limit<0 ? "unlimited" : limit)}</color>.");
    }

    //[Command("pond generatewiki", description: "Generates wiki of all the droptables", adminOnly: true)]
    public static void GenerateWiki(ChatCommandContext ctx)
    {
        ctx.Reply("<color=#DB8>Generating markdown droptables for GitHub Wiki!");
        var dropTables = new Dictionary<string, string>();
        var indexEntries = new List<string>();

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
            var prefabCollectionSystem = Core.Server.GetExistingSystemManaged<PrefabCollectionSystem>();
            var prefabGuid = dropTableEntity.Read<PrefabGUID>();
            prefabCollectionSystem._PrefabLookupMap.TryGetName(prefabGuid, out var tableName);
            var safeFileName = SanitizeFileName(tableName);

            // Create individual drop table page
            sb.AppendLine($"# {tableName} ({prefabGuid._Value})");
            sb.AppendLine();
            sb.AppendLine("## Legend");
            sb.AppendLine("- 🎲 **Drop Groups** - Contains nested drop tables");
            sb.AppendLine("- 📦 **Items** - Individual items that can be dropped");
            sb.AppendLine("- 👤 **Units** - Creatures/NPCs that can spawn");
            sb.AppendLine("- **Percentage** - Drop chance/probability");
            sb.AppendLine("- **Quantity** - Number of items/units");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();

            var dropTableDataBuffer = Core.EntityManager.GetBuffer<DropTableDataBuffer>(dropTableEntity);
            foreach (var entry in dropTableDataBuffer)
            {
                if (entry.ItemType == DropItemType.Group)
                {
                    sb.AppendLine($"- **{(entry.DropRate * 100):F1}%** 🎲 **{entry.Quantity}x** - `{entry.ItemGuid.LookupName()}`");
                    ProcessDropGroupMarkdown(sb, entry.ItemGuid, 1);
                }
                else if (entry.ItemType == DropItemType.Unit)
                {
                    sb.AppendLine($"- **{(entry.DropRate * 100):F1}%** 👤 **{entry.Quantity}x** - *{entry.ItemGuid.PrefabName()}* `{entry.ItemGuid.LookupName()}`");
                }
                else
                {
                    sb.AppendLine($"- **{(entry.DropRate * 100):F1}%** 📦 **{entry.Quantity}x** - *{entry.ItemGuid.PrefabName()}* `{entry.ItemGuid.LookupName()}`");
                }
            }

            dropTables[safeFileName] = sb.ToString();
            indexEntries.Add($"- [{tableName} ({prefabGuid._Value})]({safeFileName})");
        }

        dropTableEntities.Dispose();

        // Sort index entries alphabetically
        indexEntries.Sort();

        // Create the index page
        var indexContent = new StringBuilder();
        indexContent.AppendLine("# VRising Drop Tables Index");
        indexContent.AppendLine();
        indexContent.AppendLine("This is the main index for all VRising drop tables. Click on any drop table name to view its details.");
        indexContent.AppendLine();
        indexContent.AppendLine($"**Total Drop Tables:** {dropTables.Count}");
        indexContent.AppendLine();
        indexContent.AppendLine("## Drop Tables");
        indexContent.AppendLine();

        foreach (var entry in indexEntries)
        {
            indexContent.AppendLine(entry);
        }

        try
        {
            // Create wiki directory if it doesn't exist
            var wikiDir = "wiki";
            if (!Directory.Exists(wikiDir))
            {
                Directory.CreateDirectory(wikiDir);
            }

            // Write index page
            File.WriteAllText(Path.Combine(wikiDir, "Drop-Tables.md"), indexContent.ToString());

            // Write individual drop table pages
            foreach (var kvp in dropTables)
            {
                File.WriteAllText(Path.Combine(wikiDir, $"{kvp.Key}.md"), kvp.Value);
            }

            ctx.Reply($"Wiki generated successfully! Index: Drop-Tables.md, {dropTables.Count} individual pages created in 'wiki' folder");
        }
        catch (Exception ex)
        {
            ctx.Reply($"Failed to write files: {ex.Message}");
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

        // Create collapsible section for ALL drop groups
        var indent = new string(' ', level * 2);
        var groupName = dropGroupPrefab.LookupName();
        var itemCount = dropGroupBuffer.Length;

        sb.AppendLine($"{indent}<details>");
        sb.AppendLine($"{indent}<summary>📂 <strong>{groupName}</strong> ({itemCount} entries)</summary>");
        sb.AppendLine();

        foreach (var entry in dropGroupBuffer)
        {
            var percentage = (entry.Weight / totalWeight * 100);

            if (entry.Type == DropItemType.Group)
            {
                sb.AppendLine($"{indent}- **{percentage:F1}%** 🎲 **{entry.Quantity}x** - `{entry.DropItemPrefab.LookupName()}`");
                ProcessDropGroupMarkdown(sb, entry.DropItemPrefab, level + 1);
            }
            else if (entry.Type == DropItemType.Unit)
            {
                sb.AppendLine($"{indent}- **{percentage:F1}%** 👤 **{entry.Quantity}x** - *{entry.DropItemPrefab.PrefabName()}* `{entry.DropItemPrefab.LookupName()}`");
            }
            else
            {
                sb.AppendLine($"{indent}- **{percentage:F1}%** 📦 **{entry.Quantity}x** - *{entry.DropItemPrefab.PrefabName()}* `{entry.DropItemPrefab.LookupName()}`");
            }
        }

        sb.AppendLine();
        sb.AppendLine($"{indent}</details>");
        sb.AppendLine();
    }

    static string SanitizeFileName(string fileName)
    {
        // Remove or replace invalid characters for file names and GitHub wiki URLs
        var sanitized = fileName
            .Replace(" ", "-")
            .Replace("_", "-")
            .Replace("(", "")
            .Replace(")", "")
            .Replace("[", "")
            .Replace("]", "")
            .Replace("{", "")
            .Replace("}", "")
            .Replace(":", "")
            .Replace(";", "")
            .Replace("\"", "")
            .Replace("'", "")
            .Replace("/", "-")
            .Replace("\\", "-")
            .Replace("?", "")
            .Replace("*", "")
            .Replace("<", "")
            .Replace(">", "")
            .Replace("|", "-");

        // Remove multiple consecutive dashes and trim
        while (sanitized.Contains("--"))
        {
            sanitized = sanitized.Replace("--", "-");
        }

        return sanitized.Trim('-');
    }
}
