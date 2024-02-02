using Game;
using Game.Buildings;
using Game.Companies;
using Game.Prefabs;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace BuildingOccupancyRebalancing.Systems {

    public class PrintOfficeDetailsSystem : GameSystemBase {

        private Entity m_prevSelectedEntity;
        private ToolSystem m_toolSystem;
        private EntityQuery m_Query;

        public override int GetUpdateInterval(SystemUpdatePhase phase) {
            return 64;
        }

        protected override void OnCreate() {
            base.OnCreate();
            m_prevSelectedEntity = Entity.Null;
            m_toolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            EntityQueryBuilder builder = new EntityQueryBuilder(Allocator.Temp);
            m_Query = builder.WithAll<WorkProvider, PrefabRef>()
                .WithNone<Game.Buildings.ServiceUpgrade>()
                .Build(this.EntityManager);
        }

        protected override void OnUpdate()
        {            
            if (m_toolSystem.selected == m_prevSelectedEntity) {
                return;
            }
            m_prevSelectedEntity = m_toolSystem.selected;
            if (m_toolSystem.selected == Entity.Null) {
                return;
            }
            var selected = m_toolSystem.selected;
            if (m_Query.Matches(selected)) {
                
            }            
        }
    }

}