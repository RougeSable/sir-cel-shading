using System;
using System.Text;
using Sandbox.Graphics.GUI;
using VRage.Utils;
using VRageMath;

namespace SirCelShading
{
    // The plugin's settings, opened from Pulsar. Every change applies to the
    // next frame and is saved at once: the player sees the effect behind the
    // screen, without restarting the game.
    //
    // First the Enable plugin checkbox, then the settings of the rendering,
    // Animated film, the only one there is.
    internal sealed class SettingsScreen : MyGuiScreenBase
    {
        private const float Width = 0.62f;
        private const float Height = 0.66f;
        private const float LabelColumn = -0.27f;
        private const float ControlColumn = 0.10f;
        private const float ValueColumn = 0.27f;
        private const float LineSpacing = 0.07f;

        // A dragged slider changes value at every notch, and every new value
        // asks the game to compile the effect. We wait for the player to pause
        // a moment before applying.
        private static readonly TimeSpan SliderDelay = TimeSpan.FromMilliseconds(400);

        private readonly SirCelShadingPlugin m_plugin;
        private Settings m_settings;
        private DateTime? m_applyAt;
        private bool m_rebuild;

        public SettingsScreen(SirCelShadingPlugin plugin)
            : base(new Vector2(0.5f, 0.5f), MyGuiConstants.SCREEN_BACKGROUND_COLOR, new Vector2(Width, Height), false, null, 0.9f, 0.9f)
        {
            m_plugin = plugin;
            m_settings = plugin.Settings;
            EnabledBackgroundFade = true;
            CloseButtonEnabled = true;
            RecreateControls(true);
        }

        public override string GetFriendlyName()
        {
            return "SirCelShadingSettingsScreen";
        }

        public override void RecreateControls(bool constructor)
        {
            base.RecreateControls(constructor);
            AddCaption(Texts.ScreenTitle);

            var y = -Height / 2 + 0.13f;

            // First setting, always: the checkbox that turns everything on or off.
            AddLabel(Texts.EnableCheckbox, Texts.EnableCheckboxHelp, y);
            var enable = new MyGuiControlCheckbox(new Vector2(ControlColumn, y), null, Texts.EnableCheckboxHelp, m_settings.Enabled);
            enable.IsCheckedChanged = c =>
            {
                m_settings.Enabled = c.IsChecked;
                Apply();
            };
            Controls.Add(enable);
            y += LineSpacing;

            y = AddAnimatedFilmSliders(m_settings.AnimatedFilm, y);

            if (m_plugin.Stop != null && m_plugin.Stop.IsStopped)
            {
                Controls.Add(new MyGuiControlLabel(new Vector2(LabelColumn, y), null, Texts.ScreenStopped, null, 0.8f, "Red"));
            }

            var buttonsY = Height / 2 - 0.07f;
            var defaults = new MyGuiControlButton(new Vector2(-0.12f, buttonsY), text: new StringBuilder(Texts.DefaultsButton),
                onButtonClick: b =>
                {
                    m_settings.AnimatedFilm = new AnimatedFilmSettings();
                    Apply();
                    m_rebuild = true;
                });
            defaults.SetToolTip(Texts.DefaultsButtonHelp);
            Controls.Add(defaults);
            Controls.Add(new MyGuiControlButton(new Vector2(0.12f, buttonsY), text: new StringBuilder(Texts.CloseButton),
                onButtonClick: b => CloseScreen()));
        }

        private float AddAnimatedFilmSliders(AnimatedFilmSettings a, float y)
        {
            AddSlider(Texts.ShadeTones, Texts.ShadeTonesHelp, y, AnimatedFilmSettings.ShadeTonesMin, AnimatedFilmSettings.ShadeTonesMax,
                a.ShadeTones, v => a.ShadeTones = v, "");
            y += LineSpacing;
            AddSlider(Texts.OutlineStrength, Texts.OutlineStrengthHelp, y, AnimatedFilmSettings.OutlineStrengthMin, AnimatedFilmSettings.OutlineStrengthMax,
                a.OutlineStrength, v => a.OutlineStrength = v, " %");
            y += LineSpacing;
            AddSlider(Texts.RimLight, Texts.RimLightHelp, y, AnimatedFilmSettings.RimLightMin, AnimatedFilmSettings.RimLightMax,
                a.RimLight, v => a.RimLight = v, " %");
            y += LineSpacing;
            AddSlider(Texts.Haze, Texts.HazeHelp, y, AnimatedFilmSettings.HazeMin, AnimatedFilmSettings.HazeMax,
                a.Haze, v => a.Haze = v, " %");
            return y + LineSpacing;
        }

        private void AddLabel(string text, string help, float y)
        {
            var label = new MyGuiControlLabel(new Vector2(LabelColumn, y), null, text);
            label.SetToolTip(help);
            Controls.Add(label);
        }

        private void AddSlider(string text, string help, float y, int min, int max, int value,
            Action<int> assign, string unit)
        {
            AddLabel(text, help, y);

            var display = new MyGuiControlLabel(new Vector2(ValueColumn, y), null, value + unit,
                null, 0.8f, "White", MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_CENTER);

            var slider = new MyGuiControlSlider(new Vector2(ControlColumn, y), min, max, 0.2f, value,
                toolTip: help, intValue: true);
            slider.Value = value;
            slider.ValueChanged = c =>
            {
                var v = (int)Math.Round(c.Value);
                display.Text = v + unit;
                assign(v);
                m_applyAt = DateTime.UtcNow + SliderDelay;
            };

            Controls.Add(slider);
            Controls.Add(display);
        }

        public override bool Update(bool hasFocus)
        {
            if (m_applyAt.HasValue && DateTime.UtcNow >= m_applyAt.Value)
                Apply();
            if (m_rebuild)
            {
                m_rebuild = false;
                RecreateControls(false);
            }
            return base.Update(hasFocus);
        }

        public override bool CloseScreen(bool isUnloading = false)
        {
            if (m_applyAt.HasValue)
                Apply();
            return base.CloseScreen(isUnloading);
        }

        private void Apply()
        {
            m_applyAt = null;
            m_plugin.Apply(m_settings.Copy());
        }
    }
}
