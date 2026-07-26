using System;
using Game.Application;

namespace Game.UI
{
    /// <summary>All M7 pages rendered by the shared UI root.</summary>
    public enum UiPageId : byte
    {
        Bootstrap = 0,
        MainMenu = 1,
        CharacterSelect = 2,
        MapSelect = 3,
        Loading = 4,
        RunHud = 5,
        Pause = 6,
        LevelUpDraft = 7,
        RunResult = 8,
        Settings = 9,
        ContentError = 10
    }

    /// <summary>Page data containing localization keys only.</summary>
    public sealed class UiPageViewModel
    {
        private string[] optionKeys = Array.Empty<string>();

        public UiPageId Page { get; private set; }
        public string TitleKey { get; private set; } = string.Empty;
        public int SelectedIndex { get; private set; }
        public int OptionCount => optionKeys.Length;

        public string GetOptionKey(int index)
        {
            if (index < 0 || index >= optionKeys.Length) throw new ArgumentOutOfRangeException(nameof(index));
            return optionKeys[index];
        }

        internal void Reset(UiPageId page, string titleKey, string[] options, int selectedIndex)
        {
            Page = page;
            TitleKey = titleKey ?? string.Empty;
            optionKeys = options ?? Array.Empty<string>();
            SelectedIndex = optionKeys.Length == 0 ? 0 : Wrap(selectedIndex, optionKeys.Length);
        }

        internal void MoveSelection(int delta)
        {
            if (optionKeys.Length > 0) SelectedIndex = Wrap(SelectedIndex + delta, optionKeys.Length);
        }

        private static int Wrap(int value, int count)
        {
            var result = value % count;
            return result < 0 ? result + count : result;
        }
    }

    /// <summary>Rendering boundary implemented by the Unity UI root.</summary>
    public interface IGameFlowView
    {
        void Show(UiPageViewModel model);
    }

    /// <summary>
    /// Maps application state and UI commands without referencing Simulation. The
    /// presenter owns navigation selection; the view only renders its model.
    /// </summary>
    public sealed class GameFlowPresenter
    {
        private static readonly string[] Empty = Array.Empty<string>();
        private static readonly string[] MainMenuOptions = { "ui.main_menu.start", "ui.main_menu.settings" };
        private static readonly string[] CharacterOptions = { "content.test.character.name" };
        private static readonly string[] MapOptions = { "content.test.map.finite_arena.name" };
        private static readonly string[] PauseOptions = { "ui.pause.resume", "ui.pause.settings", "ui.pause.end_run" };
        private static readonly string[] ResultOptions = { "ui.result.main_menu" };
        private static readonly string[] ErrorOptions = { "ui.content_error.main_menu" };
        private static readonly string[] SettingsOptions =
        {
            "ui.settings.rebind",
            "ui.settings.deadzone",
            "ui.settings.vibration",
            "ui.settings.screen_shake",
            "ui.settings.flash_intensity",
            "ui.settings.damage_numbers",
            "ui.settings.auto_aim"
        };

        private readonly IGameFlowController flow;
        private readonly IGameFlowView view;
        private readonly IInputRebindService rebindService;
        private readonly UiPageViewModel model = new UiPageViewModel();
        private readonly string[] upgradeOptions = new string[3];

        public GameFlowPresenter(
            IGameFlowController flowController,
            IGameFlowView flowView,
            IInputRebindService inputRebindService)
        {
            flow = flowController ?? throw new ArgumentNullException(nameof(flowController));
            view = flowView ?? throw new ArgumentNullException(nameof(flowView));
            rebindService = inputRebindService ?? throw new ArgumentNullException(nameof(inputRebindService));
            Refresh();
        }

        public UiPageViewModel Current => model;

