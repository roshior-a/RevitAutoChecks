using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitAutoChecks.Checks;
using RevitAutoChecks.Revit;
using RevitAutoChecks.Storage;

namespace RevitAutoChecks.UI
{
    public sealed class ChecklistViewModel : ObservableObject
    {
        private readonly CheckResultStore _store;
        private readonly RevitTask _revitTask;
        private ModelInfo _model;
        private CheckItemViewModel _selectedCheck;
        private bool _isBusy;
        private string _busyText;

        public ChecklistViewModel(CheckResultStore store, RevitTask revitTask, IEnumerable<ICheck> checks, ModelInfo model)
        {
            _store = store;
            _revitTask = revitTask;
            Checks = new ObservableCollection<CheckItemViewModel>(
                checks.OrderBy(c => c.Number).Select(c => new CheckItemViewModel(c, this)));
            ShowSummaryCommand = new RelayCommand(() => SelectedCheck = null);
            RunAllCommand = new RelayCommand(() => Run(Checks.ToList()), () => !IsBusy);
            AttachModel(model, force: true);
        }

        public ObservableCollection<CheckItemViewModel> Checks { get; }

        public ICommand ShowSummaryCommand { get; }
        public ICommand RunAllCommand { get; }

        public string ModelTitle => _model?.Title ?? "";

        public string WindowTitle => "Чек-лист — " + ModelTitle;

        /// <summary>Правая часть окна: свод (сам ChecklistViewModel) или выбранная проверка.</summary>
        public object CurrentPage => (object)SelectedCheck ?? this;

        public bool IsSummaryPage => SelectedCheck == null;

        public CheckItemViewModel SelectedCheck
        {
            get => _selectedCheck;
            set
            {
                if (!SetProperty(ref _selectedCheck, value)) return;
                OnPropertyChanged(nameof(CurrentPage));
                OnPropertyChanged(nameof(IsSummaryPage));
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value)) CommandManager.InvalidateRequerySuggested();
            }
        }

        public string BusyText
        {
            get => _busyText;
            private set => SetProperty(ref _busyText, value);
        }

        public string OverviewText
        {
            get
            {
                int passed = Checks.Count(c => c.State == CheckState.Passed);
                int failed = Checks.Count(c => c.State == CheckState.Failed);
                int notRun = Checks.Count(c => c.State == CheckState.NotRun);
                int outdated = Checks.Count(c => c.IsOutdated);
                return $"Всего проверок: {Checks.Count}  ·  пройдено: {passed}  ·  с ошибками: {failed}  ·  " +
                       $"не выполнялось: {notRun}  ·  устарело: {outdated}";
            }
        }

        public string FreshnessHint =>
            $"Результат устаревает, если модель сохранена позже чем через {Freshness.StaleAfter.TotalDays:0} дней после проверки.";

        /// <summary>Переключение на другую модель (активирован вид другого документа).</summary>
        public void AttachModel(ModelInfo model, bool force = false)
        {
            if (!force && model.SameModel(_model)) return;
            _model = model;
            OnPropertyChanged(nameof(ModelTitle));
            OnPropertyChanged(nameof(WindowTitle));
            Reload();
        }

        public void OnModelSaved(ModelInfo model)
        {
            if (model.SameModel(_model)) Reload();
        }

        public void Reload()
        {
            var results = _store.Load(_model);
            var lastSave = _store.GetLastModelSave(_model);
            foreach (var item in Checks)
                item.Apply(results.Checks.FirstOrDefault(r => r.CheckId == item.Id), lastSave);
            OnPropertyChanged(nameof(OverviewText));
            CommandManager.InvalidateRequerySuggested();
        }

        public void Run(IList<CheckItemViewModel> items)
        {
            if (IsBusy || items.Count == 0) return;
            IsBusy = true;
            BusyText = items.Count == 1 ? "Выполняется проверка…" : $"Выполняются проверки ({items.Count})…";

            bool queued = _revitTask.Run(app =>
            {
                try
                {
                    RunInRevit(app, items);
                }
                finally
                {
                    IsBusy = false;
                }
            });
            if (!queued)
            {
                IsBusy = false;
                TaskDialog.Show("Чек-лист", "Revit занят. Завершите текущую команду и повторите.");
            }
        }

        private void RunInRevit(UIApplication app, IList<CheckItemViewModel> items)
        {
            var doc = ActiveProjectDocument(app);
            if (doc == null) return;

            var context = new CheckContext { Document = doc, UiApplication = app, AllowUi = true };
            string user = string.IsNullOrWhiteSpace(app.Application.Username) ? Environment.UserName : app.Application.Username;
            var stored = new List<StoredCheckResult>();

            foreach (var item in items)
            {
                var record = new StoredCheckResult { CheckId = item.Id, RunAt = DateTime.Now, RunBy = user };
                try
                {
                    var result = item.Check.Run(context);
                    record.Passed = result.Passed;
                    record.Summary = result.Summary;
                    record.ElementIds = result.ElementIds.Select(ElementIds.ToLong).Distinct().ToList();
                    record.Log = result.Log;
                }
                catch (Exception ex)
                {
                    record.Passed = false;
                    record.Summary = "Проверка завершилась с ошибкой: " + ex.Message;
                    record.Log = ex.ToString();
                }
                stored.Add(record);
            }

            _store.Save(_model, stored);
            Reload();
        }

        public void SelectElements(CheckItemViewModel item)
        {
            var ids = item.Result?.ElementIds?.ToList() ?? new List<long>();
            if (ids.Count == 0) return;

            _revitTask.Run(app =>
            {
                var doc = ActiveProjectDocument(app);
                if (doc == null) return;

                var existing = ids.Select(ElementIds.FromLong).Where(id => doc.GetElement(id) != null).ToList();
                if (existing.Count == 0)
                {
                    TaskDialog.Show("Чек-лист", "Элементы из результата проверки не найдены в модели — обновите проверку.");
                    return;
                }
                app.ActiveUIDocument.Selection.SetElementIds(existing);
                if (existing.Count < ids.Count)
                    TaskDialog.Show("Чек-лист", $"Выделено элементов: {existing.Count} из {ids.Count}. " +
                                                "Остальных уже нет в модели — обновите проверку.");
            });
        }

        /// <summary>Активный документ, если это модель окна; иначе окно переключается на активную модель.</summary>
        private Document ActiveProjectDocument(UIApplication app)
        {
            var doc = app.ActiveUIDocument?.Document;
            if (doc == null || doc.IsFamilyDocument)
            {
                TaskDialog.Show("Чек-лист", "Откройте модель проекта.");
                return null;
            }

            var active = ModelInfo.From(doc);
            if (!active.SameModel(_model))
            {
                AttachModel(active);
                TaskDialog.Show("Чек-лист", $"Активна другая модель — окно переключено на «{active.Title}». Повторите действие.");
                return null;
            }
            return doc;
        }
    }
}
