﻿using ColossalFramework.UI;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using ColossalFramework.Globalization;
using UnityEngine;
using ColossalFramework.IO;
using ColossalFramework.Packaging;
using System.IO;
using System.Xml.Serialization;
using ColossalFramework.Plugins;
using System.Linq;

namespace PloppableRICO
{
    /// <summary>
    ///This class reads the XML settings, and applies RICO settings to prefabs. Its based on boformers Sub-Building Enabler mod. Many thanks to him for his work. 
    /// </summary>
    /// 
#if DEBUG
    [ProfilerAspect()]
#endif
    public class XMLManager
    {
        public static Dictionary<BuildingInfo, BuildingData> prefabHash;
        public static List<BuildingData> prefabList;
        public static BuildingData Instance;

        public void Run()
        {
            //This is the data object that holds all of the RICO settings. Its read by the tool panel and the settings panel.
            //It contains one entry for every ploppable building, and any RICO settings they may have.

            prefabHash = new Dictionary<BuildingInfo, BuildingData>();
            prefabList = new List<BuildingData>();
            //Loop though all prefabs
            for (uint i = 0; i < PrefabCollection<BuildingInfo>.LoadedCount(); i++)
            {
                var prefab = PrefabCollection<BuildingInfo>.GetLoaded(i);

                //Add one entry for every ploppable building
                //if ( prefab.m_class.m_service == ItemClass.Service.Beautification ||
                /// prefab.m_class.m_service == ItemClass.Service.Monument ||
                //prefab.m_class.m_service == ItemClass.Service.Electricity ||
                // prefab.m_class.m_service == ItemClass.Service.Education)
                {
                    var buildingData = new BuildingData
                    {
                        prefab = prefab,
                        name = prefab.name,
                        category = AssignCategory(prefab)
                    };

                    Profiler.Info(String.Format("XMLManager.run : prefab.name = {0}, category = {1}", prefab.name, prefab.category));
                    prefabList.Add(buildingData);
                    prefabHash[prefab] = buildingData;
                }
            }

            //RICO settings can come from 3 sources. Local settings are applied first, followed by asset author settings,
            //and then finaly settings from settings mods.

            //If local settings are present, load them.
            if (File.Exists("LocalRICOSettings.xml"))
            {
                RicoSettings("LocalRICOSettings.xml", isLocal: true);
            }

            //Import settings from asset folders.
            AssetSettings();

            //If settings mod is active, load its settings.
            if (Util.IsModEnabled(629850626uL))
            {
                var workshopModSettingsPath = Path.Combine(Util.SettingsModPath("629850626"), "WorkshopRICOSettings.xml");
                RicoSettings(workshopModSettingsPath, isMod: true);
                Debug.Log("Settings Mod Works: " + workshopModSettingsPath);
            }
        }


        //Load RICO Settings from asset folders
        public void AssetSettings()
        {
            var checkedPaths = new List<string>();
            var foo = new Dictionary<string, string>();


            for (uint i = 0; i < PrefabCollection<BuildingInfo>.LoadedCount(); i++)
            {
                var prefab = PrefabCollection<BuildingInfo>.GetLoaded(i);
                if (prefab == null)
                    continue;

                // search for PloppableRICODefinition.xml
                var asset = PackageManager.FindAssetByName(prefab.name);

                if (asset == null || asset.package == null)
                    continue;

                var crpPath = asset.package.packagePath;
                if (crpPath == null)
                    continue;

                var ricoDefPath = Path.Combine(Path.GetDirectoryName(crpPath), "PloppableRICODefinition.xml");

                if (!checkedPaths.Contains(ricoDefPath))
                {
                    checkedPaths.Add(ricoDefPath);
                    foo.Add(asset.package.packageName, ricoDefPath);
                }
            }

            RicoSettings(foo, isAuthored: true);
        }

        public void RicoSettings(string ricoFileName, bool isLocal = false, bool isAuthored = false, bool isMod = false)
        {

            RicoSettings(new List<string>() { ricoFileName }, isLocal, isAuthored, isMod);
        }

        public void RicoSettings(List<string> ricoFileNames, bool isLocal = false, bool isAuthored = false, bool isMod = false)
        {
            var foo = new Dictionary<string, string>();

            foreach (var fileName in ricoFileNames)
            { 
                foo.Add("", fileName);
                
            }

            RicoSettings(foo, isLocal, isAuthored, isMod);
        }

