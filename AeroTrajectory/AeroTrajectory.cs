using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using SFS.World;
using SFS.WorldBase;
using SFS.World.Drag;
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
                                float a = (float) orbit.sma;
                                float e = (float) orbit.ecc;
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

    public class TrajectorySimulation
    {
        public Planet planet;
        readonly float dragCoefficient;
        readonly float? glidingHeatshields_dragCoefficient;
        Vector2 currentPos;
        Vector2 currentVel;
        Vector2 currentAcc;
        bool enteredAtmosphere;

        public TrajectorySimulation(Rocket player, Location startLocation, float angle)
        {
            List<Surface> exposedSurfaces = GetExposedSurfaces(player, angle);
            Location location = startLocation;

            planet = location.planet;
            dragCoefficient = GetDragCoefficent(exposedSurfaces) / player.mass.GetMass();
            currentPos = location.position.ToVector2;
            currentVel = location.velocity.ToVector2;
            currentAcc = GetAcceleration(currentPos, currentVel);
            enteredAtmosphere = false;

            glidingHeatshields_dragCoefficient = null;
            if (Main.glidingHeatshields != null)
            {
                try
                {
                    // ? https://github.com/Kaskouy/SFS-Gliding-heat-shields/blob/main/Patch_AeroModule.cs
                    (float lift, _) = ((float, Vector2)) Main.glidingHeatshields
                        .GetType("TestModSFS.Patch_AeroModule")
                        .GetMethod("CalculateLiftForce", BindingFlags.NonPublic | BindingFlags.Static)
                        .Invoke(null, new[] { exposedSurfaces });
                    glidingHeatshields_dragCoefficient = 1.5f * lift / player.mass.GetMass();
                }
                catch (Exception e)
                {
                    Debug.Log(new Exception("Aero Trajectory: Error whilst trying to get Gliding Heatshields coefficient", e));
                }
            }
        }

        public Vector2? Step()
        {
            float currentRadius = currentPos.magnitude;
            if (currentRadius <= (float) planet.Radius)
            {
                // * Hit surface.
                return null;
            }

            if (IsInsideAtmosphere(planet, currentRadius))
                enteredAtmosphere = true;
            if (enteredAtmosphere && !IsInsideAtmosphere(planet, currentRadius))
            {
                // * Trajectory escaped atmosphere.
                if (Settings.settings.showEscapeOrbit)
                {
                    Location location = new Location(WorldTime.main.worldTime, planet, (Double2) currentPos, (Double2) currentVel);
                    TrajectoryDrawer.DrawDashed(new Orbit(location, false, true), true, false, true, Settings.settings.trajectoryColor);
                }
                return null;
            }

            // ? Verlet integration - https://en.wikipedia.org/wiki/Verlet_integration#Algorithmic_representation
            float dt = Settings.settings.simulationStepSize;
            Vector2 newPos = currentPos + (currentVel * dt) + (0.5f * currentAcc * dt * dt);
            Vector2 newAcc = GetAcceleration(currentPos, currentVel);
            Vector2 newVel = currentVel + (0.5f * dt * (currentAcc + newAcc));
            currentPos = newPos;
            currentVel = newVel;
            currentAcc = newAcc;
            
            return currentPos;
        }

        public static bool IsInsideAtmosphere(Planet planet, double radius)
        {
            if (planet.data.hasAtmospherePhysics)
            {
                return radius < planet.data.basics.radius + planet.data.atmospherePhysics.height;
            }
            return false;
        }

        public static float GetDragCoefficent(List<Surface> exposedSurfaces)
        {
            return (
                ((float, Vector2)) typeof(AeroModule)
                    .GetMethod("CalculateDragForce", BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, new[] { exposedSurfaces })
                )
                .Item1;
        }

        public static List<Surface> GetExposedSurfaces(Rocket player, float angle)
        {
            return AeroModule.GetExposedSurfaces(Aero_Rocket.GetDragSurfaces(player.partHolder, Matrix2x2.Angle(-angle)));
        }

        Vector2 GetAcceleration(Vector2 pos, Vector2 vel)
        {
            return GetDragAcceleration(pos, vel) + GetGravitationalAcceleration(pos) + GetGlidingHeatshieldsAcceleration(pos, vel);
        }

        Vector2 GetDragAcceleration(Vector2 pos, Vector2 vel)
        {
            float atmoDensity = (float) planet.GetAtmosphericDensity(pos.magnitude - planet.Radius);
            float force = dragCoefficient * 1.5f * vel.sqrMagnitude;
            return force * atmoDensity * -vel.normalized;
        }

        Vector2 GetGlidingHeatshieldsAcceleration(Vector2 pos, Vector2 vel)
        {
            if (glidingHeatshields_dragCoefficient is float coefficient)
            {
                return coefficient * (float) planet.GetAtmosphericDensity(pos.magnitude - planet.Radius) * (float) vel.sqrMagnitude * (-vel.normalized).Rotate_90();
            }
            else
            {
                return Vector2.zero;
            }
        }

        Vector2 GetGravitationalAcceleration(Vector2 pos)
        {
            return (Vector2) planet.GetGravity((Double2) pos);
        }
    }
}