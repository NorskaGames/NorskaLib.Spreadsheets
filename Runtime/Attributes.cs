using System;

namespace NorskaLib.Spreadsheets
{
    [AttributeUsage(AttributeTargets.Field)]
    public class SpreadsheetContentAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field)]
    public class SpreadsheetPageAttribute : Attribute
    {
        public readonly string name;
        public readonly bool isVertical;

        public SpreadsheetPageAttribute(string name) : this(name, true) { }

        public SpreadsheetPageAttribute(string name, bool isVertical)
        {
            this.name = name;
            this.isVertical = isVertical;
        }
    }
}
