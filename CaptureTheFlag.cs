using System;
using System.Collections.Generic;
using HoldfastSharedMethods;
using UnityEngine;
using UnityEngine.UI;
using CtF;

public class CaptureTheFlag : IHoldfastSharedMethods
{
    // ----- config (make these config-driven later) -----
    private const int EnemyBaseHoldSeconds = 60;
    private const int WarningSeconds = 15;
    private const float DefaultBaseRadius = 30f;

    // ----- runtime state -----
    private bool _isServer;
    private int _elapsedSeconds;

    private FactionCountry _attackingFaction = FactionCountry.None;
    private FactionCountry _defendingFaction = FactionCountry.None;

    // Players by id
    private readonly Dictionary<int, PlayerInfo> _players = new Dictionary<int, PlayerInfo>(256);

    // Flags indexed both ways for fast lookup
    private readonly Dictionary<GameObject, FlagState> _flagsByObject = new Dictionary<GameObject, FlagState>(8);
    private readonly Dictionary<FactionCountry, FlagState> _flagsByFaction = new Dictionary<FactionCountry, FlagState>(8);

    // “Bases” / capture zones per faction for this round (map-specific)
    private readonly Dictionary<FactionCountry, CaptureZone> _basesByFaction = new Dictionary<FactionCountry, CaptureZone>(8);

    // Mapping from faction flag object name in scene
    private static readonly Dictionary<FactionCountry, string> FlagObjectName = new Dictionary<FactionCountry, string>
    {
        {FactionCountry.British,  "Flag_British_Interactable"},
        {FactionCountry.French,   "Flag_French_Interactable"},
        {FactionCountry.Prussian, "Flag_Prussian_Interactable"},
        {FactionCountry.Russian,  "Flag_Russian_Interactable"},
        {FactionCountry.Italian,  "Flag_Italian_Interactable"},
        {FactionCountry.Austrian, "Flag_Austrian_Interactable"},
    };

    // Map configs (only one filled because your code only had ChampsdAmbre)
    private readonly Dictionary<string, MapConfig> _mapConfigs =
        new Dictionary<string, MapConfig>(StringComparer.OrdinalIgnoreCase)
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

    //Data Types

    private sealed class PlayerInfo
    {
        public int PlayerId;
        public ulong SteamId;
        public GameObject PlayerObject;
        public FactionCountry Faction = FactionCountry.None;
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

        public int CarrierPlayerId; // last known carrier; 0 means none/unknown
        public int EnemyBaseDeadlineSeconds; // 0 means inactive
        public bool WarningSent;
        public FlagCountdownState CountdownState = FlagCountdownState.None;
    }

    private struct CaptureZone
    {
        public Vector3 Center;
        public float Radius;

        public CaptureZone(Vector3 center, float radius)
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
        //Currently Hard Code Logging
        CtFLogger.SetEnabled(true);

        if (!server)
        {
            return;
        }

        CommandExecutor.InitializeConsole();
    }

    public void OnPlayerJoined(int playerId, ulong steamId, string name, string regimentTag, bool isBot)
    {
        if (!_isServer) return;

        _players[playerId] = new PlayerInfo
        {
            PlayerId = playerId,
            SteamId = steamId,
            PlayerObject = null,
            Faction = FactionCountry.None
        };
    }

    public void OnPlayerSpawned(int playerId, int spawnSectionId, FactionCountry playerFaction, PlayerClass playerClass,
        int uniformId, GameObject playerObject)
    {
        if (!_isServer) return;

        PlayerInfo info;
        if (!_players.TryGetValue(playerId, out info))
        {
            // Handle spawn before join (rare, but safer)
            info = new PlayerInfo { PlayerId = playerId };
            _players[playerId] = info;
        }

        info.Faction = playerFaction;
        info.PlayerObject = playerObject;
    }

    public void OnPlayerLeft(int playerId)
    {
        if (!_isServer) return;

        _players.Remove(playerId);
    }

