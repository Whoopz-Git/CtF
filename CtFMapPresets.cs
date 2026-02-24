using System;
using System.Collections.Generic;
using UnityEngine;

namespace CtF
{
    public readonly struct MapConfig
    {
        public readonly Vector3 AttackingBase;
        public readonly Vector3 DefendingBase;
        public readonly float Radius;

        public MapConfig(Vector3 attackingBase, Vector3 defendingBase, float radius)
        {
            AttackingBase = attackingBase;
            DefendingBase = defendingBase;
            Radius = radius;
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
                    radius: 30f
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
                    radius: 30f
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
                    radius: 25f
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
                    radius: 30f
                )
            },
        };

        public static bool TryGetMapConfig(string mapName, out MapConfig config)
        {
            return _mapConfigs.TryGetValue(mapName, out config);
        }
    }
}