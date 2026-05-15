using System;
using System.Collections.Generic;
using UnityEngine;

namespace CtF
{
    public readonly struct PointOfInterest
    {
        public readonly string Name;
        public readonly Vector2 Center; // XZ only
        public readonly float Radius;

        public PointOfInterest(string name, Vector2 center, float radius)
        {
            Name = name;
            Center = center;
            Radius = radius;
        }
    }

    public readonly struct MapConfig
    {
        public readonly Vector3 AttackingBase;
        public readonly Vector3 DefendingBase;
        public readonly float Radius;
        public readonly PointOfInterest[] POIs;

        public MapConfig(Vector3 attackingBase, Vector3 defendingBase, float radius, PointOfInterest[] pois = null)
        {
            AttackingBase = attackingBase;
            DefendingBase = defendingBase;
            Radius = radius;
            POIs = pois ?? Array.Empty<PointOfInterest>();
        }
    }

    public static class CtFMapPresets
    {
        // Case-insensitive lookup by map name.
        private static readonly Dictionary<string, MapConfig> _mapConfigs = new Dictionary<string, MapConfig>(StringComparer.OrdinalIgnoreCase)
        {
            {
                "AlKimarPyramids",
                new MapConfig(
                    attackingBase: new Vector3(144.28f, 13.62f, -151.10f),
                    defendingBase: new Vector3(-153.61f, 13.85f, 123.25f),
                    radius: 30f
                )
            },

            {
                "AlUddinRuins",
                new MapConfig(
                    attackingBase: new Vector3(-36.81f, 20.69f, 205.62f),
                    defendingBase: new Vector3(63.18f, 20.45f, -221.48f),
                    radius: 30f
                )
            },

            {
                "Antiquity",
                new MapConfig(
                    attackingBase: new Vector3(-118.85f, 9.83f, -126.68f),
                    defendingBase: new Vector3(153.66f, 8.68f, 203.02f),
                    radius: 30f
                )
            },

            {
                "Avignon",
                new MapConfig(
                    attackingBase: new Vector3(-84.78f, 10.66f, 181.81f),
                    defendingBase: new Vector3(109.51f, 9.99f, -188.18f),
                    radius: 30f
                )
            },

            {
                "BlackForest",
                new MapConfig(
                    attackingBase: new Vector3(-166.94f, 12.31f, -98.65f),
                    defendingBase: new Vector3(107.31f, 19.58f, 163.39f),
                    radius: 25f
                )
            },

            {
                "ChampsdAmbre",
                new MapConfig(
                    attackingBase: new Vector3(4.0f, 13.71f, -195.0f),
                    defendingBase: new Vector3(-0.5f, 13.71f, 230.0f),
                    radius: 30f,
                    pois: new[]
                    {
                        new PointOfInterest("Attacker Spawn",        new Vector2(  4.00f, -195.00f), 30.00f),
                        new PointOfInterest("Defending Spawn",       new Vector2( -0.50f,  230.00f), 30.00f),
                        new PointOfInterest("Camp",                  new Vector2(-28.51f,    7.65f), 30.41f),
                        new PointOfInterest("Farmhouse",             new Vector2(-107.28f, 123.38f), 24.03f),
                        new PointOfInterest("Farmhouse Field",       new Vector2(-149.97f, 138.41f), 34.21f),
                        new PointOfInterest("Delta",                 new Vector2(-209.40f,   6.83f), 103.05f),
                        new PointOfInterest("Ruined House",          new Vector2(-126.56f, -115.77f), 19.95f),
                        new PointOfInterest("Northeast Field",       new Vector2( 179.51f,  134.20f), 29.72f),
                        new PointOfInterest("Great Eastern Boulder", new Vector2( 126.29f,  -17.56f), 23.73f),
                        new PointOfInterest("Southeast Camp",        new Vector2( 162.65f,  -69.94f), 18.47f),
                    }
                )
            },

            {
                "CopperValley",
                new MapConfig(
                    attackingBase: new Vector3(-3.74f, -1.14f, -180.44f),
                    defendingBase: new Vector3(-2.54f, -0.13f, 183.21f),
                    radius: 20f
                )
            },

            {
                "CostaRelitto",
                new MapConfig(
                    attackingBase: new Vector3(21.51f, 6.72f, -220.48f),
                    defendingBase: new Vector3(-22.27f, 6.73f, 217.98f),
                    radius: 30f
                )
            },

            {
                "DedborgExpanse",
                new MapConfig(
                    attackingBase: new Vector3(176.27f, 6.70f, -14.34f),
                    defendingBase: new Vector3(-202.62f, 4.71f, -3.78f),
                    radius: 30f
                )
            },

            {
                "EdenCreek",
                new MapConfig(
                    attackingBase: new Vector3(-15.20f, 16.83f, -186.63f),
                    defendingBase: new Vector3(39.72f, 16.83f, 218.42f),
                    radius: 30f
                )
            },

            {
                "FausbergForest",
                new MapConfig(
                    attackingBase: new Vector3(135.22f, 9.45f, -60.25f),
                    defendingBase: new Vector3(-190.69f, 8.19f, 217.34f),
                    radius: 20f
                )
            },

            {
                "HudreeMarsh",
                new MapConfig(
                    attackingBase: new Vector3(10.52f, 4.37f, 225.31f),
                    defendingBase: new Vector3(24.06f, 4.37f, -172.32f),
                    radius: 20f
                )
            },

            {
                "KinglyLakes",
                new MapConfig(
                    attackingBase: new Vector3(183.98f, 48.98f, 200.59f),
                    defendingBase: new Vector3(-148.36f, 48.98f, -124.17f),
                    radius: 30f,
                    pois: new[]
                    {
                        new PointOfInterest("Attacker Spawn",           new Vector2( 183.98f,  200.59f), 30.00f),
                        new PointOfInterest("Defending Spawn",          new Vector2(-148.36f, -124.17f), 30.00f),
                        new PointOfInterest("Cabin",                    new Vector2(   8.56f,  154.18f), 24.67f),
                        new PointOfInterest("Pier",                     new Vector2(   4.84f,  109.89f), 15.21f),
                        new PointOfInterest("Frozen Lake",              new Vector2(  -3.95f,   65.03f), 73.58f),
                        new PointOfInterest("Windmill",                 new Vector2(  27.71f,   -8.36f), 12.46f),
                        new PointOfInterest("Frozen River East",        new Vector2(  72.06f,   32.71f), 19.85f),
                        new PointOfInterest("Frozen River East",        new Vector2( 101.14f,   18.89f), 18.67f),
                        new PointOfInterest("Frozen River East",        new Vector2( 131.10f,    6.57f), 19.91f),
                        new PointOfInterest("Frozen River East",        new Vector2( 164.22f,  -14.63f), 25.69f),
                        new PointOfInterest("Frozen River East",        new Vector2( 210.01f,  -19.15f), 20.92f),
                        new PointOfInterest("Frozen River South",       new Vector2( 150.98f,  -48.18f), 19.83f),
                        new PointOfInterest("Frozen River South",       new Vector2( 129.10f,  -83.81f), 19.49f),
                        new PointOfInterest("Frozen River South",       new Vector2( 104.92f, -112.70f), 26.83f),
                        new PointOfInterest("Frozen River South",       new Vector2(  77.19f, -145.90f), 25.67f),
                        new PointOfInterest("Frozen River South",       new Vector2(  36.80f, -187.22f), 25.04f),
                        new PointOfInterest("Northwest Hill",           new Vector2(-140.34f,   89.97f), 37.41f),
                        new PointOfInterest("Western Outlook Tower",    new Vector2(-111.48f,    9.54f), 17.74f),
                        new PointOfInterest("Southeast Outlook Tower",  new Vector2(  91.43f,  -39.05f), 26.29f),
                    }
                )
            },

            {
                "MontePilleronePass",
                new MapConfig(
                    attackingBase: new Vector3(7.81f, 60.77f, 227.24f),
                    defendingBase: new Vector3(31.24f, 62.15f, -175.46f),
                    radius: 30f
                )
            },

            {
                "NorstenGorge",
                new MapConfig(
                    attackingBase: new Vector3(247.79f, 7.16f, 132.41f),
                    defendingBase: new Vector3(-187.23f, 7.50f, 64.43f),
                    radius: 25f
                )
            },

            {
                "Oasis",
                new MapConfig(
                    attackingBase: new Vector3(1.98f, 7.10f, -105.74f),
                    defendingBase: new Vector3(12.23f, 7.34f, 109.27f),
                    radius: 30f
                )
            },

            {
                "PinefieldMarsh",
                new MapConfig(
                    attackingBase: new Vector3(-19.19f, 9.98f, -190.56f),
                    defendingBase: new Vector3(-0.17f, 10.99f, 166.42f),
                    radius: 30f
                )
            },

            {
                "PolarWoods",
                new MapConfig(
                    attackingBase: new Vector3(-0.30f, 19.26f, -84.76f),
                    defendingBase: new Vector3(3.16f, 21.24f, 137.36f),
                    radius: 30f
                )
            },

            {
                "TahirDesert",
                new MapConfig(
                    attackingBase: new Vector3(-118.52f, 8.30f, -150.19f),
                    defendingBase: new Vector3(108.78f, 12.00f, 174.94f),
                    radius: 25f,
                    pois: new[]
                    {
                        new PointOfInterest("Attacker Spawn", new Vector2(-118.52f, -150.19f), 30f),
                        new PointOfInterest("Defending Spawn", new Vector2(108.78f, 174.94f), 30f),
                        new PointOfInterest("Ruins",    new Vector2(-116.37f,  99.86f), 40.48f),
                        new PointOfInterest("Rotunda",  new Vector2(-131.01f,  77.78f), 10.06f),
                        new PointOfInterest("Oasis",    new Vector2(  35.32f,  -1.93f), 29.87f),
                        new PointOfInterest("Oasis",    new Vector2( -68.04f,  27.96f), 17.22f),
                        new PointOfInterest("Oasis",    new Vector2( -32.16f,  31.43f), 27.10f),
                        new PointOfInterest("Temple",   new Vector2(-124.08f, 127.82f), 29.89f),
                        new PointOfInterest("Aqueduct", new Vector2( 101.44f,-105.59f), 30.77f),
                        new PointOfInterest("Aqueduct", new Vector2( 135.56f,-107.14f), 29.34f),
                        new PointOfInterest("Aqueduct", new Vector2( 187.25f,-109.13f), 31.75f),
                    }
                )
            },

            {
                "VanoiseHeights",
                new MapConfig(
                    attackingBase: new Vector3(-141.48f, 87.78f, 60.21f),
                    defendingBase: new Vector3(232.39f, 88.79f, -23.23f),
                    radius: 30f
                )
            },

            {
                "WestmillBrook",
                new MapConfig(
                    attackingBase: new Vector3(172.08f, 55.28f, 18.88f),
                    defendingBase: new Vector3(206.07f, 54.88f, -14.64f),
                    radius: 30f,
                    pois: new[]
                    {
                        new PointOfInterest("Attacker Spawn",    new Vector2( 172.08f,   18.88f), 30.00f),
                        new PointOfInterest("Defending Spawn",   new Vector2( 206.07f,  -14.64f), 30.00f),
                        new PointOfInterest("Town Fields",       new Vector2(   0.78f,  105.55f), 48.94f),
                        new PointOfInterest("Town",              new Vector2(   6.73f,  148.02f), 26.31f),
                        new PointOfInterest("Watermill",         new Vector2( -24.04f,  -12.62f), 33.41f),
                        new PointOfInterest("Burning Windmill",  new Vector2(   7.42f,  -84.71f), 17.66f),
                        new PointOfInterest("Southeast Fields",  new Vector2(  96.77f, -193.20f), 39.70f),
                        new PointOfInterest("Southern Road",     new Vector2(   9.24f, -176.96f), 24.86f),
                        new PointOfInterest("Southwest Fields",  new Vector2( -89.09f, -190.65f), 26.57f),
                    }
                )
            },
        };

        public static bool TryGetMapConfig(string mapName, out MapConfig config)
        {
            return _mapConfigs.TryGetValue(mapName, out config);
        }
    }
}