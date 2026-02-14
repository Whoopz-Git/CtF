using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HoldfastSharedMethods;
using UnityEngine.UI;

public class CaptureTheFlag : IHoldfastSharedMethods
{
    private InputField f1MenuInputField;
    public List<playerInfo> playerList = new List<playerInfo>();
    public List<captureFlags> flagList = new List<captureFlags>();
    public List<spawnObjects> spawnList = new List<spawnObjects>();
    public GameObject spawn1 = new GameObject();
    public GameObject spawn2 = new GameObject();
    public float timer;
    public float timeelapsed;

    public class playerInfo
    {
        public int pId;
        public ulong pSId;
        public GameObject pObj;
        public string faction;
    }

    public class captureFlags
    {
        public GameObject flagObj;
        public string flagFaction;
        public int flagPlayerId;
        public float endTime;
        public int callTimer;
    }

    public class spawnObjects
    {
        public GameObject spawn;
        public string spawnFaction;
    }

    public void OnPlayerJoined(int playerId, ulong steamId, string name, string regimentTag, bool isBot)
    {
        playerInfo newPlayer = new playerInfo();

        newPlayer.pId = playerId;
        newPlayer.pSId = steamId;
        newPlayer.pObj = null;
        newPlayer.faction = "";

        playerList.Add(newPlayer);
    }

    public void OnPlayerSpawned(int playerId, int spawnSectionId, FactionCountry playerFaction, PlayerClass playerClass, int uniformId, GameObject playerObject)
    {
        for (int x = 0; x < playerList.Count; x++)
        {
            if (playerList[x].pId == playerId)
            {
                playerList[x].faction = playerFaction.ToString();
                playerList[x].pObj = playerObject;
            }
        }
    }

    public void OnPlayerLeft(int playerId)
    {
        for (int x = 0; x < playerList.Count; x++)
        {
            if (playerList[x].pId == playerId)
            {
                playerList.Remove(playerList[x]);
            }
        }
    }

    public void OnInteractableObjectInteraction(int playerId, int interactableObjectId, GameObject interactableObject, InteractionActivationType interactionActivationType, int nextActivationStateTransitionIndex)
    {

        for(int x = 0; x < flagList.Count; x++)
        {
            if (interactableObject == flagList[x].flagObj)
            {
                if (interactionActivationType.ToString() == "EndInteraction")
                {
                    Debug.Log(flagList[x].flagFaction + " flag picked up.");
                    flagList[x].flagPlayerId = playerId;

                    Debug.Log("Flag picked up by " + playerId);

                    for(int y = 0; y < playerList.Count; y++)
                    {
                        if (playerList[y].pId == flagList[x].flagPlayerId)
                        {
                            if(flagList[x].flagFaction != playerList[y].faction)
                            {
                                var flagPickedUp = string.Format("broadcast The {0} flag has been captured!", flagList[x].flagFaction);
                                f1MenuInputField.onEndEdit.Invoke(flagPickedUp);
                            }
                        }
                    }

                    if(flagList[x].endTime != -1)
                    {
                        flagList[x].endTime = -1;
                    }
                }
            }
        }
    }

