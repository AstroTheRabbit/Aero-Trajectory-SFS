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
    public class TrajectoryManager : MonoBehaviour
    {
        public static TrajectoryManager main;

        public static void LoadAeroTrajectory()
        {
            main = new GameObject("AeroTrajectory").AddComponent<TrajectoryManager>();
        }

        public static void UnloadAeroTrajectory()
        {
            Destroy(main);
        }

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

        static void RunAndDrawSimulation(Rocket rocket, Location startLocation, float angle)
        {
            TrajectorySimulation simulation = new TrajectorySimulation(rocket, startLocation, angle);
            List<Vector3> points = new List<Vector3>();
            while (simulation.Step() is Vector2 point)
            {
                points.Add(point / 1000f);
                if (points.Count >= Main.settings.simulationIterations)
                    break;
            }
            (Main.settings.trajectoryDashedLine ? Map.dashedLine : Map.solidLine).DrawLine(points.ToArray(), simulation.planet, Main.settings.trajectoryColor, Main.settings.trajectoryColor);
        }

        public void DrawTrajectory()
        {
            try
            {
                if (CanRunSimulation(out Rocket player, out Location startLocation))
                {
                    switch (Main.settings.simulationType)
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
                                float drag = TrajectorySimulation.GetDragCoefficent(player, angle);
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
                Debug.Log($"AeroTrajectory: {e}");
            }
        }
    }

    public class TrajectorySimulation
    {
        public Planet planet;
        readonly float dragCoefficient;
        Vector2 currentPos;
        Vector2 currentVel;
        Vector2 currentAcc;
        bool enteredAtmosphere;

        // TODO: Get exposed surfaces from angles other than retrograde.
        public TrajectorySimulation(Rocket player, Location startLocation, float angle)
        {
            Location location = startLocation;
            planet = location.planet;
            dragCoefficient = GetDragCoefficent(player, angle) / player.mass.GetMass();
            currentPos = location.position.ToVector2;
            currentVel = location.velocity.ToVector2;
            currentAcc = GetAcceleration(currentPos, currentVel);
            enteredAtmosphere = false;
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
                if (Main.settings.showEscapeOrbit)
                {
                    Location location = new Location(WorldTime.main.worldTime, planet, (Double2) currentPos, (Double2) currentVel);
                    TrajectoryDrawer.DrawDashed(new Orbit(location, false, true), true, false, true, Main.settings.trajectoryColor);
                }
                return null;
            }

            // ? Verlet integration - https://en.wikipedia.org/wiki/Verlet_integration#Algorithmic_representation
            float dt = Main.settings.simulationStepSize;
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

        public static float GetDragCoefficent(Rocket player, float angle)
        {
            List<Surface> exposedSurfaces = AeroModule.GetExposedSurfaces(Aero_Rocket.GetDragSurfaces(player.partHolder, Matrix2x2.Angle(-angle)));
            return (
                ((float, Vector2)) typeof(AeroModule)
                    .GetMethod("CalculateDragForce", BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, new[] { exposedSurfaces })
                )
                .Item1;
        }

        Vector2 GetAcceleration(Vector2 pos, Vector2 vel)
        {
            return GetDrag(pos, vel) + GetGravity(pos);
        }

        Vector2 GetDrag(Vector2 pos, Vector2 vel)
        {
            float atmoDensity = (float) planet.GetAtmosphericDensity(pos.magnitude - planet.Radius);
            float force = dragCoefficient * 1.5f * vel.sqrMagnitude;
            return force * atmoDensity * -vel.normalized;
        }

        Vector2 GetGravity(Vector2 pos)
        {
            return (Vector2) planet.GetGravity((Double2) pos);
        }
    }
}