using Game.Buildings;
using Game.Companies;
using Game.Prefabs;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Entities;

namespace BuildingOccupancyRebalancing.Jobs {

    [BurstCompile]
    public struct UpdateOfficesJob : IJobChunk {
        public ComponentTypeHandle<PrefabRef> prefabRefHandle;
        public ComponentTypeHandle<WorkProvider> workProviderHandle;
        public ComponentLookup<ObjectGeometryData> objectGeometryLookup;
        public ComponentLookup<BuildingData> buildingDataLookup;

        public EntityCommandBuffer.ParallelWriter commandBuffer;
        public EntityTypeHandle entityTypeHandle;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask) {                
            var prefabList = chunk.GetNativeArray(ref prefabRefHandle);
            var workProviderList = chunk.GetNativeArray(ref workProviderHandle);   
            var entities = chunk.GetNativeArray(entityTypeHandle);
            for (int i = 0; i < prefabList.Length; i++) {
                Plugin.Log.LogInfo("Iterate");
                var prefab = prefabList[i];
                var workProvider = workProviderList[i];
                workProvider.m_MaxWorkers = 500;
                var entity = entities[i];
                commandBuffer.SetComponent(unfilteredChunkIndex, entity, workProvider);
            } 
        }
    }

}