    public void OnRoundDetails(int roundId, string serverName, string mapName, FactionCountry attackingFaction, FactionCountry defendingFaction, GameplayMode gameplayMode, GameType gameType)
    {
        spawnObjects spawn1 = new spawnObjects();
        spawn1.spawn = new GameObject();
        spawn1.spawnFaction = attackingFaction.ToString();
        spawnList.Add(spawn1);

        spawnObjects spawn2 = new spawnObjects();
        spawn2.spawn = new GameObject();
        spawn2.spawnFaction = defendingFaction.ToString();
        spawnList.Add(spawn2);


        if (attackingFaction.ToString() == "British")
        {
            captureFlags newFlag = new captureFlags();

            newFlag.flagFaction = "British";
            newFlag.flagObj = GameObject.Find("Flag_British_Interactable");
            newFlag.flagPlayerId = 0;
            newFlag.endTime = -1;
            newFlag.callTimer = 0;

            flagList.Add(newFlag);
            Debug.Log("Attacking flag added: UK");
        }
        if (attackingFaction.ToString() == "French")
        {
            captureFlags newFlag = new captureFlags();

            newFlag.flagFaction = "French";
            newFlag.flagObj = GameObject.Find("Flag_French_Interactable");
            newFlag.flagPlayerId = 0;
            newFlag.endTime = -1;
            newFlag.callTimer = 0;

            flagList.Add(newFlag);
            Debug.Log("Attacking flag added: FR");
        }
        if (attackingFaction.ToString() == "Prussian")
        {
            captureFlags newFlag = new captureFlags();

            newFlag.flagFaction = "Prussian";
            newFlag.flagObj = GameObject.Find("Flag_Prussian_Interactable");
            newFlag.flagPlayerId = 0;
            newFlag.endTime = -1;
            newFlag.callTimer = 0;

            flagList.Add(newFlag);
            Debug.Log("Attacking flag added: PR");
        }
        if (attackingFaction.ToString() == "Russian")
        {
            captureFlags newFlag = new captureFlags();

            newFlag.flagFaction = "Russian";
            newFlag.flagObj = GameObject.Find("Flag_Russian_Interactable");
            newFlag.flagPlayerId = 0;
            newFlag.endTime = -1;
            newFlag.callTimer = 0;

            flagList.Add(newFlag);
            Debug.Log("Attacking flag added: RU");
        }
        if (attackingFaction.ToString() == "Italian")
        {
            captureFlags newFlag = new captureFlags();

            newFlag.flagFaction = "Italian";
            newFlag.flagObj = GameObject.Find("Flag_Italian_Interactable");
            newFlag.flagPlayerId = 0;
            newFlag.endTime = -1;
            newFlag.callTimer = 0;

            flagList.Add(newFlag);
            Debug.Log("Attacking flag added: IT");
        }
        if (attackingFaction.ToString() == "Austrian")
        {
            captureFlags newFlag = new captureFlags();

            newFlag.flagFaction = "Austrian";
            newFlag.flagObj = GameObject.Find("Flag_Austrian_Interactable");
            newFlag.flagPlayerId = 0;
            newFlag.endTime = -1;
            newFlag.callTimer = 0;

            flagList.Add(newFlag);
            Debug.Log("Attacking flag added: AU");
        }

        if (defendingFaction.ToString() == "British")
        {
            captureFlags newFlag = new captureFlags();

            newFlag.flagFaction = "British";
            newFlag.flagObj = GameObject.Find("Flag_British_Interactable");
            newFlag.flagPlayerId = 0;
            newFlag.endTime = -1;
            newFlag.callTimer = 0;

            flagList.Add(newFlag);
            Debug.Log("Defending flag added: UK");
        }
        if (defendingFaction.ToString() == "French")
        {
            captureFlags newFlag = new captureFlags();

            newFlag.flagFaction = "French";
            newFlag.flagObj = GameObject.Find("Flag_French_Interactable");
            newFlag.flagPlayerId = 0;
            newFlag.endTime = -1;
            newFlag.callTimer = 0;

            flagList.Add(newFlag);
            Debug.Log("Defending flag added: FR");
        }
        if (defendingFaction.ToString() == "Prussian")
        {
            captureFlags newFlag = new captureFlags();

            newFlag.flagFaction = "Prussian";
            newFlag.flagObj = GameObject.Find("Flag_Prussian_Interactable");
            newFlag.flagPlayerId = 0;
            newFlag.endTime = -1;
            newFlag.callTimer = 0;

            flagList.Add(newFlag);
            Debug.Log("Defending flag added: PR");
        }
        if (defendingFaction.ToString() == "Russian")
        {
            captureFlags newFlag = new captureFlags();

            newFlag.flagFaction = "Russian";
            newFlag.flagObj = GameObject.Find("Flag_Russian_Interactable");
            newFlag.flagPlayerId = 0;
            newFlag.endTime = -1;
            newFlag.callTimer = 0;

            flagList.Add(newFlag);
            Debug.Log("Defending flag added: RU");
        }
        if (defendingFaction.ToString() == "Italian")
        {
            captureFlags newFlag = new captureFlags();

            newFlag.flagFaction = "Italian";
            newFlag.flagObj = GameObject.Find("Flag_Italian_Interactable");
            newFlag.flagPlayerId = 0;
            newFlag.endTime = -1;
            newFlag.callTimer = 0;

            flagList.Add(newFlag);
            Debug.Log("Defending flag added: IT");
        }
        if (defendingFaction.ToString() == "Austrian")
        {
            captureFlags newFlag = new captureFlags();

            newFlag.flagFaction = "Austrian";
            newFlag.flagObj = GameObject.Find("ww1_Flag_Australian_Interactable");
            newFlag.flagPlayerId = 0;
            newFlag.endTime = -1;
            newFlag.callTimer = 0;

            flagList.Add(newFlag);
            Debug.Log("Defending flag added: AU");
        }

        Debug.Log(mapName);
        switch (mapName)
        {
            case "ChampsdAmbre":
                Debug.Log("In ChampsdAmbre");

                spawnList[0].spawn.transform.position = new Vector3(4.0f, 13.71f, -195.0f);
                spawnList[0].spawn.transform.localScale = new Vector3(30, 30, 30);

                spawnList[0].spawn.SetActive(false);

                spawnList[1].spawn.transform.position = new Vector3(-0.5f, 13.71f, 230.0f);
                spawnList[1].spawn.transform.localScale = new Vector3(30, 30, 30);

                spawnList[1].spawn.SetActive(false);
                break;

            default:

                break;
        }
    }

