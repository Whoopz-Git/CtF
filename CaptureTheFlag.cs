using System;
using System.Collections.Generic;
using HoldfastSharedMethods;
using UnityEngine;
using CtF;

public class CaptureTheFlag : IHoldfastSharedMethods
{
    // Make Config Driven Later
    private const int BaseHoldSeconds = 60;
    private const int WarningSeconds = 30;
    private const float BaseRadius = 30f;

    private bool _isServer;
    private int _elapsedSeconds;

    private RoundInfo _roundDetails;
    private readonly Dictionary<int, PlayerState> _players = new Dictionary<int, PlayerState>();

    // Flags indexed both ways for fast lookup
    private readonly List<FlagState> _flags = new List<FlagState>();
    private readonly Dictionary<GameObject, FlagState> _flagsByObject = new Dictionary<GameObject, FlagState>(8);

    // “Bases” / capture zones per faction for this round (map-specific)
    private readonly Dictionary<FactionCountry, BaseZone> _basesByFaction = new Dictionary<FactionCountry, BaseZone>(8);

    // Mapping from faction flag object name in scene
    private readonly Dictionary<FactionCountry, string> _flagObjectName =new Dictionary<FactionCountry, string>
    {
        {FactionCountry.British,  "Flag_British_Interactable"},
        {FactionCountry.French,   "Flag_French_Interactable"},
        {FactionCountry.Prussian, "Flag_Prussian_Interactable"},
        {FactionCountry.Russian,  "Flag_Russian_Interactable"},
        {FactionCountry.Italian,  "Flag_Italian_Interactable"},
        {FactionCountry.Austrian, "Flag_Austrian_Interactable"},
    };

