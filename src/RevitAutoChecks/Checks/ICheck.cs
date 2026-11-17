using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitAutoChecks.Checks
{
    public interface ICheck
    {
        /// <summary>Постоянный ключ: по нему хранится результат.</summary>
        string Id { get; }
        int Number { get; }
        string Title { get; }
        string Description { get; }

        /// <summary>Выполняется в контексте Revit API.</summary>
        CheckRunResult Run(CheckContext context);
    }

    public sealed class CheckContext
    {
        public Document Document { get; set; }
        public UIApplication UiApplication { get; set; }
        /// <summary>Можно показывать окна и менять отображение (например, открыть рабочий набор связи).</summary>
        public bool AllowUi { get; set; }
    }

    public sealed class CheckRunResult
    {
        public bool Passed { get; set; }
        public string Summary { get; set; }
        /// <summary>Элементы текущей модели, которые можно выделить.</summary>
        public List<ElementId> ElementIds { get; set; } = new List<ElementId>();
        public string Log { get; set; }
    }

    public static class CheckRegistry
    {
        public static IReadOnlyList<ICheck> All { get; } = new ICheck[]
        {
            new PipeAccessoriesOverTraysCheck()
        };
    }
}
