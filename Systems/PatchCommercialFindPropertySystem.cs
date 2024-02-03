using System;
using System.Runtime.CompilerServices;
using BuildingOccupancyRebalancing.Jobs;
using Game;
using Game.Agents;
using Game.Areas;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Companies;
using Game.Economy;
using Game.Net;
using Game.Objects;
using Game.Pathfind;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Game.Triggers;
using Game.Zones;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Scripting;

namespace BuildingOccupancyRebalancing.Systems
{
    public class PatchCommercialFindPropertySystem : GameSystemBase
    {
        // Token: 0x06004E2A RID: 20010 RVA: 0x002A4D59 File Offset: 0x002A2F59
        public override int GetUpdateInterval(SystemUpdatePhase phase)
        {
            return 16;
        }

        // Token: 0x06004E2B RID: 20011 RVA: 0x002DF354 File Offset: 0x002DD554
        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            EntityManager.World.GetOrCreateSystemManaged<CommercialFindPropertySystem>().Enabled = false;
            this.m_RentQueue = new NativeQueue<PropertyUtils.RentAction>(Allocator.Persistent);
            this.m_ReservedProperties = new NativeList<Entity>(Allocator.Persistent);
            this.m_SimulationSystem = base.World.GetOrCreateSystemManaged<SimulationSystem>();
            this.m_EndFrameBarrier = base.World.GetOrCreateSystemManaged<EndFrameBarrier>();
            this.m_ResourceSystem = base.World.GetOrCreateSystemManaged<ResourceSystem>();
            this.m_TriggerSystem = base.World.GetOrCreateSystemManaged<TriggerSystem>();
            this.m_CityStatisticsSystem = base.World.GetOrCreateSystemManaged<CityStatisticsSystem>();
            this.m_RentEventArchetype = base.EntityManager.CreateArchetype(new ComponentType[]
            {
                ComponentType.ReadWrite<Event>(),
                ComponentType.ReadWrite<RentersUpdated>()
            });
            this.m_MovedEventArchetype = base.EntityManager.CreateArchetype(new ComponentType[]
            {
                ComponentType.ReadWrite<Event>(),
                ComponentType.ReadWrite<PathTargetMoved>()
            });
            this.m_CommerceQuery = base.GetEntityQuery(new ComponentType[]
            {
                ComponentType.ReadWrite<ServiceAvailable>(),
                ComponentType.ReadWrite<ResourceSeller>(),
                ComponentType.ReadWrite<CompanyData>(),
                ComponentType.ReadWrite<PropertySeeker>(),
                ComponentType.ReadOnly<PrefabRef>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>(),
                ComponentType.Exclude<Created>()
            });
            this.m_FreePropertyQuery = base.GetEntityQuery(new ComponentType[]
            {
                ComponentType.ReadWrite<PropertyOnMarket>(),
                ComponentType.ReadWrite<CommercialProperty>(),
                ComponentType.ReadOnly<PrefabRef>(),
                ComponentType.Exclude<Abandoned>(),
                ComponentType.Exclude<Condemned>(),
                ComponentType.Exclude<Destroyed>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>()
            });
            this.m_EconomyParameterQuery = base.GetEntityQuery(new ComponentType[]
            {
                ComponentType.ReadOnly<EconomyParameterData>()
            });
            this.m_ZonePreferenceQuery = base.GetEntityQuery(new ComponentType[]
            {
                ComponentType.ReadOnly<ZonePreferenceData>()
            });
            base.RequireForUpdate(this.m_CommerceQuery);
            base.RequireForUpdate(this.m_EconomyParameterQuery);
        }

        // Token: 0x06004E2C RID: 20012 RVA: 0x002DF57A File Offset: 0x002DD77A
        [Preserve]
        protected override void OnDestroy()
        {
            this.m_RentQueue.Dispose();
            this.m_ReservedProperties.Dispose();
            base.OnDestroy();
        }

