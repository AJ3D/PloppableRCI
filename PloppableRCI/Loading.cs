using System.Linq;
using ICities;
using ColossalFramework.Plugins;
using System;

namespace PloppableRICO
{
#if DEBUG
    [ProfilerAspect()]
#endif
    public class Loading : LoadingExtensionBase
	{
        private XMLManager xmlManager;
        private ConvertPrefabs convertPrefabs;
        
        public override void OnLevelLoaded(LoadMode mode)
        {
            base.OnLevelLoaded(mode);
            
            if (mode != LoadMode.LoadGame && mode != LoadMode.NewGame)
            return;

            //Load xml
            if (xmlManager == null)
            {
                xmlManager = new XMLManager();
                xmlManager.Run();
            }

            //Assign xml settings to prefabs.

            if (convertPrefabs == null)
                {
                convertPrefabs = new ConvertPrefabs();
                convertPrefabs.run();
                 }
            
            //Init GUI
            PloppableTool.Initialize();
            RICOSettingsPanel.Initialize();

            //Deploy Detour
            Detour.BuildingToolDetour.Deploy();

        }

        public override void OnLevelUnloading()
        { 

            Util.AssignServiceClass();
            base.OnLevelUnloading ();
            //RICO ploppables need a non private item class assigned to pass though the game reload. 
		}

		public override void OnReleased ()
		{
            Util.AssignServiceClass();
            Detour.BuildingToolDetour.Revert();
            base.OnReleased ();
		}
	}
}
