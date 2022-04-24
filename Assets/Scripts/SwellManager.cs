using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TP.ExtensionMethods;
using System;
using System.Net.Http.Headers;
using JetBrains.Annotations;
using NWH.DWP2.DefaultWater;
using UnityEditor;
using UnityEngine.UIElements;
using Object = System.Object;

namespace Swell
{
    /**
     * @brief Static class that is the Accountant of all water, meshes, waves, and floaters.
     */
    [HelpURL("https://tinyphx.github.io/Swell/html/class_swell_1_1_swell_manager.html")]
    public static class SwellManager
    {
        // private static Dictionary<Type, List<T>> registered = new ();
        private static List<SwellWater> registeredWater = new List<SwellWater>();
        private static List<SwellFloater> registeredFloater = new List<SwellFloater>();
        private static List<SwellWave> registeredWave = new List<SwellWave>();
        private static SwellWater onlyWater = null;

        private static float searchTime;

        public static void Register(this SwellWater toRegister)
        {
            registeredWater.AddUnique(toRegister);
            onlyWater = registeredWater.Count == 1 ? toRegister : null;
        }
        public static void UnRegister(this SwellWater toUnRegister) { registeredWater.Remove(toUnRegister); }
        public static void Register(this SwellFloater toRegister) { registeredFloater.AddUnique(toRegister); }
        public static void UnRegister(this SwellFloater toUnRegister) { registeredFloater.Remove(toUnRegister); }

        public static void Register(this SwellWave toRegister) { registeredWave.AddUnique(toRegister); }

        public static void UnRegister(this SwellWave toUnRegister)
        {
            registeredWave.Remove(toUnRegister);

            if (!Application.isPlaying)
            {
                foreach (var water in AllWaters())
                {
                    water.EditorUpdate();
                }
            }
        }
        
        private static void UpdateAllRegistered()
        {
            if (!Application.isPlaying && (float)EditorApplication.timeSinceStartup - searchTime > 2)
            {
                searchTime = (float)EditorApplication.timeSinceStartup;
                
                registeredWater = UnityEngine.Object.FindObjectsOfType<SwellWater>().ToList();
                registeredFloater = UnityEngine.Object.FindObjectsOfType<SwellFloater>().ToList();
                registeredWave = UnityEngine.Object.FindObjectsOfType<SwellWave>().ToList();
            }
        }

        public static List<SwellWater> AllWaters()
        {
            UpdateAllRegistered();

            return registeredWater;
        }

        public static List<SwellFloater> AllFloaters()
        {
            UpdateAllRegistered();

            return registeredFloater;
        }

        public static List<SwellWave> AllWaves()
        {
            UpdateAllRegistered();

            return registeredWave;
        }
        
        public static SwellWater GetNearestWater(Vector3 position)
        {
            return onlyWater ? onlyWater : position.NearestComponent(AllWaters());
        }
    }
}