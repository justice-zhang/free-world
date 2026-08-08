using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;
using Game.Application;
using Game.Content.Runtime;
using Game.Core;
using Game.Presentation;
using Game.UI;

namespace Game.Infrastructure
{
    /// <summary>
    /// Unity composition adapter for the Qinglan Demo. It only projects Application
    /// owners and routes commands; candidate, Profile, and run truth remain upstream.
    /// </summary>
    public sealed class QinglanDemoFlowController : IQinglanDemoUiController, IDisposable
    {
        private const string VeinPage = "qinglan.page.hub.vein_inquiry";
        private const string ScrollPage = "qinglan.page.hub.scroll_pavilion";
        private const string ArtifactPage = "qinglan.page.hub.hundred_artifact";
        private const string CollectionPage = "qinglan.page.hub.myriad_phenomena";
        private readonly GameApplication application;
        private readonly M8RuntimeServices runtimeServices;
        private readonly QinglanDemoRunFactory factory;
        private readonly DemoRunCoordinator flow;
        private readonly QinglanProfileCoordinator profile;
        private readonly M7InputRouter input;
        private readonly ILocalizationService localization;
        private readonly List<ContentId> pendingNodes = new List<ContentId>(7);
        private readonly List<ContentId> pendingInserts = new List<ContentId>(2);
        private Task<CommitResult> commitTask;
        private Task<MetaOperationResult> metaTask;
        private CommitResult lastCommit;
        private MetaOperationResult lastMeta;
        private bool commitAttempted;
        private Vector2 movement;
        private bool interactHeld;
        private bool settingsOpen;
        private bool runMapOpen;
        private bool resetLoadoutConfirmation;
        private string selectedFacilityId = string.Empty;
        private string overlayContentId = string.Empty;
        private int overlaySequenceIndex;
        private ulong runSequence;

        public QinglanDemoFlowController(
            GameApplication gameApplication,
            M8RuntimeServices persistence,
            M7InputRouter inputRouter,
            ILocalizationService localizationService)
        {
            application = gameApplication ?? throw new ArgumentNullException(nameof(gameApplication));
            runtimeServices = persistence ?? throw new ArgumentNullException(nameof(persistence));
            input = inputRouter ?? throw new ArgumentNullException(nameof(inputRouter));
            localization = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
            factory = new QinglanDemoRunFactory(application);
            flow = new DemoRunCoordinator(application.StateMachine, factory, true);
            profile = new QinglanProfileCoordinator(
                runtimeServices.SaveCoordinator,
                application.ContentRegistry,
                application.Events,
                runtimeServices.Profile);
            Settings = new AccessibilitySettings();
            Settings.Apply(runtimeServices.Settings);
            ResetPendingLoadout();
        }

        public DemoFlowStage Stage => flow.Stage;
        public AccessibilitySettings Settings { get; }
        public bool IsGameplayInputEnabled =>
            flow.Stage == DemoFlowStage.Active && !settingsOpen && !runMapOpen;
        public RunSession Session => flow.Session;
        public QinglanProfileCoordinator ProfileOwner => profile;
        public CommitResult LastCommit => lastCommit;
        public MetaOperationResult LastMeta => lastMeta;
        public string RebindDiagnosticKey => input.LastRebindDiagnosticKey;

        public int Tick(double elapsedSeconds)
        {
            PollOperations();
            var session = flow.Session;
            if (session != null)
            {
                session.SetMoveDirection(IsGameplayInputEnabled ? movement : Vector2.Zero);
                session.SetInteractHeld(IsGameplayInputEnabled && interactHeld);
            }
            var ticks = flow.Tick(elapsedSeconds);
            if (flow.Stage == DemoFlowStage.Result) EnsureCommitStarted();
            PollOperations();
            return ticks;
        }

        public void SetMovement(Vector2 value)
        {
            if (float.IsNaN(value.X) || float.IsInfinity(value.X) ||
                float.IsNaN(value.Y) || float.IsInfinity(value.Y)) return;
            movement = value.LengthSquared() > 1f ? Vector2.Normalize(value) : value;
        }

        public void SetInteractHeld(bool held) => interactHeld = held;

        public bool TogglePause()
        {
            if (settingsOpen || runMapOpen) return Cancel();
            return flow.Stage == DemoFlowStage.Active ? flow.Pause() :
                flow.Stage == DemoFlowStage.UserPaused && flow.Resume();
        }

        public bool ToggleRunMap()
        {
            if (flow.Stage != DemoFlowStage.Active && !runMapOpen) return false;
            runMapOpen = !runMapOpen;
            return true;
        }

        public bool DebugRequestLevelUp() =>
            flow.Stage == DemoFlowStage.Active && flow.Session?.GrantDebugExperience(5f) == true;

        public bool DebugCompleteRun() =>
            flow.Stage == DemoFlowStage.Active && flow.EndRun(RunEndReason.Completed);

