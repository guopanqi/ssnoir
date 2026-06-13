#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SSNoir.Core
{
    public static class SaveManager
    {
        // Set once at app startup. TerminalApp: "save.json". Unity: Application.persistentDataPath + "/save.json".
        public static string DefaultSavePath { get; set; } = "save.json";

        public static void Write(string filePath, SaveData data)
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var root = new JObject
            {
                ["version"]   = data.Version,
                ["globals"]   = WriteGlobals(data.Globals),
                ["team"]      = WriteTeam(data.Team),
                ["inventory"] = WriteInventory(data.Inventory),
                ["worldData"] = WriteScheme(data.WorldData, "worldData"),
            };

            File.WriteAllText(filePath, root.ToString(Formatting.Indented));
        }

        public static SaveData Read(string filePath)
        {
            var root = JObject.Parse(File.ReadAllText(filePath));

            var data = new SaveData
            {
                Version = root["version"]!.Value<int>(),
            };

            foreach (var prop in ((JObject)root["globals"]!).Properties())
                data.Globals[prop.Name] = ReadPrimitive(prop.Value, "globals." + prop.Name);

            data.Team = ReadTeam((JObject)root["team"]!);

            foreach (var prop in ((JObject)root["inventory"]!).Properties())
                data.Inventory[prop.Name] = prop.Value.Value<int>();

            data.WorldData = ReadScheme(root["worldData"]!, "worldData");

            return data;
        }

        // ── Write helpers ────────────────────────────────────────────────

        private static JObject WriteGlobals(Dictionary<string, object> globals)
        {
            var obj = new JObject();
            foreach (var kv in globals)
                obj[kv.Key] = WritePrimitive(kv.Value, "globals." + kv.Key);
            return obj;
        }

        private static JToken WritePrimitive(object? value, string path)
        {
            return value switch
            {
                string s => new JValue(s),
                int i    => new JValue(i),
                long l   => new JValue(l),
                double d => new JValue(d),
                bool b   => new JValue(b),
                _ => throw new InvalidDataException(
                    $"Cannot serialize global value at '{path}': unsupported type " +
                    $"{value?.GetType().Name ?? "null"}.")
            };
        }

        private static JToken WriteScheme(object? value, string path)
        {
            return value switch
            {
                null             => JValue.CreateNull(),
                string s         => new JValue(s),
                int i            => new JValue(i),
                long l           => new JValue(l),
                double d         => new JValue(d),
                bool b           => new JValue(b),
                List<object> lst => WriteSchemeList(lst, path),
                _ => throw new InvalidDataException(
                    $"world-save returned an unsupported value at '{path}': type " +
                    $"{value.GetType().Name}. Only string, int, double, bool, and list " +
                    "are allowed. Check that world-save doesn't return symbols, procedures, or native objects.")
            };
        }

        private static JArray WriteSchemeList(List<object> list, string path)
        {
            var arr = new JArray();
            for (int i = 0; i < list.Count; i++)
                arr.Add(WriteScheme(list[i], $"{path}[{i}]"));
            return arr;
        }

        private static JObject WriteTeam(TeamSaveData team)
        {
            var actorsArr = new JArray();
            foreach (var a in team.Actors)
            {
                var stats = new JObject();
                foreach (var kv in a.Stats)
                    stats[kv.Key] = kv.Value;

                actorsArr.Add(new JObject
                {
                    ["id"]                = a.Id,
                    ["name"]              = a.Name,
                    ["role"]              = a.Role,
                    ["status"]            = a.Status,
                    ["stress"]            = a.Stress,
                    ["spentGrowthPoints"] = a.SpentGrowthPoints,
                    ["stats"]             = stats,
                });
            }
            return new JObject
            {
                ["health"]      = team.Health,
                ["supplies"]    = team.Supplies,
                ["growthLevel"] = team.GrowthLevel,
                ["actors"]      = actorsArr,
            };
        }

        private static JObject WriteInventory(Dictionary<string, int> inventory)
        {
            var obj = new JObject();
            foreach (var kv in inventory)
                obj[kv.Key] = kv.Value;
            return obj;
        }

        // ── Read helpers ─────────────────────────────────────────────────

        private static object ReadPrimitive(JToken token, string path)
        {
            return token.Type switch
            {
                JTokenType.String  => (object)token.Value<string>()!,
                JTokenType.Integer => (object)token.Value<int>(),
                JTokenType.Float   => (object)token.Value<double>(),
                JTokenType.Boolean => (object)token.Value<bool>(),
                _ => throw new InvalidDataException(
                    $"Unexpected JSON type {token.Type} at '{path}'. Expected string, number, or bool.")
            };
        }

        private static object ReadScheme(JToken token, string path)
        {
            return token.Type switch
            {
                JTokenType.String  => (object)token.Value<string>()!,
                JTokenType.Integer => (object)token.Value<int>(),
                JTokenType.Float   => (object)token.Value<double>(),
                JTokenType.Boolean => (object)token.Value<bool>(),
                JTokenType.Array   => ReadSchemeArray((JArray)token, path),
                _ => throw new InvalidDataException(
                    $"Unexpected JSON type {token.Type} at '{path}' in worldData. " +
                    "Only string, number, bool, and array are valid Scheme serialized types.")
            };
        }

        private static List<object> ReadSchemeArray(JArray arr, string path)
        {
            var list = new List<object>();
            int idx = 0;
            foreach (var item in arr)
            {
                list.Add(ReadScheme(item, $"{path}[{idx}]"));
                idx++;
            }
            return list;
        }

        private static TeamSaveData ReadTeam(JObject el)
        {
            var team = new TeamSaveData
            {
                Health      = el["health"]!.Value<int>(),
                Supplies    = el["supplies"]!.Value<int>(),
                GrowthLevel = el["growthLevel"]!.Value<int>(),
            };
            foreach (var actorEl in (JArray)el["actors"]!)
            {
                var ao = (JObject)actorEl;
                var a = new ActorSaveData
                {
                    Id                = ao["id"]!.Value<string>()!,
                    Name              = ao["name"]!.Value<string>()!,
                    Role              = ao["role"]!.Value<string>()!,
                    Status            = ao["status"]!.Value<string>()!,
                    Stress            = ao["stress"]!.Value<int>(),
                    SpentGrowthPoints = ao["spentGrowthPoints"]!.Value<int>(),
                };
                foreach (var stat in ((JObject)ao["stats"]!).Properties())
                    a.Stats[stat.Name] = stat.Value.Value<int>();
                team.Actors.Add(a);
            }
            return team;
        }
    }
}
