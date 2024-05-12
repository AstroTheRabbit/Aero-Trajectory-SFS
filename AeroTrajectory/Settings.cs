using System;
using UnityEngine;
using HarmonyLib;
using UITools;
using SFS.IO;
using SFS.UI.ModGUI;
using Type = SFS.UI.ModGUI.Type;

namespace AeroTrajectory
{
    public class Settings : ModSettings<SettingsData>
    {
        public static Settings main;
        static FilePath settingsPath;
        static readonly Color defaultInputColor = new Color(0.008f, 0.090f, 0.180f, 0.941f);

        protected override FilePath SettingsFile => settingsPath;

        protected override void RegisterOnVariableChange(Action onChange)
        {
            Application.quitting += onChange;
        }

        public static void Init(FilePath path)
        {
            main = new Settings();
            settingsPath = path;
            main.Initialize();
            main.AddUI();

            // * Flip heating color gradient if the player is using the prev. default trajectory color.
            if (Settings.settings.trajectoryColor == Color.red)
                Settings.settings.trajectoryColor = Color.yellow;
        }

        void AddUI()
        {
            ConfigurationMenu.Add
            (
                "Aero Trajectory",
                new (string, Func<Transform, GameObject>)[]
                {
                    ("Simulation", (Transform transform) => CreateSimulationUI(transform, ConfigurationMenu.ContentSize)),
                    ("Trajectory", (Transform transform) => CreateTrajectoryUI(transform, ConfigurationMenu.ContentSize)),
                    ("Heating",    (Transform transform) =>    CreateHeatingUI(transform, ConfigurationMenu.ContentSize)),
                    ("Misc.",      (Transform transform) =>       CreateMiscUI(transform, ConfigurationMenu.ContentSize)),
                }
            );
        }

        GameObject CreateSimulationUI(Transform parent, Vector2Int size)
        {
            Box box = Builder.CreateBox(parent, size.x, size.y);
            box.CreateLayoutGroup(Type.Vertical, TextAnchor.UpperLeft, padding: new RectOffset(15, 15, 15, 15));
            int width = size.x - 30;

            InputWithLabel input_stepSize = null;
            input_stepSize = Builder.CreateInputWithLabel
            (
                box,
                width,
                40,
                labelText: "Step Size",
                inputText: settings.simulationStepSize.ToString(),
                onInputChange: (string input) => {
                    if (float.TryParse(input, out float res) && !float.IsNaN(res) && !float.IsInfinity(res))
                    {
                        input_stepSize.textInput.FieldColor = defaultInputColor;
                        settings.simulationStepSize = res;
                    }
                    else
                    {
                        input_stepSize.textInput.FieldColor = Color.red;
                    }
                }
            );

            InputWithLabel input_iterations = null;
            input_iterations = Builder.CreateInputWithLabel
            (
                box,
                width,
                40,
                labelText: "Iterations",
                inputText: settings.simulationIterations.ToString(),
                onInputChange: (string input) => {
                    if (int.TryParse(input, out int res))
                    {
                        input_iterations.textInput.FieldColor = defaultInputColor;
                        settings.simulationIterations = res;
                    }
                    else
                    {
                        input_iterations.textInput.FieldColor = Color.red;
                    }
                }
            );

            Builder.CreateLabel(box, width, 40, text: "Simulation Type");
            Box buttonsBox = Builder.CreateBox(box, width - 10, 125);
            buttonsBox.CreateLayoutGroup(Type.Vertical, childAlignment: TextAnchor.MiddleLeft, spacing: 5, padding: new RectOffset(10, 10, 10, 10));
            
            Container topButtonsContainer = Builder.CreateContainer(buttonsBox);
            topButtonsContainer.CreateLayoutGroup(Type.Horizontal, childAlignment: TextAnchor.MiddleCenter, spacing: 10);

            Container bottomButtonsContainer = Builder.CreateContainer(buttonsBox);
            bottomButtonsContainer.CreateLayoutGroup(Type.Horizontal, childAlignment: TextAnchor.MiddleCenter, spacing: 10);

            Button button_Prograde = null, button_Retrograde = null, button_CurrentAngle = null, button_MinMaxRange = null;
            button_Prograde     = SimulationTypeButton(topButtonsContainer, SimulationType.Prograde, "Prograde");
            button_Retrograde   = SimulationTypeButton(topButtonsContainer, SimulationType.Retrograde, "Retrograde");
            button_CurrentAngle = SimulationTypeButton(bottomButtonsContainer, SimulationType.CurrentAngle, "Current Angle");
            button_MinMaxRange  = SimulationTypeButton(bottomButtonsContainer, SimulationType.MinMaxRange, "Min-Max Range");

            Button SimulationTypeButton(Transform holder, SimulationType type, string name)
            {
                Button button = Builder.CreateButton
                (
                    holder,
                    (width / 2) - 20,
                    50,
                    text: name,
                    onClick: () =>
                    {
                        button_Prograde.SetSelected(type == SimulationType.Prograde);
                        button_Retrograde.SetSelected(type == SimulationType.Retrograde);
                        button_CurrentAngle.SetSelected(type == SimulationType.CurrentAngle);
                        button_MinMaxRange.SetSelected(type == SimulationType.MinMaxRange);
                        settings.simulationType = type;
                    }
                );
                button.SetSelected(settings.simulationType == type);
                return button;
            }

            return box.gameObject;
        }

