using HarmonyLib;
using UnityEngine;
using ModLoader;
using ModLoader.Helpers;
using UITools;
using SFS.IO;
using System.Collections.Generic;

namespace AeroTrajectory
{
    public class Main : Mod
    {
        public static Main main;
        public static FilePath settingsPath;
        public static AeroTrajectorySettings settings;
        public override string ModNameID => "aerotrajectory";
        public override string DisplayName => "Aero Trajectory";
        public override string Author => "Astro The Rabbit";
        public override string MinimumGameVersionNecessary => "1.5.10.2";
        public override string ModVersion => "v1.0";
        public override string Description => "Draws simulated atmospheric trajectories in map view.";

        // public override Dictionary<string, string> Dependencies { get; } = new Dictionary<string, string> { { "UITools", "1.1.1" } };
        public Dictionary<string, FilePath> UpdatableFiles => new Dictionary<string, FilePath>() { { "https://github.com/AstroTheRabbit/Aero-Trajectory-SFS/releases/latest/download/AeroTrajectory.dll", new FolderPath(ModFolder).ExtendToFile("AeroTrajectory.dll") } };

        public override void Early_Load()
        {
            main = this;
            new Harmony(ModNameID).PatchAll();
        }

        public override void Load()
        {
            SceneHelper.OnWorldSceneLoaded += TrajectoryManager.LoadAeroTrajectory;
            SceneHelper.OnWorldSceneUnloaded += TrajectoryManager.UnloadAeroTrajectory;
            
            settingsPath = new FolderPath(main.ModFolder).ExtendToFile("settings.txt");
            if (!AeroTrajectorySettings.TryLoad(out settings))
                (settings = new AeroTrajectorySettings()).Save();
        }
    }
}
