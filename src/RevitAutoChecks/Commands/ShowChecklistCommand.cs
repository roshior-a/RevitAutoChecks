using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitAutoChecks.Revit;
using RevitAutoChecks.UI;

namespace RevitAutoChecks.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ShowChecklistCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiDoc = commandData.Application.ActiveUIDocument;
            if (uiDoc == null || uiDoc.Document.IsFamilyDocument)
            {
                TaskDialog.Show("Чек-лист", "Откройте модель проекта.");
                return Result.Cancelled;
            }

            ChecklistWindow.ShowFor(commandData.Application, ModelInfo.From(uiDoc.Document));
            return Result.Succeeded;
        }
    }
}
