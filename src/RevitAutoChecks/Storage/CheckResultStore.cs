using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using RevitAutoChecks.Revit;

namespace RevitAutoChecks.Storage
{
    [DataContract]
    public sealed class StoredCheckResult
    {
        [DataMember(Order = 1)] public string CheckId { get; set; }
        [DataMember(Order = 2)] public DateTime RunAt { get; set; }
        [DataMember(Order = 3)] public string RunBy { get; set; }
        [DataMember(Order = 4)] public bool Passed { get; set; }
        [DataMember(Order = 5)] public string Summary { get; set; }
        [DataMember(Order = 6)] public List<long> ElementIds { get; set; } = new List<long>();
        [DataMember(Order = 7)] public string Log { get; set; }
    }

    [DataContract]
    public sealed class ModelResults
    {
        [DataMember(Order = 1)] public string ModelPath { get; set; }
        /// <summary>Последнее сохранение модели, замеченное плагином (сохранение, «Сохранить как», синхронизация).</summary>
        [DataMember(Order = 2)] public DateTime? LastModelSavedAt { get; set; }
        [DataMember(Order = 3)] public List<StoredCheckResult> Checks { get; set; } = new List<StoredCheckResult>();
    }

    /// <summary>
    /// Результаты проверок: по JSON-файлу на модель в %AppData%\RevitAutoChecks\Results.
    /// </summary>
    public sealed class CheckResultStore
    {
        private static readonly DataContractJsonSerializer Serializer = new DataContractJsonSerializer(
            typeof(ModelResults),
            new DataContractJsonSerializerSettings { DateTimeFormat = new DateTimeFormat("yyyy-MM-dd'T'HH:mm:ss.fffK") });

        private readonly string _folder;

        public CheckResultStore(string folder)
        {
            _folder = folder;
        }

        public static string DefaultFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RevitAutoChecks", "Results");

        // Несохранённую модель не к чему привязать на диске — её результаты живут до закрытия Revit
        private readonly Dictionary<string, ModelResults> _unsaved = new Dictionary<string, ModelResults>(StringComparer.OrdinalIgnoreCase);

        public ModelResults Load(ModelInfo model)
        {
            if (!model.IsSaved)
            {
                if (!_unsaved.TryGetValue(model.ModelPath, out var memory))
                    _unsaved[model.ModelPath] = memory = new ModelResults { ModelPath = model.ModelPath };
                return memory;
            }

            var file = FileFor(model);
            try
            {
                if (File.Exists(file))
                {
                    using (var stream = File.OpenRead(file))
                    {
                        var results = (ModelResults)Serializer.ReadObject(stream);
                        if (results.Checks == null) results.Checks = new List<StoredCheckResult>();
                        return results;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("RevitAutoChecks: не удалось прочитать " + file + ": " + ex.Message);
            }
            return new ModelResults { ModelPath = model.ModelPath };
        }

        public StoredCheckResult Get(ModelInfo model, string checkId) =>
            Load(model).Checks.FirstOrDefault(c => c.CheckId == checkId);

        public void Save(ModelInfo model, IEnumerable<StoredCheckResult> checkResults)
        {
            var results = Load(model);
            foreach (var r in checkResults)
            {
                results.Checks.RemoveAll(c => c.CheckId == r.CheckId);
                results.Checks.Add(r);
            }
            Write(model, results);
        }

        /// <summary>Отметка о сохранении пишется только для моделей, которые уже проверялись.</summary>
        public void MarkModelSaved(ModelInfo model, DateTime at)
        {
            if (!model.IsSaved || !File.Exists(FileFor(model))) return;
            var results = Load(model);
            results.LastModelSavedAt = at;
            Write(model, results);
        }

        /// <summary>Последнее сохранение модели: отметка плагина или дата изменения файла, что позже.</summary>
        public DateTime? GetLastModelSave(ModelInfo model)
        {
            DateTime? saved = Load(model).LastModelSavedAt;
            try
            {
                if (!string.IsNullOrWhiteSpace(model.LocalFilePath) && File.Exists(model.LocalFilePath))
                {
                    var fileTime = File.GetLastWriteTime(model.LocalFilePath);
                    if (!saved.HasValue || fileTime > saved.Value) saved = fileTime;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("RevitAutoChecks: не удалось получить дату файла модели: " + ex.Message);
            }
            return saved;
        }

        private void Write(ModelInfo model, ModelResults results)
        {
            if (!model.IsSaved) return; // результаты уже в _unsaved
            var file = FileFor(model);
            try
            {
                Directory.CreateDirectory(_folder);
                results.ModelPath = model.ModelPath;
                var temp = file + ".tmp";
                using (var stream = File.Create(temp))
                using (var writer = JsonReaderWriterFactory.CreateJsonWriter(stream, Encoding.UTF8, false, true, "  "))
                {
                    Serializer.WriteObject(writer, results);
                }
                if (File.Exists(file)) File.Replace(temp, file, null);
                else File.Move(temp, file);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("RevitAutoChecks: не удалось записать " + file + ": " + ex.Message);
            }
        }

        private string FileFor(ModelInfo model)
        {
            var name = new string(model.Title.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray());
            if (name.Length > 60) name = name.Substring(0, 60);
            return Path.Combine(_folder, name + "_" + Hash(model.ModelPath.ToLowerInvariant()) + ".json");
        }

        private static string Hash(string value)
        {
            using (var sha = SHA1.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
                return string.Concat(bytes.Take(5).Select(b => b.ToString("x2")));
            }
        }
    }

    /// <summary>Правила актуальности результата.</summary>
    public static class Freshness
    {
        /// <summary>
        /// Результат устаревает, если модель была сохранена позже, чем через 2 недели после проверки.
        /// </summary>
        public static readonly TimeSpan StaleAfter = TimeSpan.FromDays(14);

        public static bool IsOutdated(DateTime runAt, DateTime? lastModelSave) =>
            lastModelSave.HasValue && lastModelSave.Value - runAt > StaleAfter;
    }
}
