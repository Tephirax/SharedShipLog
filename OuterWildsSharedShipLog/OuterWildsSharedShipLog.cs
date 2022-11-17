using HarmonyLib;
using OWML.Common;
using OWML.ModHelper;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using TMPro;
using System.Linq;

namespace OuterWildsSharedShipLog
{
    public class OuterWildsSharedShipLog : ModBehaviour
    {
        public static OuterWildsSharedShipLog Instance; // Declare a singleton instance called Instance
        public string twitchID; // Initialise user ID for ship log upload 
        public bool uploadYN; // Initialise toggle for Ship Log auto-upload

        private void Awake()
        {
            // You won't be able to access OWML's mod helper in Awake.
            // So you probably don't want to do anything here.
            // Use Start() instead.
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
            Instance = this; // Initialise singleton. Now I can access the ModHelper anywhere using OuterWildsSharedShipLog.Instance.ModHelper
        }

        // Assign initial values of mod settings
        public void Start()
        {
            twitchID = ModHelper.Config.GetSettingsValue<string>("Twitch Username").ToLower();
            uploadYN = ModHelper.Config.GetSettingsValue<bool>("Online Ship Log");
        }

        // Listen for changes in mod settings (else settings will not change until OW is restarted) - TODO: This hasn't been tested yet!
        public override void Configure(IModConfig config)
        {
            twitchID = ModHelper.Config.GetSettingsValue<string>("Twitch Username").ToLower();
            uploadYN = ModHelper.Config.GetSettingsValue<bool>("Online Ship Log");
        }
    }

