using System.Collections.Generic;
using Colossal.Entities;
using Colossal.UI.Binding;
using Game.Buildings;
using Game.Companies;
using Game.Economy;
using Game.Prefabs;
using Game.UI;
using Game.UI.InGame;
using Game.Zones;
using Unity.Entities;


namespace BuildingOccupancyRebalancing.Systems {

    public class ExtendedCompanyListSection : InfoSectionBase
    {
        protected override string group => "ExtendedCompanyList";
        List<CompanyInfo> companies;        

        public override void OnWriteProperties(IJsonWriter writer)
        {
            throw new System.NotImplementedException();
        }

        protected override void OnProcess()
        {
            List<Entity> companyEntities;
            SpawnableBuildingData spawnableBuildingData;
            bool hasCompanies = this.HasCompanies(out companyEntities);
            if (companyEntities.Count == 0 && EntityManager.TryGetComponent(this.selectedPrefab, out spawnableBuildingData)) {
                ZonePrefab zonePrefab = this.m_PrefabSystem.GetPrefab<ZonePrefab>(spawnableBuildingData.m_ZonePrefab);
                if (zonePrefab != null) {
                    AreaType areaType = zonePrefab.m_AreaType;
                    if (areaType != AreaType.Commercial) {
                        if (areaType == AreaType.Industrial) {
                            this.tooltipKeys.Add(zonePrefab.m_Office? "VacantOffice" : "VacantIndustrial");
                        }
                    } else {
                        this.tooltipKeys.Add("VacantCommercial");
                    }
                }
            }

            DynamicBuffer<Resources> dynamicBuffer;
            PrefabRef prefabRef;
            IndustrialProcessData industrialProcessData;
            for(int i = 0; i < companyEntities.Count; i++) {
                var companyEntity = companyEntities[i];
                CompanyInfo companyInfo = new CompanyInfo();
                companyInfo.companyEntity = companyEntity;
                if (this.EntityManager.TryGetBuffer(companyEntity, true, out dynamicBuffer) && this.EntityManager.TryGetComponent(companyEntity, out prefabRef) 
                    && this.EntityManager.TryGetComponent(prefabRef.m_Prefab, out industrialProcessData)) {
                    if (this.EntityManager.HasComponent<ServiceAvailable>(companyEntity)) {
                        Resource resource = industrialProcessData.m_Input1.m_Resource;
                        Resource resource2 = industrialProcessData.m_Input2.m_Resource;
                        Resource resource3 = industrialProcessData.m_Output.m_Resource;
                        if (resource != Resource.NoResource && resource != resource3)
                        {
                            companyInfo.input1 = resource;
                        }
                        if (resource2 != Resource.NoResource && resource2 != resource3 && resource2 != resource)
                        {
                            companyInfo.input2 = resource2;
                            base.tooltipKeys.Add("Requires");
                        }
                        companyInfo.sells = resource3;
                        base.tooltipKeys.Add("Sells");                        
                    }
                    else if (base.EntityManager.HasComponent<Game.Companies.ProcessingCompany>(companyInfo.companyEntity))
                    {
                        companyInfo.input1 = industrialProcessData.m_Input1.m_Resource;
                        companyInfo.input2 = industrialProcessData.m_Input2.m_Resource;
                        companyInfo.output = industrialProcessData.m_Output.m_Resource;
                        base.tooltipKeys.Add("Requires");
                        base.tooltipKeys.Add("Produces");                        
                    }
                    else if (base.EntityManager.HasComponent<Game.Companies.ExtractorCompany>(companyInfo.companyEntity))
                    {
                        companyInfo.output = industrialProcessData.m_Output.m_Resource;
                        base.tooltipKeys.Add("Produces");                        
                    }
                    else if (base.EntityManager.HasComponent<Game.Companies.StorageCompany>(companyInfo.companyEntity))
                    {
                        StorageCompanyData componentData = base.EntityManager.GetComponentData<StorageCompanyData>(prefabRef.m_Prefab);
                        companyInfo.stores = componentData.m_StoredResources;
                        base.tooltipKeys.Add("Stores");
                    }
                }
            }
        }

        private bool Visible() {            
            return this.HasCompanies(out var _);
        }

        private bool HasCompanies(out List<Entity> entities) {
            entities = new List<Entity>();
            if (EntityManager.HasComponent<Renter>(this.selectedEntity) && EntityManager.TryGetComponent<BuildingPropertyData>(this.selectedPrefab, out var component) && component.CountProperties(AreaType.Commercial) + component.CountProperties(AreaType.Industrial) > 0)
            {
                if (EntityManager.TryGetBuffer(this.selectedEntity, isReadOnly: true, out DynamicBuffer<Renter> buffer))
                {
                    for (int i = 0; i < buffer.Length; i++)
                    {
                        if (EntityManager.HasComponent<CompanyData>(buffer[i].m_Renter))
                        {
                            entities.Add(buffer[i].m_Renter);                            
                        }
                    }
                }
                return true;
            }
            return false;
        }

        protected override void Reset()
        {
            this.companies.Clear();
        }

        public struct CompanyInfo {
            public Entity companyEntity;
            public Resource input1;
            public Resource input2;
            public Resource output;
            public Resource sells;
            public Resource stores;

        }
    }

}