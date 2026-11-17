using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using RevitAutoChecks.Commands;
using RevitAutoChecks.Revit;
using RevitAutoChecks.Storage;
using RevitAutoChecks.UI;

namespace RevitAutoChecks
{
    public class Application : IExternalApplication
    {
        public const string RibbonTabName = "R.A.C.";
        public const string HelpUrl = "https://github.com/roshior-a/RevitAutoChecks/";

        internal static RevitTask RevitTask { get; private set; }
        internal static CheckResultStore Store { get; private set; }

        public Result OnStartup(UIControlledApplication application)
        {
            RevitTask = new RevitTask();
            Store = new CheckResultStore(CheckResultStore.DefaultFolder);

            CreateRibbon(application);

            application.ControlledApplication.DocumentSaved += OnDocumentSaved;
            application.ControlledApplication.DocumentSavedAs += OnDocumentSavedAs;
            application.ControlledApplication.DocumentSynchronizedWithCentral += OnDocumentSynchronized;
            application.ViewActivated += OnViewActivated;

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            application.ControlledApplication.DocumentSaved -= OnDocumentSaved;
            application.ControlledApplication.DocumentSavedAs -= OnDocumentSavedAs;
            application.ControlledApplication.DocumentSynchronizedWithCentral -= OnDocumentSynchronized;
            application.ViewActivated -= OnViewActivated;

            ChecklistWindow.CloseIfOpen();
            RevitTask?.Dispose();
            return Result.Succeeded;
        }

        private static void CreateRibbon(UIControlledApplication application)
        {
            try
            {
                application.CreateRibbonTab(RibbonTabName);
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
                // Вкладка уже создана (например, другим плагином с тем же именем)
            }

            RibbonPanel panelUtils = application.CreateRibbonPanel(RibbonTabName, "Утилиты");

            var buttonData = new PushButtonData(
                nameof(ShowChecklistCommand),
                "Чек-лист",
                typeof(ShowChecklistCommand).Assembly.Location,
                typeof(ShowChecklistCommand).FullName)
            {
                ToolTip = "Чек-лист проверок модели.",
                LongDescription = "Список автоматических проверок модели: запуск, статусы, актуальность результатов, "
                                  + "выделение найденных элементов и журнал проверки.",
                Image = CreateIcon(16),
                LargeImage = CreateIcon(32)
            };
            buttonData.SetContextualHelp(new ContextualHelp(ContextualHelpType.Url, HelpUrl));
            panelUtils.AddItem(buttonData);
        }

        /// <summary>Значок кнопки рисуется в коде, чтобы не хранить картинки в ресурсах.</summary>
        private static BitmapSource CreateIcon(int size)
        {
            double s = size;
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                var background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2E, 0x7D, 0x32));
                dc.DrawRoundedRectangle(background, null, new Rect(s * 0.06, s * 0.06, s * 0.88, s * 0.88), s * 0.16, s * 0.16);

                var pen = new System.Windows.Media.Pen(Brushes.White, s * 0.13)
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round,
                    LineJoin = PenLineJoin.Round
                };
                var check = new StreamGeometry();
                using (var g = check.Open())
                {
                    g.BeginFigure(new System.Windows.Point(s * 0.27, s * 0.52), false, false);
                    g.PolyLineTo(new[] { new System.Windows.Point(s * 0.43, s * 0.68), new System.Windows.Point(s * 0.74, s * 0.34) }, true, true);
                }
                dc.DrawGeometry(null, pen, check);
            }

            var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }

        // ---- Сохранения модели: от них считается актуальность результатов ----

        private static void OnDocumentSaved(object sender, DocumentSavedEventArgs e)
        {
            if (e.Status == RevitAPIEventStatus.Succeeded) MarkSaved(e.Document);
        }

        private static void OnDocumentSavedAs(object sender, DocumentSavedAsEventArgs e)
        {
            if (e.Status == RevitAPIEventStatus.Succeeded) MarkSaved(e.Document);
        }

        private static void OnDocumentSynchronized(object sender, DocumentSynchronizedWithCentralEventArgs e)
        {
            if (e.Status == RevitAPIEventStatus.Succeeded) MarkSaved(e.Document);
        }

        private static void MarkSaved(Document doc)
        {
            if (doc == null || doc.IsFamilyDocument) return;
            var model = ModelInfo.From(doc);
            Store.MarkModelSaved(model, DateTime.Now);
            ChecklistWindow.Current?.ViewModel.OnModelSaved(model);
        }

        private static void OnViewActivated(object sender, ViewActivatedEventArgs e)
        {
            var window = ChecklistWindow.Current;
            if (window == null || e.Document == null || e.Document.IsFamilyDocument) return;
            window.ViewModel.AttachModel(ModelInfo.From(e.Document));
        }
    }
}
