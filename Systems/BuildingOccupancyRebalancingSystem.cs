using Colossal.Serialization.Entities;
using Game;
using Game.Buildings;
using Game.Objects;
using Game.Prefabs;
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

        ComponentLookup<BuildingPropertyData> m_buildingPropertyDataLookup;
        ComponentLookup<SpawnableBuildingData> m_spawnableBuildingDataLookup;
        ComponentLookup<ZoneData> m_zoneDataLookup;

        private EndFrameBarrier m_endFrameBarrier;
        bool m_active;

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

            m_buildingPropertyDataLookup = GetComponentLookup<BuildingPropertyData>(false);
            m_spawnableBuildingDataLookup = GetComponentLookup<SpawnableBuildingData>(true);
            m_zoneDataLookup = GetComponentLookup<ZoneData>(true);

            m_active = false;
            m_endFrameBarrier = World.GetOrCreateSystemManaged<EndFrameBarrier>();
            RequireForUpdate(m_Query);            
        }

        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);       
            m_active = mode.IsGame();        
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
            m_spawnableBuildingDataLookup.Update(this);
            m_buildingPropertyDataLookup.Update(this);
            m_zoneDataLookup.Update(this);        

            var commandBuffer = m_endFrameBarrier.CreateCommandBuffer().AsParallelWriter();             
            var job = new UpdateResidenceOccupancyJob {
                commandBuffer = commandBuffer,
                prefabRefhandle = m_prefabRefhandle,
                officePropertyHandle = m_officePropertyHandle,
                residentialPropertyHandle = m_residentialPropertyHandle,
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
                for (int i = 0; i < prefabRefs.Length; i++) {                    
                    var prefab = prefabRefs[i].m_Prefab;     
                    if (prefab == Entity.Null) continue;

                    if (!buildingPropertyDataLookup.TryGetComponent(prefab, out var property) ||
                        !spawnableBuildingDataLookup.TryGetComponent(prefab, out var buildingData)) {
                        continue;                        
                    }            

                    if (property.m_ResidentialProperties != 2) {                        
                        property.m_ResidentialProperties = 2;
                        commandBuffer.SetComponent(unfilteredChunkIndex, prefab, property);
                    }                                        
                    Debug.Log("Building Data Level: " + buildingData.m_Level);
                    if (buildingData.m_ZonePrefab != Entity.Null && 
                        zoneDataLookup.TryGetComponent(buildingData.m_ZonePrefab, out var zonedata)) {                                                
                        Debug.Log("Zone AreaType " + zonedata.m_AreaType);
                        Debug.Log("Zone MaxHeight " + zonedata.m_MaxHeight);
                        Debug.Log("Zone ZoneFlags " + zonedata.m_ZoneFlags.ToString());
                        Debug.Log("Zone ZoneType Index" + zonedata.m_ZoneType.m_Index);
                    }
                }
            }
        }

    }        
}
