using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using SFS.World.Maps;

namespace AeroTrajectory
{
    public static class Patches
    {
        [HarmonyPatch(typeof(MapManager), "DrawMap")]
        static class MapManager_DrawMap
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> codes = instructions.ToList();
                var newCodes = new CodeInstruction[]
                {
                    // CodeInstruction.LoadField(typeof(TrajectoryManager), nameof(TrajectoryManager.main)),
                    CodeInstruction.Call(typeof(TrajectoryManager), nameof(TrajectoryManager.DrawTrajectory)),
                };
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].Calls(AccessTools.Method(typeof(MapManager), "DrawTrajectories")))
                    {
                        codes.InsertRange(i + 1, newCodes);
                        return codes;
                    }
                }
                return codes;
            }
        }
    }
}