        // Token: 0x06004E2D RID: 20013 RVA: 0x002DF598 File Offset: 0x002DD798
        [Preserve]
        protected override void OnUpdate()
        {
            if (this.m_CommerceQuery.CalculateEntityCount() > 0)
            {
                this.__TypeHandle.__Game_Buildings_Renter_RO_BufferLookup.Update(ref base.CheckedStateRef);
                this.__TypeHandle.__Game_Companies_CommercialCompany_RO_ComponentLookup.Update(ref base.CheckedStateRef);
                this.__TypeHandle.__Game_Prefabs_WorkplaceData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
                this.__TypeHandle.__Game_Prefabs_SpawnableBuildingData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
                this.__TypeHandle.__Game_Companies_ServiceCompanyData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
                this.__TypeHandle.__Game_Net_LandValue_RO_ComponentLookup.Update(ref base.CheckedStateRef);
                this.__TypeHandle.__Game_Prefabs_ResourceData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
                this.__TypeHandle.__Game_Buildings_PropertyRenter_RW_ComponentLookup.Update(ref base.CheckedStateRef);
                this.__TypeHandle.__Game_Buildings_Building_RO_ComponentLookup.Update(ref base.CheckedStateRef);
                this.__TypeHandle.__Game_Prefabs_BuildingData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
                this.__TypeHandle.__Game_Net_ResourceAvailability_RO_BufferLookup.Update(ref base.CheckedStateRef);
                this.__TypeHandle.__Game_Buildings_PropertyOnMarket_RO_ComponentLookup.Update(ref base.CheckedStateRef);
                this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentLookup.Update(ref base.CheckedStateRef);
                this.__TypeHandle.__Game_Prefabs_IndustrialProcessData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
                this.__TypeHandle.__Game_Prefabs_BuildingPropertyData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
                this.__TypeHandle.__Game_Companies_StorageCompany_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
                this.__TypeHandle.__Game_Companies_CompanyData_RW_ComponentTypeHandle.Update(ref base.CheckedStateRef);
                this.__TypeHandle.__Game_Agents_PropertySeeker_RW_ComponentTypeHandle.Update(ref base.CheckedStateRef);
                this.__TypeHandle.__Game_Prefabs_PrefabRef_RW_ComponentTypeHandle.Update(ref base.CheckedStateRef);
                this.__TypeHandle.__Unity_Entities_Entity_TypeHandle.Update(ref base.CheckedStateRef);
                JobHandle job;
                JobHandle job2;
                PatchCompanyFindPropertyJob jobData = new PatchCompanyFindPropertyJob
                {
                    m_EntityType = this.__TypeHandle.__Unity_Entities_Entity_TypeHandle,
                    m_PrefabType = this.__TypeHandle.__Game_Prefabs_PrefabRef_RW_ComponentTypeHandle,
                    m_PropertySeekerType = this.__TypeHandle.__Game_Agents_PropertySeeker_RW_ComponentTypeHandle,
                    m_CompanyDataType = this.__TypeHandle.__Game_Companies_CompanyData_RW_ComponentTypeHandle,
                    m_StorageCompanyType = this.__TypeHandle.__Game_Companies_StorageCompany_RO_ComponentTypeHandle,
                    m_FreePropertyEntities = this.m_FreePropertyQuery.ToEntityListAsync(base.World.UpdateAllocator.ToAllocator, out job),
                    m_PropertyPrefabs = this.m_FreePropertyQuery.ToComponentDataListAsync<PrefabRef>(base.World.UpdateAllocator.ToAllocator, out job2),
                    m_BuildingPropertyDatas = this.__TypeHandle.__Game_Prefabs_BuildingPropertyData_RO_ComponentLookup,
                    m_IndustrialProcessDatas = this.__TypeHandle.__Game_Prefabs_IndustrialProcessData_RO_ComponentLookup,
                    m_PrefabFromEntity = this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentLookup,
                    m_PropertiesOnMarket = this.__TypeHandle.__Game_Buildings_PropertyOnMarket_RO_ComponentLookup,
                    m_Availabilities = this.__TypeHandle.__Game_Net_ResourceAvailability_RO_BufferLookup,
                    m_BuildingDatas = this.__TypeHandle.__Game_Prefabs_BuildingData_RO_ComponentLookup,
                    m_Buildings = this.__TypeHandle.__Game_Buildings_Building_RO_ComponentLookup,
                    m_PropertyRenters = this.__TypeHandle.__Game_Buildings_PropertyRenter_RW_ComponentLookup,
                    m_ResourceDatas = this.__TypeHandle.__Game_Prefabs_ResourceData_RO_ComponentLookup,
                    m_LandValues = this.__TypeHandle.__Game_Net_LandValue_RO_ComponentLookup,
                    m_ServiceCompanies = this.__TypeHandle.__Game_Companies_ServiceCompanyData_RO_ComponentLookup,
                    m_SpawnableDatas = this.__TypeHandle.__Game_Prefabs_SpawnableBuildingData_RO_ComponentLookup,
                    m_WorkplaceDatas = this.__TypeHandle.__Game_Prefabs_WorkplaceData_RO_ComponentLookup,
                    m_CommercialCompanies = this.__TypeHandle.__Game_Companies_CommercialCompany_RO_ComponentLookup,
                    m_Renters = this.__TypeHandle.__Game_Buildings_Renter_RO_BufferLookup,
                    m_EconomyParameters = this.m_EconomyParameterQuery.GetSingleton<EconomyParameterData>(),
                    m_ZonePreferences = this.m_ZonePreferenceQuery.GetSingleton<ZonePreferenceData>(),
                    m_ResourcePrefabs = this.m_ResourceSystem.GetPrefabs(),
                    m_Commercial = true,
                    m_RentQueue = this.m_RentQueue.AsParallelWriter(),
                    m_CommandBuffer = this.m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter()
                };
                base.Dependency = jobData.ScheduleParallel(this.m_CommerceQuery, JobHandle.CombineDependencies(job, job2, base.Dependency));
                this.m_EndFrameBarrier.AddJobHandleForProducer(base.Dependency);
                this.m_ResourceSystem.AddPrefabsReader(base.Dependency);
                this.__TypeHandle.__Game_Buildings_PropertyRenter_RW_ComponentLookup.Update(ref base.CheckedStateRef);
                this.__TypeHandle.__Game_Prefabs_ResourceData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
                this.__TypeHandle.__Game_Areas_Lot_RO_ComponentLookup.Update(ref base.CheckedStateRef);
                this.__TypeHandle.__Game_Areas_Geometry_RO_ComponentLookup.Update(ref base.CheckedStateRef);
                this.__TypeHandle.__Game_Areas_SubArea_RO_BufferLookup.Update(ref base.CheckedStateRef);
                this.__TypeHandle.__Game_Companies_ExtractorCompany_RO_ComponentLookup.Update(ref base.CheckedStateRef);
                this.__TypeHandle.__Game_Objects_Attached_RO_ComponentLookup.Update(ref base.CheckedStateRef);
                this.__TypeHandle.__Game_Prefabs_SpawnableBuildingData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
                this.__TypeHandle.__Game_Companies_Employee_RO_BufferLookup.Update(ref base.CheckedStateRef);
                this.__TypeHandle.__Game_Buildings_Park_RO_ComponentLookup.Update(ref base.CheckedStateRef);
                this.__TypeHandle.__Game_Citizens_HomelessHousehold_RO_ComponentLookup.Update(ref base.CheckedStateRef);
                this.__TypeHandle.__Game_Buildings_Abandoned_RO_ComponentLookup.Update(ref base.CheckedStateRef);
                this.__TypeHandle.__Game_Citizens_HouseholdCitizen_RO_BufferLookup.Update(ref base.CheckedStateRef);
                this.__TypeHandle.__Game_Citizens_Citizen_RW_ComponentLookup.Update(ref base.CheckedStateRef);
                this.__TypeHandle.__Game_Companies_WorkProvider_RW_ComponentLookup.Update(ref base.CheckedStateRef);
                this.__TypeHandle.__Game_Prefabs_IndustrialProcessData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
                this.__TypeHandle.__Game_Companies_ServiceCompanyData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
                this.__TypeHandle.__Game_Prefabs_BuildingData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
                this.__TypeHandle.__Game_Companies_CommercialCompany_RO_ComponentLookup.Update(ref base.CheckedStateRef);
                this.__TypeHandle.__Game_Companies_IndustrialCompany_RO_ComponentLookup.Update(ref base.CheckedStateRef);
                this.__TypeHandle.__Game_Citizens_Household_RO_ComponentLookup.Update(ref base.CheckedStateRef);
                this.__TypeHandle.__Game_Companies_CompanyData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
                this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentLookup.Update(ref base.CheckedStateRef);
                this.__TypeHandle.__Game_Prefabs_ParkData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
                this.__TypeHandle.__Game_Prefabs_BuildingPropertyData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
                this.__TypeHandle.__Game_Buildings_Renter_RW_BufferLookup.Update(ref base.CheckedStateRef);
                this.__TypeHandle.__Game_Buildings_PropertyOnMarket_RO_ComponentLookup.Update(ref base.CheckedStateRef);
                JobHandle job3;
                PropertyUtils.RentJob jobData2 = new PropertyUtils.RentJob
                {
                    m_RentEventArchetype = this.m_RentEventArchetype,
                    m_MovedEventArchetype = this.m_MovedEventArchetype,
                    m_PropertiesOnMarket = this.__TypeHandle.__Game_Buildings_PropertyOnMarket_RO_ComponentLookup,
                    m_Renters = this.__TypeHandle.__Game_Buildings_Renter_RW_BufferLookup,
                    m_BuildingProperties = this.__TypeHandle.__Game_Prefabs_BuildingPropertyData_RO_ComponentLookup,
                    m_ParkDatas = this.__TypeHandle.__Game_Prefabs_ParkData_RO_ComponentLookup,
                    m_Prefabs = this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentLookup,
                    m_Companies = this.__TypeHandle.__Game_Companies_CompanyData_RO_ComponentLookup,
                    m_Households = this.__TypeHandle.__Game_Citizens_Household_RO_ComponentLookup,
                    m_Industrials = this.__TypeHandle.__Game_Companies_IndustrialCompany_RO_ComponentLookup,
                    m_Commercials = this.__TypeHandle.__Game_Companies_CommercialCompany_RO_ComponentLookup,
                    m_TriggerQueue = this.m_TriggerSystem.CreateActionBuffer(),
                    m_BuildingDatas = this.__TypeHandle.__Game_Prefabs_BuildingData_RO_ComponentLookup,
                    m_ServiceCompanyDatas = this.__TypeHandle.__Game_Companies_ServiceCompanyData_RO_ComponentLookup,
                    m_ProcessDatas = this.__TypeHandle.__Game_Prefabs_IndustrialProcessData_RO_ComponentLookup,
                    m_WorkProviders = this.__TypeHandle.__Game_Companies_WorkProvider_RW_ComponentLookup,
                    m_Citizens = this.__TypeHandle.__Game_Citizens_Citizen_RW_ComponentLookup,
                    m_HouseholdCitizens = this.__TypeHandle.__Game_Citizens_HouseholdCitizen_RO_BufferLookup,
                    m_Abandoneds = this.__TypeHandle.__Game_Buildings_Abandoned_RO_ComponentLookup,
                    m_HomelessHouseholds = this.__TypeHandle.__Game_Citizens_HomelessHousehold_RO_ComponentLookup,
                    m_Parks = this.__TypeHandle.__Game_Buildings_Park_RO_ComponentLookup,
                    m_Employees = this.__TypeHandle.__Game_Companies_Employee_RO_BufferLookup,
                    m_SpawnableBuildings = this.__TypeHandle.__Game_Prefabs_SpawnableBuildingData_RO_ComponentLookup,
                    m_Attacheds = this.__TypeHandle.__Game_Objects_Attached_RO_ComponentLookup,
                    m_ExtractorCompanies = this.__TypeHandle.__Game_Companies_ExtractorCompany_RO_ComponentLookup,
                    m_SubAreas = this.__TypeHandle.__Game_Areas_SubArea_RO_BufferLookup,
                    m_Geometries = this.__TypeHandle.__Game_Areas_Geometry_RO_ComponentLookup,
                    m_Lots = this.__TypeHandle.__Game_Areas_Lot_RO_ComponentLookup,
                    m_ResourcePrefabs = this.m_ResourceSystem.GetPrefabs(),
                    m_Resources = this.__TypeHandle.__Game_Prefabs_ResourceData_RO_ComponentLookup,
                    m_StatisticsQueue = this.m_CityStatisticsSystem.GetStatisticsEventQueue(out job3),
                    m_AreaType = Game.Zones.AreaType.Commercial,
                    m_PropertyRenters = this.__TypeHandle.__Game_Buildings_PropertyRenter_RW_ComponentLookup,
                    m_CommandBuffer = this.m_EndFrameBarrier.CreateCommandBuffer(),
                    m_RentQueue = this.m_RentQueue,
                    m_ReservedProperties = this.m_ReservedProperties
                };
                base.Dependency = jobData2.Schedule(JobHandle.CombineDependencies(base.Dependency, job3));
                this.m_CityStatisticsSystem.AddWriter(base.Dependency);
                this.m_TriggerSystem.AddActionBufferWriter(base.Dependency);
                this.m_EndFrameBarrier.AddJobHandleForProducer(base.Dependency);
            }
        }