    public void OnRoundDetails(int roundId, string serverName, string mapName, FactionCountry attackingFaction,
        FactionCountry defendingFaction, GameplayMode gameplayMode, GameType gameType)
    {
        if (!_isServer) return;

        ResetRoundState();

        _attackingFaction = attackingFaction;
        _defendingFaction = defendingFaction;

        // Register the two flags for the round
        TryRegisterFlag(attackingFaction);
        TryRegisterFlag(defendingFaction);

        // Configure bases for this map (attacking/defending only)
        SetupBasesForMap(mapName, attackingFaction, defendingFaction);

        Debug.Log("CTF map: " + mapName);
    }

    public void OnInteractableObjectInteraction(int playerId, int interactableObjectId, GameObject interactableObject,
        InteractionActivationType interactionActivationType, int nextActivationStateTransitionIndex)
    {
        if (!_isServer) return;

        // Prefer direct enum compare keep ToString fallback if you’re unsure of exact enum member names.
        // if (interactionActivationType != InteractionActivationType.EndInteraction) return;
        if (!string.Equals(interactionActivationType.ToString(), "EndInteraction", StringComparison.Ordinal))
            return;

        FlagState flag;
        if (!_flagsByObject.TryGetValue(interactableObject, out flag))
            return;

        flag.CarrierPlayerId = playerId;

        // If enemy picked it up, broadcast capture.
        PlayerInfo p;
        if (_players.TryGetValue(playerId, out p) && p.Faction != FactionCountry.None && p.Faction != flag.FlagFaction)
        {
            Broadcast(string.Format("The {0} flag has been captured!", flag.FlagFaction));
        }

        // Picking it up cancels any “in enemy base” countdown
        CancelEnemyBaseCountdown(flag);
    }

    public void OnPlayerEndCarry(int playerId)
    {
        if (!_isServer) return;

        // Find any flag last carried by this player.
        // (If you want strictly “currently carried”, you’ll need additional hooks/state.)
        foreach (var kv in _flagsByFaction)
        {
            var flag = kv.Value;
            if (flag.CarrierPlayerId != playerId) continue;

            var enemyFaction = GetOpponentFaction(flag.FlagFaction);
            if (enemyFaction == FactionCountry.None) continue;

            CaptureZone enemyBase;
            if (!_basesByFaction.TryGetValue(enemyFaction, out enemyBase))
                continue; // base not configured for this map

            if (IsWithinZoneXZ(flag.FlagObject.transform.position, enemyBase))
            {
                StartEnemyBaseCountdown(flag, enemyFaction, EnemyBaseHoldSeconds);
            }
            else
            {
                // If the flag was previously “in base” but is now out, cancel.
                if (flag.EnemyBaseDeadlineSeconds > 0)
                    CancelEnemyBaseCountdown(flag);
            }
        }
    }

    public void OnUpdateElapsedTime(float time)
    {
        if (!_isServer) return;

        _elapsedSeconds = (int)Mathf.Floor(time);

        // Tick countdowns
        foreach (var kv in _flagsByFaction)
        {
            var flag = kv.Value;
            if (flag.EnemyBaseDeadlineSeconds <= 0) continue;
            if (flag.CountdownState != FlagCountdownState.CountdownActive) continue;

            var enemyFaction = GetOpponentFaction(flag.FlagFaction);
            if (enemyFaction == FactionCountry.None) continue;

            CaptureZone enemyBase;
            if (_basesByFaction.TryGetValue(enemyFaction, out enemyBase))
            {
                // Optional: cancel countdown if flag leaves base before expiry
                if (!IsWithinZoneXZ(flag.FlagObject.transform.position, enemyBase))
                {
                    CancelEnemyBaseCountdown(flag);
                    continue;
                }
            }

            int remaining = flag.EnemyBaseDeadlineSeconds - _elapsedSeconds;

            if (!flag.WarningSent && remaining == WarningSeconds)
            {
                Broadcast(string.Format(
                    "The {0} flag is in the enemy spawn! You have {1} seconds to get it out.",
                    flag.FlagFaction, WarningSeconds));

                flag.WarningSent = true;
            }

            if (_elapsedSeconds >= flag.EnemyBaseDeadlineSeconds)
            {
                // If flag belongs to A, and it’s in enemy base (B), then B wins.
                SetRoundWinner(enemyFaction);
                flag.CountdownState = FlagCountdownState.RoundEnded;
            }
        }
    }

