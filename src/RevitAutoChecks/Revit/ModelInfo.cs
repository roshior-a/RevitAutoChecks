using System;
using Autodesk.Revit.DB;

namespace RevitAutoChecks.Revit
{
    /// <summary>
    /// Модель, к которой относятся результаты: для модели с совместной работой — путь к хранилищу,
    /// иначе путь к файлу. Локальные копии одного хранилища делят общие результаты.
    /// </summary>
    public sealed class ModelInfo
    {
        public string ModelPath { get; private set; }
        public string Title { get; private set; }
        /// <summary>Файл на диске (для локальной копии — сама копия); может быть пустым или облачным.</summary>
        public string LocalFilePath { get; private set; }
        public bool IsSaved { get; private set; }

        public static ModelInfo From(Document doc)
        {
            string path = null;
            if (doc.IsWorkshared)
            {
                try
                {
                    var central = doc.GetWorksharingCentralModelPath();
                    if (central != null) path = Autodesk.Revit.DB.ModelPathUtils.ConvertModelPathToUserVisiblePath(central);
                }
                catch (Exception)
                {
                    // путь к хранилищу недоступен — берём путь к файлу
                }
            }
            if (string.IsNullOrWhiteSpace(path)) path = doc.PathName;

            bool saved = !string.IsNullOrWhiteSpace(path);
            if (!saved) path = "unsaved:" + doc.Title;

            string title = saved ? System.IO.Path.GetFileNameWithoutExtension(path) : doc.Title;
            if (string.IsNullOrWhiteSpace(title)) title = doc.Title;

            return new ModelInfo { ModelPath = path, Title = title, LocalFilePath = doc.PathName, IsSaved = saved };
        }

        public bool SameModel(ModelInfo other) =>
            other != null && string.Equals(ModelPath, other.ModelPath, StringComparison.OrdinalIgnoreCase);
    }
}
