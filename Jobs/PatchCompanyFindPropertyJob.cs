using BuildingOccupancyRebalancing.Systems;
using Game.Agents;
using Game.Areas;
using Game.Buildings;
using Game.Citizens;
using Game.City;
using Game.Companies;
using Game.Economy;
using Game.Net;
using Game.Objects;
using Game.Pathfind;
using Game.Prefabs;
using Game.Simulation;
using Game.Triggers;
using Game.Zones;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static Game.Buildings.PropertyUtils;

namespace BuildingOccupancyRebalancing.Jobs {
    public struct PatchCompanyFindPropertyJob : IJobChunk
    {
        [ReadOnly]
        public EntityTypeHandle m_EntityType;

        public ComponentTypeHandle<CompanyData> m_CompanyDataType;

        [ReadOnly]
        public ComponentTypeHandle<PrefabRef> m_PrefabType;

        public ComponentTypeHandle<PropertySeeker> m_PropertySeekerType;

        [ReadOnly]
        public ComponentTypeHandle<Game.Companies.StorageCompany> m_StorageCompanyType;

        [ReadOnly]
        public NativeList<Entity> m_FreePropertyEntities;

        [ReadOnly]
        public NativeList<PrefabRef> m_PropertyPrefabs;

        [ReadOnly]
        public ComponentLookup<IndustrialProcessData> m_IndustrialProcessDatas;

        [ReadOnly]
        public ComponentLookup<BuildingPropertyData> m_BuildingPropertyDatas;

        [ReadOnly]
        public ComponentLookup<PropertyRenter> m_PropertyRenters;

        [ReadOnly]
        public BufferLookup<ResourceAvailability> m_Availabilities;

        [ReadOnly]
        public ComponentLookup<Building> m_Buildings;

        [ReadOnly]
        public ComponentLookup<PropertyOnMarket> m_PropertiesOnMarket;

        [ReadOnly]
        public ComponentLookup<PrefabRef> m_PrefabFromEntity;

        [ReadOnly]
        public ComponentLookup<BuildingData> m_BuildingDatas;

        [ReadOnly]
        public ComponentLookup<ResourceData> m_ResourceDatas;

        [ReadOnly]
        public ComponentLookup<SpawnableBuildingData> m_SpawnableDatas;

        [ReadOnly]
        public ComponentLookup<WorkplaceData> m_WorkplaceDatas;

        [ReadOnly]
        public ComponentLookup<LandValue> m_LandValues;

        [ReadOnly]
        public ComponentLookup<ServiceCompanyData> m_ServiceCompanies;

        [ReadOnly]
        public ComponentLookup<CommercialCompany> m_CommercialCompanies;

        [ReadOnly]
        public BufferLookup<Renter> m_Renters;

        [ReadOnly]
        public ResourcePrefabs m_ResourcePrefabs;

        public EconomyParameterData m_EconomyParameters;

        public ZonePreferenceData m_ZonePreferences;

        public bool m_Commercial;

        public NativeQueue<RentAction>.ParallelWriter m_RentQueue;

        public EntityCommandBuffer.ParallelWriter m_CommandBuffer;

        private void Evaluate(int index, Entity company, ref ServiceCompanyData service, ref IndustrialProcessData process, Entity property, ref PropertySeeker propertySeeker, bool commercial, bool storage)
        {
            float num = ((!commercial) ? IndustrialFindPropertySystem.Evaluate(company, property, ref process, ref propertySeeker, m_Buildings, m_PropertiesOnMarket, m_PrefabFromEntity, m_BuildingDatas, m_SpawnableDatas, m_WorkplaceDatas, m_LandValues, m_Availabilities, m_EconomyParameters, m_ResourcePrefabs, m_ResourceDatas, m_BuildingPropertyDatas, storage) : PatchCommercialFindPropertySystem.Evaluate(company, property, ref service, ref process, ref propertySeeker, m_Buildings, m_PrefabFromEntity, m_BuildingDatas, m_Availabilities, m_LandValues, m_ResourcePrefabs, m_ResourceDatas, m_BuildingPropertyDatas, m_SpawnableDatas, m_Renters, m_CommercialCompanies, ref m_ZonePreferences));
            if (propertySeeker.m_BestProperty == Entity.Null || num > propertySeeker.m_BestPropertyScore)
            {
                propertySeeker.m_BestPropertyScore = num;
                propertySeeker.m_BestProperty = property;
            }
        }

