using CtF;
using HoldfastSharedMethods;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public class CaptureTheFlag : IHoldfastSharedMethods
{

    private int CaptureTimeInSeconds = 60;
    private const int WarningSeconds = 30;
    private const float DefaultBaseRadius = 30f;

    // If set via config, overrides preset/default base radius for all maps
    private float? _baseRadiusOverride = null;

    private bool _isServer;
    private int _elapsedSeconds;

    private bool _attackingBasePosOverrideSet;
    private Vector3 _attackingBasePosOverride;

    private bool _defendingSpawnPosOverrideSet;
    private Vector3 _defendingSpawnPosOverride;

    private RoundInfo _roundDetails;
    private readonly Dictionary<int, PlayerState> _players = new Dictionary<int, PlayerState>();

    // Flags indexed both ways for fast lookup
    private readonly List<FlagState> _flags = new List<FlagState>();

    // Bases / capture zones per faction for this round (map-specific)
    private readonly Dictionary<FactionCountry, BaseZone> _basesByFaction = new Dictionary<FactionCountry, BaseZone>(8);

    // Mapping from faction flag object name in scene
    private readonly Dictionary<FactionCountry, string> _flagObjectName = new Dictionary<FactionCountry, string>
    {
        {FactionCountry.British,  "Flag_British_Interactable"},
        {FactionCountry.French,   "Flag_French_Interactable"},
        {FactionCountry.Prussian, "Flag_Prussian_Interactable"},
        {FactionCountry.Russian,  "Flag_Russian_Interactable"},
        {FactionCountry.Italian,  "Flag_Italian_Interactable"},
        {FactionCountry.Austrian, "Flag_Austrian_Interactable"},
    };

    // Workaround: Austrian flag carryable enum has no name. ToString() returns "54"
    private const int AustrianFlagCarryableRaw = 54;

    // Mapping from carryable flag enum to its faction
    private readonly Dictionary<CarryableObjectType, FactionCountry> _flagFactionByCarryable = new Dictionary<CarryableObjectType, FactionCountry>
    {
        { CarryableObjectType.FlagBritish,  FactionCountry.British },
        { CarryableObjectType.FlagFrench,   FactionCountry.French },
        { CarryableObjectType.FlagPrussian, FactionCountry.Prussian },
        { CarryableObjectType.FlagRussian,  FactionCountry.Russian },
        { CarryableObjectType.FlagItalian,  FactionCountry.Italian },
        // Austrian flag has no named enum; use its raw value (ToString() == "54")
        { (CarryableObjectType)AustrianFlagCarryableRaw, FactionCountry.Austrian },
    };

    // Data Types
    private class PlayerState
    {
        // From OnPlayerJoined
        public int PlayerId;
        public ulong SteamId;
        public string Name;
        public string RegimentTag;
        public bool IsBot;

        // From OnPlayerSpawned
        public GameObject PlayerObject;
        public int SpawnSectionId;
        public FactionCountry Faction;
        public PlayerClass PlayerClass;
        public int UniformId;
    }

    private sealed class RoundInfo
    {
        public int RoundId;
        public string ServerName;
        public string MapName;
        public GameplayMode GameplayMode;
        public GameType GameType;
        public FactionCountry AttackingFaction;
        public FactionCountry DefendingFaction;
    }

    private enum FlagCountdownState
    {
        None = 0,
        CountdownActive = 1,
        RoundEnded = 2
    }

    private sealed class FlagState
    {
        public FactionCountry FlagFaction;
        public GameObject FlagObject;

        public int CarrierPlayerId; // 0 means none/unknown
        public int baseDeadlineTime; // 0 means inactive
        public bool WarningSent;
        public FlagCountdownState CountdownState = FlagCountdownState.None;
    }

    private struct BaseZone
    {
        public Vector3 Center;
        public float Radius;

        public BaseZone(Vector3 center, float radius)
        {
            Center = center;
            Radius = radius;
        }
    }

    public void OnIsServer(bool server)
    {
        _isServer = server;
        // Currently Hard Coded. Config driven later.
        CtFLogger.SetEnabled(true);
        CommandExecutor.SetServerState(server);

        if (!server)
        {
            return;
        }
        CommandExecutor.InitializeConsole();
    }

    public void OnPlayerJoined(int playerId, ulong steamId, string name, string regimentTag, bool isBot)
    {
        var player = new PlayerState
        {
            PlayerId = playerId,
            SteamId = steamId,
            Name = name,
            RegimentTag = regimentTag,
            IsBot = isBot,

            PlayerObject = null,
            SpawnSectionId = -1,
            Faction = FactionCountry.None,
            PlayerClass = PlayerClass.None,
            UniformId = -1
        };

        _players[playerId] = player;
    }

    public void OnPlayerSpawned(int playerId, int spawnSectionId, FactionCountry playerFaction, PlayerClass playerClass, int uniformId, GameObject playerObject)
    {
        PlayerState player;
        if (!_players.TryGetValue(playerId, out player))
        {
            player = new PlayerState
            {
                PlayerId = playerId,
                SteamId = 0UL,
                Name = string.Empty,
                RegimentTag = string.Empty,
                IsBot = false
            };
            _players[playerId] = player;
        }

        player.PlayerObject = playerObject;
        player.SpawnSectionId = spawnSectionId;
        player.Faction = playerFaction;
        player.PlayerClass = playerClass;
        player.UniformId = uniformId;
    }

    public void OnPlayerLeft(int playerId)
    {
        _players.Remove(playerId);
    }

    public void OnRoundDetails(int roundId, string serverName, string mapName, FactionCountry attackingFaction, FactionCountry defendingFaction, GameplayMode gameplayMode, GameType gameType)
    {
        ResetRoundState();

        _roundDetails = new RoundInfo
        {
            RoundId = roundId,
            ServerName = serverName,
            MapName = mapName,
            GameplayMode = gameplayMode,
            GameType = gameType,
            AttackingFaction = attackingFaction,
            DefendingFaction = defendingFaction
        };

        // Register the two flags for the round
        TryRegisterFlag(attackingFaction);
        TryRegisterFlag(defendingFaction);

        // Configure bases for this map (attacking/defending only)
        SetupBasesForMap(mapName, attackingFaction, defendingFaction);
    }

    public void OnPlayerStartCarry(int playerId, CarryableObjectType carryableObject)
    {
        // Only handle flag carryables we know about
        if (!_flagFactionByCarryable.TryGetValue(carryableObject, out var flagFaction))
        {
            return;
        }

        // Find the corresponding FlagState for this faction
        FlagState flag = null;
        foreach (var f in _flags)
        {
            if (f.FlagFaction == flagFaction)
            {
                flag = f;
                break;
            }
        }

        if (flag == null)
        {
            CtFLogger.Error($"No FlagState found for faction {flagFaction}.");
            return;
        }

        // Mark who is carrying this flag
        flag.CarrierPlayerId = playerId;

        // Look up player for logging / capture decisions
        if (_players.TryGetValue(playerId, out var player) && player.Faction != FactionCountry.None)
        {
            if (player.Faction == flag.FlagFaction)
            {
                // Friendly picking up their own flag
                CtFLogger.Log($"{player.Name} picked up their own {flag.FlagFaction} flag.");
            }
            else
            {
                // Enemy picking up the flag: broadcast full capture message
                Broadcast($"The {flag.FlagFaction} flag has been captured by player {player.Name}!");
                CtFLogger.Log($"{player.Name} captured the {flag.FlagFaction} flag.");
            }
        }
        else
        {
            CtFLogger.Warn($"{player.Name} picked up {flag.FlagFaction} flag but faction is unknown.");
        }

        CtFLogger.Log($"{player.Name} is now carrying {flag.FlagFaction} flag.");
    }

    public void OnPlayerEndCarry(int playerId)
    {
        foreach (var flag in _flags)
        {
            if (flag.CarrierPlayerId == playerId)
            {
                string playerName = _players.TryGetValue(playerId, out var playerState) ? playerState.Name : null;
                flag.CarrierPlayerId = 0;
                CtFLogger.Log($"{playerState} is no longer carrying the {flag.FlagFaction} flag.");
            }
        }
    }

    public void OnUpdateElapsedTime(float time)
    {
        _elapsedSeconds = (int)time;

        foreach (var flag in _flags)
        {
            // Determine enemy faction and base zone for this flag
            var enemyFaction = GetOpponentFaction(flag.FlagFaction);
            if (enemyFaction == FactionCountry.None)
                continue;

            if (!_basesByFaction.TryGetValue(enemyFaction, out var enemyBase))
                continue;

            // Where is the flag right now (object or carrier)?
            var flagPos = GetFlagPosition(flag);
            bool inEnemyBase = IsWithinBase(flagPos, enemyBase);

            // Is it being carried by its own faction?
            bool carriedByOwner = false;
            if (flag.CarrierPlayerId != 0 &&
                _players.TryGetValue(flag.CarrierPlayerId, out var carrier) &&
                carrier.Faction != FactionCountry.None &&
                carrier.Faction == flag.FlagFaction)
            {
                carriedByOwner = true;
            }

            // For countdown purposes, treat the flag as "threatening" only if:
            // It's in the enemy base
            // It's NOT currently carried by its own faction
            bool shouldCountAsInEnemyBase = inEnemyBase && !carriedByOwner;

            // Entering enemy base (under enemy control): start countdown if not already running
            if (shouldCountAsInEnemyBase && flag.CountdownState == FlagCountdownState.None)
            {
                StartBaseCountdown(flag, CaptureTimeInSeconds);
            }

            // Either left enemy base OR is now carried by its owner in the base: cancel countdown
            else if (!shouldCountAsInEnemyBase && flag.CountdownState == FlagCountdownState.CountdownActive)
            {
                Broadcast($"The {flag.FlagFaction} flag is no longer in enemy control within their spawn. Capture cancelled.");
                CancelBaseCountdown(flag);
                continue; // no further countdown processing this tick
            }

            // No active countdown to update
            if (flag.baseDeadlineTime <= 0 || flag.CountdownState != FlagCountdownState.CountdownActive)
                continue;

            int remaining = flag.baseDeadlineTime - _elapsedSeconds;

            // Warning before round end
            if (!flag.WarningSent && remaining == WarningSeconds)
            {
                Broadcast($"The {flag.FlagFaction} flag is in the enemy spawn under enemy control! Only {WarningSeconds} seconds to recapture it!");
                flag.WarningSent = true;
            }

            // Time expired: end round
            if (_elapsedSeconds >= flag.baseDeadlineTime)
            {
                CtFLogger.Log("Flag base countdown reached deadline; ending round.");

                if (enemyFaction != FactionCountry.None)
                {
                    SetRoundWinner(enemyFaction);
                }
                else
                {
                    CtFLogger.Warn($"Could not determine opponent faction for {flag.FlagFaction}");
                }

                flag.CountdownState = FlagCountdownState.RoundEnded;
            }
        }
    }

    public void PassConfigVariables(string[] value)
    {
        if (value == null) return;

        foreach (var raw in value)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            var split = raw.Split(':');
            if (split.Length < 3)
                continue;

            var modId = split[0];
            if (!modId.Equals("CTF", StringComparison.OrdinalIgnoreCase))
                continue;

            var key = split[1];
            var arg = split[2];

            switch (key.ToLowerInvariant())
            {
                case "capturetime":
                    {
                        if (int.TryParse(arg, out var seconds) && seconds > 0)
                        {
                            CaptureTimeInSeconds = seconds;
                            CtFLogger.Log($"CaptureTime set to {CaptureTimeInSeconds} seconds.");
                        }
                        else
                        {
                            CtFLogger.Warn($"Invalid CaptureTime value '{arg}'. Must be a positive integer. Value set to deafult.");
                        }
                        break;
                    }
                case "baseradius":
                    {
                        if (float.TryParse(arg, out var radius) && radius > 0f)
                        {
                            _baseRadiusOverride = radius;
                            CtFLogger.Log($"BaseRadius set to {_baseRadiusOverride.Value}.");
                        }
                        else
                        {
                            CtFLogger.Warn($"Invalid BaseRadius value '{arg}'. Must be a positive number.");
                        }
                        break;
                    }

                case "attackingbaseposition":
                    {
                        if (TryParseVector3(arg, out var pos))
                        {
                            _attackingBasePosOverrideSet = true;
                            _attackingBasePosOverride = pos;
                            CtFLogger.Warn($"AttackingBasePosition override set to {pos}");
                        }
                        else
                        {
                            CtFLogger.Warn($"Invalid AttackingBasePosition '{arg}'. Expected floats in format x,y,z");
                        }
                        break;
                    }

                case "defendingspawnposition":
                    {
                        if (TryParseVector3(arg, out var pos))
                        {
                            _defendingSpawnPosOverrideSet = true;
                            _defendingSpawnPosOverride = pos;
                            CtFLogger.Warn($"DefendingSpawnPosition override set to {pos}");
                        }
                        else
                        {
                            CtFLogger.Warn($"Invalid DefendingSpawnPosition '{arg}'. Expected floats in format x,y,z");
                        }
                        break;
                    }
            }
        }
    }

    //Helpers
    private void ResetRoundState()
    {
        _elapsedSeconds = 0;

        _flags.Clear();
        _basesByFaction.Clear();

        _roundDetails = null;
    }

    private void TryRegisterFlag(FactionCountry faction)
    {
        if (faction == FactionCountry.None)
            return;

        string objectName;
        if (!_flagObjectName.TryGetValue(faction, out objectName) || string.IsNullOrEmpty(objectName))
        {
            CtFLogger.Warn($"No flag object mapping for faction {faction}.");
            return;
        }

        var flagObj = GameObject.Find(objectName);
        if (flagObj == null)
        {
            var mapName = _roundDetails != null ? _roundDetails.MapName : "unknown";
            CtFLogger.Warn( $"Could not find flag object '{objectName}' for faction {faction} on map '{mapName}'.");
            return;
        }

        var flag = new FlagState
        {
            FlagFaction = faction,
            FlagObject = flagObj,
            CarrierPlayerId = 0,
            baseDeadlineTime = 0,
            WarningSent = false,
            CountdownState = FlagCountdownState.None
        };

        _flags.Add(flag);

        CtFLogger.Log($"Registered flag for faction {faction} (object '{objectName}').");
    }

    private void SetupBasesForMap(string mapName, FactionCountry attackingFaction, FactionCountry defendingFaction)
    {
        CtF.MapConfig cfg;
        bool hasPreset = CtFMapPresets.TryGetMapConfig(mapName, out cfg);

        float radius = _baseRadiusOverride ?? (hasPreset ? cfg.Radius : DefaultBaseRadius);

        Vector3 attackingPos;
        Vector3 defendingPos;

        if (_attackingBasePosOverrideSet || _defendingSpawnPosOverrideSet)
        {
            attackingPos = _attackingBasePosOverrideSet
                ? _attackingBasePosOverride
                : (hasPreset ? cfg.AttackingBase : Vector3.zero);

            defendingPos = _defendingSpawnPosOverrideSet
                ? _defendingSpawnPosOverride
                : (hasPreset ? cfg.DefendingBase : Vector3.zero);
        }
        else if (hasPreset)
        {
            attackingPos = cfg.AttackingBase;
            defendingPos = cfg.DefendingBase;
        }
        else
        {
            attackingPos = Vector3.zero;
            defendingPos = Vector3.zero;
        }

        _basesByFaction[attackingFaction] = new BaseZone(attackingPos, radius);
        _basesByFaction[defendingFaction] = new BaseZone(defendingPos, radius);

        var reason = _baseRadiusOverride.HasValue ? "config override" : (hasPreset ? "preset" : "default");

        if (hasPreset)
        {
            CtFLogger.Log($"Configured bases for '{mapName}' (attacker: {attackingFaction}, defender: {defendingFaction}, radius: {radius} via {reason}).");
        }
        else
        {
            CtFLogger.Warn($"No configuration for map '{mapName}'. Using bases at origin with radius {radius} ({reason}).");
        }
    }

    private FactionCountry GetOpponentFaction(FactionCountry faction)
    {
        if (faction == FactionCountry.None) return FactionCountry.None;
        if (_roundDetails == null) return FactionCountry.None;

        if (faction == _roundDetails.AttackingFaction)
            return _roundDetails.DefendingFaction;

        if (faction == _roundDetails.DefendingFaction)
            return _roundDetails.AttackingFaction;

        return FactionCountry.None;
    }

    private static bool IsWithinBase(Vector3 pos, BaseZone zone)
    {
        var a = new Vector2(pos.x, pos.z);
        var b = new Vector2(zone.Center.x, zone.Center.z);
        return Vector2.Distance(a, b) < zone.Radius;
    }

    private void StartBaseCountdown(FlagState flag, int seconds)
    {
        flag.baseDeadlineTime = _elapsedSeconds + seconds;
        flag.WarningSent = false;
        flag.CountdownState = FlagCountdownState.CountdownActive;

        Broadcast($"The {flag.FlagFaction} flag is in the enemy spawn! You have {seconds} seconds to get it out.");
    }

    private void CancelBaseCountdown(FlagState flag)
    {
        flag.baseDeadlineTime = 0;
        flag.WarningSent = false;
        flag.CountdownState = FlagCountdownState.None;
    }

    private void Broadcast(string message)
    {
        CommandExecutor.ExecuteCommand("broadcast " + message);
    }

    private void SetRoundWinner(FactionCountry winner)
    {
        if (winner == FactionCountry.None) return;
        CtFLogger.Log($"SetRoundWinner: {winner} won the round");
        CommandExecutor.ExecuteCommand(string.Format("set roundEndFactionWin {0} None", winner));
    }

    public bool getIsServer()
    {
        return _isServer;
    }

    private Vector3 GetFlagPosition(FlagState flag)
    {
        // If we know who is carrying it and that player's object exists,
        // treat the player's position as the flag position.
        if (flag.CarrierPlayerId != 0 && _players.TryGetValue(flag.CarrierPlayerId, out var carrier) && carrier.PlayerObject != null)
        {
            return carrier.PlayerObject.transform.position;
        }

        // Otherwise, fall back to the flag object's position.
        return flag.FlagObject.transform.position;
    }

    private static bool TryParseVector3(string s, out Vector3 v)
    {
        v = default;

        if (string.IsNullOrWhiteSpace(s))
            return false;

        var parts = s.Split(',');
        if (parts.Length != 3)
            return false;

        var style = NumberStyles.Float | NumberStyles.AllowThousands;
        var ci = CultureInfo.InvariantCulture;

        if (!float.TryParse(parts[0].Trim(), style, ci, out var x)) return false;
        if (!float.TryParse(parts[1].Trim(), style, ci, out var y)) return false;
        if (!float.TryParse(parts[2].Trim(), style, ci, out var z)) return false;

        v = new Vector3(x, y, z);
        return true;
    }

    //Unused interface methods
    public void OnInteractableObjectInteraction(int playerId, int interactableObjectId, GameObject interactableObject, InteractionActivationType interactionActivationType, int nextActivationStateTransitionIndex) { }
    public void OnPlayerKilledPlayer(int killerPlayerId, int victimPlayerId, EntityHealthChangedReason reason, string additionalDetails) { }
    public void OnPlayerBlock(int attackingPlayerId, int defendingPlayerId) { }
    public void OnScorableAction(int playerId, int score, ScorableActionType reason) { }
    public void OnPlayerHurt(int playerId, byte oldHp, byte newHp, EntityHealthChangedReason reason) { }
    public void OnIsClient(bool client, ulong steamId) { }
    public void OnUpdateTimeRemaining(float time) { }
    public void OnPlayerWeaponSwitch(int playerId, string weapon) { }
    public void OnTextMessage(int playerId, TextChatChannel channel, string text) { }
    public void OnConsoleCommand(string input, string output, bool success) { }
    public void OnSyncValueState(int value) { }
    public void OnUpdateSyncedTime(double time) { }
    public void OnDamageableObjectDamaged(GameObject damageableObject, int damageableObjectId, int shipId, int oldHp, int newHp) { }
    public void OnPlayerShoot(int playerId, bool dryShot) { }
    public void OnPlayerMeleeStartSecondaryAttack(int playerId) { }
    public void OnCapturePointCaptured(int capturePoint) { }
    public void OnCapturePointOwnerChanged(int capturePoint, FactionCountry factionCountry) { }
    public void OnCapturePointDataUpdated(int capturePoint, int defendingPlayerCount, int attackingPlayerCount) { }
    public void OnRoundEndFactionWinner(FactionCountry factionCountry, FactionRoundWinnerReason reason) { }
    public void OnRoundEndPlayerWinner(int playerId) { }
    public void OnPlayerShout(int playerId, CharacterVoicePhrase voicePhrase) { }
    public void OnEmplacementPlaced(int itemId, GameObject objectBuilt, EmplacementType emplacementType) { }
    public void OnEmplacementConstructed(int itemId) { }
    public void OnBuffStart(int playerId, BuffType buff) { }
    public void OnBuffStop(int playerId, BuffType buff) { }
    public void OnShotInfo(int playerId, int shotCount, Vector3[][] shotsPointsPositions, float[] trajectileDistances,
        float[] distanceFromFiringPositions, float[] horizontalDeviationAngles, float[] maxHorizontalDeviationAngles,
        float[] muzzleVelocities, float[] gravities, float[] damageHitBaseDamages, float[] damageRangeUnitValues,
        float[] damagePostTraitAndBuffValues, float[] totalDamages, Vector3[] hitPositions, Vector3[] hitDirections,
        int[] hitPlayerIds, int[] hitDamageableObjectIds, int[] hitShipIds, int[] hitVehicleIds)
    { }
    public void OnVehicleSpawned(int vehicleId, FactionCountry vehicleFaction, PlayerClass vehicleClass, GameObject vehicleObject, int ownerPlayerId) { }
    public void OnVehicleHurt(int vehicleId, byte oldHp, byte newHp, EntityHealthChangedReason reason) { }
    public void OnPlayerKilledVehicle(int killerPlayerId, int victimVehicleId, EntityHealthChangedReason reason, string details) { }
    public void OnShipSpawned(int shipId, GameObject shipObject, FactionCountry shipfaction, ShipType shipType, int shipNameId) { }
    public void OnShipDamaged(int shipId, int oldHp, int newHp) { }
    public void OnAdminPlayerAction(int playerId, int adminId, ServerAdminAction action, string reason) { }
    public void OnRCLogin(int playerId, string inputPassword, bool isLoggedIn) { }
    public void OnRCCommand(int playerId, string input, string output, bool success) { }
    public void OnPlayerPacket(int playerId, byte? instance, Vector3? ownerPosition, double? packetTimestamp, Vector2? ownerInputAxis,
        float? ownerRotationY, float? ownerPitch, float? ownerYaw, PlayerActions[] actionCollection, Vector3? cameraPosition,
        Vector3? cameraForward, ushort? shipID, bool swimming)
    { }
    public void OnVehiclePacket(int vehicleId, Vector2 inputAxis, bool shift, bool strafe, PlayerVehicleActions[] actionCollection) { }
    public void OnOfficerOrderStart(int officerPlayerId, HighCommandOrderType highCommandOrderType, Vector3 orderPosition, float orderRotationY, int voicePhraseRandomIndex) { }
    public void OnOfficerOrderStop(int officerPlayerId, HighCommandOrderType highCommandOrderType) { }
}