        // Token: 0x06004E2E RID: 20014 RVA: 0x002DFEF4 File Offset: 0x002DE0F4
        public static float Evaluate(Entity company, Entity property, ref ServiceCompanyData service, ref IndustrialProcessData process, ref PropertySeeker propertySeeker, ComponentLookup<Building> buildings, ComponentLookup<PrefabRef> prefabFromEntity, ComponentLookup<BuildingData> buildingDatas, BufferLookup<ResourceAvailability> availabilities, ComponentLookup<LandValue> landValues, ResourcePrefabs resourcePrefabs, ComponentLookup<ResourceData> resourceDatas, ComponentLookup<BuildingPropertyData> propertyDatas, ComponentLookup<SpawnableBuildingData> spawnableDatas, BufferLookup<Renter> renterBuffers, ComponentLookup<CommercialCompany> companies, ref ZonePreferenceData preferences)
        {
            if (buildings.HasComponent(property))
            {
                Building building = buildings[property];
                Entity prefab = prefabFromEntity[property].m_Prefab;
                BuildingData buildingData = buildingDatas[prefab];
                BuildingPropertyData buildingPropertyData = propertyDatas[prefab];
                DynamicBuffer<Renter> dynamicBuffer = renterBuffers[property];
                // for (int i = 0; i < dynamicBuffer.Length; i++)
                // {
                //     if (companies.HasComponent(dynamicBuffer[i].m_Renter))
                //     {
                //         return -1f;
                //     }
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
                num = 300; //remove
                return num;
            }
            return -1f;
        }

