﻿using ProjectM;
using ProjectM.Shared;
using Stunlock.Core;
using Stunlock.Localization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace KinPonds.Services
{
    internal class LocalizationService
    {
        Dictionary<string, string> localization = [];
        Dictionary<int, string> prefabNames = [];

        public LocalizationService()
        {
            LoadLocalization();
            LoadPrefabNames();
        }

        void LoadLocalization()
        {
            var resourceName = "KinPonds.Localization.English.json";

            var assembly = Assembly.GetExecutingAssembly();
            var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                using (var reader = new StreamReader(stream))
                {
                    string jsonContent = reader.ReadToEnd();
                    localization = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonContent);
                }
            }
            else
            {
                Console.WriteLine("Resource not found!");
            }
        }

        void LoadPrefabNames()
        {
            var resourceName = "KinPonds.Localization.PrefabNames.json";

            var assembly = Assembly.GetExecutingAssembly();
            var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                using (var reader = new StreamReader(stream))
                {
                    string jsonContent = reader.ReadToEnd();
                    prefabNames = JsonSerializer.Deserialize<Dictionary<int, string>>(jsonContent);
                }
            }
            else
            {
                Console.WriteLine("Resource not found!");
            }
        }

        public string GetLocalization(string guid)
        {
            if (localization.TryGetValue(guid, out var text))
            {
                return text;
            }
            return $"<Localization not found for {guid}>";
        }

        public string GetLocalization(LocalizationKey key)
        {
            var guid = key.Key.ToGuid().ToString();
            return GetLocalization(guid);
        }

        public string GetPrefabName(PrefabGUID itemPrefabGUID)
        {
            if(!prefabNames.TryGetValue(itemPrefabGUID._Value, out var itemLocalizationHash))
            {
                return null;
            }
            var name = GetLocalization(itemLocalizationHash);

            if (itemPrefabGUID._Value == -1265586439)
                name = "Darkmatter Pistols";

            if(Core.PrefabCollectionSystem._PrefabLookupMap.TryGetValue(itemPrefabGUID, out var prefab))
            {
                if (prefab.Has<ItemData>())
                {
                    var itemData = prefab.Read<ItemData>();
                    if (itemData.ItemType == ItemType.Tech)
                    {
                        name = "Book " + name;
                    }
                }
                if (prefab.Has<JewelInstance>())
                {
                    var jewelInstance = prefab.Read<JewelInstance>();
                    // For some reason tier 0 is special and includes this already in its name
                    if (jewelInstance.TierIndex != 0)
                        name += $" Jewel Tier {jewelInstance.TierIndex + 1}";
                }
                if (prefab.Has<LegendaryItemInstance>())
                {
                    var legendaryInstance = prefab.Read<LegendaryItemInstance>();
                    name += $" Tier {legendaryInstance.TierIndex + 1}";
                }
                if (prefab.Has<ShatteredItem>())
                {
                    name += " Shattered";
                }
            }

            // Disambuigation for some books
            if (itemPrefabGUID._Value == 1455590675 || itemPrefabGUID._Value == -651642571)
            {
                name += " Tier 1";
            }
            else if (itemPrefabGUID._Value == 1150376281 || itemPrefabGUID._Value == 686122001)
            {
                name += " Tier 2";
            }

            return name;
        }

    }
}
