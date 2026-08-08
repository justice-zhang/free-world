using System;

namespace Game.Application
{
    /// <summary>Application-owned automatic targeting policy selected by the player.</summary>
    public enum AutoAimStrategy : byte
    {
        Nearest = 0,
        MovementDirection = 1,
        LowestHealth = 2,
        Disabled = 3
    }

    /// <summary>Color-independent danger presentation palettes selected by the player.</summary>
    public enum ColorVisionMode : byte
    {
        Standard = 0,
        Protanopia = 1,
        Deuteranopia = 2,
        Tritanopia = 3,
        HighContrast = 4
    }

    /// <summary>
    /// Runtime accessibility settings. Values are normalized here so UI, input,
    /// camera, and effects consume one stable application-owned source.
    /// </summary>
    public sealed class AccessibilitySettings
    {
        public float StickDeadzone { get; private set; } = 0.15f;
        public float VibrationIntensity { get; private set; } = 1f;
        public bool ScreenShakeEnabled { get; private set; } = true;
        public float FlashIntensity { get; private set; } = 1f;
        public bool DamageNumbersEnabled { get; private set; } = true;
        public AutoAimStrategy AutoAim { get; private set; } = AutoAimStrategy.Nearest;
        public float FontScale { get; private set; } = 1f;
        public ColorVisionMode ColorVision { get; private set; } = ColorVisionMode.Standard;
        public float MasterVolume { get; private set; } = 1f;
        public float MusicVolume { get; private set; } = 1f;
        public float AmbienceVolume { get; private set; } = 1f;
        public float EffectsVolume { get; private set; } = 1f;
        public bool SubtitlesEnabled { get; private set; } = true;

        public void SetStickDeadzone(float value) => StickDeadzone = Clamp(value, 0f, 0.95f);
        public void SetVibrationIntensity(float value) => VibrationIntensity = Clamp01(value);
        public void SetScreenShakeEnabled(bool value) => ScreenShakeEnabled = value;
        public void SetFlashIntensity(float value) => FlashIntensity = Clamp01(value);
        public void SetDamageNumbersEnabled(bool value) => DamageNumbersEnabled = value;
        public void SetFontScale(float value) => FontScale = Clamp(value, 1f, 1.5f);
        public void SetMasterVolume(float value) => MasterVolume = Clamp01(value);
        public void SetMusicVolume(float value) => MusicVolume = Clamp01(value);
        public void SetAmbienceVolume(float value) => AmbienceVolume = Clamp01(value);
        public void SetEffectsVolume(float value) => EffectsVolume = Clamp01(value);
        public void SetSubtitlesEnabled(bool value) => SubtitlesEnabled = value;

        /// <summary>Applies persisted accessibility values through existing validation setters.</summary>
        public void Apply(SettingsSaveData data)
        {
            if (data == null) return;
            SetStickDeadzone(data.StickDeadzone);
            SetVibrationIntensity(data.VibrationIntensity);
            SetScreenShakeEnabled(data.ScreenShakeEnabled);
            SetFlashIntensity(data.FlashIntensity);
            SetDamageNumbersEnabled(data.DamageNumbersEnabled);
            SetAutoAim(data.AutoAim);
            SetFontScale(data.FontScale);
            SetColorVision(data.ColorVision);
            SetMasterVolume(data.MasterVolume);
            SetMusicVolume(data.MusicVolume);
            SetAmbienceVolume(data.AmbienceVolume);
            SetEffectsVolume(data.EffectsVolume);
            SetSubtitlesEnabled(data.SubtitlesEnabled);
        }

        public void SetAutoAim(AutoAimStrategy value)
        {
            if (value < AutoAimStrategy.Nearest || value > AutoAimStrategy.Disabled)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            AutoAim = value;
        }

        public void SetColorVision(ColorVisionMode value)
        {
            if (value < ColorVisionMode.Standard || value > ColorVisionMode.HighContrast)
                throw new ArgumentOutOfRangeException(nameof(value));
            ColorVision = value;
        }

        private static float Clamp01(float value) => Clamp(value, 0f, 1f);

        private static float Clamp(float value, float minimum, float maximum)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            return value < minimum ? minimum : value > maximum ? maximum : value;
        }
    }

    /// <summary>UI-safe level-up option containing localization keys, not simulation objects.</summary>
    public readonly struct UpgradeChoiceData
    {
        public UpgradeChoiceData(int index, string nameKey, string descriptionKey)
        {
            Index = index;
            LocalizedNameKey = nameKey ?? string.Empty;
            LocalizedDescriptionKey = descriptionKey ?? string.Empty;
        }

        public int Index { get; }
        public string LocalizedNameKey { get; }
        public string LocalizedDescriptionKey { get; }
    }

    /// <summary>UI-safe immutable result projection.</summary>
    public readonly struct RunResultData
    {
        public RunResultData(string reasonKey, double durationSeconds, int level, long enemyDefeats)
        {
            ReasonKey = reasonKey ?? string.Empty;
            DurationSeconds = durationSeconds;
            Level = level;
            EnemyDefeats = enemyDefeats;
        }

        public string ReasonKey { get; }
        public double DurationSeconds { get; }
        public int Level { get; }
        public long EnemyDefeats { get; }
    }

    /// <summary>
    /// Commands and read models available to UI. This deliberately exposes no
    /// SimulationWorld, store, entity state, or mutable gameplay collection.
    /// </summary>
    public interface IGameFlowController
    {
        GameState CurrentState { get; }
        AccessibilitySettings Settings { get; }
        int UpgradeChoiceCount { get; }
        UpgradeChoiceData GetUpgradeChoice(int index);
        RunResultData LatestResult { get; }

        bool ShowCharacterSelect();
        bool ShowMapSelect();
        bool BeginRun();
        bool TogglePause();
        bool SelectUpgrade(int index);
        bool SkipUpgrade();
        bool RerollUpgrades();
        bool OpenSettings();
        bool CloseSettings();
        bool EndRun(RunEndReason reason);
        bool ReturnToMainMenu();
    }

    /// <summary>Input binding customization boundary consumed by Settings UI.</summary>
    public interface IInputRebindService
    {
        bool ApplyBindingOverride(string actionName, int bindingIndex, string controlPath);
        void RemoveAllBindingOverrides();
    }
}
