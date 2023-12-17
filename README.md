# NorskaLib Spreadsheets
Switch language: RUS | **ENG**

Unity Editor tool, which helps design game-logic database (such as characters, weapons, items stats etc.) using Google Spreadsheets.

## Compatibility
- Unity Engine 2021.3+

## Installation
To install this package using Unity Package Manager simply insert this address:
```
https://github.com/NorskaGames/NorskaLibUPM.git?path=/Spreadsheets
```
## Setting up the Spreadsheet

Create your Spreadsheet and make sure you enable access via link:
![spreadsheet-setup](https://drive.google.com/uc?id=12Zo-_fQFYK8n9ljWMkfWtwbYhUUCP7ks)

_**Hint:** Design your database as any relational database (at least stick to 1st normal form: avoid lists inside a cell)._
![db-design-practices](https://drive.google.com/uc?id=1cGzRClYvEsvtzYkAlZp_nDVymvRPsjS1)

## Setting up the Container

Define your custom SpreadsheetContainer and any amount of Data classes as shown below:
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
_**Important!** Make sure you spell variables names exactly as columns headers in the spreadsheet._

## Improting the Spreadsheet
![container-inspector](https://drive.google.com/uc?id=1xT_18Z9hEKpnFv4j71CUMWJJ6zlE8ua-)

## Exporting the Spreadsheet
You can serialize your spreadsheet as .bin or .json to use it somewhere else (on the dedicated server, for example).
![container-inspector](https://drive.google.com/uc?id=1_xgex-HyugozNPIyVrS5mEe8EI5ebZZK)