        // Token: 0x06004E2F RID: 20015 RVA: 0x00002E1D File Offset: 0x0000101D
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void __AssignQueries(ref SystemState state)
        {
        }

        // Token: 0x06004E30 RID: 20016 RVA: 0x002E00AD File Offset: 0x002DE2AD
        protected override void OnCreateForCompiler()
        {
            base.OnCreateForCompiler();
            this.__AssignQueries(ref base.CheckedStateRef);
            this.__TypeHandle.__AssignHandles(ref base.CheckedStateRef);
        }

        // Token: 0x06004E31 RID: 20017 RVA: 0x00006953 File Offset: 0x00004B53
        [Preserve]
        public PatchCommercialFindPropertySystem()
        {
        }

        // Token: 0x04007AC1 RID: 31425
        private EntityQuery m_CommerceQuery;

        // Token: 0x04007AC2 RID: 31426
        private EntityQuery m_FreePropertyQuery;

        // Token: 0x04007AC3 RID: 31427
        private EntityQuery m_EconomyParameterQuery;

        // Token: 0x04007AC4 RID: 31428
        private EntityQuery m_ZonePreferenceQuery;

        // Token: 0x04007AC5 RID: 31429
        private SimulationSystem m_SimulationSystem;

        // Token: 0x04007AC6 RID: 31430
        private EndFrameBarrier m_EndFrameBarrier;

