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
        private static readonly Dictionary<string, MapConfig> _mapConfigs =
            new Dictionary<string, MapConfig>(StringComparer.OrdinalIgnoreCase)
            {
                {
                    "ChampsdAmbre",
                    new MapConfig(
                        attackingBase: new Vector3(4.0f, 13.71f, -195.0f),
                        defendingBase: new Vector3(-0.5f, 13.71f, 230.0f),
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
                    "PinefieldMarsh",
                    new MapConfig(
                        attackingBase: new Vector3(-19.19f, 9.98f, -190.56f),
                        defendingBase: new Vector3(-0.17f, 10.99f, 166.42f),
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