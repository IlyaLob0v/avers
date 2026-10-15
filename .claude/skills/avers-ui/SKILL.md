---
name: avers-ui
description: Оформление экранов Аверса и снимки экрана без монитора. Используй, когда делаешь или меняешь экран в Avers.Desktop и когда нужно показать пользователю, как он выглядит.
---

# Экраны Аверса: оформление и снимки

## Оформление

Вид задают два файла, экраны цветов числами не пишут:

- `src/Avers.Desktop/Styles/Palette.axaml` — цвета по ключам (`Face`, `Field`, `Edge`, `Accent`, `WarnBg`…).
- `src/Avers.Desktop/Styles/Controls.axaml` — общие элементы; список классов — в комментарии в начале файла.

Образец — прежний Аверс (Delphi под Windows XP): серый фон окна, белые поля, синее выделение, плотные таблицы.
Живые образцы всех элементов: меню «Справка → Образцы оформления» (`Views/StyleGuideWindow.axaml`).

Правила:

1. Группа полей — `HeaderedContentControl.group Header="…"` или `Border.card` с `TextBlock.legend`.
2. Над экраном — `Border.toolbar` (кнопки) и `Border.filterbar` (период, дом, статус). Кнопки окна — `Border.actionbar`.
3. Цветная пометка строки таблицы — `ctl:GridTone.Enabled="True"` на `DataGrid` и `IRowTone` у строки
   (`Controls/GridTone.cs`): жёлтый `Warn` — внимание/давно не платил, красный `Debt`, зелёный `Ok`,
   голубой `Info`, серый `Off`. Плашка — `Border.warn` и т. п., метка в строке — `Border.badge warn`.
4. Переход «провалиться» (договор, квартира, дом) — `Button.link`.
5. Новый модуль — строка в `NavCatalog` (`ViewModels/NavItem.cs`): он сам появится в панели, меню и на рабочем столе
   для ролей, которым разрешён в `RoleCatalog`.

## Снимки экрана

```bash
dotnet run --project tools/Avers.Screenshots -- <папка> [логин] [модуль ...]
# пример: dotnet run --project tools/Avers.Screenshots -- /tmp/shots sokolova Desktop Disp Ticket
```

Логины учебной базы — в `DemoSeeder` (пароль у всех `avers`), модули — `AppModule`. Без модулей снимаются
основные экраны. Всегда снимается вход (`00-vhod.png`) и образцы оформления (`99-obraztsy.png`).
Смотри снимки сам (Read по PNG) до того, как показывать пользователю: шрифт в облаке — Inter, на ПК — Tahoma.