        // Token: 0x04007AC7 RID: 31431
        private ResourceSystem m_ResourceSystem;

        // Token: 0x04007AC8 RID: 31432
        private TriggerSystem m_TriggerSystem;

        // Token: 0x04007AC9 RID: 31433
        private CityStatisticsSystem m_CityStatisticsSystem;

        // Token: 0x04007ACA RID: 31434
        private NativeQueue<PropertyUtils.RentAction> m_RentQueue;

        // Token: 0x04007ACB RID: 31435
        private NativeList<Entity> m_ReservedProperties;

        // Token: 0x04007ACC RID: 31436
        private EntityArchetype m_RentEventArchetype;

        // Token: 0x04007ACD RID: 31437
        private EntityArchetype m_MovedEventArchetype;

        // Token: 0x04007ACE RID: 31438
        private PatchCommercialFindPropertySystem.TypeHandle __TypeHandle;

        // Token: 0x02001173 RID: 4467
        private struct TypeHandle
        {
            // Token: 0x06004E32 RID: 20018 RVA: 0x002E00D4 File Offset: 0x002DE2D4
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void __AssignHandles(ref SystemState state)
            {
                this.__Unity_Entities_Entity_TypeHandle = state.GetEntityTypeHandle();
                this.__Game_Prefabs_PrefabRef_RW_ComponentTypeHandle = state.GetComponentTypeHandle<PrefabRef>(false);
                this.__Game_Agents_PropertySeeker_RW_ComponentTypeHandle = state.GetComponentTypeHandle<PropertySeeker>(false);
                this.__Game_Companies_CompanyData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<CompanyData>(false);
                this.__Game_Companies_StorageCompany_RO_ComponentTypeHandle = state.GetComponentTypeHandle<Game.Companies.StorageCompany>(true);
                this.__Game_Prefabs_BuildingPropertyData_RO_ComponentLookup = state.GetComponentLookup<BuildingPropertyData>(true);
                this.__Game_Prefabs_IndustrialProcessData_RO_ComponentLookup = state.GetComponentLookup<IndustrialProcessData>(true);
                this.__Game_Prefabs_PrefabRef_RO_ComponentLookup = state.GetComponentLookup<PrefabRef>(true);
                this.__Game_Buildings_PropertyOnMarket_RO_ComponentLookup = state.GetComponentLookup<PropertyOnMarket>(true);
                this.__Game_Net_ResourceAvailability_RO_BufferLookup = state.GetBufferLookup<ResourceAvailability>(true);
                this.__Game_Prefabs_BuildingData_RO_ComponentLookup = state.GetComponentLookup<BuildingData>(true);
                this.__Game_Buildings_Building_RO_ComponentLookup = state.GetComponentLookup<Building>(true);
                this.__Game_Buildings_PropertyRenter_RW_ComponentLookup = state.GetComponentLookup<PropertyRenter>(false);
                this.__Game_Prefabs_ResourceData_RO_ComponentLookup = state.GetComponentLookup<ResourceData>(true);
                this.__Game_Net_LandValue_RO_ComponentLookup = state.GetComponentLookup<LandValue>(true);
                this.__Game_Companies_ServiceCompanyData_RO_ComponentLookup = state.GetComponentLookup<ServiceCompanyData>(true);
                this.__Game_Prefabs_SpawnableBuildingData_RO_ComponentLookup = state.GetComponentLookup<SpawnableBuildingData>(true);
                this.__Game_Prefabs_WorkplaceData_RO_ComponentLookup = state.GetComponentLookup<WorkplaceData>(true);
                this.__Game_Companies_CommercialCompany_RO_ComponentLookup = state.GetComponentLookup<CommercialCompany>(true);
                this.__Game_Buildings_Renter_RO_BufferLookup = state.GetBufferLookup<Renter>(true);
                this.__Game_Buildings_Renter_RW_BufferLookup = state.GetBufferLookup<Renter>(false);
                this.__Game_Prefabs_ParkData_RO_ComponentLookup = state.GetComponentLookup<ParkData>(true);
                this.__Game_Companies_CompanyData_RO_ComponentLookup = state.GetComponentLookup<CompanyData>(true);
                this.__Game_Citizens_Household_RO_ComponentLookup = state.GetComponentLookup<Household>(true);
                this.__Game_Companies_IndustrialCompany_RO_ComponentLookup = state.GetComponentLookup<IndustrialCompany>(true);
                this.__Game_Companies_WorkProvider_RW_ComponentLookup = state.GetComponentLookup<WorkProvider>(false);
                this.__Game_Citizens_Citizen_RW_ComponentLookup = state.GetComponentLookup<Citizen>(false);
                this.__Game_Citizens_HouseholdCitizen_RO_BufferLookup = state.GetBufferLookup<HouseholdCitizen>(true);
                this.__Game_Buildings_Abandoned_RO_ComponentLookup = state.GetComponentLookup<Abandoned>(true);
                this.__Game_Citizens_HomelessHousehold_RO_ComponentLookup = state.GetComponentLookup<HomelessHousehold>(true);
                this.__Game_Buildings_Park_RO_ComponentLookup = state.GetComponentLookup<Game.Buildings.Park>(true);
                this.__Game_Companies_Employee_RO_BufferLookup = state.GetBufferLookup<Employee>(true);
                this.__Game_Objects_Attached_RO_ComponentLookup = state.GetComponentLookup<Attached>(true);
                this.__Game_Companies_ExtractorCompany_RO_ComponentLookup = state.GetComponentLookup<Game.Companies.ExtractorCompany>(true);
                this.__Game_Areas_SubArea_RO_BufferLookup = state.GetBufferLookup<Game.Areas.SubArea>(true);
                this.__Game_Areas_Geometry_RO_ComponentLookup = state.GetComponentLookup<Geometry>(true);
                this.__Game_Areas_Lot_RO_ComponentLookup = state.GetComponentLookup<Game.Areas.Lot>(true);
            }

