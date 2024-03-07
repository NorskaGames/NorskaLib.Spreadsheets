using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

namespace NorskaLib.Spreadsheets
{
    internal enum SupportedContentFieldTypes
    {
        UNSUPPORTED,

        Object,
        List,
        Array,
    }

    public class SpreadsheetImporter
    {
        public const string URLFormat = @"https://docs.google.com/spreadsheets/d/{0}/gviz/tq?tqx=out:csv&sheet={1}";
        private readonly CultureInfo dotCulture = new CultureInfo("en-US");
        private readonly CultureInfo commaCulture = new CultureInfo("fr-FR");

        private readonly object targetObject;
        private readonly FieldInfo[] targetListsFields;
        private readonly string documentID;

        public event Action onComplete;

        private string output;
        public string Output
        {
            get => output;

            private set
            {
                output = value;
                onOutputChanged.Invoke();
            }
        }
        public event Action onOutputChanged;

        public float progress;
        public float Progress
        {
            get => progress;

            private set
            {
                progress = Mathf.Clamp01(value);
                onProgressChanged.Invoke();
            }
        }
        public event Action onProgressChanged;

        public bool operationFailed;
        public event Action<string> onOperationFailed;

        private float ProgressElementDelta
            => 1f / targetListsFields.Length;

        public SpreadsheetImporter(object targetObject, FieldInfo[] targetListsFields, string documentID)
        {
            this.targetObject = targetObject;
            this.targetListsFields = targetListsFields;
            this.documentID = documentID;
        }

        public async void Run()
        {
            operationFailed = false;
            var webClient = new WebClient();

            for (int i = 0; i < targetListsFields.Length && !operationFailed; i++)
                await PopulateList(targetObject, targetListsFields[i], webClient);

            webClient.Dispose();

            onComplete?.Invoke();
        }

