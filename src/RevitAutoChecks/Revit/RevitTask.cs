using System;
using System.Collections.Concurrent;
using Autodesk.Revit.UI;

namespace RevitAutoChecks.Revit
{
    /// <summary>
    /// Выполнение действий в контексте Revit API из немодального окна (через ExternalEvent).
    /// Создавать только в контексте API, например в OnStartup.
    /// </summary>
    public sealed class RevitTask : IExternalEventHandler, IDisposable
    {
        private readonly ConcurrentQueue<Action<UIApplication>> _queue = new ConcurrentQueue<Action<UIApplication>>();
        private readonly ExternalEvent _event;

        public RevitTask()
        {
            _event = ExternalEvent.Create(this);
        }

        /// <returns>false — Revit отклонил запрос, действие не будет выполнено.</returns>
        public bool Run(Action<UIApplication> action)
        {
            _queue.Enqueue(action);
            var request = _event.Raise();
            if (request == ExternalEventRequest.Accepted || request == ExternalEventRequest.Pending) return true;

            while (_queue.TryDequeue(out _)) { }
            return false;
        }

        public void Execute(UIApplication app)
        {
            while (_queue.TryDequeue(out var action))
            {
                try
                {
                    action(app);
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("Чек-лист", "Ошибка: " + ex.Message);
                }
            }
        }

        public string GetName() => "RevitAutoChecks";

        public void Dispose() => _event.Dispose();
    }
}
