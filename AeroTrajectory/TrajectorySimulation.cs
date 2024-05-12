using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using SFS.World;
using SFS.WorldBase;
using SFS.World.Drag;
using SFS.World.Maps;
using System.Linq;

namespace AeroTrajectory
{
    public class TrajectorySimulation
    {
        public Planet planet;
        readonly float dragCoefficient;
        readonly float? glidingHeatshields_dragCoefficient;
        readonly float heatingConst_num4;
        Vector2 currentPos;
        Vector2 currentVel;
        Vector2 currentAcc;
        float currentTemp;
        bool enteredAtmosphere;

        public TrajectorySimulation(Rocket player, Location startLocation, float angle)
        {
            List<Surface> exposedSurfaces = GetExposedSurfaces(player, angle);
            Location location = startLocation;

            planet = location.planet;
            dragCoefficient = 1.5f * GetDragCoefficent(exposedSurfaces) / player.mass.GetMass();
            currentPos = location.position.ToVector2;
            currentVel = location.velocity.ToVector2;
            currentAcc = GetAcceleration(currentPos, currentVel);

            try
            {
                HeatModuleBase heatModule = player.aero.heatManager.GetMostHeatedModules(1).First();
                currentTemp = float.IsNegativeInfinity(heatModule.Temperature) ? 0f : heatModule.Temperature;
                heatingConst_num4 = 1f + Mathf.Log10(heatModule.ExposedSurface + 1f);
            }
            catch (InvalidOperationException)
            {
                currentTemp = 0f;
                heatingConst_num4 = 1f;
            }

            enteredAtmosphere = false;

            glidingHeatshields_dragCoefficient = null;
            if (Main.glidingHeatshields != null && Settings.settings.glidingHeatshieldsForces)
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

        public Vector2? Step(out Color trajectoryColor)
        {
            trajectoryColor = Settings.settings.trajectoryColor;
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

            UpdateHeating(dt);
            float maxTemp = Settings.settings.realMaxTemperature ? 1.03f * currentTemp : currentTemp;
            if (maxTemp >= AeroModule.GetHeatTolerance(HeatTolerance.High))
                trajectoryColor = Settings.settings.heatingColorHigh;
            else if (maxTemp >= AeroModule.GetHeatTolerance(HeatTolerance.Mid))
                trajectoryColor = Settings.settings.heatingColorMid;
            else if (maxTemp >= AeroModule.GetHeatTolerance(HeatTolerance.Low))
                trajectoryColor = Settings.settings.heatingColorLow;
            
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
            return dragCoefficient * vel.sqrMagnitude * atmoDensity * -vel.normalized;
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

        // ? Based on HeatManager.ApplyHeat() & HeatManager.DissipateHeat().
        void UpdateHeating(float dt)
        {
            Location location = new Location(planet, (Double2) currentPos, (Double2) currentVel);
            AeroModule.GetTemperatureAndShockwave(location, out _, out _, out float tempBase);

            float num3 = tempBase - currentTemp;
            if (num3 > 0f)
            {
                float num1 = 0.02f * dt;
                float num5 = ((num3 < 1000f) ? num3 : (num3 * num3 / 1000f));
                currentTemp += heatingConst_num4 * num5 * num1;
            }
            else if (currentTemp > 0f)
            {
                float num1 = 0.01f * WorldTime.FixedDeltaTime;
	            float num2 = 10f * WorldTime.FixedDeltaTime;
                currentTemp -= num2 + currentTemp * num1;

                if (currentTemp < 0f)
                    currentTemp = 0f;
            }
        }
    }
}