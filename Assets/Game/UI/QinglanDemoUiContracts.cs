using System;
using Game.Application;

namespace Game.UI
{
    public enum QinglanUiPageId : byte
    {
        TitleProfile = 1,
        CharacterSelect = 2,
        MapSelect = 3,
        Loadout = 4,
        Loading = 5,
        RunHud = 6,
        LevelUpChoice = 7,
        RewardChoice = 8,
        Pause = 9,
        Settings = 10,
        StoryOverlay = 11,
        RunResult = 12,
        Hub = 13,
        HubFacility = 14,
        Collection = 15,
        ContentError = 16,
        LoadoutConfirmation = 17
    }

    public enum QinglanUiCommand : byte
    {
        None = 0,
        Start = 1,
        Continue = 2,
        Back = 3,
        OpenLoadout = 4,
        BeginRun = 5,
        Resume = 6,
        OpenSettings = 7,
        AbandonRun = 8,
        SelectUpgrade = 9,
        SkipUpgrade = 10,
        RerollUpgrade = 11,
        SelectReward = 12,
        CommitResult = 13,
        RetrySave = 14,
        ContinueToHub = 15,
        OpenFacility = 16,
        Purchase = 17,
        ResetLoadout = 18,
        OpenStories = 19,
        OpenCollection = 20,
        StartAgain = 21,
        ReturnToTitle = 22,
        CycleSetting = 23,
        Rebind = 24,
        CloseOverlay = 25,
        ToggleLoadout = 26,
        ConfirmResetLoadout = 27
    }

    public readonly struct QinglanUiOption
    {
        public QinglanUiOption(
            string stableId,
            string labelKey,
            string descriptionKey,
            QinglanUiCommand command,
            bool enabled = true,
            string valueText = "",
            string tagKey = "",
            string relationKey = "",
            string eligibilityKey = "")
        {
            StableId = stableId ?? string.Empty;
            LabelKey = labelKey ?? string.Empty;
            DescriptionKey = descriptionKey ?? string.Empty;
            Command = command;
            Enabled = enabled;
            ValueText = valueText ?? string.Empty;
            TagKey = tagKey ?? string.Empty;
            RelationKey = relationKey ?? string.Empty;
            EligibilityKey = eligibilityKey ?? string.Empty;
        }

        public string StableId { get; }
        public string LabelKey { get; }
        public string DescriptionKey { get; }
        public QinglanUiCommand Command { get; }
        public bool Enabled { get; }
        public string ValueText { get; }
        public string TagKey { get; }
        public string RelationKey { get; }
        public string EligibilityKey { get; }
    }

    /// <summary>Reusable page projection containing keys and pure values only.</summary>
    public sealed class QinglanPageViewModel
    {
        private readonly QinglanUiOption[] options;

        public QinglanPageViewModel(int capacity = 64)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            options = new QinglanUiOption[capacity];
        }

        public QinglanUiPageId Page { get; private set; }
        public string TitleKey { get; private set; } = string.Empty;
        public string SubtitleKey { get; private set; } = string.Empty;
        public string StatusKey { get; private set; } = string.Empty;
        public string StatusValue { get; private set; } = string.Empty;
        public int SelectedIndex { get; private set; }
        public int OptionCount { get; private set; }

        public QinglanUiOption GetOptionAt(int index)
        {
            if (index < 0 || index >= OptionCount) throw new ArgumentOutOfRangeException(nameof(index));
            return options[index];
        }

        public void Reset(
            QinglanUiPageId page,
            string titleKey,
            string subtitleKey = "",
            string statusKey = "",
            string statusValue = "")
        {
            Page = page;
            TitleKey = titleKey ?? string.Empty;
            SubtitleKey = subtitleKey ?? string.Empty;
            StatusKey = statusKey ?? string.Empty;
            StatusValue = statusValue ?? string.Empty;
            OptionCount = 0;
            SelectedIndex = 0;
        }

        public bool Add(in QinglanUiOption option)
        {
            if (OptionCount >= options.Length) return false;
            options[OptionCount++] = option;
            return true;
        }

        public void RestoreSelection(int index)
        {
            if (OptionCount == 0) { SelectedIndex = 0; return; }
            index = index < 0 ? 0 : index >= OptionCount ? OptionCount - 1 : index;
            if (options[index].Enabled) { SelectedIndex = index; return; }
            for (var offset = 1; offset < OptionCount; offset++)
            {
                var next = (index + offset) % OptionCount;
                if (options[next].Enabled) { SelectedIndex = next; return; }
            }
            SelectedIndex = 0;
        }

        public bool MoveSelection(int delta)
        {
            if (OptionCount == 0 || delta == 0) return false;
            var direction = delta > 0 ? 1 : -1;
            for (var attempts = 0; attempts < OptionCount; attempts++)
            {
                var next = (SelectedIndex + direction + OptionCount) % OptionCount;
                SelectedIndex = next;
                if (options[next].Enabled) return true;
            }
            return false;
        }
    }

    public interface IQinglanDemoUiController
    {
        DemoFlowStage Stage { get; }
        AccessibilitySettings Settings { get; }
        bool IsGameplayInputEnabled { get; }
        bool PopulatePage(QinglanPageViewModel target);
        bool PopulateHud(RunUiSnapshot target);
        bool Execute(QinglanUiCommand command, string stableId, int optionIndex);
        bool Cancel();
        bool CycleTab(int direction);
        bool CyclePage(int direction);
    }

    public interface IQinglanDemoView
    {
        void ShowPage(QinglanPageViewModel page);
        void ShowHud(RunUiSnapshot snapshot);
        void ApplyAccessibility(AccessibilitySettings settings);
    }
}