        public bool PopulatePage(QinglanPageViewModel target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (settingsOpen) { BuildSettings(target); return true; }
            if (resetLoadoutConfirmation) { BuildLoadoutConfirmation(target); return true; }
            if (!string.IsNullOrEmpty(overlayContentId)) { BuildOverlay(target); return true; }
            if (string.Equals(selectedFacilityId, "loadout", StringComparison.Ordinal)) { BuildLoadout(target); return true; }
            if (!string.IsNullOrEmpty(selectedFacilityId)) { BuildFacility(target); return true; }
            switch (flow.Stage)
            {
                case DemoFlowStage.Title: BuildTitle(target); break;
                case DemoFlowStage.CharacterSelect: BuildCharacterSelect(target); break;
                case DemoFlowStage.MapSelect: BuildMapSelect(target); break;
                case DemoFlowStage.Preparing:
                    target.Reset(QinglanUiPageId.Loading, "ui.qinglan.loading.title", "ui.qinglan.loading.subtitle");
                    break;
                case DemoFlowStage.Active:
                    target.Reset(QinglanUiPageId.RunHud,
                        runMapOpen ? "ui.qinglan.map_overlay.title" : "ui.qinglan.run_hud.title",
                        runMapOpen ? "ui.qinglan.map_overlay.hint" : string.Empty);
                    if (runMapOpen) AddMapRows(target);
                    break;
                case DemoFlowStage.UpgradePaused: BuildUpgradeChoice(target); break;
                case DemoFlowStage.RewardPaused: BuildRewardChoice(target); break;
                case DemoFlowStage.UserPaused: BuildPause(target); break;
                case DemoFlowStage.Ending:
                    target.Reset(QinglanUiPageId.Loading, "ui.qinglan.ending.title", "ui.qinglan.ending.subtitle");
                    break;
                case DemoFlowStage.Result: BuildResult(target); break;
                case DemoFlowStage.Hub: BuildHub(target); break;
                case DemoFlowStage.ContentError: BuildContentError(target); break;
                default: return false;
            }
            return true;
        }

        public bool PopulateHud(RunUiSnapshot target)
        {
            return flow.Session?.CaptureUiSnapshot(target) == true;
        }

        public bool Execute(QinglanUiCommand command, string stableId, int optionIndex)
        {
            PollOperations();
            switch (command)
            {
                case QinglanUiCommand.Start:
                    return flow.ShowCharacterSelect();
                case QinglanUiCommand.Continue:
                    return flow.Stage == DemoFlowStage.CharacterSelect
                        ? flow.ShowMapSelect()
                        : flow.Stage == DemoFlowStage.MapSelect && OpenLoadout();
                case QinglanUiCommand.OpenLoadout:
                    return OpenLoadout();
                case QinglanUiCommand.BeginRun:
                    return BeginRun();
                case QinglanUiCommand.Back:
                    return Cancel();
                case QinglanUiCommand.Resume:
                    return flow.Resume();
                case QinglanUiCommand.OpenSettings:
                    settingsOpen = true;
                    return true;
                case QinglanUiCommand.AbandonRun:
                    return flow.EndRun(RunEndReason.Abandoned);
                case QinglanUiCommand.SelectUpgrade:
                    return flow.SelectUpgrade(optionIndex);
                case QinglanUiCommand.SkipUpgrade:
                    return flow.SkipUpgrade();
                case QinglanUiCommand.RerollUpgrade:
                    return flow.Session?.Reroll() == true;
                case QinglanUiCommand.SelectReward:
                    return flow.SelectReward(optionIndex);
                case QinglanUiCommand.CommitResult:
                case QinglanUiCommand.RetrySave:
                    return StartCommit();
                case QinglanUiCommand.ContinueToHub:
                    return flow.ContinueToHub();
                case QinglanUiCommand.OpenFacility:
                    selectedFacilityId = stableId ?? string.Empty;
                    return !string.IsNullOrEmpty(selectedFacilityId);
                case QinglanUiCommand.Purchase:
                    return StartPurchase(stableId);
                case QinglanUiCommand.ToggleLoadout:
                    return TogglePendingLoadout(stableId);
                case QinglanUiCommand.ResetLoadout:
                    resetLoadoutConfirmation = true;
                    return true;
                case QinglanUiCommand.ConfirmResetLoadout:
                    if (!StartResetLoadout()) return false;
                    resetLoadoutConfirmation = false;
                    return true;
                case QinglanUiCommand.OpenStories:
                case QinglanUiCommand.OpenCollection:
                    overlayContentId = stableId ?? string.Empty;
                    overlaySequenceIndex = 0;
                    return !string.IsNullOrEmpty(overlayContentId);
                case QinglanUiCommand.StartAgain:
                    ResetPendingLoadout();
                    return flow.StartAgain();
                case QinglanUiCommand.ReturnToTitle:
                    return flow.ReturnToTitle();
                case QinglanUiCommand.CycleSetting:
                    return CycleSetting(stableId);
                case QinglanUiCommand.Rebind:
                    return CycleSubmitBinding();
                case QinglanUiCommand.CloseOverlay:
                    overlayContentId = string.Empty;
                    overlaySequenceIndex = 0;
                    return true;
                default:
                    return false;
            }
        }

