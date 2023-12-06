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
using Unity.Entities.UniversalDelegates;
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
                buildingDataLookup = m_buildingDataLookup,
                randomSeed = RandomSeed.Next()         
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

            public RandomSeed randomSeed;

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
                var random = randomSeed.GetRandom(1);                                                           
                var prefabRefs = chunk.GetNativeArray(ref prefabRefhandle);                           
                var underConstruction = chunk.GetNativeArray(ref underConstructionHandle);
                for (int i = 0; i < underConstruction.Length; i++) {                        
                    if (underConstruction[i].m_Progress < 100) continue;                                       
                    var prefab = underConstruction[i].m_NewPrefab;    
                    if (prefab == Entity.Null) prefab = prefabRefs[i].m_Prefab;
                    if (prefab == Entity.Null) {
                        Plugin.Log.LogInfo("Prefab is null!");
                        continue;                    
                    }
                    if (!spawnableBuildingDataLookup.TryGetComponent(prefab, out var spawnBuildingData) ||
                        !buildingDataLookup.TryGetComponent(prefab, out var buildingData) ||
                        !zoneDataLookup.TryGetComponent(spawnBuildingData.m_ZonePrefab, out var zonedata)) {
                        Plugin.Log.LogInfo("No Building Data");
                        continue;                        
                    }            

                    // Set the data
                    if(!objectGeometryLookup.TryGetComponent(prefab, out var geom)) {
                        Plugin.Log.LogInfo("No Object Geometry!");
                        continue;
                    }                                                            
                    float width = geom.m_Size.x;
                    float length = geom.m_Size.z;
                    float height = geom.m_Size.y;                    
                    if (isResidential) {
                        UpdateResidential(unfilteredChunkIndex, width, length, height, random, zonedata, prefab);
                    }
                    else if (isOffice) {
                        float OFFICE_HEIGHT = 4.0f;// 4 Metre Floor Height for offices      
                        float SPACE_PER_EMPLOYEE = 15; // 15 metres needed per employee
                        float COMPANIES_PER_FLOOR = 0.5f;
                        int floorCount = (int)math.floor(height / OFFICE_HEIGHT);
                    }
                    
                    // Plugin.Log.LogInfo($"Object Geometry: x {geom.m_Size.x}, y {geom.m_Size.y}, z {geom.m_Size.z}");
                    // Plugin.Log.LogInfo($"Building Lot Size: x {buildingData.m_LotSize.x}, y {buildingData.m_LotSize.y}");                                        

                    // Plugin.Log.LogInfo("Building Data Level: " + spawnBuildingData.m_Level);                    
                    // Plugin.Log.LogInfo("Zone AreaType " + zonedata.m_AreaType);
                    // Plugin.Log.LogInfo("Zone MaxHeight " + zonedata.m_MaxHeight);
                    // Plugin.Log.LogInfo("Zone ZoneFlags " + zonedata.m_ZoneFlags.ToString());
                    // Plugin.Log.LogInfo("Zone ZoneType Index" + zonedata.m_ZoneType.m_Index);                    
                    // Plugin.Log.LogInfo("====");
                }                                  
            }

            void UpdateResidential(int unfilteredChunkIndex, float width, float length, float height, Unity.Mathematics.Random random, 
                    ZoneData zonedata, Entity prefab) {
                if (!buildingPropertyDataLookup.TryGetComponent(prefab, out var property)) {
                    return;
                }
                bool is_singleFamilyResidence = property.m_ResidentialProperties == 1; // Probably safe assumption to make until we find something else                                      
                if (is_singleFamilyResidence) {
                    // Plugin.Log.LogInfo("Skipping Single Family Residential\n=======");
                    return;
                }
                // Plugin.Log.LogInfo($"Default Residences {property.m_ResidentialProperties}");
                float RESIDENTIAL_HEIGHT = 3.5f;// 3.5 Metre Floor Height for residences (looks like the vanilla height)                                   
                float FOUNDATION_HEIGHT = 1.0f; // Looks like that'd be it? Only using this for row homes    
                float MIN_RESIDENCE_SIZE = 80;                    
                float MAX_RESIDENCE_SIZE = 111; // between 800sqft and 1200sqft  (80sqm and 111sqm)  

                bool is_RowHome = zonedata.m_ZoneFlags.HasFlag(ZoneFlags.SupportNarrow);
                if (is_RowHome) {                        
                    float floorCount = (height-FOUNDATION_HEIGHT) / RESIDENTIAL_HEIGHT;                      
                    // Plugin.Log.LogInfo($"Floor count {floorCount}");                        
                    property.m_ResidentialProperties = (int)math.floor(math.floor(floorCount) * 1.5f);// For Row Homes max 1.5 residences per floor. No basement                                                                           
                } else {
                    var floorSize = width * length;
                    // Plugin.Log.LogInfo($"Floor Size: {floorSize}");
                    int floorUnits = 0;
                    if (height < 52) { // Ignore mid-rise buildings, they're usually fine
                        // Plugin.Log.LogInfo("Midrise, ignoring");
                        // Plugin.Log.LogInfo("===");
                        return;
                    }
                    while(floorSize > MIN_RESIDENCE_SIZE+2) {                                                   
                        float maximum = floorSize < MAX_RESIDENCE_SIZE? floorSize : MAX_RESIDENCE_SIZE;                            
                        float minimum = MIN_RESIDENCE_SIZE;                            
                        floorSize -= random.NextFloat(minimum, maximum);
                        floorUnits++;
                    }                        
                    // Plugin.Log.LogInfo($"Floor Units: {floorUnits}");
                    var floorCount = (int)math.floor(height/RESIDENTIAL_HEIGHT); 
                    // Plugin.Log.LogInfo($"Floor count {floorCount}");                           
                    floorCount -= 1; // Remove for the lobby floor 
                    property.m_ResidentialProperties = floorUnits * floorCount;
                    // Plugin.Log.LogInfo($"New Residential Property Count: {floorUnits * floorCount}");                                     
                } 
                commandBuffer.SetComponent(unfilteredChunkIndex, prefab, property); 
                // Plugin.Log.LogInfo($"New Residential Property Count: {property.m_ResidentialProperties}");      
            }
        }

    }        
}