    // Hard Coded Map configs
    private readonly Dictionary<string, MapConfig> _mapConfigs = new Dictionary<string, MapConfig>(StringComparer.OrdinalIgnoreCase)
    {
         {
             "ChampsdAmbre",
             new MapConfig(
                 attackingBase: new Vector3(4.0f, 13.71f, -195.0f),
                 defendingBase: new Vector3(-0.5f, 13.71f, 230.0f),
                 radius: 30f
             )
         }
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

    private readonly struct MapConfig
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

    public void OnInteractableObjectInteraction(int playerId, int interactableObjectId, GameObject interactableObject, InteractionActivationType interactionActivationType, int nextActivationStateTransitionIndex)
    {
        // Only care about the end of an interaction (flag actually picked up).
        if (interactionActivationType != InteractionActivationType.EndInteraction)
            return;

        // Is this one of our tracked flags?
        if (!_flagsByObject.TryGetValue(interactableObject, out var flag))
            return;

        // Remember who picked it up.
        flag.CarrierPlayerId = playerId;

        // If the carrier is from the enemy faction, announce the capture.
        if (_players.TryGetValue(playerId, out var player) &&
            player.Faction != FactionCountry.None &&
            player.Faction != flag.FlagFaction)
        {
            Broadcast($"The {flag.FlagFaction} flag has been captured!");
        }

        // Picking up the flag cancels any "flag in enemy base" countdown.
        CancelBaseCountdown(flag);
    }

    public void OnPlayerEndCarry(int playerId)
    {
        CtFLogger.Log("OnPlayerEndCarry");

        foreach (var flag in _flags)
        {
            // Only care about the flag that this player was carrying
            if (flag.CarrierPlayerId != playerId)
                continue;

            // Determine which faction is the enemy for this flag
            var enemyFaction = GetOpponentFaction(flag.FlagFaction);
            if (enemyFaction == FactionCountry.None)
            {
                CtFLogger.Warn($"OnPlayerEndCarry: could not resolve enemy faction for {flag.FlagFaction}");
                continue;
            }

            // Get the enemy base zone
            if (!_basesByFaction.TryGetValue(enemyFaction, out var enemyBase))
            {
                CtFLogger.Warn($"OnPlayerEndCarry: no base configured for enemy faction {enemyFaction} on map '{_roundDetails?.MapName ?? "unknown"}'.");
                continue;
            }

            var flagPos = flag.FlagObject.transform.position;

            if (IsWithinBaseXZ(flagPos, enemyBase))
            {
                CtFLogger.Log($"The {flag.FlagFaction} flag is in enemy base ({enemyFaction}).");
                StartBaseCountdown(flag, BaseHoldSeconds);
            }
            else
            {
                // If you want dropping the flag outside the base to cancel a running countdown,
                // uncomment this:
                // CancelEnemyBaseCountdown(flag);
            }
        }
    }

    public void OnUpdateElapsedTime(float time)
    {
        _elapsedSeconds = (int)time;

        foreach (var flag in _flags)
        {
            // No countdown active
            if (flag.baseDeadlineTime <= 0)
                continue;

            if (flag.CountdownState != FlagCountdownState.CountdownActive)
                continue;

            int remaining = flag.baseDeadlineTime - _elapsedSeconds;

            // 15-second warning
            if (!flag.WarningSent && remaining == WarningSeconds)
            {
                Broadcast($"The {flag.FlagFaction} flag is in the enemy spawn! You have {WarningSeconds} seconds to get it out.");
                flag.WarningSent = true;
            }

            // Time expired: end round
            if (_elapsedSeconds >= flag.baseDeadlineTime)
            {
                CtFLogger.Log("Flag base countdown reached deadline; ending round.");

                var winner = GetOpponentFaction(flag.FlagFaction);
                if (winner != FactionCountry.None)
                {
                    SetRoundWinner(winner);
                }
                else
                {
                    CtFLogger.Warn($"OnUpdateElapsedTime: could not determine opponent faction for {flag.FlagFaction}");
                }

                flag.CountdownState = FlagCountdownState.RoundEnded;
            }
        }
    }

    //Helpers
    private void ResetRoundState()
    {
        _elapsedSeconds = 0;

        _flags.Clear();
        _flagsByObject.Clear();
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
            CtFLogger.Warn($"TryRegisterFlag: no flag object mapping for faction {faction}.");
            return;
        }

        var flagObj = GameObject.Find(objectName);
        if (flagObj == null)
        {
            var mapName = _roundDetails != null ? _roundDetails.MapName : "unknown";
            CtFLogger.Warn( $"TryRegisterFlag: could not find flag object '{objectName}' for faction {faction} on map '{mapName}'.");
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
        _flagsByObject[flagObj] = flag;

        CtFLogger.Log($"TryRegisterFlag: registered flag for faction {faction} (object '{objectName}').");
    }

    private void SetupBasesForMap(string mapName, FactionCountry attackingFaction, FactionCountry defendingFaction)
    {
        MapConfig cfg;
        if (_mapConfigs.TryGetValue(mapName, out cfg))
        {
            _basesByFaction[attackingFaction] = new BaseZone(cfg.AttackingBase, cfg.Radius);
            _basesByFaction[defendingFaction] = new BaseZone(cfg.DefendingBase, cfg.Radius);

            CtFLogger.Log($"SetupBasesForMap: configured bases for '{mapName}' " + $"(attacker: {attackingFaction}, defender: {defendingFaction}, radius: {cfg.Radius}).");
        }
        else
        {
            // Still define something so we don't crash; behavior is just not meaningful.
            _basesByFaction[attackingFaction] = new BaseZone(Vector3.zero, BaseRadius);
            _basesByFaction[defendingFaction] = new BaseZone(Vector3.zero, BaseRadius);

            CtFLogger.Warn($"SetupBasesForMap: no configuration for map '{mapName}'. " + $"Using default bases at origin with radius {BaseRadius}.");
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

    private static bool IsWithinBaseXZ(Vector3 pos, BaseZone zone)
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

    public void PassConfigVariables(string[] value)
    {
        // EnemyBaseHoldSeconds
        // Base radius per map/faction
        // Flag object names per faction/era/map
    }

    //Unused interface methods
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
    public void OnPlayerStartCarry(int playerId, CarryableObjectType carryableObject) { }
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
