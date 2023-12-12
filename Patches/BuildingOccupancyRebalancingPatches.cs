﻿using System.Collections.Generic;
using System.Linq;
using Colossal.UI;
using Game.SceneFlow;
using Game.Audio;
using Game.UI.Menu;
using HarmonyLib;
using System.Reflection.Emit;
using Game;
using BuildingOccupancyRebalancing.Systems;
using Unity.Entities;
using Game.Prefabs;

namespace BuildingOccupancyRebalancing.Patches {

    // This example patch adds the loading of a custom ECS System after the AudioManager has
    // its "OnGameLoadingComplete" method called. We're just using it as a entrypoint, and
    // it won't affect anything related to audio.
    [HarmonyPatch(typeof(BuildingInitializeSystem), "OnCreate")]
    class AudioManager_OnCreate
    {
        static void Postfix(BuildingInitializeSystem __instance)
        {
            // if (!mode.IsGameOrEditor())
            //     return;

            // Here we add our custom ECS System to the game's ECS World, so it's "online" at runtime
            // ((ComponentSystemBase)__instance).World.GetOrCreateSystemManaged<UpdateSystem>().UpdateAt<BuildingOccupancyRebalancingSystem>(SystemUpdatePhase.GameSimulation);
            ((ComponentSystemBase)__instance).World.GetOrCreateSystemManaged<UpdateSystem>().UpdateAt<OccupanyPrefabInitSystem>(SystemUpdatePhase.PrefabUpdate);
        }
    }

    // This example patch enables the editor in the main menu
    // [HarmonyPatch(typeof(MenuUISystem), "IsEditorEnabled")]
    // class MenuUISystem_IsEditorEnabledPatch
    // {
    //     static bool Prefix(ref bool __result)
    //     {
    //         __result = true;

    //         return false; // Ignore original function
    //     }
    // }
    // Thanks to @89pleasure for the MenuUISystem_IsEditorEnabledPatch snippet above
    // https://github.com/89pleasure/cities2-mod-collection/blob/71385c000779c23b85e5cc023fd36022a06e9916/EditorEnabled/Patches/MenuUISystemPatches.cs
}