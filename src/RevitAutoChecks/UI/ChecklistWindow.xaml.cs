using System;
using System.Windows;
using System.Windows.Interop;
using Autodesk.Revit.UI;
using RevitAutoChecks.Checks;
using RevitAutoChecks.Revit;

namespace RevitAutoChecks.UI
{
    /// <summary>Немодальное окно чек-листа, одно на сеанс Revit.</summary>
    public partial class ChecklistWindow : Window
    {
        public static ChecklistWindow Current { get; private set; }

        public ChecklistViewModel ViewModel { get; }

        private ChecklistWindow(ChecklistViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            DataContext = viewModel;
        }

        public static void ShowFor(UIApplication uiApp, ModelInfo model)
        {
            if (Current != null)
            {
                Current.ViewModel.AttachModel(model);
                if (Current.WindowState == WindowState.Minimized) Current.WindowState = WindowState.Normal;
                Current.Activate();
                return;
            }

            var viewModel = new ChecklistViewModel(Application.Store, Application.RevitTask, CheckRegistry.All, model);
            var window = new ChecklistWindow(viewModel);
            new WindowInteropHelper(window).Owner = uiApp.MainWindowHandle;
            window.Closed += (s, e) => Current = null;
            Current = window;
            window.Show();
        }

        public static void CloseIfOpen() => Current?.Close();
    }
}
