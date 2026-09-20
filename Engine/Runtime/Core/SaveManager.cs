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
        public const int SlotCount = 5;

        // Unity 在启动时设置正式路径；无界面测试需要时显式传入自己的临时路径。
        public static string DefaultSavePath { get; set; } = "save.json";

        public static void Write(string filePath, SaveData data)
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var root = new JObject
            {
                ["saveTime"]  = data.SaveTime,
                ["settings"]  = WriteGlobals(data.Settings),
                ["globals"]   = WriteGlobals(data.Globals),
                ["team"]      = WriteTeam(data.Team),
                ["inventory"] = WriteInventory(data.Inventory),
                ["worldData"] = WriteScheme(data.WorldData, "worldData"),
            };

            string temporaryPath = filePath + ".tmp";
            try
            {
                File.WriteAllText(temporaryPath, root.ToString(Formatting.Indented));
                if (File.Exists(filePath))
                    File.Replace(temporaryPath, filePath, destinationBackupFileName: null);
                else
                    File.Move(temporaryPath, filePath);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        public static SaveData Read(string filePath)
        {
            var root = JObject.Parse(File.ReadAllText(filePath));

            var data = new SaveData
            {
                SaveTime = root["saveTime"]?.Value<string>() ?? "",
            };

            // 旧存档没有 settings；调用方保留各设置自己的默认值。
            if (root["settings"] is JObject settings)
                foreach (var prop in settings.Properties())
                    data.Settings[prop.Name] = ReadPrimitive(prop.Value, "settings." + prop.Name);

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
                    ["composure"]         = a.Composure,
                    ["hangoverSlotId"]    = a.HangoverSlotId == null ? JValue.CreateNull() : new JValue(a.HangoverSlotId.Value),
                    ["permanentDiePenaltyLabel"] = a.PermanentDiePenaltyLabel,
                    ["permanentDiePenalty"] = a.PermanentDiePenalty,
                    ["spentGrowthPoints"] = a.SpentGrowthPoints,
                    ["stats"]             = stats,
                    ["actionDice"]        = new JArray(a.ActionDice),
                    ["actionDiceSlotIds"] = new JArray(a.ActionDiceSlotIds),
                });
            }
            return new JObject
            {
                ["injurySeverity"] = team.InjurySeverity,
                ["injuryPart"]     = team.InjuryPart,
                ["growthLevel"] = team.GrowthLevel,
                ["actors"]      = actorsArr,
                ["supports"]    = new JArray(team.Supports),
                ["carriedSupport"] = team.CarriedSupport,
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
                InjurySeverity = el["injurySeverity"]!.Value<int>(),
                InjuryPart     = el["injuryPart"]!.Value<string>()!,
                GrowthLevel = el["growthLevel"]!.Value<int>(),
            };
            bool? hasSavedActionDice = null;
            foreach (var actorEl in (JArray)el["actors"]!)
            {
                var ao = (JObject)actorEl;
                bool actorHasDice = ao["actionDice"] != null || ao["actionDiceSlotIds"] != null;
                if ((ao["actionDice"] == null) != (ao["actionDiceSlotIds"] == null))
                    throw new InvalidDataException($"Actor '{ao["id"]}' save data must contain both actionDice and actionDiceSlotIds.");
                if (hasSavedActionDice != null && hasSavedActionDice.Value != actorHasDice)
                    throw new InvalidDataException("Save data mixes actors with and without saved action dice.");
                hasSavedActionDice = actorHasDice;
                var a = new ActorSaveData
                {
                    Id                = ao["id"]!.Value<string>()!,
                    Name              = ao["name"]!.Value<string>()!,
                    Role              = ao["role"]!.Value<string>()!,
                    Status            = ao["status"]!.Value<string>()!,
                    Composure         = ao["composure"]!.Value<int>(),
                    HangoverSlotId = ao["hangoverSlotId"]?.Type == JTokenType.Null ? null : ao["hangoverSlotId"]?.Value<int>(),
                    PermanentDiePenaltyLabel = ao["permanentDiePenaltyLabel"]?.Value<string>() ?? string.Empty,
                    PermanentDiePenalty = ao["permanentDiePenalty"]?.Value<int>() ?? 0,
                    SpentGrowthPoints = ao["spentGrowthPoints"]!.Value<int>(),
                };
                foreach (var stat in ((JObject)ao["stats"]!).Properties())
                    a.Stats[stat.Name] = stat.Value.Value<int>();
                if (actorHasDice)
                {
                    foreach (var die in (JArray)ao["actionDice"]!)
                        a.ActionDice.Add(die.Value<int>());
                    foreach (var slotId in (JArray)ao["actionDiceSlotIds"]!)
                        a.ActionDiceSlotIds.Add(slotId.Value<int>());
                }
                team.Actors.Add(a);
            }
            team.HasSavedActionDice = hasSavedActionDice ?? false;
            // 旧存档没有关系支援字段：没有就是没有。
            if (el["supports"] is JArray supports)
                foreach (var id in supports)
                    team.Supports.Add(id.Value<string>()!);
            team.CarriedSupport = el["carriedSupport"]?.Value<string>() ?? string.Empty;
            return team;
        }

        public static string GetSlotFilePath(int slotIndex)
        {
            if (slotIndex < 1 || slotIndex > SlotCount)
                throw new ArgumentOutOfRangeException(nameof(slotIndex), $"Save slot must be between 1 and {SlotCount}.");
            var dir = Path.GetDirectoryName(DefaultSavePath);
            var filename = $"save_slot{slotIndex}.json";
            if (string.IsNullOrEmpty(dir))
                return filename;
            return Path.Combine(dir, filename);
        }

        public static string GetSaveTime(string filePath)
        {
            if (!File.Exists(filePath))
                return "";
            try
            {
                var content = File.ReadAllText(filePath);
                var root = JObject.Parse(content);
                var saveTime = root["saveTime"]?.Value<string>();
                if (!string.IsNullOrEmpty(saveTime))
                    return saveTime;

                return File.GetLastWriteTime(filePath).ToString("yyyy-MM-dd HH:mm");
            }
            catch
            {
                try
                {
                    return File.GetLastWriteTime(filePath).ToString("yyyy-MM-dd HH:mm");
                }
                catch
                {
                    return "";
                }
            }
        }
    }
}
