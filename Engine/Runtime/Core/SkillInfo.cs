#nullable enable

namespace SSNoir.Core
{
    // 技能/属性的展示信息。唯一的 key→中文名 映射源，取代之前散落在
    // NativeFunctions 与内容脚本里的重复字面量。
    public static class SkillInfo
    {
        public static string DisplayName(string key)
        {
            return key switch
            {
                "violence"  => "力量",
                "knowledge" => "见识",
                "sharpness" => "敏锐",
                "social"    => "交际",
                _ => key,
            };
        }
    }
}
