using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace NorskaLib.Spreadsheets
{
    internal enum SupportedContentFieldTypes
    {
        Unsupported,
        Object,
        List,
        Array,
    }

    [DataContract]
    internal sealed class VisualizationQueryResponse
    {
        [DataMember(Name = "status")]
        public string Status { get; set; }

        [DataMember(Name = "table")]
        public VisualizationTable Table { get; set; }

        [DataMember(Name = "errors")]
        public VisualizationQueryError[] Errors { get; set; }
    }

    [DataContract]
    internal sealed class VisualizationTable
    {
        [DataMember(Name = "cols")]
        public VisualizationColumn[] Columns { get; set; }

        [DataMember(Name = "rows")]
        public VisualizationRow[] Rows { get; set; }
    }

    [DataContract]
    internal sealed class VisualizationColumn
    {
        [DataMember(Name = "label")]
        public string Label { get; set; }
    }

    [DataContract]
    internal sealed class VisualizationRow
    {
        [DataMember(Name = "c")]
        public VisualizationCell[] Cells { get; set; }
    }

    [DataContract]
    internal sealed class VisualizationCell
    {
        [DataMember(Name = "v")]
        public object Value { get; set; }

        [DataMember(Name = "f")]
        public string FormattedValue { get; set; }
    }

    [DataContract]
    internal sealed class VisualizationQueryError
    {
        [DataMember(Name = "reason")]
        public string Reason { get; set; }

        [DataMember(Name = "message")]
        public string Message { get; set; }

        [DataMember(Name = "detailed_message")]
        public string DetailedMessage { get; set; }
    }

    public class SpreadsheetImporter
    {
        public const string DataSourceEndpointFormat = "https://docs.google.com/spreadsheets/d/{0}/gviz/tq";

        private const string ResponseHandler = "NorskaLibSpreadsheetCallback";

        private static readonly HttpClient HttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30),
        };

        private readonly object targetObject;
        private readonly FieldInfo[] targetContentFields;
        private readonly string documentId;

        public event Action onComplete;

        private string output;
        public string Output
        {
            get => output;

            private set
            {
                output = value;
                onOutputChanged?.Invoke();
            }
        }
        public event Action onOutputChanged;

        private float progress;
        public float Progress
        {
            get => progress;

            private set
            {
                progress = Mathf.Clamp01(value);
                onProgressChanged?.Invoke();
            }
        }
        public event Action onProgressChanged;

        public bool operationFailed;
        public event Action<string> onOperationFailed;

        public SpreadsheetImporter(object targetObject, FieldInfo[] targetContentFields, string documentId)
        {
            this.targetObject = targetObject ?? throw new ArgumentNullException(nameof(targetObject));
            this.targetContentFields = targetContentFields ?? throw new ArgumentNullException(nameof(targetContentFields));
            this.documentId = documentId;
        }

        public async Task Run()
        {
            operationFailed = false;
            Progress = 0f;

            try
            {
                ValidateRequest();

                var tables = new string[targetContentFields.Length][][];
                var downloadProgressStep = 0.5f / targetContentFields.Length;
                for (var i = 0; i < targetContentFields.Length; i++)
                {
                    var pageAttribute = GetPageAttribute(targetContentFields[i]);
                    var pageName = pageAttribute.name;
                    Output = $"Downloading spreadsheet page '{pageName}'...";
                    tables[i] = await DownloadTable(pageName, pageAttribute.isVertical);
                    Progress += downloadProgressStep;
                }

                var populateProgressStep = 0.5f / targetContentFields.Length;

                for (var i = 0; i < targetContentFields.Length; i++)
                {
                    var targetContentField = targetContentFields[i];
                    var pageName = GetPageName(targetContentField);
                    Output = $"Populating '{pageName}'...";

                    PopulateField(targetContentField, tables[i]);
                    Progress += populateProgressStep;
                }

                Progress = 1f;
                Output = "Spreadsheet import completed.";
                onComplete?.Invoke();
            }
            catch (Exception exception)
            {
                operationFailed = true;
                var message = $"Spreadsheet import failed: {exception.Message}";
                Output = message;
                onOperationFailed?.Invoke(message);
                Debug.LogException(exception);
            }
        }

        private void ValidateRequest()
        {
            if (targetContentFields.Length == 0)
                throw new InvalidOperationException("No spreadsheet pages were selected for import.");

            if (string.IsNullOrWhiteSpace(documentId))
                throw new InvalidOperationException("Spreadsheet document ID is not specified.");

            foreach (var targetContentField in targetContentFields)
            {
                if (Attribute.GetCustomAttribute(targetContentField, typeof(SpreadsheetPageAttribute)) == null)
                {
                    throw new InvalidOperationException(
                        $"Field '{targetContentField.Name}' is missing SpreadsheetPageAttribute.");
                }
            }
        }

        private async Task<string[][]> DownloadTable(string pageName, bool isVertical)
        {
            if (isVertical)
                return await DownloadTableRange(pageName, 1);

            return await DownloadHorizontalTable(pageName);
        }

        private async Task<string[][]> DownloadHorizontalTable(string pageName)
        {
            // Visualization DataTables enforce one type per column. Reading one source row at a time
            // prevents mixed horizontal field types from being returned as null before transposition.
            var headerColumn = await DownloadTableRange(pageName, 0, "A:A");
            var sourceRows = new string[headerColumn.Length][];

            for (var rowIndex = 0; rowIndex < sourceRows.Length; rowIndex++)
            {
                Output = $"Downloading horizontal spreadsheet page '{pageName}' " +
                         $"row {rowIndex + 1}/{sourceRows.Length}...";

                var rowNumber = rowIndex + 1;
                var rowTable = await DownloadTableRange(pageName, 0, $"{rowNumber}:{rowNumber}");
                sourceRows[rowIndex] = rowTable.FirstOrDefault() ?? Array.Empty<string>();
            }

            return Transpose(sourceRows);
        }

        private async Task<string[][]> DownloadTableRange(
            string pageName,
            int headerRows,
            string range = null)
        {
            using var response = await HttpClient.GetAsync(BuildRequestUri(pageName, headerRows, range));
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Spreadsheet page '{pageName}' returned {(int)response.StatusCode} " +
                    $"({response.StatusCode}): {response.ReasonPhrase}");
            }

            var responseJson = ExtractJsonPayload(responseBody);
            var result = Deserialize<VisualizationQueryResponse>(responseJson);
            var querySucceeded = result != null &&
                                 (string.Equals(result.Status, "ok", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(result.Status, "warning", StringComparison.OrdinalIgnoreCase));
            if (!querySucceeded)
            {
                var error = result?.Errors?.FirstOrDefault();
                var details = error?.DetailedMessage ?? error?.Message ?? error?.Reason ?? "Unknown query error.";
                throw new InvalidDataException($"Could not read spreadsheet page '{pageName}': {details}");
            }

            if (result.Table == null)
                throw new InvalidDataException($"Spreadsheet page '{pageName}' returned no table.");

            return ConvertTable(result.Table, headerRows > 0);
        }

        private Uri BuildRequestUri(string pageName, int headerRows, string range)
        {
            var endpoint = string.Format(DataSourceEndpointFormat, Uri.EscapeDataString(documentId.Trim()));
            var responseOptions = $"out:json;responseHandler:{ResponseHandler}";
            var query = $"sheet={Uri.EscapeDataString(pageName)}" +
                        $"&headers={headerRows}" +
                        $"&tqx={Uri.EscapeDataString(responseOptions)}";

            if (!string.IsNullOrWhiteSpace(range))
                query += $"&range={Uri.EscapeDataString(range)}";

            return new Uri($"{endpoint}?{query}");
        }

        private static string ExtractJsonPayload(string responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
                throw new InvalidDataException("Spreadsheet data source returned an empty response.");

            var callbackPrefix = ResponseHandler + "(";
            var payloadStart = responseBody.IndexOf(callbackPrefix, StringComparison.Ordinal);
            var payloadEnd = responseBody.LastIndexOf(");", StringComparison.Ordinal);

            if (payloadStart < 0 || payloadEnd < 0 || payloadEnd <= payloadStart)
                throw new InvalidDataException("Spreadsheet data source returned an unexpected response format.");

            payloadStart += callbackPrefix.Length;
            return responseBody.Substring(payloadStart, payloadEnd - payloadStart);
        }

        private static string[][] ConvertTable(VisualizationTable table, bool includeColumnHeaders)
        {
            var columns = table.Columns ?? Array.Empty<VisualizationColumn>();
            var rows = table.Rows ?? Array.Empty<VisualizationRow>();
            var headerRowCount = includeColumnHeaders ? 1 : 0;
            var result = new string[rows.Length + headerRowCount][];

            if (includeColumnHeaders)
            {
                result[0] = columns
                    .Select(column => column?.Label ?? string.Empty)
                    .ToArray();
            }

            for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
            {
                var sourceCells = rows[rowIndex]?.Cells ?? Array.Empty<VisualizationCell>();
                var targetCells = new string[columns.Length];

                for (var columnIndex = 0; columnIndex < targetCells.Length; columnIndex++)
                {
                    var cell = columnIndex < sourceCells.Length ? sourceCells[columnIndex] : null;
                    targetCells[columnIndex] = GetCellText(cell);
                }

                result[rowIndex + headerRowCount] = targetCells;
            }

            return result;
        }

        private static string[][] Transpose(string[][] table)
        {
            if (table.Length == 0)
                return Array.Empty<string[]>();

            var columnCount = table.Max(row => row?.Length ?? 0);
            var result = new string[columnCount][];

            for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
            {
                var transposedRow = new string[table.Length];
                for (var rowIndex = 0; rowIndex < table.Length; rowIndex++)
                    transposedRow[rowIndex] = GetCell(table[rowIndex], columnIndex);

                result[columnIndex] = transposedRow;
            }

            return result;
        }

        private static string GetCellText(VisualizationCell cell)
        {
            if (cell?.Value != null)
                return Convert.ToString(cell.Value, CultureInfo.InvariantCulture) ?? string.Empty;

            return cell?.FormattedValue ?? string.Empty;
        }

        private void PopulateField(FieldInfo targetContentField, string[][] table)
        {
            ResolveContentType(targetContentField, out var contentType, out var contentFieldType);
            Debug.Log($"[SpreadsheetImporter]Populating field '{targetContentField.Name}' of type {contentType.Name} " +
                      $"as {contentFieldType} from spreadsheet page '{GetPageName(targetContentField)}'.");

            if (table.Length == 0)
                throw new InvalidDataException($"Spreadsheet page '{GetPageName(targetContentField)}' is empty.");

            var headers = table[0] ?? Array.Empty<string>();
            if (headers.Length == 0)
                throw new InvalidDataException($"Spreadsheet page '{GetPageName(targetContentField)}' has no headers.");

            var bindings = BuildColumnBindings(contentType, headers);
            var idColumnIndex = Array.FindIndex(
                headers,
                header => string.Equals(header, "id", StringComparison.OrdinalIgnoreCase));

            var rows = table
                .Skip(1)
                .Where(row => ShouldImportRow(row, idColumnIndex))
                .ToArray();

            ValidateOrientation(contentType, targetContentField, bindings, rows);

            switch (contentFieldType)
            {
                case SupportedContentFieldTypes.Object:
                {
                    if (rows.Length == 0)
                    {
                        throw new InvalidDataException(
                            $"Spreadsheet page '{GetPageName(targetContentField)}' contains no data rows.");
                    }

                    var contentItem = CreateContentItem(contentType, bindings, rows[0]);
                    targetContentField.SetValue(targetObject, contentItem);
                    break;
                }

                case SupportedContentFieldTypes.List:
                {
                    var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(contentType));
                    foreach (var row in rows)
                        list.Add(CreateContentItem(contentType, bindings, row));

                    targetContentField.SetValue(targetObject, list);
                    break;
                }

                case SupportedContentFieldTypes.Array:
                {
                    var array = Array.CreateInstance(contentType, rows.Length);
                    for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
                        array.SetValue(CreateContentItem(contentType, bindings, rows[rowIndex]), rowIndex);

                    targetContentField.SetValue(targetObject, array);
                    break;
                }

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static IReadOnlyList<ColumnBinding> BuildColumnBindings(Type contentType, string[] headers)
        {
            var result = new List<ColumnBinding>(headers.Length);
            var seenHeaders = new HashSet<string>(StringComparer.Ordinal);
            var bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            for (var columnIndex = 0; columnIndex < headers.Length; columnIndex++)
            {
                var header = headers[columnIndex];
                if (string.IsNullOrWhiteSpace(header))
                    continue;

                if (!seenHeaders.Add(header))
                {
                    Debug.LogWarning($"Duplicate spreadsheet header '{header}' was ignored.");
                    continue;
                }

                var fieldInfo = contentType.GetField(header, bindingFlags);
                if (fieldInfo == null)
                {
                    Debug.LogWarning($"Header '{header}' matches no field in {contentType.Name}.");
                    continue;
                }

                result.Add(new ColumnBinding(columnIndex, fieldInfo));
            }

            return result;
        }

        private static object CreateContentItem(Type contentType, IReadOnlyList<ColumnBinding> bindings, string[] row)
        {
            var contentItem = Activator.CreateInstance(contentType);

            foreach (var binding in bindings)
            {
                var cellValue = GetCell(row, binding.ColumnIndex);
                binding.Field.SetValue(contentItem, Parse(cellValue, binding.Field.FieldType));
            }

            return contentItem;
        }

        private static bool ShouldImportRow(string[] row, int idColumnIndex)
        {
            if (row == null || row.Length == 0)
                return false;

            if (idColumnIndex >= 0)
                return !string.IsNullOrWhiteSpace(GetCell(row, idColumnIndex));

            return row.Any(cell => !string.IsNullOrWhiteSpace(cell));
        }

        private static void ValidateOrientation(
            Type contentType,
            FieldInfo targetContentField,
            IReadOnlyCollection<ColumnBinding> bindings,
            string[][] rows)
        {
            if (bindings.Count > 1 || rows.Length < 2)
                return;

            var bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var fieldNames = new HashSet<string>(
                contentType.GetFields(bindingFlags).Select(field => field.Name),
                StringComparer.Ordinal);
            var matchingFieldNames = rows
                .Select(row => GetCell(row, 0))
                .Where(fieldNames.Contains)
                .Distinct(StringComparer.Ordinal)
                .Take(2)
                .Count();

            if (matchingFieldNames < 2)
                return;

            var pageAttribute = GetPageAttribute(targetContentField);
            var detectedHeaderLocation = pageAttribute.isVertical ? "first column" : "first row";
            var suggestedOrientation = !pageAttribute.isVertical;
            throw new InvalidDataException(
                $"Spreadsheet page '{pageAttribute.name}' appears to use its {detectedHeaderLocation} as headers, " +
                $"but its SpreadsheetPageAttribute has isVertical: {pageAttribute.isVertical.ToString().ToLowerInvariant()}. " +
                $"Set isVertical: {suggestedOrientation.ToString().ToLowerInvariant()}.");
        }

        private static string GetCell(string[] row, int columnIndex)
        {
            if (row == null || columnIndex < 0 || columnIndex >= row.Length)
                return string.Empty;

            return row[columnIndex] ?? string.Empty;
        }

        private static void ResolveContentType(
            FieldInfo targetContentField,
            out Type contentType,
            out SupportedContentFieldTypes contentFieldType)
        {
            if (targetContentField.FieldType.IsArray)
            {
                contentType = targetContentField.FieldType.GetElementType();
                contentFieldType = SupportedContentFieldTypes.Array;
                return;
            }

            if (targetContentField.FieldType.IsGenericType &&
                targetContentField.FieldType.GetGenericTypeDefinition() == typeof(List<>))
            {
                contentType = targetContentField.FieldType.GetGenericArguments().Single();
                contentFieldType = SupportedContentFieldTypes.List;
                return;
            }

            if (targetContentField.FieldType.IsAbstract ||
                targetContentField.FieldType.IsEnum ||
                !targetContentField.FieldType.IsSerializable)
            {
                throw new InvalidOperationException(
                    $"Could not identify the data type stored in '{targetContentField.Name}'.");
            }

            contentType = targetContentField.FieldType;
            contentFieldType = SupportedContentFieldTypes.Object;
        }

        private static string GetPageName(FieldInfo targetContentField)
        {
            return GetPageAttribute(targetContentField)?.name;
        }

        private static SpreadsheetPageAttribute GetPageAttribute(FieldInfo targetContentField)
        {
            return (SpreadsheetPageAttribute)Attribute.GetCustomAttribute(
                targetContentField,
                typeof(SpreadsheetPageAttribute));
        }

        private static T Deserialize<T>(string json) where T : class
        {
            var serializer = new DataContractJsonSerializer(typeof(T));
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            return serializer.ReadObject(stream) as T;
        }

        public static object Parse(string value, Type type)
        {
            Debug.Log($"[SpreadsheetImporter]Parsing value '{value}' to type {type.Name}.");

            if (type == typeof(string))
                return value;

            if (type == typeof(int) &&
                TryParseIntegral(value, int.MinValue, int.MaxValue, out var intValue))
            {
                return decimal.ToInt32(intValue);
            }

            if (type == typeof(byte) &&
                TryParseIntegral(value, byte.MinValue, byte.MaxValue, out var byteValue))
            {
                return decimal.ToByte(byteValue);
            }

            if (type == typeof(short) &&
                TryParseIntegral(value, short.MinValue, short.MaxValue, out var shortValue))
            {
                return decimal.ToInt16(shortValue);
            }

            if (type == typeof(long) &&
                TryParseIntegral(value, long.MinValue, long.MaxValue, out var longValue))
            {
                return decimal.ToInt64(longValue);
            }

            if (type == typeof(bool) && bool.TryParse(value, out var boolValue))
                return boolValue;

            if (type == typeof(float) &&
                float.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var floatValue))
            {
                return floatValue;
            }

            if (type == typeof(double) &&
                double.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue))
            {
                return doubleValue;
            }

            if (type.IsEnum && Enum.TryParse(type, value, true, out var enumValue))
                return enumValue;

            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }

        private static bool TryParseIntegral(
            string value,
            decimal minValue,
            decimal maxValue,
            out decimal result)
        {
            var normalizedValue = value?.Replace(',', '.') ?? string.Empty;
            return decimal.TryParse(
                       normalizedValue,
                       NumberStyles.Float,
                       CultureInfo.InvariantCulture,
                       out result) &&
                   result == decimal.Truncate(result) &&
                   result >= minValue &&
                   result <= maxValue;
        }

        private readonly struct ColumnBinding
        {
            public readonly int ColumnIndex;
            public readonly FieldInfo Field;

            public ColumnBinding(int columnIndex, FieldInfo field)
            {
                ColumnIndex = columnIndex;
                Field = field;
            }
        }
    }
}
