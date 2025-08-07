using ProjectM;
using Stunlock.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Collections;
using VampireCommandFramework;

namespace KinPonds.Commands.Converters;

internal record class ItemParameter(PrefabGUID Value);

internal class ItemParameterConverter : CommandArgumentConverter<ItemParameter>
{
    const int MAX_REPLY_LENGTH = 509;

    static Dictionary<string, PrefabGUID> itemCache = null;

    static void InitializeItemCache()
    {
        if (itemCache != null) return;
        itemCache = new Dictionary<string, PrefabGUID>(StringComparer.OrdinalIgnoreCase);
        var prefabs = Core.PrefabCollectionSystem._PrefabLookupMap.GuidToEntityMap.GetKeyArray(Allocator.Temp);
        foreach (var prefabGuid in prefabs)
        {
            var name = prefabGuid.LookupName();
            if (name.StartsWith("Item_") && !itemCache.ContainsKey(name))
            {
                itemCache[name] = prefabGuid;
                var ingameName = Core.Localization.GetPrefabName(prefabGuid);
                if (!string.IsNullOrEmpty(ingameName))
                    itemCache[ingameName] = prefabGuid;
            }
        }
        prefabs.Dispose();
    }

    public override ItemParameter Parse(ICommandContext ctx, string input)
	{
        if (input.ToLowerInvariant() == "clear")
            return new ItemParameter(PrefabGUID.Empty);

        InitializeItemCache();
        if (int.TryParse(input, out var integral))
		{
            var prefabGuid = new PrefabGUID(integral);
            if (!Core.PrefabCollectionSystem._PrefabLookupMap.TryGetValue(prefabGuid, out var prefab))
                throw ctx.Error($"Invalid item prefabId: {input}");
            if (!prefab.Has<ItemData>())
                throw ctx.Error($"Not an item prefabId: {prefabGuid.LookupName()}");
            return new ItemParameter(prefabGuid);
        }

		if (TryGet(input, out var result)) return result;

        var inputItemAdded = "Item_" + input;
        if (TryGet(inputItemAdded, out result)) return result;

        var inputIngredientAdded = "Item_Ingredient_" + input;
		if (TryGet(inputIngredientAdded, out result)) return result;

		// Standard postfix
		var standardPostfix = inputIngredientAdded + "_Standard";
		if (TryGet(standardPostfix, out result)) return result;

		Dictionary<PrefabGUID, List<string>> searchResults = [];
		foreach (var (name, prefabGuid) in itemCache)
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
			return new ItemParameter(searchResults.First().Key);
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
            foreach (var (name, prefabGuid) in itemCache)
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
				return new ItemParameter(searchResults.First().Key);
			}
		}

		var resultsFromFirstSplit = searchResults;
		searchResults = [];

		// Try a double search splitting the input with _ prepended
		for (var i = 3; i < input.Length; ++i)
		{
			var inputOne = "_" + input[..i];
			var inputTwo = input[i..];
            foreach (var (name, prefabGuid) in itemCache)
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
				return new ItemParameter(searchResults.First().Key);
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
            foreach (var (prefabGuid, matches) in resultsFromFirstSplit)
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

		throw ctx.Error($"Invalid item id: {input}");
	}

	private static bool TryGet(string input, out ItemParameter item)
	{
		if (itemCache.TryGetValue(input, out var prefab))
		{
			item = new ItemParameter(prefab);
			return true;
		}

		item = new ItemParameter(new(0));
		return false;
	}
}