        GameObject CreateTrajectoryUI(Transform parent, Vector2Int size)
        {
            Box box = Builder.CreateBox(parent, size.x, size.y);
            box.CreateLayoutGroup(Type.Vertical, TextAnchor.UpperLeft, padding: new RectOffset(15, 15, 15, 15));
            int width = size.x - 30;

            Builder.CreateLabel(box, width, 40, text: "Main Color");
            Box colorBox = Builder.CreateBox(box, width - 10, 170);
            colorBox.CreateLayoutGroup(Type.Vertical, spacing: 10);
            InputWithLabel input_red = null, input_green = null, input_blue = null;

            input_red = Builder.CreateInputWithLabel
            (
                colorBox,
                width - 30,
                40,
                labelText: "Red",
                inputText: settings.trajectoryColor.r.ToString(),
                onInputChange: (string input) => {
                    if (float.TryParse(input, out float res) && !float.IsNaN(res) && !float.IsInfinity(res))
                    {
                        input_red.textInput.FieldColor = defaultInputColor;
                        settings.trajectoryColor.r = res;
                    }
                    else
                    {
                        input_red.textInput.FieldColor = Color.red;
                    }
                }
            );

            input_green = Builder.CreateInputWithLabel
            (
                colorBox,
                width - 30,
                40,
                labelText: "Green",
                inputText: settings.trajectoryColor.g.ToString(),
                onInputChange: (string input) => {
                    if (float.TryParse(input, out float res) && !float.IsNaN(res) && !float.IsInfinity(res))
                    {
                        input_green.textInput.FieldColor = defaultInputColor;
                        settings.trajectoryColor.g = res;
                    }
                    else
                    {
                        input_green.textInput.FieldColor = Color.red;
                    }
                }
            );

            input_blue = Builder.CreateInputWithLabel
            (
                colorBox,
                width - 30,
                40,
                labelText: "Blue",
                inputText: settings.trajectoryColor.b.ToString(),
                onInputChange: (string input) => {
                    if (float.TryParse(input, out float res) && !float.IsNaN(res) && !float.IsInfinity(res))
                    {
                        input_blue.textInput.FieldColor = defaultInputColor;
                        settings.trajectoryColor.b = res;
                    }
                    else
                    {
                        input_blue.textInput.FieldColor = Color.red;
                    }
                }
            );

            Button button_dashedLine = null;
            button_dashedLine = Builder.CreateButton
            (
                box,
                width,
                40,
                text: "Dashed Line",
                onClick: () =>
                {
                    settings.trajectoryDashedLine = !settings.trajectoryDashedLine;
                    button_dashedLine.SetSelected(settings.trajectoryDashedLine);
                }
            );
            button_dashedLine.SetSelected(settings.trajectoryDashedLine);

            Button button_showEscapeOrbit = null;
            button_showEscapeOrbit = Builder.CreateButton
            (
                box,
                width,
                40,
                text: "Show Escape Orbit",
                onClick: () =>
                {
                    settings.showEscapeOrbit = !settings.showEscapeOrbit;
                    button_showEscapeOrbit.SetSelected(settings.showEscapeOrbit);
                }
            );
            button_showEscapeOrbit.SetSelected(settings.showEscapeOrbit);

            return box.gameObject;
        }

