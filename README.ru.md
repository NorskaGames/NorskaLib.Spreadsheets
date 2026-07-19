# NorskaLib Spreadsheets
Выбор языка: **RUS** | ENG

Инструмент для редактора Unity, предназначенный для импорта типизированной базы данных игры (настроек персонажей, оружия, предметов и т. п.) из публичных таблиц Google.

## Совместимость
- Unity Engine 2021.3+

## Установка
Чтобы установить данный ассет через Unity Package Manager используйте адрес:
```
https://github.com/NorskaGames/NorskaLib.Spreadsheets.git
```
## Настройка таблицы

1. Создайте таблицу и разрешите анонимный просмотр (например, **Все, у кого есть ссылка** — Читатель).
2. Укажите идентификатор документа в секции **Import** инспектора контейнера.

Проект Google Cloud, API-ключ, OAuth и сервисный аккаунт не требуются. Приватные таблицы не поддерживаются.

![spreadsheet-setup](https://drive.google.com/uc?id=12Zo-_fQFYK8n9ljWMkfWtwbYhUUCP7ks)

_**Подсказка:** Проектируйте базу данных как любую другую БД (придерживайтесь хотя бы 1-ой нормальной формы: избегайте вложенных списков)._
![db-design-practices](https://drive.google.com/uc?id=1cGzRClYvEsvtzYkAlZp_nDVymvRPsjS1)

Выбранные страницы загружаются через Google Visualization data-source endpoint как структурированные DataTable. Запятые, кавычки и переносы строк внутри текста больше не нарушают структуру таблицы.

## Настройка контейнера

Создайте класс DataContainer и произвольный набор Data-классов как в примере ниже:
```
using NorskaLib.Spreadsheets;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace NorskaLibExamples.Spreadsheets
{
    [Serializable]
    public class SpreadshetContent
    {
        [SpreadsheetPage("Units")]
        public List<UnitData> Units;
        [SpreadsheetPage("Skills")]
        public List<SkillData> Skills;
        [SpreadsheetPage("UnitsSkills")]
        public List<UnitSkillData> UnitsSkills;
    }

    [CreateAssetMenu(fileName = "SpreadsheetContainer", menuName = "SpreadsheetContainer")]
    public class SpreadsheetContainer : SpreadsheetsContainerBase
    {
        [SpreadsheetContent]
        [SerializeField] SpreadshetContent content;
        public SpreadshetContent Content => content;
    }
}
```
_**Важно!** Убедитесь, что имена переменных совпадают с именами столбцов в таблице._

По умолчанию страницы имеют вертикальную ориентацию: первая строка содержит имена полей, а каждая следующая строка — один импортируемый объект. Для горизонтальной страницы, где первый столбец содержит имена полей, а каждый следующий столбец — один импортируемый объект, укажите:

```csharp
[SpreadsheetPage("Units", isVertical: false)]
public List<UnitData> Units;
```

Ориентация настраивается отдельно для каждого `[SpreadsheetPage]`, поэтому вертикальные и горизонтальные страницы могут находиться в одном объекте данных.

## Импорт таблицы
![container-inspector](https://drive.google.com/uc?id=1xT_18Z9hEKpnFv4j71CUMWJJ6zlE8ua-)

## Экспорт таблицы
Вы можете экспортировать таблицу в форматах .bin и .json, например, для использования на сервере.
![container-inspector](https://drive.google.com/uc?id=1_xgex-HyugozNPIyVrS5mEe8EI5ebZZK)