    public void OnPlayerEndCarry(int playerId)
    {
        Debug.Log("In OnPlayerEndCarry");

        timer = 60.0f;

        for (int x = 0; x < flagList.Count; x++)
        {
            for (int y = 0; y < spawnList.Count; y++)
            {
                Debug.Log(flagList[x].flagFaction + " " + spawnList[y].spawnFaction);

                if (flagList[x].flagFaction != spawnList[y].spawnFaction)
                {
                    Debug.Log(flagList[x].flagPlayerId + " " + playerId);
                    if (flagList[x].flagPlayerId == playerId)
                    {
                        Debug.Log(flagList[x].flagObj.transform.position.x + " " + spawnList[y].spawn.transform.position.x);
                        if (Mathf.Abs(flagList[x].flagObj.transform.position.x - spawnList[y].spawn.transform.position.x) < 30)
                        {
                            Debug.Log(flagList[x].flagObj.transform.position.z + " " + spawnList[y].spawn.transform.position.z);
                            if (Mathf.Abs(flagList[x].flagObj.transform.position.z - spawnList[y].spawn.transform.position.z) < 30)
                            {
                                Debug.Log("The " + flagList[x].flagFaction + " flag is in enemy spawn.");

                                var flagInSpawn = string.Format("broadcast The {0} flag is in the enemy spawn! You have {1} seconds to get it out.", flagList[x].flagFaction, timer);
                                f1MenuInputField.onEndEdit.Invoke(flagInSpawn);

                                flagList[x].endTime = timeelapsed + timer;
                                Debug.Log(flagList[x].endTime);
                            }
                        }
                    }
                }
            }
        }
    }

    public void OnUpdateElapsedTime(float time)
    {
        timeelapsed = Mathf.Floor(time);

        string fact1 = flagList[0].flagFaction;
        string fact2 = flagList[1].flagFaction;

        for (int x = 0; x < flagList.Count; x++)
        {
            if(Mathf.Floor(time) == flagList[x].endTime - 15.0 && flagList[x].callTimer == 0)
            {
                Debug.Log("15 second warning");

                var endRoundFromFlag = string.Format("broadcast The {0} flag is in the enemy spawn! You have 15 seconds to get it out.", flagList[x].flagFaction);
                f1MenuInputField.onEndEdit.Invoke(endRoundFromFlag);
                flagList[x].callTimer = 1;
            }


            if (flagList[x].endTime == Mathf.Floor(time) && flagList[x].callTimer == 1)
            {
                Debug.Log("endTime is equal to time");

                if (flagList[x].flagFaction == fact1)
                {
                    var endRoundFromFlag = string.Format("set roundEndFactionWin {0} None", fact2);
                    f1MenuInputField.onEndEdit.Invoke(endRoundFromFlag);

                    flagList[x].callTimer = 2;
                }
                else
                {
                    {
                        var endRoundFromFlag = string.Format("set roundEndFactionWin {0} None", fact1);
                        f1MenuInputField.onEndEdit.Invoke(endRoundFromFlag);

                        flagList[x].callTimer = 2;
                    }
                }
            }
        }
    }