        GameObject CreateHeatingUI(Transform parent, Vector2Int size)
        {
            Box box = Builder.CreateBox(parent, size.x, size.y);
            box.CreateLayoutGroup(Type.Vertical, TextAnchor.UpperLeft, padding: new RectOffset(15, 15, 15, 15));
            int width = size.x - 30;

            Builder.CreateLabel(box, width, 40, text: "Low Color");
            Box colorBox_low = Builder.CreateBox(box, width - 10, 170);
            colorBox_low.CreateLayoutGroup(Type.Vertical, spacing: 10);
            InputWithLabel input_red_low = null, input_green_low = null, input_blue_low = null;

            input_red_low = Builder.CreateInputWithLabel
            (
                colorBox_low,
                width - 30,
                40,
                labelText: "Red",
                inputText: settings.heatingColorLow.r.ToString(),
                onInputChange: (string input) => {
                    if (float.TryParse(input, out float res) && !float.IsNaN(res) && !float.IsInfinity(res))
                    {
                        input_red_low.textInput.FieldColor = defaultInputColor;
                        settings.heatingColorLow.r = res;
                    }
                    else
                    {
                        input_red_low.textInput.FieldColor = Color.red;
                    }
                }
            );

            input_green_low = Builder.CreateInputWithLabel
            (
                colorBox_low,
                width - 30,
                40,
                labelText: "Green",
                inputText: settings.heatingColorLow.g.ToString(),
                onInputChange: (string input) => {
                    if (float.TryParse(input, out float res) && !float.IsNaN(res) && !float.IsInfinity(res))
                    {
                        input_green_low.textInput.FieldColor = defaultInputColor;
                        settings.heatingColorLow.g = res;
                    }
                    else
                    {
                        input_green_low.textInput.FieldColor = Color.red;
                    }
                }
            );

            input_blue_low = Builder.CreateInputWithLabel
            (
                colorBox_low,
                width - 30,
                40,
                labelText: "Blue",
                inputText: settings.heatingColorLow.b.ToString(),
                onInputChange: (string input) => {
                    if (float.TryParse(input, out float res) && !float.IsNaN(res) && !float.IsInfinity(res))
                    {
                        input_blue_low.textInput.FieldColor = defaultInputColor;
                        settings.heatingColorLow.b = res;
                    }
                    else
                    {
                        input_blue_low.textInput.FieldColor = Color.red;
                    }
                }
            );

            Builder.CreateLabel(box, width, 40, text: "Mid Color");
            Box colorBox_mid = Builder.CreateBox(box, width - 10, 170);
            colorBox_mid.CreateLayoutGroup(Type.Vertical, spacing: 10);
            InputWithLabel input_red_mid = null, input_green_mid = null, input_blue_mid = null;

            input_red_mid = Builder.CreateInputWithLabel
            (
                colorBox_mid,
                width - 30,
                40,
                labelText: "Red",
                inputText: settings.heatingColorMid.r.ToString(),
                onInputChange: (string input) => {
                    if (float.TryParse(input, out float res) && !float.IsNaN(res) && !float.IsInfinity(res))
                    {
                        input_red_mid.textInput.FieldColor = defaultInputColor;
                        settings.heatingColorMid.r = res;
                    }
                    else
                    {
                        input_red_mid.textInput.FieldColor = Color.red;
                    }
                }
            );

            input_green_mid = Builder.CreateInputWithLabel
            (
                colorBox_mid,
                width - 30,
                40,
                labelText: "Green",
                inputText: settings.heatingColorMid.g.ToString(),
                onInputChange: (string input) => {
                    if (float.TryParse(input, out float res) && !float.IsNaN(res) && !float.IsInfinity(res))
                    {
                        input_green_mid.textInput.FieldColor = defaultInputColor;
                        settings.heatingColorMid.g = res;
                    }
                    else
                    {
                        input_green_mid.textInput.FieldColor = Color.red;
                    }
                }
            );

            input_blue_mid = Builder.CreateInputWithLabel
            (
                colorBox_mid,
                width - 30,
                40,
                labelText: "Blue",
                inputText: settings.heatingColorMid.b.ToString(),
                onInputChange: (string input) => {
                    if (float.TryParse(input, out float res) && !float.IsNaN(res) && !float.IsInfinity(res))
                    {
                        input_blue_mid.textInput.FieldColor = defaultInputColor;
                        settings.heatingColorMid.b = res;
                    }
                    else
                    {
                        input_blue_mid.textInput.FieldColor = Color.red;
                    }
                }
            );

            Builder.CreateLabel(box, width, 40, text: "High Color");
            Box colorBox_high = Builder.CreateBox(box, width - 10, 170);
            colorBox_high.CreateLayoutGroup(Type.Vertical, spacing: 10);
            InputWithLabel input_red_high = null, input_green_high = null, input_blue_high = null;

            input_red_high = Builder.CreateInputWithLabel
            (
                colorBox_high,
                width - 30,
                40,
                labelText: "Red",
                inputText: settings.heatingColorHigh.r.ToString(),
                onInputChange: (string input) => {
                    if (float.TryParse(input, out float res) && !float.IsNaN(res) && !float.IsInfinity(res))
                    {
                        input_red_high.textInput.FieldColor = defaultInputColor;
                        settings.heatingColorHigh.r = res;
                    }
                    else
                    {
                        input_red_high.textInput.FieldColor = Color.red;
                    }
                }
            );

            input_green_high = Builder.CreateInputWithLabel
            (
                colorBox_high,
                width - 30,
                40,
                labelText: "Green",
                inputText: settings.heatingColorHigh.g.ToString(),
                onInputChange: (string input) => {
                    if (float.TryParse(input, out float res) && !float.IsNaN(res) && !float.IsInfinity(res))
                    {
                        input_green_high.textInput.FieldColor = defaultInputColor;
                        settings.heatingColorHigh.g = res;
                    }
                    else
                    {
                        input_green_high.textInput.FieldColor = Color.red;
                    }
                }
            );

            input_blue_high = Builder.CreateInputWithLabel
            (
                colorBox_high,
                width - 30,
                40,
                labelText: "Blue",
                inputText: settings.heatingColorHigh.b.ToString(),
                onInputChange: (string input) => {
                    if (float.TryParse(input, out float res) && !float.IsNaN(res) && !float.IsInfinity(res))
                    {
                        input_blue_high.textInput.FieldColor = defaultInputColor;
                        settings.heatingColorHigh.b = res;
                    }
                    else
                    {
                        input_blue_high.textInput.FieldColor = Color.red;
                    }
                }
            );

            Button button = null;
            button = Builder.CreateButton
            (
                box,
                width,
                40,
                text: "Real Max Temperature",
                onClick: () =>
                {
                    settings.realMaxTemperature = !settings.realMaxTemperature;
                    button.SetSelected(settings.realMaxTemperature);
                }
            );
            button.SetSelected(settings.realMaxTemperature);

            return box.gameObject;
        }