        public bool Cancel()
        {
            if (settingsOpen) { settingsOpen = false; SaveSettings(); return true; }
            if (resetLoadoutConfirmation) { resetLoadoutConfirmation = false; return true; }
            if (!string.IsNullOrEmpty(overlayContentId))
            {
                overlayContentId = string.Empty;
                overlaySequenceIndex = 0;
                return true;
            }
            if (!string.IsNullOrEmpty(selectedFacilityId))
            {
                selectedFacilityId = string.Empty;
                return true;
            }
            if (runMapOpen) { runMapOpen = false; return true; }
            if (flow.Stage == DemoFlowStage.Active) return flow.Pause();
            if (flow.Stage == DemoFlowStage.UserPaused) return flow.Resume();
            if (flow.Stage == DemoFlowStage.MapSelect) return flow.ShowCharacterSelect();
            if (flow.Stage == DemoFlowStage.CharacterSelect) return flow.ReturnToTitle();
            return false;
        }

        public bool CycleTab(int direction)
        {
            if (flow.Stage != DemoFlowStage.Hub || direction == 0) return false;
            selectedFacilityId = string.Empty;
            overlayContentId = string.Empty;
            return true;
        }

        public bool CyclePage(int direction)
        {
            if (string.IsNullOrEmpty(overlayContentId) || direction == 0) return false;
            if (TryDefinition(overlayContentId, out var definition) &&
                definition is RuntimeStoryDefinition story && story.SequenceKeys.Count > 0)
            {
                overlaySequenceIndex = Math.Max(0, Math.Min(
                    story.SequenceKeys.Count - 1,
                    overlaySequenceIndex + (direction > 0 ? 1 : -1)));
                return true;
            }
            return false;
        }

        public SettingsSaveData CaptureSettings()
        {
            return new SettingsSaveData(
                localization.SelectedLocaleCode,
                Settings.StickDeadzone,
                Settings.VibrationIntensity,
                Settings.ScreenShakeEnabled,
                Settings.FlashIntensity,
                Settings.DamageNumbersEnabled,
                Settings.AutoAim,
                Settings.FontScale,
                Settings.ColorVision,
                Settings.MasterVolume,
                Settings.MusicVolume,
                Settings.AmbienceVolume,
                Settings.EffectsVolume,
                Settings.SubtitlesEnabled,
                input.CaptureBindingOverrides());
        }

        public void Dispose() => flow.Dispose();

        private void BuildTitle(QinglanPageViewModel target)
        {
            target.Reset(
                QinglanUiPageId.TitleProfile,
                "ui.qinglan.title.name",
                "ui.qinglan.title.subtitle",
                "ui.qinglan.profile.ready");
            Add(target, "start", "ui.qinglan.title.start", "ui.qinglan.title.start.description", QinglanUiCommand.Start);
            Add(target, "settings", "ui.qinglan.settings.title", "ui.qinglan.settings.description", QinglanUiCommand.OpenSettings);
        }

        private void BuildCharacterSelect(QinglanPageViewModel target)
        {
            target.Reset(QinglanUiPageId.CharacterSelect, "ui.qinglan.character_select.title", "ui.qinglan.character_select.subtitle");
            AddContent(target, QinglanDemoRunFactory.CharacterId, QinglanUiCommand.Continue);
        }

        private void BuildMapSelect(QinglanPageViewModel target)
        {
            target.Reset(QinglanUiPageId.MapSelect, "ui.qinglan.map_select.title", "ui.qinglan.map_select.subtitle");
            AddContent(target, QinglanDemoRunFactory.MapId, QinglanUiCommand.OpenLoadout);
        }

        private void BuildLoadout(QinglanPageViewModel target)
        {
            var projection = profile.Meta.ProjectLoadout(profile.Profile);
            target.Reset(
                QinglanUiPageId.Loadout,
                "ui.qinglan.loadout.title",
                projection.UsedSafeFallback ? "meta.error.missing_loadout_content" : "ui.qinglan.loadout.subtitle");
            for (var index = 0; index < profile.Profile.ActiveMetaLoadoutIds.Count; index++)
                AddContent(target, profile.Profile.ActiveMetaLoadoutIds[index].Value, QinglanUiCommand.None, false);
            Add(target, "begin", "ui.qinglan.loadout.depart", "ui.qinglan.loadout.depart.description", QinglanUiCommand.BeginRun);
            Add(target, "back", "ui.common.back", "", QinglanUiCommand.Back);
        }

        private void BuildUpgradeChoice(QinglanPageViewModel target)
        {
            target.Reset(QinglanUiPageId.LevelUpChoice, "ui.qinglan.level_up.title", "ui.qinglan.level_up.subtitle");
            var offers = flow.Session?.CurrentOffers;
            var hud = new RunUiSnapshot();
            flow.Session?.CaptureUiSnapshot(hud);
            if (offers != null)
            {
                for (var index = 0; index < offers.Count; index++)
                {
                    var offer = offers.GetAt(index).Source;
                    var current = FindBuildLevel(hud, offer.TargetContentId.Value);
                    Add(target, offer.Id.Value, offer.LocalizedNameKey, offer.LocalizedDescriptionKey,
                        QinglanUiCommand.SelectUpgrade, true,
                        "Lv." + current.ToString(CultureInfo.InvariantCulture) + " → Lv." +
                        (current + 1).ToString(CultureInfo.InvariantCulture),
                        CardTagKey(offer.TargetContentId),
                        current > 0 ? "ui.qinglan.card.relation.upgrade" : "ui.qinglan.card.relation.new",
                        CardEligibilityKey(offer.TargetContentId));
                }
            }
            Add(target, "reroll", "ui.qinglan.level_up.reroll", "ui.qinglan.level_up.reroll.description", QinglanUiCommand.RerollUpgrade);
            Add(target, "skip", "ui.qinglan.level_up.skip", "ui.qinglan.level_up.skip.description", QinglanUiCommand.SkipUpgrade);
        }

