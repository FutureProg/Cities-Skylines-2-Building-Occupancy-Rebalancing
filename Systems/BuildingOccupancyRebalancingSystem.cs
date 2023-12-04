using Colossal.Serialization.Entities;
using Game;
using Game.Audio;
using Game.Buildings;
using Game.Prefabs;
using Game.Simulation;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Scripting;

namespace BuildingOccupancyRebalancing.Systems
{
    public class BuildingOccupancyRebalancingSystem : GameSystemBase
    {
        // private SimulationSystem simulation;

        EntityQuery entityQuery;
        ComponentTypeHandle<BuildingPropertyData> m_propertyDataTypeHandle;  
        bool active;

        protected override void OnCreate()
        {
            base.OnCreate();            
            // Example on how to get a existing ECS System from the ECS World
            // this.simulation = World.GetExistingSystemManaged<SimulationSystem>();            
            entityQuery = GetEntityQuery(ComponentType.ReadWrite<BuildingPropertyData>());
            this.m_propertyDataTypeHandle = GetComponentTypeHandle<BuildingPropertyData>();
            active = false;
        }

        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);            
            UnityEngine.Debug.Log("Game has loaded!");
            active = mode.IsGame();        
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
            if (!this.active) return;   
            m_propertyDataTypeHandle.Update(this);
            var job = new SetResidenceCountZeroJob();
            job.buildingPropertyDataTypeHandle = m_propertyDataTypeHandle;
            this.Dependency = job.ScheduleParallel(entityQuery, this.Dependency);
        }              

        [BurstCompile]
        public struct SetResidenceCountZeroJob : IJobChunk
        {

            public ComponentTypeHandle<BuildingPropertyData> buildingPropertyDataTypeHandle;

            // public void Execute(ref BuildingPropertyData propertyData)
            // {            
            //     if (propertyData.m_ResidentialProperties > 0) {
            //         propertyData.m_ResidentialProperties = 0;
            //     }            
            // }

            [BurstCompile]
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var propertyList = chunk.GetNativeArray<BuildingPropertyData>(ref buildingPropertyDataTypeHandle);
                for(int i = 0; i <  propertyList.Length; i++) {                
                    if (propertyList[i].m_ResidentialProperties != 2) {
                        var nObj = propertyList[i];
                        nObj.m_ResidentialProperties = 2;
                        propertyList[i] = nObj;
                    } 
                }
            }
        }

    }        
}
