using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TP.ExtensionMethods;

namespace Swell
{
    public static class SwellManager
    {
        private static List<SwellWater> allWaters = new List<SwellWater>();
        private static List<SwellWave> allWaves = new List<SwellWave>();
        private static List<SwellFloater> allFloaters = new List<SwellFloater>();

        public static List<SwellWater> AllWaters => allWaters;
        public static List<SwellWave> AllWaves => allWaves;
        public static List<SwellFloater> AllFloaters => allFloaters;

        private static SwellWater onlyWater = null;

        public static void Register(SwellWater water)
        {
            if (AllWaters.Count == 0)
            {
                onlyWater = water;
            }
            else
            {
                onlyWater = null;
            }
            
            AllWaters.Add(water);
        }

        public static void Unregister(SwellWater water)
        {
            AllWaters.Remove(water);
        }

        public static void Register(SwellWave wave)
        {
            allWaves.Add(wave);
        }

        public static void Unregister(SwellWave wave)
        {
            allWaves.Remove(wave);
        }

        public static void Register(SwellFloater floater)
        {
            allFloaters.Add(floater);
        }

        public static void Unregister(SwellFloater floater)
        {
            allFloaters.Remove(floater);
        }

        public static SwellWater GetNearestWater(Vector3 position)
        {
            return onlyWater ? onlyWater : position.NearestComponent(AllWaters);
        }
    }
}