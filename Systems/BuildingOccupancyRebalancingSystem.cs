﻿using Colossal.Serialization.Entities;
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
using Unity.Mathematics;
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
        ComponentLookup<ObjectGeometryData> m_objectGeometryLookup;
        ComponentLookup<BuildingData> m_buildingDataLookup;        

        private EndFrameBarrier m_endFrameBarrier;
        bool m_active;

        public override int GetUpdateInterval(SystemUpdatePhase phase)
		{
			return 64; // same as BuildingConstructionSystem
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
            m_objectGeometryLookup = GetComponentLookup<ObjectGeometryData>(true);
            m_buildingDataLookup = GetComponentLookup<BuildingData>(true);            

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
            m_objectGeometryLookup.Update(this);
            m_buildingDataLookup.Update(this);            

            var commandBuffer = m_endFrameBarrier.CreateCommandBuffer().AsParallelWriter();             
            var job = new UpdateResidenceOccupancyJob {
                commandBuffer = commandBuffer,
                prefabRefhandle = m_prefabRefhandle,
                officePropertyHandle = m_officePropertyHandle,
                residentialPropertyHandle = m_residentialPropertyHandle,
                underConstructionHandle = m_underConstructionHandle,
                spawnableBuildingDataLookup = m_spawnableBuildingDataLookup,
                buildingPropertyDataLookup = m_buildingPropertyDataLookup,
                zoneDataLookup = m_zoneDataLookup,
                objectGeometryLookup = m_objectGeometryLookup,
                buildingDataLookup = m_buildingDataLookup                
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
            public ComponentLookup<ObjectGeometryData> objectGeometryLookup;
            public ComponentLookup<BuildingData> buildingDataLookup;            

            public EntityCommandBuffer.ParallelWriter commandBuffer;

            [BurstCompile]
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {   
                // Only doing this for High Density Office and High/Medium density residential
                // because we can't find the size of the actual building without looking at the mesh.
                // Will look for mesh access later on.
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
                        !spawnableBuildingDataLookup.TryGetComponent(prefab, out var spawnBuildingData) ||
                        !buildingDataLookup.TryGetComponent(prefab, out var buildingData) ||
                        !zoneDataLookup.TryGetComponent(spawnBuildingData.m_ZonePrefab, out var zonedata)) {
                        Plugin.Log.LogInfo("No Building Property Data");
                        continue;                        
                    }            

                    // Set the data
                    if(!objectGeometryLookup.TryGetComponent(prefab, out var geom)) {
                        Plugin.Log.LogInfo("No Object Geometry");
                        continue;
                    }
                    Plugin.Log.LogInfo($"Object Geometry: x {geom.m_Size.x}, y {geom.m_Size.y}, z {geom.m_Size.z}");
                    Plugin.Log.LogInfo($"Building Lot Size: x {buildingData.m_LotSize.x}, y {buildingData.m_LotSize.y}");                    
                    Plugin.Log.LogInfo($"Default Residences {property.m_ResidentialProperties}");
                    var RESIDENTIAL_HEIGHT = 4;// 4 Metre Floor Height for residences (looks like the vanilla height)
                    var OFFICE_HEIGHT = 4;// 4 Metre Floor Height for offices                                        
                    bool is_singleFamilyResidence = property.m_ResidentialProperties == 1; // Probably safe assumption to make until we find something else      
                    if (is_singleFamilyResidence) {
                        continue;
                    }            
                    float width = geom.m_Size.x;
                    float length = geom.m_Size.z;
                    float height = geom.m_Size.y;
                    bool is_RowHome = zonedata.m_ZoneFlags.HasFlag(ZoneFlags.SupportNarrow);
                    if (is_RowHome) {                      
                        property.m_ResidentialProperties = (int)math.floor((height/RESIDENTIAL_HEIGHT) * 1.5f);// For Row Homes max 1.5 residences per floor. No basement                                                   
                        commandBuffer.SetComponent(unfilteredChunkIndex, prefab, property);
                    } else {
                        var floorSize = width * length;
                        var floorUnits = (int) math.floor(floorSize/60); // For Medium & High Density, for now, we'll assume that there's one unit per 60 metres ()
                        var floorCount = (int)math.floor(height/RESIDENTIAL_HEIGHT); 
                        property.m_ResidentialProperties = floorUnits * floorCount;
                    }                                               
                    Plugin.Log.LogInfo("Building Data Level: " + spawnBuildingData.m_Level);                    
                    Plugin.Log.LogInfo("Zone AreaType " + zonedata.m_AreaType);
                    Plugin.Log.LogInfo("Zone MaxHeight " + zonedata.m_MaxHeight);
                    Plugin.Log.LogInfo("Zone ZoneFlags " + zonedata.m_ZoneFlags.ToString());
                    Plugin.Log.LogInfo("Zone ZoneType Index" + zonedata.m_ZoneType.m_Index);                    
                    Plugin.Log.LogInfo("====");
                }
            }
        }

    }        
}
