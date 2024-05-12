using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UITools;
using SFS.IO;
using ModLoader;
using ModLoader.Helpers;

namespace AeroTrajectory
{
    public class Main : Mod, IUpdatable
    {
        public static Main main;
        public static Assembly glidingHeatshields;
        
        public override string ModNameID => "aerotrajectory";
        public override string DisplayName => "Aero Trajectory";
        public override string Author => "Astro The Rabbit";
        public override string MinimumGameVersionNecessary => "1.5.10.2";
        public override string ModVersion => "v1.4";
        public override string Description => "Adds simulated aerodynamic trajectories to the map view.";

       public Dictionary<string, FilePath> UpdatableFiles => new Dictionary<string, FilePath>() { { "https://github.com/AstroTheRabbit/Aero-Trajectory-SFS/releases/latest/download/AeroTrajectory.dll", new FolderPath(ModFolder).ExtendToFile("AeroTrajectory.dll") } };

        public override void Early_Load()
        {
            main = this;
            new Harmony(ModNameID).PatchAll();
        }

        public override void Load()
        {
            Settings.Init(new FolderPath(main.ModFolder).ExtendToFile("settings.txt"));
            
            try
            {
                glidingHeatshields = Loader.main.GetLoadedMods().First((Mod m) => m.ModNameID == "GLIDING_HEAT_SHIELDS").GetType().Assembly;
            }
            catch
            {
                Debug.Log("Aero Trajectory: Gliding Heatshields not installed/active.");
            }
        }
    }
}
