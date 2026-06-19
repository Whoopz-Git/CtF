using CtF;
using HoldfastBridge;
using HoldfastSharedMethods;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

public class CaptureTheFlag : IHoldfastSharedMethods, IHoldfastGame
{
    private int CaptureTimeInSeconds = 60;
    private const int WarningSeconds = 30;
    private const float DefaultBaseRadius = 30f;

    private int _respawnWaveTimerSeconds = 30;
    private int _lastRespawnWaveTime = 0;
    private readonly List<FactionCountry> _revivedFactions = new List<FactionCountry>();

    private int _flagLocationIntervalSeconds = 30;
    private int _lastFlagLocationBroadcastTime = 0;

    private int _respawnTickets = -1;
    private readonly Dictionary<FactionCountry, int> _factionTickets = new Dictionary<FactionCountry, int>();

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
        public bool IsAlive;

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

    private float _elapsedTime; // raw float, kept alongside _elapsedSeconds

    private struct ScheduledCommand
    {
        public float ReadyAt;   // earliest _elapsedTime at which this may run (0 = ASAP)
        public Action Run;
    }
    private readonly Queue<ScheduledCommand> _commandQueue = new Queue<ScheduledCommand>();

    private void EnqueueCommand(Action run, float readyAt = 0f)
    {
        _commandQueue.Enqueue(new ScheduledCommand { Run = run, ReadyAt = readyAt });
    }

    public void OnGameMethodsInitialized(IHoldfastGameMethods holdfastGameMethods)
    {
        Debug.Log("[CtF] Trying to find game console...");
        CommandExecutor.Initialize(holdfastGameMethods);
    }

