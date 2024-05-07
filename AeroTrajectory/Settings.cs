using UnityEngine;
using SFS.Parsers.Json;

namespace AeroTrajectory
{
    public class AeroTrajectorySettings
    {
        public float simulationStepSize = 0.05f;
        public int simulationIterations = 25000;
        public SimulationType simulationType = SimulationType.CurrentAngle;
        public Color trajectoryColor = Color.red;
        public bool trajectoryDashedLine = true;
        public bool showEscapeOrbit = true;

        public static bool TryLoad(out AeroTrajectorySettings settings)
        {
            return JsonWrapper.TryLoadJson(Main.settingsPath, out settings);
        }

        public void Save()
        {
            JsonWrapper.SaveAsJson(Main.settingsPath, this, true);
        }
    }

    public enum SimulationType
    {
        Prograde,
        Retrograde,
        CurrentAngle,
        MinMaxRange,
    }
}