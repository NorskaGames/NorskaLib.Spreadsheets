using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace NorskaLib.Spreadsheets
{
    [CustomEditor(typeof(SpreadsheetsContainerBase), true)]
    public class SpreadsheetContainerEditor : Editor
    {
        private const string RelativePathNotation = "..";

        private SpreadsheetsContainerBase container;
        private object content;
        private SpreadsheetImporter importer;
        private string[] possibleTogglesIds;

        public override void OnInspectorGUI()
        {
            container = (SpreadsheetsContainerBase)target;
            var contentFieldBinding = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
            var contentField = container.GetType().GetFields(contentFieldBinding)
                .Where(fi => Attribute.IsDefined(fi, typeof(SpreadsheetContentAttribute)))
                .FirstOrDefault();
            if (contentField == null)
            {
                EditorGUILayout.HelpBox("Error: Missing field marked with [SpreadsheetContent] attribute!", MessageType.Error);
                base.OnInspectorGUI();
                return;
            }
            content = contentField.GetValue(container);

            container.foldoutImportGUI = EditorGUILayout.BeginFoldoutHeaderGroup(container.foldoutImportGUI, "Import");
            if (container.foldoutImportGUI)
                DrawImportGUI();
            EditorGUILayout.EndFoldoutHeaderGroup();

            container.foldoutSerializationGUI = EditorGUILayout.BeginFoldoutHeaderGroup(container.foldoutSerializationGUI, "Serialization");
            if (container.foldoutSerializationGUI)
                DrawSerializationGUI();
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(16);

            base.OnInspectorGUI();

            if (GUI.changed)
            {
                EditorUtility.SetDirty(target);
            }
        }

        void DrawImportGUI()
        {
            var listsFieldBinding = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
            var contentFields = content.GetType().GetFields(listsFieldBinding)
                .Where(fi => Attribute.IsDefined(fi, typeof(SpreadsheetPageAttribute)))
                .OrderBy(fi => fi.Name)
                .ToArray();

            possibleTogglesIds ??= contentFields
                    .Select(fi => fi.Name)
                    .ToArray();

            var anySelected = container.selectedTogglesIds.Any();
            var allSelected = !possibleTogglesIds.Any(toggleId => !container.selectedTogglesIds.Contains(toggleId));

            #region Document Id field

            container.documentId = EditorGUILayout.TextField(
                new GUIContent(
                    "Document Id",
                    "The XXXX part in 'https://docs.google.com/spreadsheets/d/XXXX/edit' URL.\n\nNOTE: The document must be accessable by link."),
                container.documentId);

            #endregion

            #region Control buttons

            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginDisabledGroup(allSelected);
            if (GUILayout.Button("All"))
                SelectAll(true);
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(!anySelected);
            if (GUILayout.Button("None"))
                SelectAll(false);

            if (GUILayout.Button("Import"))
            {
                var selectedContentFields = contentFields.Where(fi => container.selectedTogglesIds.Contains(fi.Name)).ToArray();
                OnClickImport(selectedContentFields);
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();

            #endregion

            #region Pages toggles

            EditorGUILayout.LabelField("Pages to import:");

            EditorGUI.indentLevel += 1;

            foreach (var toggleId in possibleTogglesIds)
            {
                var crntValue = container.selectedTogglesIds.Contains(toggleId);
                var newValue = EditorGUILayout.Toggle($"{toggleId}", crntValue);
                if (newValue != crntValue)
                    SelectToggle(toggleId, newValue);
            }

            EditorGUI.indentLevel -= 1;

            #endregion
        }

        void DrawSerializationGUI()
        {
            #region Settings fields

            container.serializationOutputPath = EditorGUILayout.TextField(
                new GUIContent(
                    "Output path",
                    "HINT: use '../' notation to specify path, relative to your project 'Assets' directory."),
                container.serializationOutputPath);

            container.serializationFileName = EditorGUILayout.TextField(
                "File Name",
                container.serializationFileName);

            container.serializationFormat = (SpreadsheetSerializationFormat)EditorGUILayout.EnumPopup("Format", container.serializationFormat);

            #endregion

            #region Control buttons
            EditorGUILayout.BeginHorizontal();

            var disableSerializeButton = string.IsNullOrWhiteSpace(container.serializationOutputPath) || string.IsNullOrWhiteSpace(container.serializationFileName);
            EditorGUI.BeginDisabledGroup(disableSerializeButton);
            if (GUILayout.Button("Serialize"))
            {
                var serializer = default(SpreadsheetSerializer);
                var outputPath = container.serializationOutputPath.StartsWith(RelativePathNotation)
                    ? Path.GetFullPath(Path.Combine(Application.dataPath, container.serializationOutputPath))
                    : container.serializationOutputPath;
                if (!Directory.Exists(outputPath))
                    throw new Exception($"Missing directory '{outputPath}'!");

                switch (container.serializationFormat)
                {
                    default:
                    case SpreadsheetSerializationFormat.JSON:

                        outputPath = Path.Combine(outputPath, container.serializationFileName + ".json");
                        serializer = new SpreadsheetJSONSerializer(content, outputPath);
                        serializer.Run();
                        break;

                    case SpreadsheetSerializationFormat.Binary:
                        outputPath = Path.Combine(outputPath, container.serializationFileName + ".bin");
                        serializer = new SpreadsheetBinarySerializer(content, outputPath);
                        serializer.Run();
                        break;

                }
            }
            EditorGUI.EndDisabledGroup();


            EditorGUILayout.EndHorizontal();
            #endregion
        }

        void SelectAll(bool mode)
        {
            foreach (var toggleId in possibleTogglesIds)
                SelectToggle(toggleId, mode);
        }

        void SelectToggle(string toggleId, bool mode)
        {
            if (mode && !container.selectedTogglesIds.Contains(toggleId))
                container.selectedTogglesIds.Add(toggleId);
            else if (!mode)
                container.selectedTogglesIds.Remove(toggleId);
        }

        void OnClickImport(IEnumerable<FieldInfo> selectedContentFields)
        {
            if (!container.selectedTogglesIds.Any())
            {
                Debug.LogWarning("Nothing is selected to import");
                return;
            }

            if (string.IsNullOrWhiteSpace(container.documentId))
                throw new Exception($"Document ID is not specified!");


            EditorUtility.DisplayProgressBar("Downloading definitions", "Initializing...", 0);

            importer = new SpreadsheetImporter(content, selectedContentFields.ToArray(), container.documentId);

            importer.onComplete += OnImportQueueComplete;
            importer.onOutputChanged += OnOutputChanged;
            importer.onProgressChanged += OnProgressChanged;
            importer.onOperationFailed += OnOperationFailed;

            importer.Run();
        }

        void OnProgressChanged()
        {
            EditorUtility.DisplayProgressBar("Downloading definitions", importer.Output, importer.Progress);
        }

        void OnOutputChanged()
        {
            EditorUtility.DisplayProgressBar("Downloading definitions", importer.Output, importer.Progress);
        }

        void OnImportQueueComplete()
        {
            EditorUtility.SetDirty(target);

            EditorUtility.ClearProgressBar();

            importer.onComplete -= OnImportQueueComplete;
            importer.onOutputChanged -= OnOutputChanged;
            importer.onProgressChanged -= OnProgressChanged;
            importer.onOperationFailed -= OnOperationFailed;
            importer = null;
        }

        void OnOperationFailed(string message)
        {
            EditorUtility.ClearProgressBar();

            importer.onComplete -= OnImportQueueComplete;
            importer.onOutputChanged -= OnOutputChanged;
            importer.onProgressChanged -= OnProgressChanged;
            importer.onOperationFailed -= OnOperationFailed;
            importer = null;
        }
    } 
}