        private void BuildRewardChoice(QinglanPageViewModel target)
        {
            target.Reset(QinglanUiPageId.RewardChoice, "ui.qinglan.reward.title", "ui.qinglan.reward.subtitle");
            var choice = flow.Session?.CurrentRewardChoice;
            if (choice == null || choice.CandidateIds.Count == 0)
            {
                target.Reset(QinglanUiPageId.RewardChoice, "ui.qinglan.reward.title", "ui.qinglan.reward.fallback");
                return;
            }
            for (var index = 0; index < choice.CandidateIds.Count; index++)
            {
                var candidateId = choice.CandidateIds[index];
                if (!TryDefinition(candidateId.Value, out var candidate)) continue;
                var targetId = candidate is RuntimeUpgradeOfferDefinition offer
                    ? offer.TargetContentId
                    : candidate.Id;
                Add(target, candidate.Id.Value, candidate.LocalizedNameKey, candidate.LocalizedDescriptionKey,
                    QinglanUiCommand.SelectReward, true, string.Empty,
                    CardTagKey(targetId), "ui.qinglan.card.relation.reward", CardEligibilityKey(targetId));
            }
        }

        private void BuildPause(QinglanPageViewModel target)
        {
            target.Reset(QinglanUiPageId.Pause, "ui.qinglan.pause.title", "ui.qinglan.pause.subtitle");
            Add(target, "resume", "ui.qinglan.pause.resume", "", QinglanUiCommand.Resume);
            Add(target, "settings", "ui.qinglan.settings.title", "", QinglanUiCommand.OpenSettings);
            Add(target, "abandon", "ui.qinglan.pause.abandon", "ui.qinglan.pause.abandon.description", QinglanUiCommand.AbandonRun);
        }

        private void BuildSettings(QinglanPageViewModel target)
        {
            target.Reset(QinglanUiPageId.Settings, "ui.qinglan.settings.title", "ui.qinglan.settings.description",
                input.LastRebindDiagnosticKey);
            AddSetting(target, "rebind", "ui.settings.rebind", "UI/Submit", QinglanUiCommand.Rebind);
            AddSetting(target, "locale", "ui.settings.language", localization.SelectedLocaleCode);
            AddSetting(target, "deadzone", "ui.settings.deadzone", Format(Settings.StickDeadzone));
            AddSetting(target, "vibration", "ui.settings.vibration", Format(Settings.VibrationIntensity));
            AddSetting(target, "screen_shake", "ui.settings.screen_shake", OnOff(Settings.ScreenShakeEnabled));
            AddSetting(target, "flash", "ui.settings.flash_intensity", Format(Settings.FlashIntensity));
            AddSetting(target, "damage_numbers", "ui.settings.damage_numbers", OnOff(Settings.DamageNumbersEnabled));
            AddSetting(target, "auto_aim", "ui.settings.auto_aim", Settings.AutoAim.ToString());
            AddSetting(target, "font_scale", "ui.qinglan.settings.font_scale", Math.Round(Settings.FontScale * 100f).ToString(CultureInfo.InvariantCulture) + "%");
            AddSetting(target, "color_vision", "ui.qinglan.settings.color_vision", Settings.ColorVision.ToString());
            AddSetting(target, "master_volume", "ui.qinglan.settings.master_volume", Format(Settings.MasterVolume));
            AddSetting(target, "music_volume", "ui.qinglan.settings.music_volume", Format(Settings.MusicVolume));
            AddSetting(target, "ambience_volume", "ui.qinglan.settings.ambience_volume", Format(Settings.AmbienceVolume));
            AddSetting(target, "effects_volume", "ui.qinglan.settings.effects_volume", Format(Settings.EffectsVolume));
            AddSetting(target, "subtitles", "ui.qinglan.settings.subtitles", OnOff(Settings.SubtitlesEnabled));
            Add(target, "back", "ui.common.back", "", QinglanUiCommand.Back);
        }

        private void BuildResult(QinglanPageViewModel target)
        {
            EnsureCommitStarted();
            var result = flow.LatestResult;
            var status = commitTask != null ? "ui.qinglan.result.saving" :
                lastCommit.IsSuccess ? "ui.qinglan.result.saved" :
                lastCommit.Status == CommitStatus.SaveFailed ? "ui.qinglan.result.not_saved" : string.Empty;
            target.Reset(
                QinglanUiPageId.RunResult,
                OutcomeKey(result.Outcome),
                "ui.qinglan.result.subtitle",
                status,
                FormatResult(result));
            if (lastCommit.Status == CommitStatus.SaveFailed || lastCommit.Status == CommitStatus.ValidationFailed)
                Add(target, "retry", "ui.qinglan.result.retry_save", lastCommit.Diagnostic.MessageKey, QinglanUiCommand.RetrySave);
            Add(target, "hub", "ui.qinglan.result.continue_hub", "ui.qinglan.result.continue_hub.description",
                QinglanUiCommand.ContinueToHub, lastCommit.IsSuccess && !flow.HasUncommittedResult);
        }

