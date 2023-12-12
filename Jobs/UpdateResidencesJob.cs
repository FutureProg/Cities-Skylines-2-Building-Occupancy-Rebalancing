using System.Linq;
using Colossal.Serialization.Entities;
using Game;
using Game.Common;
using Game.Prefabs;
using Game.Simulation;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace BuildingOccupancyRebalancing.Jobs {
    
    [BurstCompile]
    public struct UpdateResidenceOccupancyJob : IJobChunk
    {
        public EntityTypeHandle entityHandle;
        public ComponentTypeHandle<BuildingPropertyData> buildingPropertyDataHandle;
        public ComponentTypeHandle<SpawnableBuildingData> spawnableBuildingDataHandle;            
        public ComponentTypeHandle<ObjectGeometryData> objectGeometryHandle;
        public ComponentTypeHandle<BuildingData> buildingDataHandle;                     

        public ComponentLookup<ZoneData> zoneDataLookup;

        public EntityCommandBuffer.ParallelWriter commandBuffer;

        public RandomSeed randomSeed;

        [BurstCompile]
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {   
            // Only doing this for High Density Office and High/Medium density residential
            // because we can't find the size of the actual building without looking at the mesh.
            // Will look for mesh access later on.
            var spawnBuildingDataArr = chunk.GetNativeArray(ref spawnableBuildingDataHandle);
            var buildingDataArr = chunk.GetNativeArray(ref buildingDataHandle);
            var buildingPropertyDataArr = chunk.GetNativeArray(ref buildingPropertyDataHandle);
            var objectGeometryArr = chunk.GetNativeArray(ref objectGeometryHandle);                
            var entityArr = chunk.GetNativeArray(entityHandle);
            var random = randomSeed.GetRandom(1);                                  

            int changed = 0;
            // Plugin.Log.LogInfo($"Updating {buildingDataArr.Count()} Items");                
            for (int i = 0; i < buildingDataArr.Length; i++) {                                                                               
                var spawnBuildingData = spawnBuildingDataArr[i];  
                var geom = objectGeometryArr[i];     
                var property = buildingPropertyDataArr[i];   
                var entity = entityArr[i];                                   
                if (spawnBuildingData.m_ZonePrefab == Entity.Null) {                    
                    Plugin.Log.LogInfo("Zone Prefab is null!");
                    continue;                    
                }
                if (!zoneDataLookup.TryGetComponent(spawnBuildingData.m_ZonePrefab, out var zonedata)) {
                    Plugin.Log.LogInfo("Zone Data not found!");
                    continue;                        
                }            
                bool isResidential = zonedata.m_AreaType == Game.Zones.AreaType.Residential;                    
                float width = geom.m_Size.x;
                float length = geom.m_Size.z;
                float height = geom.m_Size.y;                    
                if (isResidential) {                        
                    buildingPropertyDataArr[i] = UpdateResidential(unfilteredChunkIndex, width, length, height, random, zonedata, property, entity);   
                    changed += 1;                               
                }   
            }                 
            // Plugin.Log.LogInfo($"Successfully Updated {changed} Items!");              
        }

        BuildingPropertyData UpdateResidential(int unfilteredChunkIndex, float width, float length, float height, Unity.Mathematics.Random random, 
                ZoneData zonedata, BuildingPropertyData property, Entity prefab) 
        {
            bool is_singleFamilyResidence = property.m_ResidentialProperties == 1; // Probably safe assumption to make until we find something else                                      
            if (is_singleFamilyResidence) {
                // Plugin.Log.LogInfo("Skipping Single Family Residential\n=======");
                return property;
            }
            // Plugin.Log.LogInfo($"Default Residences {property.m_ResidentialProperties}");
            float RESIDENTIAL_HEIGHT = 3.5f;// 3.5 Metre Floor Height for residences (looks like the vanilla height)                                   
            float FOUNDATION_HEIGHT = 1.0f; // Looks like that'd be it? Only using this for row homes    
            float MIN_RESIDENCE_SIZE = 80;                    
            float MAX_RESIDENCE_SIZE = 111; // between 800sqft and 1200sqft  (80sqm and 111sqm)  

            bool is_RowHome = zonedata.m_ZoneFlags.HasFlag(ZoneFlags.SupportNarrow);
            if (is_RowHome) {                        
                float floorCount = (height-FOUNDATION_HEIGHT) / RESIDENTIAL_HEIGHT;                     
                property.m_ResidentialProperties = (int)math.floor(math.floor(floorCount) * 1.5f);// For Row Homes max 1.5 residences per floor. No basement                                                                           
            } else {
                var floorSize = width * length;
                int floorUnits = 0;
                if (height < 52) { // Ignore mid-rise buildings, they're usually fine
                    return property;
                }
                while(floorSize > MIN_RESIDENCE_SIZE+2) {                                                   
                    float maximum = floorSize < MAX_RESIDENCE_SIZE? floorSize : MAX_RESIDENCE_SIZE;                            
                    float minimum = MIN_RESIDENCE_SIZE;                            
                    floorSize -= random.NextFloat(minimum, maximum);
                    floorUnits++;
                }                                            
                var floorCount = (int)math.floor(height/RESIDENTIAL_HEIGHT);                                        
                floorCount -= 1; // Remove for the lobby floor 
                property.m_ResidentialProperties = floorUnits * floorCount;                            
            } 
            return property;                 
        }
    }
}