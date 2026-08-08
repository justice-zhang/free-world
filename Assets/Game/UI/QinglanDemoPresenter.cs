using System;
using Game.Application;

namespace Game.UI
{
    /// <summary>Owns page focus and routes UI commands without gameplay truth.</summary>
    public sealed class QinglanDemoPresenter
    {
        private readonly IQinglanDemoUiController controller;
        private readonly IQinglanDemoView view;
        private readonly QinglanPageViewModel page = new QinglanPageViewModel();
        private readonly RunUiSnapshot hud = new RunUiSnapshot();
        private readonly int[] rememberedFocus = new int[18];
        private QinglanUiPageId lastPage;

        public QinglanDemoPresenter(IQinglanDemoUiController uiController, IQinglanDemoView demoView)
        {
            controller = uiController ?? throw new ArgumentNullException(nameof(uiController));
            view = demoView ?? throw new ArgumentNullException(nameof(demoView));
            Refresh(true);
        }

        public QinglanPageViewModel CurrentPage => page;
        public RunUiSnapshot CurrentHud => hud;

        public void Refresh(bool forcePage = false)
        {
            var previous = page.Page;
            var previousSelection = page.SelectedIndex;
            if (previous != 0) rememberedFocus[(int)previous] = previousSelection;
            if (controller.PopulatePage(page))
            {
                var changed = previous != page.Page;
                page.RestoreSelection(changed ? rememberedFocus[(int)page.Page] : previousSelection);
                if (forcePage || changed || lastPage != page.Page) lastPage = page.Page;
                view.ShowPage(page);
            }
            if (controller.PopulateHud(hud)) view.ShowHud(hud);
            view.ApplyAccessibility(controller.Settings);
        }

        public void Navigate(float vertical)
        {
            if (page.MoveSelection(vertical < -0.5f ? 1 : vertical > 0.5f ? -1 : 0))
                view.ShowPage(page);
        }

        public void Submit()
        {
            if (page.OptionCount == 0) return;
            var option = page.GetOptionAt(page.SelectedIndex);
            if (option.Enabled && controller.Execute(option.Command, option.StableId, page.SelectedIndex))
                Refresh(true);
        }

        public void Cancel()
        {
            if (controller.Cancel()) Refresh(true);
        }

        public void Tab(int direction)
        {
            if (controller.CycleTab(direction)) Refresh(true);
        }

        public void Page(int direction)
        {
            if (controller.CyclePage(direction)) Refresh(true);
        }

        /// <summary>Reapplies a valid focus after a control device changes or disconnects.</summary>
        public void RestoreFocus()
        {
            page.RestoreSelection(rememberedFocus[(int)page.Page]);
            view.ShowPage(page);
        }
    }
}