        public void Refresh()
        {
            var selected = model.SelectedIndex;
            switch (flow.CurrentState)
            {
                case GameState.None:
                case GameState.Bootstrap:
                    Set(UiPageId.Bootstrap, "ui.bootstrap.title", Empty, 0);
                    break;
                case GameState.MainMenu:
                    Set(UiPageId.MainMenu, "ui.main_menu.title", MainMenuOptions, selected);
                    break;
                case GameState.CharacterSelect:
                    Set(UiPageId.CharacterSelect, "ui.character_select.title", CharacterOptions, selected);
                    break;
                case GameState.MapSelect:
                    Set(UiPageId.MapSelect, "ui.map_select.title", MapOptions, selected);
                    break;
                case GameState.Loading:
                    Set(UiPageId.Loading, "ui.loading.title", Empty, 0);
                    break;
                case GameState.InRun:
                    Set(UiPageId.RunHud, "ui.run_hud.title", Empty, 0);
                    break;
                case GameState.Pause:
                    Set(UiPageId.Pause, "ui.pause.title", PauseOptions, selected);
                    break;
                case GameState.LevelUpChoice:
                    BuildUpgradeOptions();
                    Set(UiPageId.LevelUpDraft, "ui.level_up.title", upgradeOptions, selected);
                    break;
                case GameState.RunResult:
                    Set(UiPageId.RunResult, "ui.result.title", ResultOptions, selected);
                    break;
                case GameState.Settings:
                    Set(UiPageId.Settings, "ui.settings.title", SettingsOptions, selected);
                    break;
                case GameState.ContentError:
                    Set(UiPageId.ContentError, "ui.content_error.title", ErrorOptions, selected);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void Navigate(float vertical)
        {
            if (vertical > 0.5f) model.MoveSelection(-1);
            else if (vertical < -0.5f) model.MoveSelection(1);
            else return;
            view.Show(model);
        }

        public void Submit()
        {
            switch (flow.CurrentState)
            {
                case GameState.MainMenu:
                    if (model.SelectedIndex == 0) flow.ShowCharacterSelect();
                    else flow.OpenSettings();
                    break;
                case GameState.CharacterSelect:
                    flow.ShowMapSelect();
                    break;
                case GameState.MapSelect:
                    flow.BeginRun();
                    break;
                case GameState.Pause:
                    if (model.SelectedIndex == 0) flow.TogglePause();
                    else if (model.SelectedIndex == 1) flow.OpenSettings();
                    else flow.EndRun(RunEndReason.Abandoned);
                    break;
                case GameState.LevelUpChoice:
                    if (model.SelectedIndex < flow.UpgradeChoiceCount)
                        flow.SelectUpgrade(model.SelectedIndex);
                    else
                        flow.SkipUpgrade();
                    break;
                case GameState.RunResult:
                case GameState.ContentError:
                    flow.ReturnToMainMenu();
                    break;
                case GameState.Settings:
                    ApplySelectedSetting();
                    break;
            }

            Refresh();
        }

        public void Cancel()
        {
            switch (flow.CurrentState)
            {
                case GameState.CharacterSelect:
                    flow.ReturnToMainMenu();
                    break;
                case GameState.MapSelect:
                    flow.ShowCharacterSelect();
                    break;
                case GameState.InRun:
                case GameState.Pause:
                    flow.TogglePause();
                    break;
                case GameState.Settings:
                    flow.CloseSettings();
                    break;
            }

            Refresh();
        }

        public void TogglePause()
        {
            flow.TogglePause();
            Refresh();
        }

        public bool Rebind(string actionName, int bindingIndex, string controlPath)
        {
            return rebindService.ApplyBindingOverride(actionName, bindingIndex, controlPath);
        }

        private void ApplySelectedSetting()
        {
            var settings = flow.Settings;
            switch (model.SelectedIndex)
            {
                case 0:
                    break;
                case 1:
                    settings.SetStickDeadzone(settings.StickDeadzone >= 0.4f ? 0.1f : settings.StickDeadzone + 0.05f);
                    break;
                case 2:
                    settings.SetVibrationIntensity(settings.VibrationIntensity > 0f ? 0f : 1f);
                    break;
                case 3:
                    settings.SetScreenShakeEnabled(!settings.ScreenShakeEnabled);
                    break;
                case 4:
                    settings.SetFlashIntensity(settings.FlashIntensity > 0f ? 0f : 1f);
                    break;
                case 5:
                    settings.SetDamageNumbersEnabled(!settings.DamageNumbersEnabled);
                    break;
                case 6:
                    settings.SetAutoAim((AutoAimStrategy)(((int)settings.AutoAim + 1) % 4));
                    break;
            }
        }

        private void BuildUpgradeOptions()
        {
            var count = flow.UpgradeChoiceCount;
            for (var index = 0; index < upgradeOptions.Length; index++)
                upgradeOptions[index] = index < count ? flow.GetUpgradeChoice(index).LocalizedNameKey : "ui.level_up.skip";
        }

        private void Set(UiPageId page, string titleKey, string[] options, int selected)
        {
            if (model.Page != page) selected = 0;
            model.Reset(page, titleKey, options, selected);
            view.Show(model);
        }
    }
}
