# NorskaLib Spreadsheets
Выбор языка: **RUS** | ENG

Инструмент для редактора Unity, призванный помочь в дизайне и импорте базы данных игры (настроек персонажей, оружия, предметов и т. п.) из таблиц Google.

## Совместимость
- Unity Engine 2021.3+

## Установка
Чтобы установить данный ассет через Unity Package Manager используйте адрес:
```
https://github.com/NorskaGames/NorskaLibUPM.git?path=/Spreadsheets
```
## Настройка таблицы

Создайте таблицу и убедитесь, что она доступна по ссылке:
![spreadsheet-setup](https://drive.google.com/uc?id=12Zo-_fQFYK8n9ljWMkfWtwbYhUUCP7ks)

_**Подсказка:** Проектируйте базу данных как любую другую БД (придерживайтесь хотя бы 1-ой нормальной формы: избегайте вложенных списков)._
![db-design-practices](https://drive.google.com/uc?id=1cGzRClYvEsvtzYkAlZp_nDVymvRPsjS1)

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

## Импорт таблицы
![container-inspector](https://drive.google.com/uc?id=1xT_18Z9hEKpnFv4j71CUMWJJ6zlE8ua-)

## Экспорт таблицы
Вы можете экспортировать таблицу в форматах .bin и .json, например, для использования на сервере.
![container-inspector](https://drive.google.com/uc?id=1_xgex-HyugozNPIyVrS5mEe8EI5ebZZK)
