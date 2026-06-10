using System.IO;
using SSNoir.Core;

namespace SSNoir
{
    public class LocalScriptLoader : IScriptLoader
    {
        public string LoadScriptText(string path)
        {
            // Try ../Content/ path first (for dotnet run in TerminalApp directory)
            var devPath = Path.Combine("..", "Content", path);
            if (File.Exists(devPath))
            {
                return File.ReadAllText(devPath);
            }

            // Fallback to Content/ path (for compiled executable output)
            var prodPath = Path.Combine("Content", path);
            if (File.Exists(prodPath))
            {
                return File.ReadAllText(prodPath);
            }

            // Direct fallback
            var directPath = Path.GetFullPath(path);
            if (File.Exists(directPath))
            {
                return File.ReadAllText(directPath);
            }

            throw new FileNotFoundException($"Could not load script at: {path} (Checked {devPath} and {prodPath})");
        }
    }
}
