using System.Collections.Generic;
using UnityEngine;
using TP.ExtensionMethods;

public static class SwellManager
{
    private static List<SwellWater> allWaters = new List<SwellWater>();
    private static List<SwellWave> allWaves = new List<SwellWave>();
    private static List<SwellFloater> allFloaters = new List<SwellFloater>();

    public static List<SwellWater> AllWaters => allWaters;
    public static List<SwellWave> AllWaves => allWaves;
    public static List<SwellFloater> AllFloaters => allFloaters;

    public static void Register(SwellWater water)
    {
        AllWaters.Add(water);
    }
    
    public static void Register(SwellWave wave)
    {
        allWaves.Add(wave);
    }
    
    public static void Register(SwellFloater floater)
    {
        allFloaters.Add(floater);
    }

    public static SwellWater GetNearestWater(Vector3 position)
    {
        return position.NearestComponent(AllWaters);
    }
}