            // Token: 0x04007ACF RID: 31439
            [ReadOnly]
            public EntityTypeHandle __Unity_Entities_Entity_TypeHandle;

            // Token: 0x04007AD0 RID: 31440
            public ComponentTypeHandle<PrefabRef> __Game_Prefabs_PrefabRef_RW_ComponentTypeHandle;

            // Token: 0x04007AD1 RID: 31441
            public ComponentTypeHandle<PropertySeeker> __Game_Agents_PropertySeeker_RW_ComponentTypeHandle;

            // Token: 0x04007AD2 RID: 31442
            public ComponentTypeHandle<CompanyData> __Game_Companies_CompanyData_RW_ComponentTypeHandle;

            // Token: 0x04007AD3 RID: 31443
            [ReadOnly]
            public ComponentTypeHandle<Game.Companies.StorageCompany> __Game_Companies_StorageCompany_RO_ComponentTypeHandle;

            // Token: 0x04007AD4 RID: 31444
            [ReadOnly]
            public ComponentLookup<BuildingPropertyData> __Game_Prefabs_BuildingPropertyData_RO_ComponentLookup;

            // Token: 0x04007AD5 RID: 31445
            [ReadOnly]
            public ComponentLookup<IndustrialProcessData> __Game_Prefabs_IndustrialProcessData_RO_ComponentLookup;