        private void BuildLoadoutConfirmation(QinglanPageViewModel target)
        {
            target.Reset(
                QinglanUiPageId.LoadoutConfirmation,
                "ui.qinglan.loadout.confirm.title",
                "ui.qinglan.loadout.confirm.description");
            Add(target, "confirm", "ui.qinglan.loadout.confirm.apply", "",
                QinglanUiCommand.ConfirmResetLoadout, metaTask == null);
            Add(target, "cancel", "ui.common.cancel", "", QinglanUiCommand.Back);
        }

        private void BuildHub(QinglanPageViewModel target)
        {
            var sand = Counter(profile.Profile.Currencies, QinglanMetaProgression.SpiritSandCurrency);
            target.Reset(QinglanUiPageId.Hub, "ui.qinglan.hub.title", "ui.qinglan.hub.subtitle",
                "ui.qinglan.hub.spirit_sand", sand.ToString(CultureInfo.InvariantCulture));
            var facilities = profile.Meta.ProjectFacilities(profile.Profile);
            for (var index = 0; index < facilities.Count; index++)
            {
                var item = facilities[index];
                AddContent(target, item.FacilityId.Value, QinglanUiCommand.OpenFacility,
                    item.State != MetaFacilityState.Locked,
                    item.State == MetaFacilityState.Locked ? "ui.qinglan.hub.locked" : string.Empty);
            }
            Add(target, "again", "ui.qinglan.hub.start_again", "", QinglanUiCommand.StartAgain);
            Add(target, "title", "ui.qinglan.hub.return_title", "", QinglanUiCommand.ReturnToTitle);
        }

        private void BuildFacility(QinglanPageViewModel target)
        {
            if (!TryDefinition(selectedFacilityId, out var source) || !(source is RuntimeMetaFacilityDefinition facility))
            {
                selectedFacilityId = string.Empty;
                BuildHub(target);
                return;
            }
            target.Reset(QinglanUiPageId.HubFacility, facility.LocalizedNameKey, facility.LocalizedDescriptionKey,
                lastMeta.IsSuccess ? "ui.qinglan.meta.saved" : lastMeta.Diagnostic.MessageKey);
            var pageId = facility.PageProfileId.Value;
            if (string.Equals(pageId, ScrollPage, StringComparison.Ordinal)) AddStories(target);
            else if (string.Equals(pageId, CollectionPage, StringComparison.Ordinal)) AddCollectibles(target);
            else if (string.Equals(pageId, VeinPage, StringComparison.Ordinal) ||
                     string.Equals(pageId, ArtifactPage, StringComparison.Ordinal)) AddMeta(target);
            Add(target, "back", "ui.common.back", "", QinglanUiCommand.Back);
        }

        private void BuildOverlay(QinglanPageViewModel target)
        {
            if (!TryDefinition(overlayContentId, out var definition))
            {
                overlayContentId = string.Empty;
                BuildHub(target);
                return;
            }
            if (definition is RuntimeStoryDefinition story)
            {
                var key = story.SequenceKeys.Count == 0 ? story.LocalizedDescriptionKey :
                    story.SequenceKeys[Math.Max(0, Math.Min(story.SequenceKeys.Count - 1, overlaySequenceIndex))];
                target.Reset(QinglanUiPageId.StoryOverlay, story.LocalizedNameKey, key,
                    "ui.qinglan.story.page", (overlaySequenceIndex + 1).ToString(CultureInfo.InvariantCulture) + "/" +
                    Math.Max(1, story.SequenceKeys.Count).ToString(CultureInfo.InvariantCulture));
            }
            else if (definition is RuntimeCollectibleDefinition collectible)
            {
                target.Reset(QinglanUiPageId.Collection, collectible.LocalizedNameKey,
                    Owns(profile.Profile.CollectedCollectibleIds, collectible.Id)
                        ? collectible.BodyLocalizationKey
                        : "ui.qinglan.collection.hint");
            }
            else target.Reset(QinglanUiPageId.Collection, definition.LocalizedNameKey, definition.LocalizedDescriptionKey);
            Add(target, "close", "ui.common.close", "", QinglanUiCommand.CloseOverlay);
        }

        private void BuildContentError(QinglanPageViewModel target)
        {
            target.Reset(QinglanUiPageId.ContentError, "ui.content_error.title",
                flow.ContentErrorKey, "ui.qinglan.content_error.code", flow.LastError.Code.ToString());
            Add(target, "title", "ui.content_error.main_menu", "", QinglanUiCommand.ReturnToTitle);
        }