        private void SelectProperty(int jobIndex, Entity company, ref PropertySeeker propertySeeker, bool storage)
        {
            Entity bestProperty = propertySeeker.m_BestProperty;
            if (m_PropertiesOnMarket.HasComponent(bestProperty) && (!m_PropertyRenters.HasComponent(company) || !m_PropertyRenters[company].m_Property.Equals(bestProperty)))
            {
                m_RentQueue.Enqueue(new RentAction
                {
                    m_Property = bestProperty,
                    m_Renter = company,
                    m_Flags = (storage ? RentActionFlags.Storage : ((RentActionFlags)0))
                });
                m_CommandBuffer.RemoveComponent<PropertySeeker>(jobIndex, company);
            }
            else if (m_PropertyRenters.HasComponent(company))
            {
                m_CommandBuffer.RemoveComponent<PropertySeeker>(jobIndex, company);
            }
            else
            {
                propertySeeker.m_CurrentProperty = Entity.Null;
                propertySeeker.m_BestProperty = Entity.Null;
                propertySeeker.m_BestPropertyScore = 0f;
            }
        }

        private bool PropertyAllowsResource(int index, Resource resource, bool storage)
        {
            Entity prefab = m_PropertyPrefabs[index].m_Prefab;
            BuildingPropertyData buildingPropertyData = m_BuildingPropertyDatas[prefab];
            Resource resource2 = ((!storage) ? (m_Commercial ? buildingPropertyData.m_AllowedSold : buildingPropertyData.m_AllowedManufactured) : buildingPropertyData.m_AllowedStored);
            return (resource & resource2) != 0;
        }

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            NativeArray<Entity> nativeArray = chunk.GetNativeArray(m_EntityType);
            NativeArray<PrefabRef> nativeArray2 = chunk.GetNativeArray(ref m_PrefabType);
            NativeArray<PropertySeeker> nativeArray3 = chunk.GetNativeArray(ref m_PropertySeekerType);
            chunk.GetNativeArray(ref m_CompanyDataType);
            bool storage = chunk.Has(ref m_StorageCompanyType);
            for (int i = 0; i < nativeArray.Length; i++)
            {
                Entity entity = nativeArray[i];
                Entity prefab = nativeArray2[i].m_Prefab;
                if (!m_IndustrialProcessDatas.HasComponent(prefab))
                {
                    break;
                }

                IndustrialProcessData process = m_IndustrialProcessDatas[prefab];
                PropertySeeker propertySeeker = nativeArray3[i];
                Resource resource = process.m_Output.m_Resource;
                ServiceCompanyData service = default(ServiceCompanyData);
                if (m_Commercial)
                {
                    service = m_ServiceCompanies[prefab];
                }

                if (m_PropertyRenters.HasComponent(entity))
                {
                    Entity property = m_PropertyRenters[entity].m_Property;
                    Evaluate(i, entity, ref service, ref process, property, ref propertySeeker, m_Commercial, storage);
                }

                for (int j = 0; j < m_FreePropertyEntities.Length; j++)
                {
                    if (PropertyAllowsResource(j, resource, storage))
                    {
                        Evaluate(i, entity, ref service, ref process, m_FreePropertyEntities[j], ref propertySeeker, m_Commercial, storage);
                    }
                }

                SelectProperty(unfilteredChunkIndex, entity, ref propertySeeker, storage);
                nativeArray3[i] = propertySeeker;
            }
        }

        void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
        }
    }
}