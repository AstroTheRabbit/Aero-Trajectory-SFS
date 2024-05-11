using System;
using System.Collections.Generic;
using UnityEngine;
using SFS.World;
using SFS.World.Maps;

namespace AeroTrajectory
{
    public static class TrajectoryManager
    {
        static SettingsData Settings => AeroTrajectory.Settings.settings;

        static bool CanRunSimulation(out Rocket player, out Location startLocation)
        {
            try
            {
                if (PlayerController.main.player.Value is Rocket rocket)
                {
                    player = rocket;
                    Location currentLocation = player.location.Value;
                    Orbit orbit = Orbit.TryCreateOrbit(currentLocation, true, false, out bool success);
                    if (success)
                    {
                        if (TrajectorySimulation.IsInsideAtmosphere(currentLocation.planet, orbit.periapsis))
                        {
                            if (TrajectorySimulation.IsInsideAtmosphere(currentLocation.planet, currentLocation.Radius))
                            {
                                startLocation = currentLocation;
                                return true;
                            }
                            else
                            {
                                // * Find the starting position of the atmosphere mathematically to save unnecessary simulation steps.
                                // ? Derived from r = a * (1 - e^2) / (1 + e * cos(θ + ω)).
                                float e = (float) orbit.ecc;
                                float a = (float) orbit.sma;
                                if (float.IsInfinity(a))
                                {
                                    // * Hyperbolic trajectory calculations (since a is calculated to equal infinity by the game).
                                    float radius = (float) currentLocation.position.magnitude;
                                    float v_squared = (float) currentLocation.velocity.sqrMagnitude;
                                    float mu = (float) currentLocation.planet.mass;
                                    a = 1 / ((2 / radius) - (v_squared / mu));
                                }

                                float r = (float) (currentLocation.planet.AtmosphereHeightPhysics + currentLocation.planet.Radius);
                                float theta = -orbit.direction * Mathf.Acos((a * (1 - e * e) - r) / (e * r));

                                startLocation = new Location
                                (
                                    currentLocation.planet,
                                    orbit.GetPositionAtTrueAnomaly(theta),
                                    orbit.GetVelocityAtTrueAnomaly(theta)
                                );
                                return true;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.Log(e);
            }
            player = null;
            startLocation = null;
            return false;
        }

        public static void DrawTrajectory()
        {
            try
            {
                if (CanRunSimulation(out Rocket player, out Location startLocation))
                {
                    switch (Settings.simulationType)
                    {
                        case SimulationType.Prograde:
                            RunAndDrawSimulation(player, startLocation, (Mathf.Deg2Rad * player.GetRotation()) - (Mathf.PI / 2));
                            return;
                        case SimulationType.Retrograde:
                            RunAndDrawSimulation(player, startLocation, (Mathf.Deg2Rad * player.GetRotation()) + (Mathf.PI / 2));
                            return;
                        case SimulationType.CurrentAngle:
                            RunAndDrawSimulation(player, startLocation, (float) player.location.velocity.Value.AngleRadians - (Mathf.PI / 2));
                            return;
                        case SimulationType.MinMaxRange:
                            float angleMin = 0, angleMax = 0, dragMin = float.PositiveInfinity, dragMax = float.NegativeInfinity;
                            for (int i = 0; i < 100; i++)
                            {
                                float angle = Mathf.PI * i / 50f;
                                float drag = TrajectorySimulation.GetDragCoefficent(TrajectorySimulation.GetExposedSurfaces(player, angle));
                                if (drag < dragMin)
                                {
                                    dragMin = drag;
                                    angleMin = angle;
                                }
                                else if (drag > dragMax)
                                {
                                    dragMax = drag;
                                    angleMax = angle;
                                }
                            }
                            RunAndDrawSimulation(player, startLocation, angleMin);
                            RunAndDrawSimulation(player, startLocation, angleMax);
                            return;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.Log($"Aero Trajectory: {e}");
            }
        }

        static void RunAndDrawSimulation(Rocket rocket, Location startLocation, float angle)
        {
            TrajectorySimulation simulation = new TrajectorySimulation(rocket, startLocation, angle);
            List<Vector3> points = new List<Vector3>();
            while (simulation.Step() is Vector2 point)
            {
                points.Add(point / 1000f);
                if (points.Count >= Settings.simulationIterations)
                    break;
            }
            (Settings.trajectoryDashedLine ? Map.dashedLine : Map.solidLine).DrawLine(points.ToArray(), simulation.planet, Settings.trajectoryColor, Settings.trajectoryColor);
        }
    }
}