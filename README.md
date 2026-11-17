# RevitAutoChecks

Плагин для Revit 2022–2027 с чек-листом автоматических проверок модели. Кнопка находится на вкладке «roshior-a», панель «Утилиты».

## Сборка

Для каждой версии Revit своя конфигурация: `Debug R22` … `Release R27`.

| Revit     | .NET            |
|-----------|-----------------|
| 2022–2024 | .NET Framework 4.8 |
| 2025–2026 | .NET 8          |
| 2027      | .NET 10         |

```bash
dotnet build src/RevitAutoChecks/RevitAutoChecks.csproj -c "Release R27"
```

- **Release** собирает готовую папку `dist/Revit<год>/`: в ней `RevitAutoChecks.addin` и папка `RevitAutoChecks/`. Её содержимое копируется в `%AppData%\Autodesk\Revit\Addins\<год>\`.
- **Debug** после сборки сам устанавливает плагин в `%AppData%\Autodesk\Revit\Addins\<год>\`. Revit при этом должен быть закрыт.

## Результаты проверок

Результаты хранятся в `%AppData%\RevitAutoChecks\Results\`, по одному JSON-файлу на модель. Для моделей с совместной работой файл привязан к пути хранилища.

Результат считается устаревшим, если модель сохранили позже чем через 14 дней после проверки.

## Добавление проверки

1. Реализовать `ICheck` в `src/RevitAutoChecks/Checks/`.
2. Добавить экземпляр в `CheckRegistry.All`.