    public void OnIsServer(bool server)
    {
        _isServer = server;
        CtFLogger.SetEnabled(true);
        CommandExecutor.IsServer = _isServer;
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
                IsBot = false,
                IsAlive = true
            };
            _players[playerId] = player;
        }

        player.PlayerObject = playerObject;
        player.SpawnSectionId = spawnSectionId;
        player.Faction = playerFaction;
        player.PlayerClass = playerClass;
        player.UniformId = uniformId;
        player.IsAlive = true;
    }

    public void OnPlayerLeft(int playerId)
    {
        _players.Remove(playerId);
    }

    public void OnPlayerHurt(int playerId, byte oldHp, byte newHp, EntityHealthChangedReason reason)
    {
        if (newHp <= 0 && _players.TryGetValue(playerId, out var player))
            player.IsAlive = false;
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

        // Set spawn ticket amount
        _factionTickets[attackingFaction] = _respawnTickets;
        _factionTickets[defendingFaction] = _respawnTickets;
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
        if (!_players.TryGetValue(playerId, out var player) || player == null)
        {
            CtFLogger.Warn($"Player {playerId} picked up the {flag.FlagFaction} flag but is not known.");
            return;
        }

        if (player.Faction == FactionCountry.None)
        {
            CtFLogger.Warn($"{player.Name} picked up the {flag.FlagFaction} flag but their faction is unknown.");
        }
        else if (player.Faction == flag.FlagFaction)
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

        CtFLogger.Log($"{player.Name} is now carrying the {flag.FlagFaction} flag.");
    }

    public void OnPlayerEndCarry(int playerId)
    {
        foreach (var flag in _flags)
        {
            if (flag.CarrierPlayerId == playerId)
            {
                string playerName = _players.TryGetValue(playerId, out var playerState) ? playerState.Name : null;
                flag.CarrierPlayerId = 0;
                CtFLogger.Log($"{playerName} is no longer carrying the {flag.FlagFaction} flag.");
            }
        }
    }

    public void OnUpdateElapsedTime(float time)
    {
        _elapsedTime = time;
        _elapsedSeconds = (int)time;

        // Process queued commands: at most one per tick, and only once it's due.
        // FIFO order matches ReadyAt order within a wave (revives are all ASAP and
        // drain first; teleports are appended as their revives fire), so if the
        // front entry isn't due yet, nothing behind it is either � waiting is correct.
        if (_commandQueue.Count > 0 && _elapsedTime >= _commandQueue.Peek().ReadyAt)
        {
            _commandQueue.Dequeue().Run();
        }

        // Flag capture countdown logic
        foreach (var flag in _flags)
        {
            var enemyFaction = GetOpponentFaction(flag.FlagFaction);
            if (enemyFaction == FactionCountry.None)
                continue;

            if (!_basesByFaction.TryGetValue(enemyFaction, out var enemyBase))
                continue;

            var flagPos = GetFlagPosition(flag);
            bool inEnemyBase = IsWithinBase(flagPos, enemyBase);

            bool carriedByOwner = false;
            if (flag.CarrierPlayerId != 0 &&
                _players.TryGetValue(flag.CarrierPlayerId, out var carrier) &&
                carrier.Faction != FactionCountry.None &&
                carrier.Faction == flag.FlagFaction)
            {
                carriedByOwner = true;
            }

            bool shouldCountAsInEnemyBase = inEnemyBase && !carriedByOwner;

            if (shouldCountAsInEnemyBase && flag.CountdownState == FlagCountdownState.None)
            {
                if (CaptureTimeInSeconds != -1)
                    StartBaseCountdown(flag, CaptureTimeInSeconds);
            }
            else if (!shouldCountAsInEnemyBase && flag.CountdownState == FlagCountdownState.CountdownActive)
            {
                Broadcast($"The {flag.FlagFaction} flag is no longer in enemy control within their spawn. Capture cancelled.");
                CancelBaseCountdown(flag);
                continue;
            }

            if (flag.baseDeadlineTime <= 0 || flag.CountdownState != FlagCountdownState.CountdownActive)
                continue;

            int remaining = flag.baseDeadlineTime - _elapsedSeconds;

            if (!flag.WarningSent && remaining == WarningSeconds)
            {
                Broadcast($"The {flag.FlagFaction} flag is in the enemy spawn under enemy control! Only {WarningSeconds} seconds to recapture it!");
                flag.WarningSent = true;
            }

            if (_elapsedSeconds >= flag.baseDeadlineTime)
            {
                CtFLogger.Log("Flag base countdown reached deadline; ending round.");

                if (enemyFaction != FactionCountry.None)
                    SetRoundWinner(enemyFaction);
                else
                    CtFLogger.Warn($"Could not determine opponent faction for {flag.FlagFaction}");

                flag.CountdownState = FlagCountdownState.RoundEnded;
            }
        }

        // Respawn wave: only start once the previous wave's commands have fully
        // drained. This is what prevents waves stacking and bounds the command load.
        if (_respawnWaveTimerSeconds != -1
            && _commandQueue.Count == 0
            && _elapsedSeconds - _lastRespawnWaveTime >= _respawnWaveTimerSeconds)
        {
            _lastRespawnWaveTime = _elapsedSeconds;
            WaveRespawn();
        }

        // Flag location broadcast
        if (_flagLocationIntervalSeconds != -1 && _elapsedSeconds - _lastFlagLocationBroadcastTime >= _flagLocationIntervalSeconds)
        {
            _lastFlagLocationBroadcastTime = _elapsedSeconds;
            BroadcastFlagLocations();
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
                        if (int.TryParse(arg, out var seconds) && (seconds > 0 || seconds == -1))
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

                case "respawntimer":
                    {
                        if (int.TryParse(arg, out var seconds) && (seconds > 0 || seconds == -1))
                        {
                            _respawnWaveTimerSeconds = seconds;
                            CtFLogger.Log($"RespawnTimer set to {_respawnWaveTimerSeconds} seconds.");
                        }
                        else
                        {
                            CtFLogger.Warn($"Invalid RespawnTimer value '{arg}'. Must be a positive integer.");
                        }
                        break;
                    }

                case "flaglocationtimer":
                    {
                        if (int.TryParse(arg, out var seconds) && (seconds > 0 || seconds == -1))
                        {
                            _flagLocationIntervalSeconds = seconds;
                            CtFLogger.Log($"FlagLocationTimer set to {_flagLocationIntervalSeconds} seconds.");
                        }
                        else
                        {
                            CtFLogger.Warn($"Invalid FlagLocationTimer value '{arg}'. Must be a positive integer.");
                        }
                        break;
                    }

                case "respawntickets":
                    {
                        if (int.TryParse(arg, out var tickets) && (tickets > 0 || tickets == -1))
                        {
                            _respawnTickets = tickets;
                            CtFLogger.Log($"RespawnTickets set to {_respawnTickets}.");
                        }
                        else
                        {
                            CtFLogger.Warn($"Invalid RespawnTickets value '{arg}'. Must be a positive integer or -1 for infinite.");
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
        _lastRespawnWaveTime = 0;
        _lastFlagLocationBroadcastTime = 0;

        _flags.Clear();
        _basesByFaction.Clear();
        _factionTickets.Clear();
        _commandQueue.Clear();

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
        bool hasPreset = CtFMapPresets.TryGetMapConfig(mapName, out MapConfig cfg);
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

    public bool GetIsServer()
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

    private void WaveRespawn()
    {
        _revivedFactions.Clear();

        foreach (var flag in _flags)
        {
            if (flag.CarrierPlayerId == 0) continue;
            if (!_players.TryGetValue(flag.CarrierPlayerId, out var carrier)) continue;
            if (carrier.Faction != flag.FlagFaction) continue;

            if (!_factionTickets.TryGetValue(flag.FlagFaction, out var tickets)) continue;
            if (tickets == 0) continue;

            var flagPos = GetFlagPosition(flag);
            int queuedCount = 0;

            foreach (var kvp in _players)
            {
                var player = kvp.Value;
                if (player.Faction != flag.FlagFaction) continue;
                if (player.IsAlive) continue;
                if (tickets == 0) break;

                int pid = player.PlayerId;
                EnqueueCommand(() =>
                {
                    // Player may have left or already respawned by drain time
                    if (!_players.TryGetValue(pid, out var p) || p.IsAlive)
                        return;

                    CommandExecutor.ExecuteCommand($"serverAdmin revive {pid}");

                    // Teleport is queued behind the revive with a 0.1s minimum gap.
                    // Spawn (raycast) is computed here, at revive-fire time, so it's
                    // one raycast per tick rather than a burst at wave time.
                    var spawnPos = GetRandomSpawnAroundFlag(flagPos);
                    EnqueueCommand(() =>
                        CommandExecutor.ExecuteCommand(string.Format(CultureInfo.InvariantCulture,
                            "teleport {0} {1},{2},{3}",
                            pid, spawnPos.x, spawnPos.y, spawnPos.z)),
                        _elapsedTime + 0.1f);
                });

                if (tickets != -1)
                {
                    tickets--;
                    if (tickets == 0)
                    {
                        _factionTickets[flag.FlagFaction] = tickets;
                        Say($"The {flag.FlagFaction} faction has run out of respawn tickets!");
                    }
                }

                queuedCount++;
            }

            if (tickets != -1)
                _factionTickets[flag.FlagFaction] = tickets;

            if (queuedCount > 0)
                _revivedFactions.Add(flag.FlagFaction);
        }

        if (_revivedFactions.Count == 0) return;

        if (_factionTickets.Any(kvp => kvp.Value != -1))
        {
            var parts = new List<string>();
            foreach (var kvp in _factionTickets)
            {
                var ticketDisplay = kvp.Value == -1 ? "Infinite" : kvp.Value.ToString();
                parts.Add($"{kvp.Key}: {ticketDisplay} tickets remaining");
            }
            Say(string.Join(" | ", parts));
        }
    }

    private void BroadcastFlagLocations()
    {
        if (_roundDetails == null || _flags.Count == 0) return;

        CtFMapPresets.TryGetMapConfig(_roundDetails.MapName, out var cfg);

        // Remap sentinel spawn POI names to actual faction names
        var pois = new PointOfInterest[cfg.POIs.Length];
        for (int i = 0; i < cfg.POIs.Length; i++)
        {
            var poi = cfg.POIs[i];
            if (poi.Name.Equals("Attacker Spawn", StringComparison.OrdinalIgnoreCase))
                pois[i] = new PointOfInterest($"{_roundDetails.AttackingFaction} Spawn", poi.Center, poi.Radius);
            else if (poi.Name.Equals("Defending Spawn", StringComparison.OrdinalIgnoreCase))
                pois[i] = new PointOfInterest($"{_roundDetails.DefendingFaction} Spawn", poi.Center, poi.Radius);
            else
                pois[i] = poi;
        }

        var parts = new string[_flags.Count];
        for (int i = 0; i < _flags.Count; i++)
        {
            var flag = _flags[i];
            var pos = GetFlagPosition(flag);
            parts[i] = $"{flag.FlagFaction} Flag: {GetFlagLocationString(pos, pois)}";
        }

        Broadcast(string.Join(" | ", parts));
    }

    private static string GetFlagLocationString(Vector3 flagPos, PointOfInterest[] pois)
    {
        var pos2D = new Vector2(flagPos.x, flagPos.z);

        if (pois != null && pois.Length > 0)
        {
            string closestName = null;
            float closestDist = float.MaxValue;

            foreach (var poi in pois)
            {
                float dist = Vector2.Distance(pos2D, poi.Center);
                if (dist <= poi.Radius && dist < closestDist)
                {
                    closestDist = dist;
                    closestName = poi.Name;
                }
            }

            if (closestName != null)
                return $"Near {closestName}";
        }

        if (pos2D.magnitude <= 50f)
            return "Map Center";

        return GetCompassDirection(flagPos);
    }

    private static string GetCompassDirection(Vector3 pos)
    {
        float angle = Mathf.Atan2(pos.x, pos.z) * Mathf.Rad2Deg;
        if (angle < 0f) angle += 360f;

        if (angle < 22.5f || angle >= 337.5f) return "North";
        if (angle < 67.5f) return "North East";
        if (angle < 112.5f) return "East";
        if (angle < 157.5f) return "South East";
        if (angle < 202.5f) return "South";
        if (angle < 247.5f) return "South West";
        if (angle < 292.5f) return "West";
        return "North West";
    }

    private static Vector3 GetRandomSpawnAroundFlag(Vector3 flagPos)
    {
        float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float radius = UnityEngine.Random.Range(1f, 5f);

        float x = flagPos.x + radius * Mathf.Cos(angle);
        float z = flagPos.z + radius * Mathf.Sin(angle);
        float y = TerrainSampler.SampleTerrain(new Vector2(x, z));

        return new Vector3(x, y, z);
    }

    private void Say(string message)
    {
        CommandExecutor.ExecuteCommand("serverAdmin say " + message);
    }

    //Unused interface methods
    public void OnPlayerKilledPlayer(int killerPlayerId, int victimPlayerId, EntityHealthChangedReason reason, string additionalDetails) { }
    public void OnInteractableObjectInteraction(int playerId, int interactableObjectId, GameObject interactableObject, InteractionActivationType interactionActivationType, int nextActivationStateTransitionIndex) { }
    public void OnPlayerBlock(int attackingPlayerId, int defendingPlayerId) { }
    public void OnScorableAction(int playerId, int score, ScorableActionType reason) { }
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
