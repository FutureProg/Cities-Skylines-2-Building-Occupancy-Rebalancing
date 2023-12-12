using Colossal.Serialization.Entities;
using Game;
using Game.Common;
using Game.Prefabs;
using Game.Simulation;
using BuildingOccupancyRebalancing.Jobs;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace BuildingOccupancyRebalancing.Systems
{
    public class OccupanyPrefabInitSystem : GameSystemBase
    {        
                
        EntityQuery m_Query;                   
        EntityTypeHandle m_entityTypeHandle;

        ComponentTypeHandle<BuildingPropertyData> m_buildingPropertyDataHandle;
        ComponentTypeHandle<SpawnableBuildingData> m_spawnableBuildingDataHandle;
        ComponentLookup<ZoneData> m_zoneDataLookup;
        ComponentTypeHandle<ObjectGeometryData> m_objectGeometryHandle;
        ComponentTypeHandle<BuildingData> m_buildingDataHandle;                 

        protected override void OnCreate()
        {
            base.OnCreate();            
            // We'll focus on getting the building while it's under construction.
            EntityQueryBuilder builder = new EntityQueryBuilder(Allocator.Temp);            
            
            m_Query = builder.WithAll<PrefabData, BuildingData, BuildingPropertyData, SpawnableBuildingData, ObjectGeometryData>()
                // .WithAny<ResidentialProperty>()
                .Build(this.EntityManager);
            builder.Reset();

            // m_EmployerQuery = builder.WithAll<WorkProvider, PrefabRef>()
            //     .WithAny<Created, Updated>()
            //     .WithNone<Game.Buildings.ServiceUpgrade, Deleted, Temp, Game.Objects.OutsideConnection>()
            //     .Build(this.EntityManager);  // From WorkplaceInitializationSystem
            // builder.Dispose();

            m_entityTypeHandle = GetEntityTypeHandle();
            m_buildingPropertyDataHandle = GetComponentTypeHandle<BuildingPropertyData>(false);
            m_spawnableBuildingDataHandle = GetComponentTypeHandle<SpawnableBuildingData>(true);            
            m_objectGeometryHandle = GetComponentTypeHandle<ObjectGeometryData>(true);
            m_buildingDataHandle = GetComponentTypeHandle<BuildingData>(true);

            m_zoneDataLookup = GetComponentLookup<ZoneData>(true);
            World.GetOrCreateSystemManaged<ZoneSpawnSystem>().debugFastSpawn = true; // REMOVE FOR RELEASE                        
            RequireForUpdate(m_Query);                        
        }

        private void UpdateHandlesAndLookups() {
            m_spawnableBuildingDataHandle.Update(this);
            m_buildingPropertyDataHandle.Update(this);
            m_zoneDataLookup.Update(this);        
            m_objectGeometryHandle.Update(this);
            m_buildingDataHandle.Update(this);      
            m_entityTypeHandle.Update(this);
        }

        protected override void OnGamePreload(Purpose purpose, GameMode gameMode) {
            if (gameMode != GameMode.Game) {
                return;        
            }
            this.UpdateHandlesAndLookups();                             
            var commandBuffer = new EntityCommandBuffer(Allocator.TempJob);        
            var residentialJob = new UpdateResidenceOccupancyJob {
                entityHandle = m_entityTypeHandle,
                commandBuffer = commandBuffer.AsParallelWriter(),
                spawnableBuildingDataHandle = m_spawnableBuildingDataHandle,
                buildingPropertyDataHandle = m_buildingPropertyDataHandle,
                zoneDataLookup = m_zoneDataLookup,
                objectGeometryHandle = m_objectGeometryHandle,
                buildingDataHandle = m_buildingDataHandle,                
                randomSeed = RandomSeed.Next()       
            };                
            residentialJob.ScheduleParallel(m_Query, this.Dependency).Complete();                                        
        }
        
        protected override void OnUpdate()
        {            
            // Only Use When Game is Opened and Prefabs are Updated
        }          

    }        
}