    //Helpers

    private void ResetRoundState()
    {
        _elapsedSeconds = 0;
        _attackingFaction = FactionCountry.None;
        _defendingFaction = FactionCountry.None;

        _flagsByObject.Clear();
        _flagsByFaction.Clear();
        _basesByFaction.Clear();
    }

    private void TryRegisterFlag(FactionCountry faction)
    {
        if (faction == FactionCountry.None) return;

        string objName;
        if (!FlagObjectName.TryGetValue(faction, out objName))
        {
            Debug.LogWarning("No flag object mapping for faction: " + faction);
            return;
        }

        var obj = GameObject.Find(objName);
        if (obj == null)
        {
            Debug.LogWarning("Flag object not found in scene: " + objName);
            return;
        }

        var state = new FlagState
        {
            FlagFaction = faction,
            FlagObject = obj,
            CarrierPlayerId = 0,
            EnemyBaseDeadlineSeconds = 0,
            WarningSent = false,
            CountdownState = FlagCountdownState.None
        };

        _flagsByObject[obj] = state;
        _flagsByFaction[faction] = state;

        Debug.Log("Registered flag: " + faction);
    }

    private void SetupBasesForMap(string mapName, FactionCountry attackingFaction, FactionCountry defendingFaction)
    {
        MapConfig cfg;
        if (_mapConfigs.TryGetValue(mapName, out cfg))
        {
            _basesByFaction[attackingFaction] = new CaptureZone(cfg.AttackingBase, cfg.Radius);
            _basesByFaction[defendingFaction] = new CaptureZone(cfg.DefendingBase, cfg.Radius);
            return;
        }

        // Fallback: no map config means the “in enemy spawn” win condition can’t run reliably.
        // You can still run pickup/carry tracking, broadcasts, etc.
        _basesByFaction[attackingFaction] = new CaptureZone(Vector3.zero, DefaultBaseRadius);
        _basesByFaction[defendingFaction] = new CaptureZone(Vector3.zero, DefaultBaseRadius);

        Debug.LogWarning("No base positions configured for map '" + mapName +
                         "'. Add to _mapConfigs to enable enemy-spawn countdown.");
    }

    private FactionCountry GetOpponentFaction(FactionCountry faction)
    {
        // Assumes 2-team round.
        if (faction == FactionCountry.None) return FactionCountry.None;
        if (faction == _attackingFaction) return _defendingFaction;
        if (faction == _defendingFaction) return _attackingFaction;
        return FactionCountry.None;
    }

    private static bool IsWithinZoneXZ(Vector3 pos, CaptureZone zone)
    {
        var a = new Vector2(pos.x, pos.z);
        var b = new Vector2(zone.Center.x, zone.Center.z);
        return Vector2.Distance(a, b) < zone.Radius;
    }

    private void StartEnemyBaseCountdown(FlagState flag, FactionCountry enemyFaction, int seconds)
    {
        flag.EnemyBaseDeadlineSeconds = _elapsedSeconds + seconds;
        flag.WarningSent = false;
        flag.CountdownState = FlagCountdownState.CountdownActive;

        Broadcast(string.Format(
            "The {0} flag is in the enemy spawn! You have {1} seconds to get it out.",
            flag.FlagFaction, seconds));
    }

    private void CancelEnemyBaseCountdown(FlagState flag)
    {
        flag.EnemyBaseDeadlineSeconds = 0;
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
        CommandExecutor.ExecuteCommand(string.Format("set roundEndFactionWin {0} None", winner));
    }

    public void PassConfigVariables(string[] value)
    {
        if (!_isServer) return;

        // Keep as stub for now. When you implement it, parse into typed fields:
        // - EnemyBaseHoldSeconds
        // - base radius per map/faction
        // - flag object names per faction/era/map
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
