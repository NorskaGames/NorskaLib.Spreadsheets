using System;

namespace NorskaLib.Spreadsheets
{
	[AttributeUsage(AttributeTargets.Field)]
    public class SpreadsheetContentAttribute : Attribute { }

	[AttributeUsage(AttributeTargets.Field)]
	public class SpreadsheetPageAttribute : Attribute
	{
		public readonly string name;

		public SpreadsheetPageAttribute(string name)
		{
			this.name = name;
		}
	}
}