        void RicoSettings(Dictionary<string, string> foo, bool isLocal = false, bool isAuthored = false, bool isMod = false)
        {
            var allParseErrors = new List<string>();

            foreach (var packageId in foo.Keys)
            {
                var ricoDefPath = foo[packageId];

                if (!File.Exists(ricoDefPath))
                {
                    continue;
                }

                PloppableRICODefinition ricoDef = null;

                //if (isMod) ricoDef = RICOReader.ParseRICODefinition(packageId, ricoDefPath, insanityOK: true);
                ricoDef = RICOReader.ParseRICODefinition(packageId, ricoDefPath);

                if (ricoDef != null)
                {
                    var j = 0;
                    foreach (var buildingDef in ricoDef.Buildings)
                    {
                        j++;
                        BuildingInfo pf;

                        pf = Util.FindPrefab(buildingDef.name, packageId);

                        if (pf == null)
                        {
                            try
                            {
                                pf = prefabHash.Values
                                    .Select((p) => p.prefab)
                                    .First((p) => p.name.StartsWith(packageId));
                            }
                            catch { }
                        }
                         
                        if (pf == null)
                        {
                            ricoDef.errors.Add(String.Format("Error while processing RICO - file {0} at building #{1}. ({2})", packageId, j, "Building has not been loaded. Either it is broken, deactivated or not subscribed to." + buildingDef.name + " not loaded. (" + packageId + ")"));
                            continue;
                        }

                        //Add asset author settings to dictionary.
                        if (prefabHash.ContainsKey(pf))
                        {
                            if (isAuthored)
                            {
                                prefabHash[pf].author = buildingDef;
                                prefabHash[pf].hasAuthor = true;
                            }
                            else if (isLocal)
                            {
                                prefabHash[pf].local = buildingDef;
                                prefabHash[pf].hasLocal = true;
                            }
                            else if (isMod)
                            {
                                prefabHash[pf].mod = buildingDef;
                                prefabHash[pf].hasMod = true;
                            }
                        }

                        allParseErrors.AddRange(ricoDef.errors);
                    }
                }
                else
                {
                    allParseErrors.AddRange(RICOReader.LastErrors);
                }
            }

            if (allParseErrors.Count > 0)
            {
                var errorMessage = new StringBuilder();
                foreach (var error in allParseErrors)
                    errorMessage.Append(error).Append('\n');

                UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("Ploppable RICO", errorMessage.ToString(), true);
            }
        }

        private Category AssignCategory(BuildingInfo prefab)
        {

            if (prefab.m_buildingAI is MonumentAI)
            {
                return Category.Monument;
            }
            else if (prefab.m_buildingAI is ParkAI)
            {
                return Category.Beautification;
            }
            else if (prefab.m_buildingAI is PowerPlantAI)
            {
                return Category.Power;
            }
            else if (prefab.m_buildingAI is WaterFacilityAI)
            {
                return Category.Water;
            }
            else if (prefab.m_buildingAI is SchoolAI)
            {
                return Category.Education;
            }
            else if (prefab.m_buildingAI is HospitalAI)
            {
                return Category.Health;
            }
            else if (prefab.m_buildingAI is ResidentialBuildingAI)
            {
                return Category.Residential;
            }
            else if (prefab.m_buildingAI is IndustrialExtractorAI)
            {
                return Category.Industrial;
            }
            else if (prefab.m_buildingAI is IndustrialBuildingAI)
            {
                return Category.Industrial;
            }
            else if (prefab.m_buildingAI is OfficeBuildingAI)
            {
                return Category.Office;
            }
            else if (prefab.m_buildingAI is CommercialBuildingAI)
            {
                return Category.Commercial;
            }



            else return Category.Beautification;

        }

        //This is called by the settings panel. It will serialize any new local settings the player sets in game. 
        public static void SaveLocal(RICOBuilding newBuildingData)
        {
            Debug.Log( "SaveLocal");
            
            if (File.Exists("LocalRICOSettings.xml") && newBuildingData != null)
            {
                PloppableRICODefinition localSettings = null;
                var newlocalSettings = new PloppableRICODefinition();

                var xmlSerializer = new XmlSerializer(typeof(PloppableRICODefinition));

                using (StreamReader streamReader = new System.IO.StreamReader("LocalRICOSettings.xml"))
                {
                    localSettings = xmlSerializer.Deserialize(streamReader) as PloppableRICODefinition;
                }

                foreach (var buildingDef in localSettings.Buildings)
                {
                    if (buildingDef.name != newBuildingData.name)
                    {
                        newlocalSettings.Buildings.Add(buildingDef);
                    }
                }

                //newBuildingData.name = newBuildingData.name;
                newlocalSettings.Buildings.Add(newBuildingData);

                using (TextWriter writer = new StreamWriter("LocalRICOSettings.xml"))
                {
                    xmlSerializer.Serialize(writer, newlocalSettings);
                }
            } 
        }
    }

    //This is the data object definition for the dictionary. It contains one entry for every ploppable building. 
    //Each entry contains up to 3 PloppableRICODef entries. 

    public class BuildingData
    {
        private string m_displayName;
        public BuildingInfo prefab;
        public string name;
        public Category category;

        //RICO settings. 
        public RICOBuilding local;
        public RICOBuilding author;
        public RICOBuilding mod;

        //These are used by the settings panel and tool to determine which settings to use. It will first use local, then asset, and finaly mod. 
        public bool hasAuthor;
        public bool hasLocal;
        public bool hasMod;

        //Called by the settings panel fastlist. 
        public string displayName
        {
            get
            {
                m_displayName = Locale.GetUnchecked("BUILDING_TITLE", name);
                if (m_displayName.StartsWith("BUILDING_TITLE"))
                {
                    m_displayName = name.Substring(name.IndexOf('.') + 1).Replace("_Data", "");
                }
                m_displayName = CleanName(m_displayName, !name.Contains("."));

                return m_displayName;
            }
        }
        public static string CleanName(string name, bool cleanNumbers = false)
        {
            name = Regex.Replace(name, @"^{{.*?}}\.", "");
            name = Regex.Replace(name, @"[_+\.]", " ");
            name = Regex.Replace(name, @"(\d[xX]\d)|([HL]\d)", "");
            if (cleanNumbers)
            {
                name = Regex.Replace(name, @"(\d+[\da-z])", "");
                name = Regex.Replace(name, @"\s\d+", " ");
            }
            name = Regex.Replace(name, @"\s+", " ").Trim();

            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name);
        }
    }
}



