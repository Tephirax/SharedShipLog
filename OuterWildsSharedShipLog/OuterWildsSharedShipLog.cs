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

        protected static string DisplayName(string astralBodyID)
        {
            switch (astralBodyID)
            {
                case "SUN_STATION":
                    return "Sun Station";
                case "CAVE_TWIN":
                    return "Ember Twin";
                case "TOWER_TWIN":
                    return "Ash Twin";
                case "TIMBER_HEARTH":
                    return "Timber Hearth";
                case "TIMBER_MOON":
                    return "The Attlerock";
                case "BRITTLE_HOLLOW":
                    return "Brittle Hollow";
                case "VOLCANIC_MOON":
                    return "Hollow's Lantern";
                case "GIANTS_DEEP":
                    return "Giant's Deep";
                case "ORBITAL_PROBE_CANNON":
                    return "Orbital Probe Cannon";
                case "DARK_BRAMBLE":
                    return "Dark Bramble";
                case "WHITE_HOLE":
                    return "White Hole";
                case "COMET":
                    return "The Interloper";
                case "QUANTUM_MOON":
                    return "Quantum Moon";
                case "INVISIBLE_PLANET":
                    return "The Stranger";
                default:
                    return astralBodyID;
            };
        }

        public static void WriteLog(List<ShipLogEntry> entryList)
        {
            // Create a list of astral bodies which will be stacked under the user.
            Dictionary<string, List<Entry>> astralBodies = new Dictionary<string, List<Entry>>();

            foreach (ShipLogEntry astralBodyEntry in entryList)
            {
                // Create new entry to store those facts
                Entry writeEntry = new Entry();

                // Get boolean of moreToExplore from the OW method
                writeEntry._moreToExplore = astralBodyEntry.HasMoreToExplore();

                // Create a list of entryContents stacked under the entry, called writeEntry.entryContents
                writeEntry.entryContents = new List<EntryContents>();

                // Get log facts for display from the OW method
                List<ShipLogFact> factsForDisplay = astralBodyEntry.GetFactsForDisplay();

                // If there are no facts in the list, skip to next astral body
                if (factsForDisplay.Count() == 0) continue;

                // Initialise a factCounter variable for fact ordering in the output
                int factCounter = 0;

                // Cycle through all facts in the list
                do
                {
                    ShipLogFact fact = factsForDisplay[factCounter++];

                    // Write values
                    writeEntry._entryID = fact._entryID;
                    writeEntry._entryName = astralBodyEntry.GetName(false);
                    writeEntry._rumor = fact._rumor;

                    // Create new entryContents to store the id and text
                    EntryContents writeEntryContents = new EntryContents();
                    writeEntryContents._factID = factCounter;
                    writeEntryContents._text = fact._text;

                    // Write entryContents developed above to writeEntry object
                    writeEntry.entryContents.Add(writeEntryContents);
                }
                while (factCounter < factsForDisplay.Count());

                // Write entry developed above to writeUser object only if at least one fact is to be displayed
                string astralBodyName = DisplayName(astralBodyEntry._astroObjectID);
                if (!astralBodies.ContainsKey(astralBodyName)) astralBodies.Add(astralBodyName, new List<Entry>());
                astralBodies[astralBodyName].Add(writeEntry);
            }

            // Convert dictionary back to a list that can be serialized into the existing JSON format
            User writeUser = new User() { _twitchID = OuterWildsSharedShipLog.Instance.twitchID, astralBodies = new List<AstralBody>() };
            foreach (var (key, value) in astralBodies) writeUser.astralBodies.Add(new AstralBody() { _astroObjectID = key, entry = value });
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
                var uwr = new UnityWebRequest("https://shared-ship-log-server.vercel.app/api/shiplogs", "POST");
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