        private async Task PopulateList(object contentObject, FieldInfo targetContentField, WebClient webClient)
        {
            var contentType = default(Type);
            var contentTypeFlag = default(SupportedContentFieldTypes);
            if (targetContentField.FieldType.IsArray)
            {
                contentType = targetContentField.FieldType.GetElementType();
                contentTypeFlag = SupportedContentFieldTypes.Array;
            }
            else if (targetContentField.FieldType.IsGenericType && targetContentField.FieldType.GetGenericTypeDefinition() == typeof(List<>))
            {
                contentTypeFlag = SupportedContentFieldTypes.List;
                contentType = targetContentField.FieldType.GetGenericArguments().SingleOrDefault();
            }
            else if (targetContentField.FieldType.IsAbstract
                || targetContentField.FieldType.IsEnum
                || !targetContentField.FieldType.IsSerializable)
            {
                var message = $"Could not identify type of defs stored in {targetContentField.Name}";
                onOperationFailed?.Invoke(message);
                throw new Exception(message);
            }
            else
            {
                contentType = targetContentField.FieldType;
                contentTypeFlag = SupportedContentFieldTypes.Object;
            }

            #region Downloading page

            var pageAttribute = (SpreadsheetPageAttribute)Attribute.GetCustomAttribute(targetContentField, typeof(SpreadsheetPageAttribute));
            var pageName = pageAttribute.name;

            Output = $"Downloading page '{pageName}'...";

            var url = string.Format(URLFormat, documentID, pageName);
            var request = default(Task<string>);

            try
            {
                request = webClient.DownloadStringTaskAsync(url);
            }
            catch (WebException)
            {
                var message = $"Bad URL '{url}'";
                operationFailed = true;
                onOperationFailed?.Invoke(message);
                throw new Exception(message);
            }

            while (!request.IsCompleted)
                await Task.Delay(100);

            if (request.IsFaulted)
            {
                var message = $"Bad URL '{url}'";
                operationFailed = true;
                onOperationFailed?.Invoke(message);
                throw new Exception(message);
            }

            var rawTable = Regex.Split(request.Result, "\r\n|\r|\n");
            request.Dispose();

            Progress += 1 / 3f * ProgressElementDelta;

            #endregion

            #region Analyzing and splitting raw text

            Output = $"Analysing headers...";

            var headersRaw = Split(rawTable[0]);

            var idHeaderIdx = -1;
            var headers = new List<string>();
            var emptyHeadersIdxs = new List<int>();
            for (int i = 0; i < headersRaw.Length; i++)
            {
                if (string.IsNullOrEmpty(headersRaw[i]))
                {
                    emptyHeadersIdxs.Add(i);
                    continue;
                }

                if (idHeaderIdx == -1 && headersRaw[i].ToLower() == "id")
                    idHeaderIdx = i;

                headers.Add(headersRaw[i]);
            }

            var rows = new List<string[]>();
            for (int i = 1; i < rawTable.Length; i++)
            {
                var substrings = Split(rawTable[i]);
                if (idHeaderIdx != -1 && string.IsNullOrEmpty(substrings[idHeaderIdx]))
                    continue;

                rows.Add(substrings.Where((val, index) => !emptyHeadersIdxs.Contains(index)).ToArray());
            }

            Progress += 1 / 3f * ProgressElementDelta;

            #endregion

            #region Parsing and populating list of defs 

            Output = $"Populating list of defs '{targetContentField.Name}'<{contentType.Name}>...";

            var headersToFields = new Dictionary<string, FieldInfo>();
            foreach (var header in headers)
            {
                // TO DO:
                // Add support of fields with names other than the header names via an attribute
                var bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var fieldInfo = contentType.GetField(header, bindingFlags);
                if (fieldInfo is null)
                {
                    Debug.LogWarning($"Header '{header}' match no field in {contentType.Name} type");
                    continue;
                }
                headersToFields.Add(header, fieldInfo);
            }

            switch (contentTypeFlag)
            {
                case SupportedContentFieldTypes.Object:
                    {
                        var row = rows[0];
                        var obj = Activator.CreateInstance(contentType);
                        for (int i = 0; i < headers.Count; i++)
                            if (headersToFields.TryGetValue(headers[i], out var field))
                                field.SetValue(obj, Parse(row[i], field.FieldType));
                        targetContentField.SetValue(contentObject, obj);
                        break; 
                    }

                case SupportedContentFieldTypes.List:
                    {
                        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(contentType));
                        foreach (var row in rows)
                        {
                            var contentItem = Activator.CreateInstance(contentType);

                            for (int h = 0; h < headers.Count; h++)
                                if (headersToFields.TryGetValue(headers[h], out var field))
                                    field.SetValue(contentItem, Parse(row[h], field.FieldType));

                            list.Add(contentItem);
                        }
                        targetContentField.SetValue(contentObject, list);
                        break; 
                    }

                case SupportedContentFieldTypes.Array:
                    {
                        var array = (Array)Activator.CreateInstance(contentType.MakeArrayType(), rows.Count);
                        for (int i = 0; i < array.Length; i++)
                        {
                            var row = rows[i];
                            var contentItem = Activator.CreateInstance(contentType);
                            for (int h = 0; h < headers.Count; h++)
                                if (headersToFields.TryGetValue(headers[h], out var field))
                                    field.SetValue(contentItem, Parse(row[h], field.FieldType));
                            array.SetValue(contentItem, i);
                        }
                        targetContentField.SetValue(contentObject, array);
                        break; 
                    }
            }
            Progress += 1 / 3f * ProgressElementDelta;

            #endregion
        }

        public string[] Split(string line)
        {
            bool isInsideQuotes = false;
            List<string> result = new List<string>();

            string temp = string.Empty;
            for (int i = 0; i < line.Length; i++)
                if (line[i] == '"')
                {
                    isInsideQuotes = !isInsideQuotes;

                    if (i == line.Length - 1)
                        result.Add(temp);
                }
                else
                {
                    if (!isInsideQuotes && line[i] == ',')
                    {
                        result.Add(temp);
                        temp = string.Empty;
                    }
                    else
                        temp += line[i];
                }

            return result.ToArray();
        }

        public static object Parse(string s, Type type)
        {
            var result = default(object);

            if (type == typeof(string))
                return s;
            else if (type == typeof(int))
            {
                if (int.TryParse(s, out var resultInt))
                    return resultInt;
            }
            else if (type == typeof(byte))
            {
                if (byte.TryParse(s, out var resultInt))
                    return resultInt;
            }
            else if (type == typeof(short))
            {
                if (short.TryParse(s, out var resultInt))
                    return resultInt;
            }
            else if (type == typeof(long))
            {
                if (long.TryParse(s, out var resultInt))
                    return resultInt;
            }
            else if(type == typeof(bool))
            {
                if (bool.TryParse(s, out var resultBool))
                    return resultBool;
            }
            else if (type == typeof(float))
            {
                if (float.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var resultFloat))
                    return resultFloat;
            }
            else if (type == typeof(double))
            {
                if (double.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var resultFloat))
                    return resultFloat;
            }
            else if (type.IsEnum)
            {
                try
                {
                    result = Enum.Parse(type, s, true);
                }
                catch (ArgumentException)
                {
                    result = default(object);
                }
                return result;
            }

            return result;
        }
    }
}
