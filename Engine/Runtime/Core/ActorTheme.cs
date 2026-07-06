#nullable enable
using System.Collections.Generic;

namespace SSNoir.Core
{
    // 角色主题色：按加入队伍的顺序（Team.Actors / snapshot.Actors 的下标）分配一组固定预设色。
    // 卡面右栏的能力对比框、以及手牌区的行动骰子共用同一映射，玩家因此能凭颜色一眼认出是谁。
    // 引擎层不依赖任何渲染库，故以 (r,g,b) 字节返回，由各渲染器转换成自己的 Color 类型。
    public static class ActorTheme
    {
        private static readonly (byte r, byte g, byte b)[] Palette =
        {
            (0x5A, 0xA2, 0xFF), // 0 主角  — 冷蓝
            (0xE2, 0x6A, 0xA0), // 1       — 品红
            (0xE0, 0xA9, 0x4E), // 2       — 琥珀
            (0x4F, 0xC4, 0xA1), // 3       — 青绿
        };

        public static (byte r, byte g, byte b) ColorFor(int actorIndex)
        {
            if (actorIndex < 0)
            {
                actorIndex = 0;
            }
            return Palette[actorIndex % Palette.Length];
        }

        // 按 actorId 在队伍中的加入顺序取色；找不到时退回主角色。
        public static (byte r, byte g, byte b) ColorFor(IReadOnlyList<ActorSnapshot> actors, string actorId)
        {
            for (int i = 0; i < actors.Count; i++)
            {
                if (actors[i].Id == actorId)
                {
                    return ColorFor(i);
                }
            }
            return ColorFor(0);
        }
    }
}
