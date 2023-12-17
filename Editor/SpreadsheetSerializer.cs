using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using UnityEngine;

namespace NorskaLib.Spreadsheets
{
    public abstract class SpreadsheetSerializer
    {
        protected readonly object targetObject;
        protected readonly string outputPath;

        public SpreadsheetSerializer(object targetObject, string outputPath)
        {
            this.targetObject = targetObject;
            this.outputPath = outputPath;
        }

        public abstract Task Run();
    }

    public class SpreadsheetJSONSerializer : SpreadsheetSerializer
    {
        public SpreadsheetJSONSerializer(object targetObject, string outputPath) : base(targetObject, outputPath) { }

        public override async Task Run()
        {
            var serilizedObject = JsonUtility.ToJson(targetObject, true);
            await File.WriteAllTextAsync(outputPath, serilizedObject);

            //onComplete?.Invoke();
        }
    }

    public class SpreadsheetBinarySerializer : SpreadsheetSerializer
    {
        public SpreadsheetBinarySerializer(object targetObject, string outputPath) : base(targetObject, outputPath) { }

        public override async Task Run()
        {
            var binaryFormatter = new BinaryFormatter();
            using var fileStream = new FileStream(outputPath, FileMode.Create);
            await Task.Run(() => binaryFormatter.Serialize(fileStream, targetObject));

            //onComplete?.Invoke();
        }
    }
}