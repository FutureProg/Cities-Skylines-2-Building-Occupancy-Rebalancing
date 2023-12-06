using Colossal.Serialization.Entities;
using Game;
using Game.Buildings;
using Game.Common;
using Game.Objects;
using Game.Prefabs;
using Game.Simulation;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

namespace BuildingOccupancyRebalancing.Systems
{
    public class BuildingOccupancyRebalancingSystem : GameSystemBase
    {        

        EntityQuery m_Query; 
        ComponentTypeHandle<PrefabRef> m_prefabRefhandle;        
        ComponentTypeHandle<OfficeProperty> m_officePropertyHandle;        
        ComponentTypeHandle<ResidentialProperty> m_residentialPropertyHandle;
        ComponentTypeHandle<UnderConstruction> m_underConstructionHandle;

        ComponentLookup<BuildingPropertyData> m_buildingPropertyDataLookup;
        ComponentLookup<SpawnableBuildingData> m_spawnableBuildingDataLookup;
        ComponentLookup<ZoneData> m_zoneDataLookup;

        private EndFrameBarrier m_endFrameBarrier;
        bool m_active;

        public override int GetUpdateInterval(SystemUpdatePhase phase)
		{
			return 64;
		}

        protected override void OnCreate()
        {
            base.OnCreate();            
            // We'll focus on getting the building while it's under construction.
            EntityQueryBuilder builder = new EntityQueryBuilder(Allocator.Temp);            
            
            m_Query = builder.WithAll<UnderConstruction, PrefabRef>()
                .WithAny<ResidentialProperty, OfficeProperty>()                
                .Build(this.EntityManager);            

            m_prefabRefhandle = GetComponentTypeHandle<PrefabRef>(true);
            m_officePropertyHandle = GetComponentTypeHandle<OfficeProperty>(true);
            m_residentialPropertyHandle = GetComponentTypeHandle<ResidentialProperty>(true);
            m_underConstructionHandle = GetComponentTypeHandle<UnderConstruction>(true);

            m_buildingPropertyDataLookup = GetComponentLookup<BuildingPropertyData>(false);
            m_spawnableBuildingDataLookup = GetComponentLookup<SpawnableBuildingData>(true);
            m_zoneDataLookup = GetComponentLookup<ZoneData>(true);

            m_active = false;
            m_endFrameBarrier = World.GetOrCreateSystemManaged<EndFrameBarrier>();
            RequireForUpdate(m_Query);            
            World.GetOrCreateSystemManaged<ZoneSpawnSystem>().debugFastSpawn = true; // REMOVE FOR RELEASE
        }

        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);       
            m_active = mode.IsGame();     
            Debug.Log("Loaded");   
        }

        // private void CreateKeyBinding()
        // {
        //     var inputAction = new InputAction("MyModHotkeyPress");
        //     inputAction.AddBinding("<Keyboard>/n");
        //     inputAction.performed += OnHotkeyPress;
        //     inputAction.Enable();
        // }

        // private void OnHotkeyPress(InputAction.CallbackContext obj)
        // {
        //     UnityEngine.Debug.Log("You pressed the hotkey, very cool! Good job matey");
        // }

        protected override void OnUpdate() {     
            if (!m_active) return;               
            m_prefabRefhandle.Update(this);   
            m_officePropertyHandle.Update(this);
            m_residentialPropertyHandle.Update(this);
            m_underConstructionHandle.Update(this);
            m_spawnableBuildingDataLookup.Update(this);
            m_buildingPropertyDataLookup.Update(this);
            m_zoneDataLookup.Update(this);        

            var commandBuffer = m_endFrameBarrier.CreateCommandBuffer().AsParallelWriter();             
            var job = new UpdateResidenceOccupancyJob {
                commandBuffer = commandBuffer,
                prefabRefhandle = m_prefabRefhandle,
                officePropertyHandle = m_officePropertyHandle,
                residentialPropertyHandle = m_residentialPropertyHandle,
                underConstructionHandle = m_underConstructionHandle,
                spawnableBuildingDataLookup = m_spawnableBuildingDataLookup,
                buildingPropertyDataLookup = m_buildingPropertyDataLookup,
                zoneDataLookup = m_zoneDataLookup
            };                        
            this.Dependency = job.ScheduleParallel(m_Query, this.Dependency);            
            m_endFrameBarrier.AddJobHandleForProducer(Dependency);            
        }              

        [BurstCompile]
        public struct UpdateResidenceOccupancyJob : IJobChunk
        {
            public ComponentTypeHandle<PrefabRef> prefabRefhandle;        
            public ComponentTypeHandle<OfficeProperty> officePropertyHandle;        
            public ComponentTypeHandle<ResidentialProperty> residentialPropertyHandle;
            public ComponentTypeHandle<UnderConstruction> underConstructionHandle;
            public ComponentLookup<BuildingPropertyData> buildingPropertyDataLookup;
            public ComponentLookup<SpawnableBuildingData> spawnableBuildingDataLookup;
            public ComponentLookup<ZoneData> zoneDataLookup;

            public EntityCommandBuffer.ParallelWriter commandBuffer;

            [BurstCompile]
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {   
                var isResidential = chunk.Has(ref residentialPropertyHandle);
                var isOffice = chunk.Has(ref officePropertyHandle);
                if (!isResidential) {
                    return;
                }                                                        
                var prefabRefs = chunk.GetNativeArray(ref prefabRefhandle);                           
                var underConstruction = chunk.GetNativeArray(ref underConstructionHandle);
                for (int i = 0; i < underConstruction.Length; i++) {                        
                    if (underConstruction[i].m_Progress < 100) continue;
                    Plugin.Log.LogInfo($"Construction Complete! {i}");                    
                    var prefab = underConstruction[i].m_NewPrefab;    
                    if (prefab == Entity.Null) prefab = prefabRefs[i].m_Prefab;
                    if (prefab == Entity.Null) {
                        Plugin.Log.LogInfo("Prefab is null though...");
                        continue;                    
                    }
                    if (!buildingPropertyDataLookup.TryGetComponent(prefab, out var property) ||
                        !spawnableBuildingDataLookup.TryGetComponent(prefab, out var buildingData)) {
                        Plugin.Log.LogInfo("No Building Property Data");
                        continue;                        
                    }            

                    if (property.m_ResidentialProperties != 2) {                        
                        property.m_ResidentialProperties = 2;
                        commandBuffer.SetComponent(unfilteredChunkIndex, prefab, property);
                    }                                        
                    Plugin.Log.LogInfo("Building Data Level: " + buildingData.m_Level);
                    if (buildingData.m_ZonePrefab != Entity.Null && 
                        zoneDataLookup.TryGetComponent(buildingData.m_ZonePrefab, out var zonedata)) {                                                
                        Plugin.Log.LogInfo("Zone AreaType " + zonedata.m_AreaType);
                        Plugin.Log.LogInfo("Zone MaxHeight " + zonedata.m_MaxHeight);
                        Plugin.Log.LogInfo("Zone ZoneFlags " + zonedata.m_ZoneFlags.ToString());
                        Plugin.Log.LogInfo("Zone ZoneType Index" + zonedata.m_ZoneType.m_Index);
                    }
                }
            }
        }

    }        
}