        private void AddMapRows(QinglanPageViewModel target)
        {
            var hud = new RunUiSnapshot();
            if (flow.Session?.CaptureUiSnapshot(hud) != true) return;
            for (var index = 0; index < hud.MapCount; index++)
            {
                var item = hud.GetMapAt(index);
                if (!TryDefinition(item.ContentId, out var definition)) continue;
                Add(target, item.ContentId, definition.LocalizedNameKey, definition.LocalizedDescriptionKey,
                    QinglanUiCommand.None, false,
                    Math.Round(item.Progress * 100f).ToString(CultureInfo.InvariantCulture) + "%");
            }
        }

        private void AddMeta(QinglanPageViewModel target)
        {
            for (var index = 0; index < application.ContentRegistry.Count; index++)
            {
                var entry = application.ContentRegistry.Get(new RuntimeContentIndex(index));
                if (!entry.IsSuccess || !(entry.Value.Definition is RuntimeMetaDefinition meta) ||
                    meta is RuntimeMetaFacilityDefinition) continue;
                var owned = Owns(profile.Profile.UnlockedContentIds, meta.Id);
                var selected = ContainsPending(meta.Id);
                Add(target, meta.Id.Value, meta.LocalizedNameKey, meta.LocalizedDescriptionKey,
                    owned ? QinglanUiCommand.ToggleLoadout : QinglanUiCommand.Purchase,
                    metaTask == null,
                    owned ? (selected ? "ui.qinglan.meta.equipped" : "ui.qinglan.meta.owned") :
                    meta.Cost.ToString(CultureInfo.InvariantCulture));
            }
            Add(target, "apply", "ui.qinglan.meta.apply_loadout", "ui.qinglan.meta.apply_loadout.description",
                QinglanUiCommand.ResetLoadout, metaTask == null);
        }

        private void AddStories(QinglanPageViewModel target)
        {
            for (var index = 0; index < application.ContentRegistry.Count; index++)
            {
                var entry = application.ContentRegistry.Get(new RuntimeContentIndex(index));
                if (!entry.IsSuccess || !(entry.Value.Definition is RuntimeStoryDefinition story)) continue;
                var unlocked = Owns(profile.Profile.CompletedStoryIds, story.Id);
                Add(target, story.Id.Value, story.LocalizedNameKey,
                    unlocked ? story.LocalizedDescriptionKey : "ui.qinglan.story.locked",
                    QinglanUiCommand.OpenStories, unlocked);
            }
        }

        private void AddCollectibles(QinglanPageViewModel target)
        {
            for (var index = 0; index < application.ContentRegistry.Count; index++)
            {
                var entry = application.ContentRegistry.Get(new RuntimeContentIndex(index));
                if (!entry.IsSuccess || !(entry.Value.Definition is RuntimeCollectibleDefinition collectible)) continue;
                var unlocked = Owns(profile.Profile.CollectedCollectibleIds, collectible.Id);
                Add(target, collectible.Id.Value, collectible.LocalizedNameKey,
                    unlocked ? collectible.LocalizedDescriptionKey : "ui.qinglan.collection.hint",
                    QinglanUiCommand.OpenCollection, true,
                    unlocked ? "ui.qinglan.collection.collected" : "ui.qinglan.collection.unknown");
            }
        }

        private bool OpenLoadout()
        {
            if (flow.Stage != DemoFlowStage.MapSelect) return false;
            selectedFacilityId = "loadout";
            return true;
        }

        private bool BeginRun()
        {
            if (flow.Stage != DemoFlowStage.MapSelect) return false;
            var loadout = profile.Meta.ProjectLoadout(profile.Profile).Loadout;
            var unique = Copy(profile.Profile.ClaimedUniqueRewardIds);
            var now = unchecked((ulong)DateTime.UtcNow.Ticks + ++runSequence);
            var descriptor = factory.CreateDescriptor(now, now ^ 0x514C414E44454D4FUL, loadout, unique);
            if (!descriptor.IsSuccess) return false;
            selectedFacilityId = string.Empty;
            var started = flow.BeginRun(descriptor.Value);
            if (!started) return false;
            commitAttempted = false;
            lastCommit = default;
            var initialSkill = default(ContentId);
            if (application.ContentRegistry.TryGet(descriptor.Value.CharacterId, out RuntimeCharacterDefinition character) &&
                character.StartingSkillIds.Count > 0) initialSkill = character.StartingSkillIds[0];
            application.Events.Publish(ApplicationEvent.RunStarted(
                descriptor.Value.Seed,
                descriptor.Value.CharacterId,
                descriptor.Value.MapId,
                initialSkill));
            return true;
        }

        private bool StartCommit()
        {
            if (commitTask != null || flow.Stage != DemoFlowStage.Result || !flow.HasUncommittedResult) return false;
            commitAttempted = true;
            commitTask = profile.CommitRunResultAsync(flow.LatestResult, flow).AsTask();
            return true;
        }

        private void EnsureCommitStarted()
        {
            if (commitAttempted || lastCommit.IsSuccess || commitTask != null || !flow.HasUncommittedResult ||
                flow.LatestResult.Outcome == RunOutcome.RecoveryRejected) return;
            StartCommit();
        }

