using ProjectM;
using Stunlock.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using VampireCommandFramework;

namespace KinPonds.Commands.Converters;

internal record class DropTableParameter(PrefabGUID Value);

internal class DropTableParameterConverter : CommandArgumentConverter<DropTableParameter>
{
    const int MAX_REPLY_LENGTH = 509;

    static Dictionary<string, PrefabGUID> dropTableCache = null;

    static void InitializeDropTableCache()
    {
        if (dropTableCache != null) return;
        dropTableCache = new Dictionary<string, PrefabGUID>(StringComparer.OrdinalIgnoreCase);

        var eqb = new EntityQueryBuilder(Allocator.Temp).
                  AddAll(ComponentType.ReadOnly<DropTableDataBuffer>()).
                  AddAll(ComponentType.ReadOnly<Prefab>()).
                  WithOptions(EntityQueryOptions.IncludePrefab);
        var eq = Core.EntityManager.CreateEntityQuery(ref eqb);
        eqb.Dispose();

        var dropTableEntities = eq.ToEntityArray(Allocator.Temp);
        foreach (var dropTableEntity in dropTableEntities)
        {
            var prefabEntity = dropTableEntity;
            if (!Core.EntityManager.Exists(prefabEntity)) continue;
            if (!prefabEntity.Has<DropTableDataBuffer>()) continue;

            var prefabGuid = dropTableEntity.Read<PrefabGUID>();
            Core.PrefabCollectionSystem._PrefabLookupMap.TryGetName(prefabGuid, out var name);
            dropTableCache[name] = prefabGuid;
            var ingameName = Core.Localization.GetPrefabName(prefabGuid);
            if (!string.IsNullOrEmpty(ingameName))
                dropTableCache[ingameName] = prefabGuid;
        }
        dropTableEntities.Dispose();
        eq.Dispose();
    }

    public override DropTableParameter Parse(ICommandContext ctx, string input)
	{
        if (input.ToLowerInvariant() == "clear")
            return new DropTableParameter(PrefabGUID.Empty);

        InitializeDropTableCache();
        if (int.TryParse(input, out var integral))
        {
            var prefabGuid = new PrefabGUID(integral);
            if (!Core.PrefabCollectionSystem._PrefabLookupMap.TryGetValue(prefabGuid, out var prefab))
                throw ctx.Error($"Invalid drop table prefabId: {input}");
            if (!prefab.Has<DropTableDataBuffer>())
                throw ctx.Error($"Not a drop table prefabId: {prefabGuid.LookupName()}");
            return new DropTableParameter(prefabGuid);
        }

        if (TryGet(input, out var result)) return result;

        var dtAdded = "DT_" + input;
        if (TryGet(dtAdded, out result)) return result;

        Dictionary<PrefabGUID, List<string>> searchResults = [];
		foreach (var (name, prefabGuid) in dropTableCache)
		{
			if (name.Contains(input, StringComparison.OrdinalIgnoreCase))
			{
				if (!searchResults.ContainsKey(prefabGuid))
					searchResults[prefabGuid] = [];
				searchResults[prefabGuid].Add(name);
			}
		}


        if (searchResults.Count == 1)
		{
			return new DropTableParameter(searchResults.First().Key);
		}

		var lengthOfFail = 60 + "\n...".Length;

        if (searchResults.Count > 1)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Multiple results be more specific:<color=#fff>");
            foreach (var (prefabGuid, matches) in searchResults)
            {
                var matchesText = string.Join("</color> or <color=#fff>", matches);
                if (sb.Length + matchesText.Length + lengthOfFail >= MAX_REPLY_LENGTH)
                {
                    sb.AppendLine("...");
                    throw ctx.Error(sb.ToString());
                }
                else
                {
                    sb.AppendLine(matchesText);
                }
            }
            throw ctx.Error(sb.ToString());
        }

        // Try a double search splitting the input
        for (var i = 3; i < input.Length; ++i)
		{
			var inputOne = input[..i];
			var inputTwo = input[i..];
            foreach (var (name, prefabGuid) in dropTableCache)
            {
				if (name.Contains(inputOne, StringComparison.OrdinalIgnoreCase) &&
                    name.Contains(inputTwo, StringComparison.OrdinalIgnoreCase))
				{
					if (!searchResults.ContainsKey(prefabGuid))
						searchResults[prefabGuid] = [];
					searchResults[prefabGuid].Add(name);
				}
			}

			if (searchResults.Count == 1)
			{
				return new DropTableParameter(searchResults.First().Key);
			}
		}

		var resultsFromFirstSplit = searchResults;
		searchResults = [];

		// Try a double search splitting the input with _ prepended
		for (var i = 3; i < input.Length; ++i)
		{
			var inputOne = "_" + input[..i];
			var inputTwo = input[i..];
            foreach (var (name, prefabGuid) in dropTableCache)
            {
				if (name.Contains(inputOne, StringComparison.OrdinalIgnoreCase) &&
                    name.Contains(inputTwo, StringComparison.OrdinalIgnoreCase))
				{
					if (!searchResults.ContainsKey(prefabGuid))
						searchResults[prefabGuid] = [];
					searchResults[prefabGuid].Add(name);
				}
			}

			if (searchResults.Count == 1)
			{
				return new DropTableParameter(searchResults.First().Key);
			}

			if (searchResults.Count > 1)
			{
				var sb = new StringBuilder();
				sb.AppendLine("Multiple results be more specific:<color=#fff>");
				foreach (var (prefabGuid, matches) in searchResults)
				{
					var matchesText = string.Join("</color> or <color=#fff>", matches);
					if (sb.Length + matchesText.Length + lengthOfFail >= MAX_REPLY_LENGTH)
					{
						sb.AppendLine("...");
						throw ctx.Error(sb.ToString());
					}
					else
					{
						sb.AppendLine(matchesText);
					}
				}
				throw ctx.Error(sb.ToString());
			}
		}

		if (resultsFromFirstSplit.Count > 1)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Multiple results be more specific:<color=#fff>");
            foreach (var (prefabGuid, matches) in searchResults)
            {
                var matchesText = string.Join("</color> or <color=#fff>", matches);
                if (sb.Length + matchesText.Length + lengthOfFail >= MAX_REPLY_LENGTH)
                {
                    sb.AppendLine("...");
                    throw ctx.Error(sb.ToString());
                }
                else
                {
                    sb.AppendLine(matchesText);
                }
            }
            throw ctx.Error(sb.ToString());
        }

		throw ctx.Error($"Invalid drop table id: {input}");
	}

	private static bool TryGet(string input, out DropTableParameter item)
	{
		if (dropTableCache.TryGetValue(input, out var prefab))
		{
			item = new DropTableParameter(prefab);
			return true;
		}

		item = new DropTableParameter(new(0));
		return false;
	}
}

