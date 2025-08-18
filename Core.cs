using BepInEx.Logging;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using KinPonds.Services;
using ProjectM;
using ProjectM.CastleBuilding;
using ProjectM.Physics;
using System.Collections;
using Unity.Entities;
using UnityEngine;

namespace KinPonds;

internal static class Core
{
    static World server;
    public static World Server => GetServer();

    static GenerateCastleSystem generateCastleSystem;
    public static GenerateCastleSystem GenerateCastle => generateCastleSystem ??= Server.GetExistingSystemManaged<GenerateCastleSystem>();
    public static PrefabCollectionSystem PrefabCollectionSystem { get; } = Server.GetExistingSystemManaged<PrefabCollectionSystem>();

    public static LocalizationService Localization { get; } = new();
    public static PondService Ponds { get; internal set; }
    public static RegionService Region { get; internal set; }

	static MonoBehaviour monoBehaviour;

    static World GetServer()
    {
        server ??= GetWorld("Server") ?? throw new System.Exception("There is no Server world (yet). Did you install a server mod on the client?");
        return server;
    }

	public static EntityManager EntityManager { get; } = Server.EntityManager;

	public static ManualLogSource Log { get; } = Plugin.LogInstance;

	internal static void InitializeAfterLoaded()
	{
		if (_hasInitialized) return;

        Ponds = new();
        Region = new();

        _hasInitialized = true;
		Log.LogInfo($"KinPonds initialized");
    }
	private static bool _hasInitialized = false;

	private static World GetWorld(string name)
	{
		foreach (var world in World.s_AllWorlds)
		{
			if (world.Name == name)
			{
				return world;
			}
		}

		return null;
    }

    public static Coroutine StartCoroutine(IEnumerator routine)
    {
        if (monoBehaviour == null)
        {
            var go = new GameObject("KindredCommands");
            monoBehaviour = go.AddComponent<IgnorePhysicsDebugSystem>();
            Object.DontDestroyOnLoad(go);
        }

        return monoBehaviour.StartCoroutine(routine.WrapToIl2Cpp());
    }

    public static void StopCoroutine(Coroutine coroutine)
    {
        if (monoBehaviour == null)
        {
            return;
        }

        monoBehaviour.StopCoroutine(coroutine);
    }
}