        private bool StartPurchase(string id)
        {
            if (metaTask != null || !TryId(id, out var contentId)) return false;
            metaTask = profile.PurchaseAsync(contentId).AsTask();
            return true;
        }

        private bool StartResetLoadout()
        {
            if (metaTask != null) return false;
            var terminal = default(ContentId);
            var branches = new List<ContentId>(pendingNodes.Count);
            for (var index = 0; index < pendingNodes.Count; index++)
            {
                if (application.ContentRegistry.TryGet(pendingNodes[index], out RuntimeMetaNodeDefinition node) &&
                    node.NodeKind == MetaNodeKind.Terminal) terminal = node.Id;
                else branches.Add(pendingNodes[index]);
            }
            var loadout = terminal.IsValid
                ? new MetaLoadout(branches.ToArray(), terminal, pendingInserts.ToArray())
                : new MetaLoadout(branches.ToArray(), pendingInserts.ToArray());
            metaTask = profile.ResetLoadoutAsync(loadout).AsTask();
            return true;
        }

        private bool TogglePendingLoadout(string id)
        {
            if (!TryId(id, out var contentId) || !Owns(profile.Profile.UnlockedContentIds, contentId)) return false;
            if (application.ContentRegistry.TryGet(contentId, out RuntimeMetaInsertDefinition _))
                Toggle(pendingInserts, contentId);
            else if (application.ContentRegistry.TryGet(contentId, out RuntimeMetaNodeDefinition _))
                Toggle(pendingNodes, contentId);
            else return false;
            return true;
        }

        private void PollOperations()
        {
            if (commitTask != null && commitTask.IsCompleted)
            {
                try { lastCommit = commitTask.GetAwaiter().GetResult(); }
                catch { lastCommit = new CommitResult(CommitStatus.SaveFailed,
                    new SaveDiagnostic(SaveFailureCode.IoFailure, "save.error.write_failed")); }
                commitTask = null;
            }
            if (metaTask != null && metaTask.IsCompleted)
            {
                try { lastMeta = metaTask.GetAwaiter().GetResult(); }
                catch { lastMeta = default; }
                metaTask = null;
                if (lastMeta.IsSuccess) ResetPendingLoadout();
            }
        }

        private bool CycleSetting(string id)
        {
            switch (id)
            {
                case "deadzone": Settings.SetStickDeadzone(Step(Settings.StickDeadzone, 0.05f, 0.1f, 0.4f)); break;
                case "locale": localization.SelectNextLocale(); break;
                case "vibration": Settings.SetVibrationIntensity(Step(Settings.VibrationIntensity, 0.25f, 0f, 1f)); break;
                case "screen_shake": Settings.SetScreenShakeEnabled(!Settings.ScreenShakeEnabled); break;
                case "flash": Settings.SetFlashIntensity(Step(Settings.FlashIntensity, 0.25f, 0f, 1f)); break;
                case "damage_numbers": Settings.SetDamageNumbersEnabled(!Settings.DamageNumbersEnabled); break;
                case "auto_aim": Settings.SetAutoAim((AutoAimStrategy)(((int)Settings.AutoAim + 1) % 4)); break;
                case "font_scale": Settings.SetFontScale(Settings.FontScale >= 1.5f ? 1f : Settings.FontScale + 0.25f); break;
                case "color_vision": Settings.SetColorVision((ColorVisionMode)(((int)Settings.ColorVision + 1) % 5)); break;
                case "master_volume": Settings.SetMasterVolume(Step(Settings.MasterVolume, 0.25f, 0f, 1f)); break;
                case "music_volume": Settings.SetMusicVolume(Step(Settings.MusicVolume, 0.25f, 0f, 1f)); break;
                case "ambience_volume": Settings.SetAmbienceVolume(Step(Settings.AmbienceVolume, 0.25f, 0f, 1f)); break;
                case "effects_volume": Settings.SetEffectsVolume(Step(Settings.EffectsVolume, 0.25f, 0f, 1f)); break;
                case "subtitles": Settings.SetSubtitlesEnabled(!Settings.SubtitlesEnabled); break;
                default: return false;
            }
            input.SetStickDeadzone(Settings.StickDeadzone);
            SaveSettings();
            return true;
        }

        private bool CycleSubmitBinding()
        {
            var action = input.Actions?.FindAction("UI/Submit", false);
            if (action == null || action.bindings.Count == 0) return false;
            var next = string.Equals(action.bindings[0].effectivePath, "<Keyboard>/numpadEnter", StringComparison.Ordinal)
                ? "<Keyboard>/enter"
                : "<Keyboard>/numpadEnter";
            var changed = input.ApplyBindingOverride("UI/Submit", 0, next);
            if (changed) SaveSettings();
            return changed;
        }

        private void SaveSettings() => application.Events.Publish(ApplicationEvent.SettingsChanged(CaptureSettings()));

        private void ResetPendingLoadout()
        {
            pendingNodes.Clear();
            pendingInserts.Clear();
            var projection = profile.Meta.ProjectLoadout(profile.Profile).Loadout;
            for (var index = 0; index < projection.EquippedNodeIds.Count; index++) pendingNodes.Add(projection.EquippedNodeIds[index]);
            if (projection.HasTerminalNode) pendingNodes.Add(projection.TerminalNodeId);
            for (var index = 0; index < projection.EquippedInsertIds.Count; index++) pendingInserts.Add(projection.EquippedInsertIds[index]);
        }

