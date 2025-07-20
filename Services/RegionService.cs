using ProjectM.Terrain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;

namespace KinPonds.Services;
internal class RegionService
{
	struct RegionPolygon
	{
		public WorldRegionType Region;
		public Aabb Aabb;
		public float2[] Vertices;
	};

	List<RegionPolygon> regionPolygons = new();

	public RegionService()
	{
        var eqb = new EntityQueryBuilder(Allocator.Temp).
                  AddAll(ComponentType.ReadOnly<WorldRegionPolygon>()).
                  WithOptions(EntityQueryOptions.IncludeDisabled);

        var eq = Core.EntityManager.CreateEntityQuery(ref eqb);
        eqb.Dispose();

        var worldRegionPolygons = eq.ToEntityArray(Allocator.Temp);
        foreach (var worldRegionPolygonEntity in worldRegionPolygons)
		{
			var wrp = worldRegionPolygonEntity.Read<WorldRegionPolygon>();
			var vertices = Core.EntityManager.GetBuffer<WorldRegionPolygonVertex>(worldRegionPolygonEntity);

			regionPolygons.Add(
				new RegionPolygon
				{
					Region = wrp.WorldRegion,
					Aabb = wrp.PolygonBounds,
					Vertices = vertices.ToNativeArray(allocator: Allocator.Temp).ToArray().Select(x => x.VertexPos).ToArray()
				});
		}
        worldRegionPolygons.Dispose();
        eq.Dispose();
    }

	public WorldRegionType GetRegion(float3 pos)
	{
		foreach(var worldRegionPolygon in regionPolygons)
		{
			if (worldRegionPolygon.Aabb.Contains(pos))
			{
				if (IsPointInPolygon(worldRegionPolygon.Vertices, pos.xz))
				{
					return worldRegionPolygon.Region;
				}
			}
		}
		return WorldRegionType.None;
	}

	static bool IsPointInPolygon(float2[] polygon, Vector2 point)
	{
		int intersections = 0;
		int vertexCount = polygon.Length;

		for (int i = 0, j = vertexCount - 1; i < vertexCount; j = i++)
		{
			if ((polygon[i].y > point.y) != (polygon[j].y > point.y) &&
				(point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x))
			{
				intersections++;
			}
		}

		return intersections % 2 != 0;
	}


	internal class RegionConverter : JsonConverter<WorldRegionType>
	{
		public override WorldRegionType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType != JsonTokenType.String)
			{
				throw new JsonException();
			}

			reader.GetString();

			foreach(var value in Enum.GetValues<WorldRegionType>())
			{
				if (value.ToString() == reader.GetString())
				{
					return value;
				}
			}

			return WorldRegionType.None;
		}

		public override void Write(Utf8JsonWriter writer, WorldRegionType value, JsonSerializerOptions options)
		{
			writer.WriteStringValue(value.ToString());
		}
	}
}