            // Token: 0x04007AD6 RID: 31446
            [ReadOnly]
            public ComponentLookup<PrefabRef> __Game_Prefabs_PrefabRef_RO_ComponentLookup;

            // Token: 0x04007AD7 RID: 31447
            [ReadOnly]
            public ComponentLookup<PropertyOnMarket> __Game_Buildings_PropertyOnMarket_RO_ComponentLookup;

            // Token: 0x04007AD8 RID: 31448
            [ReadOnly]
            public BufferLookup<ResourceAvailability> __Game_Net_ResourceAvailability_RO_BufferLookup;

            // Token: 0x04007AD9 RID: 31449
            [ReadOnly]
            public ComponentLookup<BuildingData> __Game_Prefabs_BuildingData_RO_ComponentLookup;

            // Token: 0x04007ADA RID: 31450
            [ReadOnly]
            public ComponentLookup<Building> __Game_Buildings_Building_RO_ComponentLookup;

            // Token: 0x04007ADB RID: 31451
            public ComponentLookup<PropertyRenter> __Game_Buildings_PropertyRenter_RW_ComponentLookup;

            // Token: 0x04007ADC RID: 31452
            [ReadOnly]
            public ComponentLookup<ResourceData> __Game_Prefabs_ResourceData_RO_ComponentLookup;

