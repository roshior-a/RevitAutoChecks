using System;
using System.Windows.Input;
using RevitAutoChecks.Checks;
using RevitAutoChecks.Storage;

namespace RevitAutoChecks.UI
{
    public enum CheckState
    {
        NotRun,
        Passed,
        Failed
    }

    /// <summary>Проверка в окне: последний результат для текущей модели и команды.</summary>
    public sealed class CheckItemViewModel : ObservableObject
    {
        private readonly ChecklistViewModel _owner;
        private StoredCheckResult _result;
        private bool _isOutdated;
        private bool _isLogVisible;

        public CheckItemViewModel(ICheck check, ChecklistViewModel owner)
        {
            Check = check;
            _owner = owner;
            RunCommand = new RelayCommand(() => _owner.Run(new[] { this }), () => !_owner.IsBusy);
            SelectCommand = new RelayCommand(() => _owner.SelectElements(this), () => !_owner.IsBusy && ElementCount > 0);
            ToggleLogCommand = new RelayCommand(() => IsLogVisible = !IsLogVisible, () => HasLog);
        }

        public ICheck Check { get; }
        public string Id => Check.Id;
        public int Number => Check.Number;
        public string Title => Check.Title;
        public string Description => Check.Description;

        public ICommand RunCommand { get; }
        public ICommand SelectCommand { get; }
        public ICommand ToggleLogCommand { get; }

        public StoredCheckResult Result => _result;

        public CheckState State => _result == null ? CheckState.NotRun : _result.Passed ? CheckState.Passed : CheckState.Failed;

        public string StatusText
        {
            get
            {
                switch (State)
                {
                    case CheckState.Passed: return "Пройдена";
                    case CheckState.Failed: return ElementCount > 0 ? $"Ошибки: {ElementCount}" : "Ошибка";
                    default: return "Не выполнялась";
                }
            }
        }

        public bool IsOutdated => _isOutdated;

        public string ActualityText => _result == null ? "—" : _isOutdated ? "Устарела" : "Актуальна";

        public string RunAtText => _result == null ? "—" : _result.RunAt.ToString("dd.MM.yyyy HH:mm");

        public string RunBy => _result?.RunBy ?? "—";

        public string Summary => _result?.Summary ?? "Проверка ещё не выполнялась для этой модели.";

        public int ElementCount => _result?.ElementIds?.Count ?? 0;

        public string Log => string.IsNullOrWhiteSpace(_result?.Log) ? "Лог пуст." : _result.Log.Trim();

        public bool HasLog => _result != null;

        public bool IsLogVisible
        {
            get => _isLogVisible;
            set
            {
                if (SetProperty(ref _isLogVisible, value)) OnPropertyChanged(nameof(LogButtonText));
            }
        }

        public string LogButtonText => IsLogVisible ? "Скрыть лог" : "Показать лог";

        public void Apply(StoredCheckResult result, DateTime? lastModelSave)
        {
            _result = result;
            _isOutdated = result != null && Freshness.IsOutdated(result.RunAt, lastModelSave);
            if (result == null) IsLogVisible = false;
            OnPropertyChanged(string.Empty);
        }
    }
}
