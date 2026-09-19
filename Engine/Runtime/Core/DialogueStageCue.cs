#nullable enable

namespace SSNoir.Core
{
    // 一句台词附带的舞台指示：说话人换什么姿势、往哪儿挪、灯怎么样、抖不抖。
    // 全部是终态而不是过程——渲染器只负责从上一个终态过渡到这个终态，跳过动画也得到同一画面。
    // 这些都是可选的；没有指示的台词等于「保持上一句的样子」。
    //
    // 灯的语法（:light）只有一个词，分两类：
    //   亮度状态，保持到下一次改变：normal（招牌本来的样子）/ surge（灼：光晕撑开、颜色洗台）/ faint（弱：只剩细管）
    //                            / ember（残烛：欠压的钨丝，管子发暗橙、没有光晕）
    //   电流状态，保持到下一次改变：still / pulse（电流沿管子缓慢游走）/ racing（狂飙）
    //   事件，几拍就过去：flicker（颤：接触不良闪几拍）/ relight（燃：全灭后从脚到头重新点亮）/ blackout（黑：整台黑半秒）
    // 画面的语法（:screen）对象是整个屏幕：状态 normal / negative（负片），事件 flash（白闪）。
    // 心里话（:inner）：这句没说出口，是说话人心里的；对白框换一副样子。
    // 听者（:other）：写在它后面的人物指示落到台上另一个人身上，而不是说话人。
    // 「谁说话谁亮、听的人压暗、上台通电点亮」是默认规则，不写在脚本里。
    public sealed class DialogueStageCue
    {
        // 立绘变体名，如 "逼近"、"背身"；"基础" 回到招牌姿势；null 表示不换。资源名是 <说话人>_<姿势>。
        public string? Pose { get; init; }
        // "in"（向舞台中央逼近）/ "back"（退回边上）。null 表示不动。
        public string? Move { get; init; }
        // 灯的状态："normal" / "surge" / "faint" / "ember"。null 表示不变。
        public string? Light { get; init; }
        // 电流："still"（不走）/ "pulse"（缓慢游走，平静的呼吸）/ "racing"（狂飙，心跳加速）。null 表示不变。
        // 和亮度是两个轴，可以叠：:light 'surge :light 'racing。
        public string? Current { get; init; }
        // 灯的事件：说这句时触发一次。
        public bool Flicker { get; init; }
        public bool Relight { get; init; }
        public bool Blackout { get; init; }
        // 说这句时短促一震：情绪爆发、被戳中。
        public bool Shake { get; init; }
        // 画面（整个屏幕，不是某个人）。状态："normal" / "negative"（负片：环境全白、灯管变黑，白热化）。null 表示不变。
        public string? Screen { get; init; }
        // 画面事件：白闪一帧（枪声、闪光灯、耳光）。
        public bool Flash { get; init; }
        // 心里话：没说出口。只改对白框的样子，舞台照常认说话人。
        public bool Inner { get; init; }
        // 落到台上另一个人身上的人物指示（姿势/距离/灯/震）。null 表示对方不动。
        public DialogueStageCue? Other { get; init; }

        public static readonly DialogueStageCue None = new DialogueStageCue();
    }
}