    // Run whenever the Ship Log Reveal Animation completes
    [HarmonyPatch]
    public class FinishRevealAnimationPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(ShipLogDetectiveMode), nameof(ShipLogDetectiveMode.FinishRevealAnimation))]
        public static void ShipLogDetectiveMode_FinishRevealAnimation_Postfix(ShipLogDetectiveMode __instance)
        {
            OuterWildsSharedShipLog.Instance.ModHelper.Console.WriteLine("Exporting Ship Log for user " + OuterWildsSharedShipLog.Instance.twitchID);

            // Get list of entries
            List<ShipLogEntry> entryList = __instance._manager.GetEntryList();
            
            WriteLog(entryList);
        }

        public static void WriteLog(List<ShipLogEntry> entryList)
        {
            // Create new user and assign twitchID
            User writeUser = new User();
            writeUser._twitchID = OuterWildsSharedShipLog.Instance.twitchID;

            // Create a list of astral bodies stacked under the user, called writeUser.astralBodies.
            writeUser.astralBodies = new List<AstralBody>();

            string _prevAstralBody = "";

            foreach (ShipLogEntry astralBodyEntry in entryList)
            {
                // Only do this for the first entry for each Astral Body
                if (astralBodyEntry._astroObjectID != _prevAstralBody)
                {

                    AstralBody writeAstralBody = new AstralBody();
                    
                    writeAstralBody._astroObjectID = astralBodyEntry._astroObjectID;

                    //OuterWildsSharedShipLog.Instance.ModHelper.Console.WriteLine("Current astral body = " + writeAstralBody._astroObjectID);
                    //OuterWildsSharedShipLog.Instance.ModHelper.Console.WriteLine("Previous astral body = " + _prevAstralBody);

                    _prevAstralBody = astralBodyEntry._astroObjectID;

                    // Create a list of entries stacked under the AstralBody, called writeAstralBody.entry
                    writeAstralBody.entry = new List<Entry>();

                    // Initialise an entryCounter variable to check if any entries exist in the astral body
                    int entryCounter = 0;

                    // Then cycle through entries in the list again, but only adding those which are associated with this astral body
                    foreach (ShipLogEntry entry in entryList)
                    {
                        if (entry._astroObjectID == writeAstralBody._astroObjectID)
                        {

                            // Create new entry to store those facts
                            Entry writeEntry = new Entry();

                            // Get boolean of moreToExplore from the OW method
                            writeEntry._moreToExplore = entry.HasMoreToExplore();

                            // Create a list of entryContents stacked under the entry, called writeEntry.entryContents
                            writeEntry.entryContents = new List<EntryContents>();

                            // Get log facts for display from the OW method
                            List<ShipLogFact> factsForDisplay = entry.GetFactsForDisplay();

                            // Initialise a factCounter variable for fact ordering in the output
                            int factCounter = 0;

                            // Cycle through all facts in the list
                            foreach (ShipLogFact fact in factsForDisplay)
                            {
                                // Increment entryCounter and factCounter
                                entryCounter++;
                                factCounter++;

                                // Write values
                                writeEntry._entryID = fact._entryID;
                                writeEntry._entryName = entry.GetName(false);
                                writeEntry._rumor = fact._rumor;

                                // Create new entryContents to store the id and text
                                EntryContents writeEntryContents = new EntryContents();
                                writeEntryContents._factID = factCounter;
                                writeEntryContents._text = fact._text;

                                // Write entryContents developed above to writeEntry object
                                writeEntry.entryContents.Add(writeEntryContents);
                            }

                            // Write entry developed above to writeUser object only if at least one fact is to be displayed
                            if (factCounter > 0)
                            {
                                writeAstralBody.entry.Add(writeEntry);
                                //OuterWildsSharedShipLog.Instance.ModHelper.Console.WriteLine("Wrote Entries for " + writeEntry._entryID);
                            }
                        }
                    }
                    if (entryCounter > 0) {
                        // Map values to display values
                        switch (writeAstralBody._astroObjectID)
                        {
                            case "SUN_STATION":
                                writeAstralBody._astroObjectID = "Sun Station";
                                break;
                            case "CAVE_TWIN":
                                writeAstralBody._astroObjectID = "Ember Twin";
                                break;
                            case "TOWER_TWIN":
                                writeAstralBody._astroObjectID = "Ash Twin";
                                break;
                            case "TIMBER_HEARTH":
                                writeAstralBody._astroObjectID = "Timber Hearth";
                                break;
                            case "TIMBER_MOON":
                                writeAstralBody._astroObjectID = "The Attlerock";
                                break;
                            case "BRITTLE_HOLLOW":
                                writeAstralBody._astroObjectID = "Brittle Hollow";
                                break;
                            case "VOLCANIC_MOON":
                                writeAstralBody._astroObjectID = "Hollow's Lantern";
                                break;
                            case "GIANTS_DEEP":
                                writeAstralBody._astroObjectID = "Giant's Deep";
                                break;
                            case "ORBITAL_PROBE_CANNON":
                                writeAstralBody._astroObjectID = "Orbital Probe Cannon";
                                break;
                            case "DARK_BRAMBLE":
                                writeAstralBody._astroObjectID = "Dark Bramble";
                                break;
                            case "WHITE_HOLE":
                                writeAstralBody._astroObjectID = "White Hole";
                                break;
                            case "COMET":
                                writeAstralBody._astroObjectID = "The Interloper";
                                break;
                            case "QUANTUM_MOON":
                                writeAstralBody._astroObjectID = "Quantum Moon";
                                break;
                            case "INVISIBLE_PLANET":
                                writeAstralBody._astroObjectID = "The Stranger";
                                break;
                            default:
                                writeAstralBody._astroObjectID = astralBodyEntry._astroObjectID;
                                break;
                        };
                        writeUser.astralBodies.Add(writeAstralBody);
                        //OuterWildsSharedShipLog.Instance.ModHelper.Console.WriteLine("Wrote AstralBody for " + writeAstralBody._astroObjectID);
                    }
                }
            }
            WriteLogJSON(writeUser);
        }

        public static void WriteLogJSON(User user)
        {
            //OuterWildsSharedShipLog.Instance.ModHelper.Console.WriteLine("_twitchID=" + user._twitchID);
            string jsonString = JsonConvert.SerializeObject(user);
            //string jsonString = JsonUtility.ToJson(user);
            string filePath = Path.Combine(Application.dataPath, "SharedShipLog.json");
            
            Debug.Log("Attempting to write log to" + filePath);
            File.WriteAllText(filePath, jsonString);
            PostJSON(jsonString);
        }

        public static void PostJSON(string jsonString)
        {
            if (OuterWildsSharedShipLog.Instance.uploadYN && OuterWildsSharedShipLog.Instance.twitchID != "")
            {
                var uwr = new UnityWebRequest("http://localhost:3000/api/shiplogs", "POST");
                byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(jsonString);
                uwr.uploadHandler = (UploadHandler)new UploadHandlerRaw(jsonToSend);
                uwr.SetRequestHeader("Content-Type", "application/json");
                uwr.SendWebRequest();
            }
        }
    }
        
    [Serializable]
    public class User
    {
        public string _twitchID;
        public List<AstralBody> astralBodies;
    }

    [Serializable]
    public class AstralBody
    {
        public string _astroObjectID;
        public List<Entry> entry;
    }

    [Serializable]
    public class Entry
    {
        public string _entryID;
        public string _entryName;
        public bool _moreToExplore;
        public bool _rumor;
        public List<EntryContents> entryContents;
    }

    [Serializable]
    public class EntryContents
    {
        public int _factID;
        public string _text;
    }
}

    