            // Token: 0x04007ADD RID: 31453
            [ReadOnly]
            public ComponentLookup<LandValue> __Game_Net_LandValue_RO_ComponentLookup;

            // Token: 0x04007ADE RID: 31454
            [ReadOnly]
            public ComponentLookup<ServiceCompanyData> __Game_Companies_ServiceCompanyData_RO_ComponentLookup;

            // Token: 0x04007ADF RID: 31455
            [ReadOnly]
            public ComponentLookup<SpawnableBuildingData> __Game_Prefabs_SpawnableBuildingData_RO_ComponentLookup;

            // Token: 0x04007AE0 RID: 31456
            [ReadOnly]
            public ComponentLookup<WorkplaceData> __Game_Prefabs_WorkplaceData_RO_ComponentLookup;

            // Token: 0x04007AE1 RID: 31457
            [ReadOnly]
            public ComponentLookup<CommercialCompany> __Game_Companies_CommercialCompany_RO_ComponentLookup;

            // Token: 0x04007AE2 RID: 31458
            [ReadOnly]
            public BufferLookup<Renter> __Game_Buildings_Renter_RO_BufferLookup;

            // Token: 0x04007AE3 RID: 31459
            public BufferLookup<Renter> __Game_Buildings_Renter_RW_BufferLookup;

            // Token: 0x04007AE4 RID: 31460
            [ReadOnly]
            public ComponentLookup<ParkData> __Game_Prefabs_ParkData_RO_ComponentLookup;

            // Token: 0x04007AE5 RID: 31461
            [ReadOnly]
            public ComponentLookup<CompanyData> __Game_Companies_CompanyData_RO_ComponentLookup;

            // Token: 0x04007AE6 RID: 31462
            [ReadOnly]
            public ComponentLookup<Household> __Game_Citizens_Household_RO_ComponentLookup;

            // Token: 0x04007AE7 RID: 31463
            [ReadOnly]
            public ComponentLookup<IndustrialCompany> __Game_Companies_IndustrialCompany_RO_ComponentLookup;

            // Token: 0x04007AE8 RID: 31464
            public ComponentLookup<WorkProvider> __Game_Companies_WorkProvider_RW_ComponentLookup;

            // Token: 0x04007AE9 RID: 31465
            public ComponentLookup<Citizen> __Game_Citizens_Citizen_RW_ComponentLookup;

            // Token: 0x04007AEA RID: 31466
            [ReadOnly]
            public BufferLookup<HouseholdCitizen> __Game_Citizens_HouseholdCitizen_RO_BufferLookup;

            // Token: 0x04007AEB RID: 31467
            [ReadOnly]
            public ComponentLookup<Abandoned> __Game_Buildings_Abandoned_RO_ComponentLookup;

            // Token: 0x04007AEC RID: 31468
            [ReadOnly]
            public ComponentLookup<HomelessHousehold> __Game_Citizens_HomelessHousehold_RO_ComponentLookup;

            // Token: 0x04007AED RID: 31469
            [ReadOnly]
            public ComponentLookup<Game.Buildings.Park> __Game_Buildings_Park_RO_ComponentLookup;

            // Token: 0x04007AEE RID: 31470
            [ReadOnly]
            public BufferLookup<Employee> __Game_Companies_Employee_RO_BufferLookup;

            // Token: 0x04007AEF RID: 31471
            [ReadOnly]
            public ComponentLookup<Attached> __Game_Objects_Attached_RO_ComponentLookup;

            // Token: 0x04007AF0 RID: 31472
            [ReadOnly]
            public ComponentLookup<Game.Companies.ExtractorCompany> __Game_Companies_ExtractorCompany_RO_ComponentLookup;

            // Token: 0x04007AF1 RID: 31473
            [ReadOnly]
            public BufferLookup<Game.Areas.SubArea> __Game_Areas_SubArea_RO_BufferLookup;

            // Token: 0x04007AF2 RID: 31474
            [ReadOnly]
            public ComponentLookup<Geometry> __Game_Areas_Geometry_RO_ComponentLookup;

            // Token: 0x04007AF3 RID: 31475
            [ReadOnly]
            public ComponentLookup<Game.Areas.Lot> __Game_Areas_Lot_RO_ComponentLookup;
        }
    }
}