    public void PassConfigVariables(string[] value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            var splitData = value[i].Split(':');

            if (splitData[0] == "ctf")
            {
                if (splitData[1] == "dropHeldWeapon")
                {
                    if (splitData[2] == "true")
                    {

                    }
                    else
                    {

                    }
                }
                else if (splitData[1] == "class")
                {
                    if (splitData[2] == "ArmyInfantryOfficer")
                    {

                    }
                    else if (splitData[2] == "ArmyLineInfantry")
                }
            }
        }
    }

    public void OnPlayerKilledPlayer(int killerPlayerId, int victimPlayerId, EntityHealthChangedReason reason, string additionalDetails)
    {

    }

    public void OnPlayerBlock(int attackingPlayerId, int defendingPlayerId)
    {
        
    }

    public void OnScorableAction(int playerId, int score, ScorableActionType reason)
    {
    
    }

    public void OnPlayerHurt(int playerId, byte oldHp, byte newHp, EntityHealthChangedReason reason)
    {
    
    }

    public void OnIsClient(bool client, ulong steamId)
    {

    }

    public void OnUpdateTimeRemaining(float time)
    {

    }

    public void OnPlayerWeaponSwitch(int playerId, string weapon)
    {

    }

    public void OnTextMessage(int playerId, TextChatChannel channel, string text)
    {

    }

    public void OnIsServer(bool server)
    {
        var canvases = Resources.FindObjectsOfTypeAll<Canvas>();
        for (int i = 0; i < canvases.Length; i++)
        {
            //Find the one that's called "Game Console Panel"
            if (string.Compare(canvases[i].name, "Game Console Panel", true) == 0)
            {
                //Inside this, now we need to find the input field where the player types messages.
                f1MenuInputField = canvases[i].GetComponentInChildren<InputField>(true);
                if (f1MenuInputField != null)
                {
                    Debug.Log("Found the Game Console Panel");
                }
                else
                {
                    Debug.Log("We did Not find Game Console Panel");
                }
                break;
            }
        }
    }

    public void OnConsoleCommand(string input, string output, bool success)
    {

    }

    public void OnSyncValueState(int value)
    {

    }

    public void OnUpdateSyncedTime(double time)
    {

    }

    public void OnDamageableObjectDamaged(GameObject damageableObject, int damageableObjectId, int shipId, int oldHp, int newHp)
    {

    }

    public void OnPlayerShoot(int playerId, bool dryShot)
    {

    }

    public void OnPlayerMeleeStartSecondaryAttack(int playerId)
    {

    }

    public void OnCapturePointCaptured(int capturePoint)
    {

    }

    public void OnCapturePointOwnerChanged(int capturePoint, FactionCountry factionCountry)
    {

    }

    public void OnCapturePointDataUpdated(int capturePoint, int defendingPlayerCount, int attackingPlayerCount)
    {

    }

    public void OnRoundEndFactionWinner(FactionCountry factionCountry, FactionRoundWinnerReason reason)
    {

    }

    public void OnRoundEndPlayerWinner(int playerId)
    {

    }

    public void OnPlayerStartCarry(int playerId, CarryableObjectType carryableObject)
    {

    }

    public void OnPlayerShout(int playerId, CharacterVoicePhrase voicePhrase)
    {

    }

    public void OnEmplacementPlaced(int itemId, GameObject objectBuilt, EmplacementType emplacementType)
    {

    }

    public void OnEmplacementConstructed(int itemId)
    {

    }

    public void OnBuffStart(int playerId, BuffType buff)
    {

    }

    public void OnBuffStop(int playerId, BuffType buff)
    {

    }

    public void OnShotInfo(int playerId, int shotCount, Vector3[][] shotsPointsPositions, float[] trajectileDistances, float[] distanceFromFiringPositions, float[] horizontalDeviationAngles, float[] maxHorizontalDeviationAngles, float[] muzzleVelocities, float[] gravities, float[] damageHitBaseDamages, float[] damageRangeUnitValues, float[] damagePostTraitAndBuffValues, float[] totalDamages, Vector3[] hitPositions, Vector3[] hitDirections, int[] hitPlayerIds, int[] hitDamageableObjectIds, int[] hitShipIds, int[] hitVehicleIds)
    {

    }

    public void OnVehicleSpawned(int vehicleId, FactionCountry vehicleFaction, PlayerClass vehicleClass, GameObject vehicleObject, int ownerPlayerId)
    {

    }

    public void OnVehicleHurt(int vehicleId, byte oldHp, byte newHp, EntityHealthChangedReason reason)
    {

    }

    public void OnPlayerKilledVehicle(int killerPlayerId, int victimVehicleId, EntityHealthChangedReason reason, string details)
    {

    }

    public void OnShipSpawned(int shipId, GameObject shipObject, FactionCountry shipfaction, ShipType shipType, int shipNameId)
    {

    }

    public void OnShipDamaged(int shipId, int oldHp, int newHp)
    {

    }

    public void OnAdminPlayerAction(int playerId, int adminId, ServerAdminAction action, string reason)
    {

    }

    public void OnRCLogin(int playerId, string inputPassword, bool isLoggedIn)
    {

    }

    public void OnRCCommand(int playerId, string input, string output, bool success)
    {

    }

    public void OnPlayerPacket(int playerId, byte? instance, Vector3? ownerPosition, double? packetTimestamp, Vector2? ownerInputAxis, float? ownerRotationY, float? ownerPitch, float? ownerYaw, PlayerActions[] actionCollection, Vector3? cameraPosition, Vector3? cameraForward, ushort? shipID, bool swimming)
    {

    }

    public void OnVehiclePacket(int vehicleId, Vector2 inputAxis, bool shift, bool strafe, PlayerVehicleActions[] actionCollection)
    {

    }

    public void OnOfficerOrderStart(int officerPlayerId, HighCommandOrderType highCommandOrderType, Vector3 orderPosition, float orderRotationY, int voicePhraseRandomIndex)
    {

    }

    public void OnOfficerOrderStop(int officerPlayerId, HighCommandOrderType highCommandOrderType)
    {

    }
}