        private bool ContainsPending(ContentId id) => Contains(pendingNodes, id) || Contains(pendingInserts, id);

        private bool TryDefinition(string id, out RuntimeContentDefinition definition)
        {
            definition = null;
            return TryId(id, out var contentId) &&
                   application.ContentRegistry.TryGet(contentId, out ContentRegistryEntry entry) &&
                   (definition = entry.Definition) != null;
        }

        private static bool TryId(string value, out ContentId id)
        {
            var parsed = ContentId.Create(value);
            id = parsed.IsSuccess ? parsed.Value : default;
            return parsed.IsSuccess;
        }

        private void AddContent(
            QinglanPageViewModel target,
            string id,
            QinglanUiCommand command,
            bool enabled = true,
            string value = "")
        {
            if (TryDefinition(id, out var definition))
                Add(target, id, definition.LocalizedNameKey, definition.LocalizedDescriptionKey, command, enabled, value);
        }

        private static void Add(
            QinglanPageViewModel target,
            string id,
            string label,
            string description,
            QinglanUiCommand command,
            bool enabled = true,
            string value = "",
            string tagKey = "",
            string relationKey = "",
            string eligibilityKey = "") =>
            target.Add(new QinglanUiOption(
                id, label, description, command, enabled, value, tagKey, relationKey, eligibilityKey));

        private static void AddSetting(
            QinglanPageViewModel target,
            string id,
            string key,
            string value,
            QinglanUiCommand command = QinglanUiCommand.CycleSetting) =>
            Add(target, id, key, "", command, true, value);

        private static int FindBuildLevel(RunUiSnapshot snapshot, string id)
        {
            for (var index = 0; index < snapshot.BuildCount; index++)
            {
                var entry = snapshot.GetBuildAt(index);
                if (string.Equals(entry.ContentId, id, StringComparison.Ordinal)) return entry.Level;
            }
            return 0;
        }

        private string CardTagKey(ContentId id)
        {
            if (!application.ContentRegistry.TryGet(id, out RuntimeContentDefinition definition))
                return "ui.qinglan.card.tag.reward";
            if (definition is RuntimeSkillDefinition) return "ui.qinglan.card.tag.skill";
            if (definition is RuntimePassiveDefinition) return "ui.qinglan.card.tag.passive";
            if (definition is RuntimeEvolutionDefinition) return "ui.qinglan.card.tag.evolution";
            if (definition is RuntimeRelicDefinition) return "ui.qinglan.card.tag.relic";
            return "ui.qinglan.card.tag.reward";
        }

        private string CardEligibilityKey(ContentId id) =>
            application.ContentRegistry.TryGet(id, out RuntimeEvolutionDefinition _)
                ? "ui.qinglan.card.evolution.ready"
                : string.Empty;

        private static string OutcomeKey(RunOutcome outcome)
        {
            switch (outcome)
            {
                case RunOutcome.Victory: return "ui.qinglan.result.victory";
                case RunOutcome.Defeat: return "ui.qinglan.result.defeat";
                case RunOutcome.Abandoned: return "ui.qinglan.result.abandoned";
                default: return "ui.qinglan.result.recovery_rejected";
            }
        }

        private static string FormatResult(RunResult result) =>
            Math.Round(result.DurationSeconds, 1).ToString(CultureInfo.InvariantCulture) + "s · Lv." +
            result.Level.ToString(CultureInfo.InvariantCulture) + " · " +
            result.Statistics.EnemyDefeats.ToString(CultureInfo.InvariantCulture);

        private static string Format(float value) =>
            Math.Round(value * 100f).ToString(CultureInfo.InvariantCulture) + "%";

        private static string OnOff(bool value) => value ? "ui.common.on" : "ui.common.off";

        private static float Step(float value, float amount, float minimum, float maximum) =>
            value >= maximum ? minimum : Math.Min(maximum, value + amount);

        private static long Counter(IReadOnlyList<SavedCounter> source, string key)
        {
            for (var index = 0; index < source.Count; index++)
                if (string.Equals(source[index].Key, key, StringComparison.Ordinal)) return source[index].Value;
            return 0;
        }

        private static bool Owns(IReadOnlyList<ContentId> source, ContentId id)
        {
            for (var index = 0; index < source.Count; index++) if (source[index] == id) return true;
            return false;
        }

        private static bool Contains(List<ContentId> source, ContentId id)
        {
            for (var index = 0; index < source.Count; index++) if (source[index] == id) return true;
            return false;
        }

        private static void Toggle(List<ContentId> source, ContentId id)
        {
            for (var index = 0; index < source.Count; index++)
            {
                if (source[index] != id) continue;
                source.RemoveAt(index);
                return;
            }
            source.Add(id);
        }

        private static ContentId[] Copy(IReadOnlyList<ContentId> source)
        {
            var result = new ContentId[source.Count];
            for (var index = 0; index < result.Length; index++) result[index] = source[index];
            return result;
        }
    }
}
