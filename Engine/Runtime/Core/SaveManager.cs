#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SSNoir.Core
{
    public static class SaveManager
    {
        public static void Write(string filePath, SaveData data)
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            using var stream = new MemoryStream();
            var options = new JsonWriterOptions { Indented = true };
            using (var w = new Utf8JsonWriter(stream, options))
            {
                w.WriteStartObject();
                w.WriteNumber("version", data.Version);

                w.WritePropertyName("globals");
                w.WriteStartObject();
                foreach (var kv in data.Globals)
                {
                    w.WritePropertyName(kv.Key);
                    WritePrimitive(w, kv.Value, "globals." + kv.Key);
                }
                w.WriteEndObject();

                w.WritePropertyName("team");
                WriteTeam(w, data.Team);

                w.WritePropertyName("inventory");
                w.WriteStartObject();
                foreach (var kv in data.Inventory)
                    w.WriteNumber(kv.Key, kv.Value);
                w.WriteEndObject();

                w.WritePropertyName("worldData");
                WriteScheme(w, data.WorldData, "worldData");

                w.WriteEndObject();
            }

            File.WriteAllBytes(filePath, stream.ToArray());
        }

        public static SaveData Read(string filePath)
        {
            var bytes = File.ReadAllBytes(filePath);
            using var doc = JsonDocument.Parse(bytes);
            var root = doc.RootElement;

            var data = new SaveData
            {
                Version = root.GetProperty("version").GetInt32(),
            };

            foreach (var prop in root.GetProperty("globals").EnumerateObject())
                data.Globals[prop.Name] = ReadPrimitive(prop.Value, "globals." + prop.Name);

            data.Team = ReadTeam(root.GetProperty("team"));

            foreach (var prop in root.GetProperty("inventory").EnumerateObject())
                data.Inventory[prop.Name] = prop.Value.GetInt32();

            data.WorldData = ReadScheme(root.GetProperty("worldData"), "worldData");

            return data;
        }

        // ── Write helpers ────────────────────────────────────────────────

        private static void WritePrimitive(Utf8JsonWriter w, object? value, string path)
        {
            switch (value)
            {
                case string s:   w.WriteStringValue(s); break;
                case int i:      w.WriteNumberValue(i); break;
                case long l:     w.WriteNumberValue(l); break;
                case double d:   w.WriteNumberValue(d); break;
                case bool b:     w.WriteBooleanValue(b); break;
                default:
                    throw new InvalidDataException(
                        $"Cannot serialize global value at '{path}': unsupported type " +
                        $"{value?.GetType().Name ?? "null"}.");
            }
        }

        private static void WriteScheme(Utf8JsonWriter w, object? value, string path)
        {
            switch (value)
            {
                case null:
                    w.WriteNullValue();
                    break;
                case string s:
                    w.WriteStringValue(s);
                    break;
                case int i:
                    w.WriteNumberValue(i);
                    break;
                case long l:
                    w.WriteNumberValue(l);
                    break;
                case double d:
                    w.WriteNumberValue(d);
                    break;
                case bool b:
                    w.WriteBooleanValue(b);
                    break;
                case List<object> list:
                    w.WriteStartArray();
                    for (int idx = 0; idx < list.Count; idx++)
                        WriteScheme(w, list[idx], $"{path}[{idx}]");
                    w.WriteEndArray();
                    break;
                default:
                    throw new InvalidDataException(
                        $"world-save returned an unsupported value at '{path}': type " +
                        $"{value.GetType().Name}. Only string, int, double, bool, and list " +
                        "are allowed. Check that world-save doesn't return symbols, procedures, or native objects.");
            }
        }

        private static void WriteTeam(Utf8JsonWriter w, TeamSaveData team)
        {
            w.WriteStartObject();
            w.WriteNumber("health", team.Health);
            w.WriteNumber("supplies", team.Supplies);
            w.WriteNumber("growthLevel", team.GrowthLevel);
            w.WritePropertyName("actors");
            w.WriteStartArray();
            foreach (var a in team.Actors)
            {
                w.WriteStartObject();
                w.WriteString("id", a.Id);
                w.WriteString("name", a.Name);
                w.WriteString("role", a.Role);
                w.WriteString("status", a.Status);
                w.WriteNumber("stress", a.Stress);
                w.WriteNumber("spentGrowthPoints", a.SpentGrowthPoints);
                w.WritePropertyName("stats");
                w.WriteStartObject();
                foreach (var kv in a.Stats)
                    w.WriteNumber(kv.Key, kv.Value);
                w.WriteEndObject();
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }

        // ── Read helpers ─────────────────────────────────────────────────

        private static object ReadPrimitive(JsonElement elem, string path)
        {
            return elem.ValueKind switch
            {
                JsonValueKind.String  => (object)elem.GetString()!,
                JsonValueKind.Number  => elem.TryGetInt32(out int i) ? i : (object)elem.GetDouble(),
                JsonValueKind.True    => (object)true,
                JsonValueKind.False   => (object)false,
                _ => throw new InvalidDataException(
                    $"Unexpected JSON kind {elem.ValueKind} at '{path}'. Expected string, number, or bool.")
            };
        }

        private static object ReadScheme(JsonElement elem, string path)
        {
            return elem.ValueKind switch
            {
                JsonValueKind.String  => (object)elem.GetString()!,
                JsonValueKind.Number  => elem.TryGetInt32(out int i) ? i : (object)elem.GetDouble(),
                JsonValueKind.True    => (object)true,
                JsonValueKind.False   => (object)false,
                JsonValueKind.Array   => ReadSchemeArray(elem, path),
                _ => throw new InvalidDataException(
                    $"Unexpected JSON kind {elem.ValueKind} at '{path}' in worldData. " +
                    "Only string, number, bool, and array are valid Scheme serialized types.")
            };
        }

        private static List<object> ReadSchemeArray(JsonElement elem, string path)
        {
            var list = new List<object>();
            int idx = 0;
            foreach (var item in elem.EnumerateArray())
            {
                list.Add(ReadScheme(item, $"{path}[{idx}]"));
                idx++;
            }
            return list;
        }

        private static TeamSaveData ReadTeam(JsonElement el)
        {
            var team = new TeamSaveData
            {
                Health     = el.GetProperty("health").GetInt32(),
                Supplies   = el.GetProperty("supplies").GetInt32(),
                GrowthLevel = el.GetProperty("growthLevel").GetInt32(),
            };
            foreach (var actorEl in el.GetProperty("actors").EnumerateArray())
            {
                var a = new ActorSaveData
                {
                    Id                = actorEl.GetProperty("id").GetString()!,
                    Name              = actorEl.GetProperty("name").GetString()!,
                    Role              = actorEl.GetProperty("role").GetString()!,
                    Status            = actorEl.GetProperty("status").GetString()!,
                    Stress            = actorEl.GetProperty("stress").GetInt32(),
                    SpentGrowthPoints = actorEl.GetProperty("spentGrowthPoints").GetInt32(),
                };
                foreach (var stat in actorEl.GetProperty("stats").EnumerateObject())
                    a.Stats[stat.Name] = stat.Value.GetInt32();
                team.Actors.Add(a);
            }
            return team;
        }
    }
}
