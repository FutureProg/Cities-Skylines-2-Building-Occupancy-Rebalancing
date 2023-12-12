using System.Collections.Generic;
using System.Linq;
using Colossal.UI;
using Game.SceneFlow;
using Game.Audio;
using Game.UI.Menu;
using HarmonyLib;
using System.Reflection.Emit;
using Game;
using BuildingOccupancyRebalancing.Systems;
using Unity.Entities;
using Game.Prefabs;
using Game.Zones;
using Game.Economy;
using Game.UI.InGame;
using Game.Buildings;
using Colossal.Entities;
using Game.Companies;

namespace BuildingOccupancyRebalancing.Patches {

    // This example patch adds the loading of a custom ECS System after the AudioManager has
    // its "OnGameLoadingComplete" method called. We're just using it as a entrypoint, and
    // it won't affect anything related to audio.
    [HarmonyPatch(typeof(BuildingInitializeSystem), "OnCreate")]
    class BuildingInitializeSystem_OnCreate
    {
        static void Postfix(BuildingInitializeSystem __instance)
        {
            // if (!mode.IsGameOrEditor())
            //     return;

            // Here we add our custom ECS System to the game's ECS World, so it's "online" at runtime
            // ((ComponentSystemBase)__instance).World.GetOrCreateSystemManaged<UpdateSystem>().UpdateAt<BuildingOccupancyRebalancingSystem>(SystemUpdatePhase.GameSimulation);
            ((ComponentSystemBase)__instance).World.GetOrCreateSystemManaged<UpdateSystem>().UpdateAt<OccupanyPrefabInitSystem>(SystemUpdatePhase.PrefabUpdate);
        }
    }

    [HarmonyPatch(typeof(BuildingPropertyData), "CountProperties", new System.Type[] {typeof(AreaType)})]
    class BuildingPropertyData_CountProperties {

        static bool Prefix(BuildingPropertyData __instance, ref int __result, AreaType areaType) {
            switch (areaType)
			{
                case AreaType.Residential:
                    return true;
                case AreaType.Commercial:
                    if (__instance.m_AllowedSold == Resource.NoResource)
                    {
                        __result =  0;
                        return false;
                    }
                    __result =  3;
                    return false;
                case AreaType.Industrial:
                    if (__instance.m_AllowedStored != Resource.NoResource)
                    {
                        __result = 3;
                        return false;
                    }
                    if (__instance.m_AllowedManufactured == Resource.NoResource)
                    {
                        __result = 0;
                        return false;
                    }
                    __result = 3;
                    return false;
                default:
                    __result = 0;
                    return false;                
			}            
        }
    }

    [HarmonyPatch(typeof(DeveloperInfoUISystem), "HasCompany")]
    class DeveloperInfoUISystem_HasCompany_Prefix {

        static bool Prefix(DeveloperInfoUISystem __instance, ref bool __result, Entity entity, Entity prefab) {           
            int renterCount = 0; 
			if (__instance.EntityManager.HasComponent<Renter>(entity) && __instance.EntityManager.HasComponent<BuildingPropertyData>(prefab) 
                && __instance.EntityManager.TryGetBuffer<Renter>(entity, true, out var dynamicBuffer) 
                && dynamicBuffer.Length > 0)
			{
				for (int i = 0; i < dynamicBuffer.Length; i++)
				{
					if (__instance.EntityManager.HasComponent<CompanyData>(dynamicBuffer[i].m_Renter))
					{
						renterCount++;				
					}
				}
                Plugin.Log.LogInfo($"Company Renter Count: {renterCount}");
                BuildingPropertyData propData = __instance.EntityManager.GetComponentData<BuildingPropertyData>(prefab);
                Plugin.Log.LogInfo($"Property Count: {propData.CountProperties()}");
                Plugin.Log.LogInfo($"==========");
			}            			
            return true;
        }

    }

    // This example patch enables the editor in the main menu
    // [HarmonyPatch(typeof(MenuUISystem), "IsEditorEnabled")]
    // class MenuUISystem_IsEditorEnabledPatch
    // {
    //     static bool Prefix(ref bool __result)
    //     {
    //         __result = true;

    //         return false; // Ignore original function
    //     }
    // }
    // Thanks to @89pleasure for the MenuUISystem_IsEditorEnabledPatch snippet above
    // https://github.com/89pleasure/cities2-mod-collection/blob/71385c000779c23b85e5cc023fd36022a06e9916/EditorEnabled/Patches/MenuUISystemPatches.cs
}