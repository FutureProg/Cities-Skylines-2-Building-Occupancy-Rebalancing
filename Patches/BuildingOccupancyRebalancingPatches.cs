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
using Game.Simulation;
using Game.Agents;
using Game.Net;
using Unity.Mathematics;

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

    [HarmonyPatch(typeof(CommercialFindPropertySystem), "Evaluate")]
    class CommercialFindPropertySystem_Evaluate_Postfix {

        static void Postfix(ref float __result, 
        Entity company, Entity property, ref ServiceCompanyData service, 
        ref IndustrialProcessData process, ref PropertySeeker propertySeeker, 
        ComponentLookup<Building> buildings, ComponentLookup<PrefabRef> prefabFromEntity, 
        ComponentLookup<BuildingData> buildingDatas, BufferLookup<ResourceAvailability> availabilities, 
        ComponentLookup<LandValue> landValues, ResourcePrefabs resourcePrefabs, 
        ComponentLookup<ResourceData> resourceDatas, ComponentLookup<BuildingPropertyData> propertyDatas, 
        ComponentLookup<SpawnableBuildingData> spawnableDatas, BufferLookup<Renter> renterBuffers, 
        ComponentLookup<CommercialCompany> companies, ref ZonePreferenceData preferences) {                
            if (__result == -1f && buildings.HasComponent(property)) 
			{
				Building building = buildings[property];
				Entity prefab = prefabFromEntity[property].m_Prefab;
				BuildingData buildingData = buildingDatas[prefab];
				BuildingPropertyData buildingPropertyData = propertyDatas[prefab];
				DynamicBuffer<Renter> dynamicBuffer = renterBuffers[property];
                // bool failedUnpatched = false;
				// for (int i = 0; i < dynamicBuffer.Length; i++)
				// {
				// 	if (companies.HasComponent(dynamicBuffer[i].m_Renter))
				// 	{
				// 		failedUnpatched = true;
                //         break;
				// 	}
				// }
                // if (!failedUnpatched) {
                //     return;
                // }

                float num = 500f;
				if (availabilities.HasBuffer(building.m_RoadEdge))
				{
					DynamicBuffer<ResourceAvailability> availabilities2 = availabilities[building.m_RoadEdge];
					float num2 = 0f;
					if (landValues.HasComponent(building.m_RoadEdge))
					{
						num2 = landValues[building.m_RoadEdge].m_LandValue;
					}
					float spaceMultiplier = buildingPropertyData.m_SpaceMultiplier;
					int level = (int)spawnableDatas[prefab].m_Level;
					num = ZoneEvaluationUtils.GetCommercialScore(availabilities2, building.m_CurvePosition, ref preferences, num2 / (spaceMultiplier * (1f + 0.5f * (float)level) * service.m_MaxWorkersPerCell), process.m_Output.m_Resource == Resource.Lodging);
					AvailableResource availableResourceSupply = EconomyUtils.GetAvailableResourceSupply(process.m_Input1.m_Resource);
					if (availableResourceSupply != AvailableResource.Count)
					{
						float weight = EconomyUtils.GetWeight(process.m_Input1.m_Resource, resourcePrefabs, ref resourceDatas);
						float marketPrice = EconomyUtils.GetMarketPrice(process.m_Output.m_Resource, resourcePrefabs, ref resourceDatas);
						float num3 = weight * (float)process.m_Input1.m_Amount / ((float)process.m_Output.m_Amount * marketPrice);
						num -= 200f * num3 / math.max(1f, NetUtils.GetAvailability(availabilities2, availableResourceSupply, building.m_CurvePosition));
					}
				}
				__result = num;
            }            
        }

    }

    [HarmonyPatch(typeof(DeveloperInfoUISystem), "HasCompany")]
    class DeveloperInfoUISystem_HasCompany_Prefix {

        static bool Prefix(DeveloperInfoUISystem __instance, ref bool __result, Entity entity, Entity prefab) {           
            int renterCount = 0; 
            Plugin.Log.LogInfo("Opening System");
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
			}  
            Plugin.Log.LogInfo($"Property on Market?: {__instance.EntityManager.HasComponent<PropertyOnMarket>(entity)}");
            Plugin.Log.LogInfo($"Property to be on Market?: {__instance.EntityManager.HasComponent<PropertyToBeOnMarket>(entity)}");
            if (__instance.EntityManager.HasComponent<BuildingPropertyData>(prefab)) {
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