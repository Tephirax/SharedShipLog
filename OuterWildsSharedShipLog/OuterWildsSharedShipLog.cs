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
            OuterWildsSharedShipLog.Instance.ModHelper.Console.WriteLine("Exporting Ship Log to JSON format");

            // Get list of entries
            List<ShipLogEntry> entryList = __instance._manager.GetEntryList();
            
            WriteLog(entryList);
        }

        public static void WriteLog(List<ShipLogEntry> entryList)
        {
            // Create new user and assign twitchID
            User writeUser = new User();
            writeUser._twitchID = OuterWildsSharedShipLog.Instance.twitchID;

            // Create a list of entries stacked under the user, called writeUser.entry
            writeUser.entry = new List<Entry>();

            // Cycle through entries in the list
            foreach (ShipLogEntry entry in entryList)
            {
                // Create new entry to store those facts
                Entry writeEntry = new Entry();

                writeEntry._astroObjectID = entry._astroObjectID;
                
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
                    // Increment factCounter
                    factCounter++;
                    writeEntry._entryID = fact._entryID;
                    writeEntry._entryName = fact._entryName;
                    writeEntry._rumor = fact._rumor;

                    // Create new entryContents to store the id and text
                    EntryContents writeEntryContents = new EntryContents();
                    writeEntryContents._factID = factCounter;
                    writeEntryContents._text = fact._text;

                    // Write entryContents developed above to writeEntry object
                    writeEntry.entryContents.Add(writeEntryContents);
                }

                // Write entry developed above to writeUser object only if at least one fact is to be displayed
                if (factCounter > 0) {
                    writeUser.entry.Add(writeEntry);
                    OuterWildsSharedShipLog.Instance.ModHelper.Console.WriteLine("Wrote Entry for " + writeEntry._entryName);
                }
            }
        
            WriteLogJSON(writeUser);
        }

        public static void WriteLogJSON(User user)
        {
            OuterWildsSharedShipLog.Instance.ModHelper.Console.WriteLine("_twitchID=" + user._twitchID);
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
                OuterWildsSharedShipLog.Instance.ModHelper.Console.WriteLine("uploadYN is true and outputID is not missing. Upload should be attempted");

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
        public List<Entry> entry;
    }

    [Serializable]
    public class Entry
    {
        public string _entryID;
        public string _astroObjectID;
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

    

