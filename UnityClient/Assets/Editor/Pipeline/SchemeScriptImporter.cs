using System.IO;
using System.Text;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace SSNoir.Editor
{
    [ScriptedImporter(1, "scm")]
    public sealed class SchemeScriptImporter : ScriptedImporter
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);

        public override void OnImportAsset(AssetImportContext context)
        {
            string source = StrictUtf8.GetString(File.ReadAllBytes(context.assetPath));
            if (string.IsNullOrWhiteSpace(source))
                throw new InvalidDataException($"Scheme 文件为空: {context.assetPath}");

            var textAsset = new TextAsset(source)
            {
                name = Path.GetFileNameWithoutExtension(context.assetPath)
            };
            context.AddObjectToAsset("script", textAsset);
            context.SetMainObject(textAsset);
        }
    }
}