        GameObject CreateMiscUI(Transform parent, Vector2Int size)
        {
            Box box = Builder.CreateBox(parent, size.x, size.y);
            box.CreateLayoutGroup(Type.Vertical, TextAnchor.UpperLeft, padding: new RectOffset(15, 15, 15, 15));
            int width = size.x - 30;

            Button button_glidingHeatshields = null;
            button_glidingHeatshields = Builder.CreateButton
            (
                box,
                width,
                40,
                text: "Gliding Heatshields Forces",
                onClick: () =>
                {
                    settings.glidingHeatshieldsForces = !settings.glidingHeatshieldsForces;
                    button_glidingHeatshields.SetSelected(settings.glidingHeatshieldsForces);
                }
            );
            button_glidingHeatshields.SetSelected(settings.glidingHeatshieldsForces);

            return box.gameObject;
        }
    }

    public enum SimulationType
    {
        Prograde,
        Retrograde,
        CurrentAngle,
        MinMaxRange,
    }

    public class SettingsData
    {
        public float simulationStepSize = 0.05f;
        public int simulationIterations = 25000;
        public SimulationType simulationType = SimulationType.CurrentAngle;
        public Color trajectoryColor = Color.yellow;
        public Color heatingColorLow = Color.Lerp(Color.yellow, Color.red, 1 / 3f);
        public Color heatingColorMid = Color.Lerp(Color.yellow, Color.red, 2 / 3f);
        public Color heatingColorHigh = Color.red;
        public bool realMaxTemperature = true;
        public bool trajectoryDashedLine = true;
        public bool showEscapeOrbit = true;
        public bool glidingHeatshieldsForces = true;
    }

    public static class ButtonExtension
    {
        public static void SetSelected(this Button button, bool selected)
        {
            AccessTools.FieldRefAccess<Button, SFS.UI.ButtonPC>(button, "_button").SetSelected(selected);
        }
    }
}