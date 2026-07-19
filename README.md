# NorskaLib Spreadsheets
Switch language: [RUS](https://github.com/NorskaGames/NorskaLib.Spreadsheets/blob/master/README.ru.md) | **ENG**

Unity Editor tool for importing a typed game-logic database (such as characters, weapons, and item stats) from public Google Sheets tables.

## Compatibility
- Unity Engine 2021.3+

## Installation
To install this package using Unity Package Manager simply insert this address:
```
https://github.com/NorskaGames/NorskaLib.Spreadsheets.git
```
**Important:** You need to have Git installed in your OS. You can get one from here:
```
https://git-scm.com/
```
## Setting up the Spreadsheet

1. Create your spreadsheet and make it available to anonymous viewers—for example, **Anyone with the link** as Viewer.
2. Enter the spreadsheet document ID in the container's **Import** inspector.

No Google Cloud project, API key, OAuth flow, or service account is required. Private spreadsheets are not supported.

![spreadsheet-setup](https://drive.google.com/uc?id=12Zo-_fQFYK8n9ljWMkfWtwbYhUUCP7ks)

_**Hint:** Design your database as any relational database (at least stick to 1st normal form: avoid lists inside a cell)._
![db-design-practices](https://drive.google.com/uc?id=1cGzRClYvEsvtzYkAlZp_nDVymvRPsjS1)

Selected pages are downloaded through the Google Visualization data-source endpoint as structured DataTables. Cell text may contain commas, quotation marks, and line breaks without corrupting the table shape.

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

By default, pages are vertical: the first row contains field names and each following row is one imported item. To use a horizontal page instead, where the first column contains field names and each following column is one imported item, mark that page with:

```csharp
[SpreadsheetPage("Units", isVertical: false)]
public List<UnitData> Units;
```

Orientation is configured independently for each `[SpreadsheetPage]`, so vertical and horizontal pages can coexist in one content object.

## Importing the Spreadsheet
![container-inspector](https://drive.google.com/uc?id=1xT_18Z9hEKpnFv4j71CUMWJJ6zlE8ua-)

## Exporting the Spreadsheet
You can serialize your spreadsheet as .bin or .json to use it somewhere else (on the dedicated server, for example).
![container-inspector](https://drive.google.com/uc?id=1_xgex-HyugozNPIyVrS5mEe8EI5ebZZK)
