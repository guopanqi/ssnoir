#nullable enable
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace SSNoir.Editor
{
    /// <summary>
    /// 把过场视频的亮度范围规范化：满范围数据压回有限范围，并打上一致的标签。
    ///
    /// <b>要修的是范围，不是矩阵。</b>生成服务吐出来的片子，亮度实际跌出 16–235（量到过
    /// YMIN=0、YMAX=255），也就是满范围（pc）数据；而无论是无标签靠猜、还是被剪映盖上
    /// <c>tv</c> 标签，解码端都会按有限范围展开——16 拉成 0、235 拉成 255。后果正好打在这套
    /// 画风最疼的地方：暗部塌成纯黑、白描线爆掉、中间调被硬拉 255/219 ≈ 1.164 倍对比。
    ///
    /// 拿首帧原图做过对照实验（四种解码假设各解一遍算 PSNR）：范围切对了值 +1.1 dB，
    /// bt601 换成 bt709 只值 0.02 dB。矩阵基本无关——它只管色度怎么混进 RGB，而这套画面几乎
    /// 没有色度（U/V 全挤在中性点 128 附近）。所以别再在 601/709 上花时间。
    ///
    /// <b>为什么压数据而不是打 pc 标签。</b>标签只是个声明，各家解码器对满范围标志的支持参差
    /// 不齐，Unity 的 <c>VideoPlayer</c> 更是连色彩范围的开关都没暴露。把数据真正压进 16–235
    /// 再标 tv，谁来解码都不用猜，代价只有约 0.2 bit 的精度——远小于现在两头被削平的损失。
    ///
    /// <b>为什么已经落在 16–235 内的片子直接跳过。</b>那种情况判不出来：可能本就是有限范围
    /// 数据，也可能是满范围但对比不够、没顶到边。而两种猜错的代价不对称——跳过最多留下一点
    /// 对比误差，误修则是把 16–235 再压一次进 31–219，画面直接发灰。存疑就不动。
    ///
    /// <b>光看亮度范围认不出"已经处理过"。</b>转换本身是精确的（无损编码量到正好 16–235），
    /// 但 libx264 在这套画风的纯黑／纯白硬边上会振铃过冲，成片实测能跑到 7–240。拿范围当
    /// 判据的话，处理过的片子下次会被当成满范围再压一遍，一次比一次灰。所以处理时往容器里
    /// 写一个 <see cref="ProcessedMarker"/>，认标记不认数据。
    ///
    /// <b>剪映要排在这一步前面。</b>它会重编一遍、把 24fps 重采样成 30fps，并且原样保留满范围
    /// 采样值却盖上 tv 标签——先规范化再进剪映等于白做。顺序是：下载 →（要剪就剪）→ 跑这个。
    /// </summary>
    public static class CutsceneVideoNormalizer
    {
        // 有限范围的定义窗口。亮度跌出这两个数，就是满范围数据的铁证。
        private const int LimitedFloor = 16;
        private const int LimitedCeiling = 235;

        // 描边只有一两像素宽，压缩一糊就没了，码率上不吝啬。
        private const int Crf = 16;

        /// <summary>
        /// 写进容器 comment 的处理标记。带上版本号：以后换了转换参数，改这里的数字就能让
        /// 旧片子重新排进队列。
        /// </summary>
        private const string ProcessedMarker = "ssnoir-range-normalized-v1";

        /// <summary>
        /// 备份目录。<b>不能放进 StreamingAssets</b>——那个目录是原样拷进包里的，备份会跟着
        /// 一起发出去，包大小直接翻倍。结尾的 <c>~</c> 让 Unity 不把它当资源导入。
        /// </summary>
        private const string BackupDirName = "CutsceneSource~";

        [MenuItem("SSNoir/规范化过场视频")]
        public static void NormalizeAll()
        {
            string? ffmpeg = FindTool("ffmpeg");
            string? ffprobe = FindTool("ffprobe");
            if (ffmpeg == null || ffprobe == null)
            {
                Debug.LogError(
                    "[CutsceneVideo] 找不到 ffmpeg／ffprobe。从 Finder 启动的 Unity 拿不到 shell 的"
                    + " PATH，所以这里只找 /opt/homebrew/bin、/usr/local/bin、/usr/bin 和 PATH。"
                    + "装了 Homebrew 的话执行 brew install ffmpeg。");
                return;
            }

            string cutsceneDir = Path.GetFullPath(
                Path.Combine(Application.dataPath, "StreamingAssets", "Cutscenes"));
            if (!Directory.Exists(cutsceneDir))
            {
                Debug.LogError($"[CutsceneVideo] 过场目录不存在：{cutsceneDir}");
                return;
            }

            string[] videos = Directory.GetFiles(cutsceneDir, "*.mp4", SearchOption.TopDirectoryOnly);
            if (videos.Length == 0)
            {
                Debug.LogWarning($"[CutsceneVideo] {cutsceneDir} 下一个 mp4 都没有。");
                return;
            }

            string backupDir = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", BackupDirName));
            Directory.CreateDirectory(backupDir);

            var fixedUp = new List<string>();
            var skipped = new List<string>();
            var failed = new List<string>();

            try
            {
                for (int i = 0; i < videos.Length; i++)
                {
                    string path = videos[i];
                    string name = Path.GetFileName(path);

                    if (EditorUtility.DisplayCancelableProgressBar(
                            "规范化过场视频", $"({i + 1}/{videos.Length}) {name}",
                            (float)i / videos.Length))
                    {
                        Debug.LogWarning("[CutsceneVideo] 已取消。已经处理完的片子保持处理后的状态。");
                        break;
                    }

                    if (IsAlreadyNormalized(ffprobe, path))
                    {
                        Debug.Log($"[CutsceneVideo] {name}：带着处理标记，跳过。");
                        skipped.Add(name);
                        continue;
                    }

                    if (!MeasureLumaRange(ffmpeg, path, out int yMin, out int yMax))
                    {
                        Debug.LogError($"[CutsceneVideo] {name}：读不出亮度范围，跳过。");
                        failed.Add(name);
                        continue;
                    }

                    if (yMin >= LimitedFloor && yMax <= LimitedCeiling)
                    {
                        Debug.Log(
                            $"[CutsceneVideo] {name}：亮度 {yMin}–{yMax} 已落在 "
                            + $"{LimitedFloor}–{LimitedCeiling} 内，判不出是不是满范围，不动它。");
                        skipped.Add(name);
                        continue;
                    }

                    if (Normalize(ffmpeg, path, backupDir, yMin, yMax))
                        fixedUp.Add(name);
                    else
                        failed.Add(name);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();

            Debug.Log(
                $"[CutsceneVideo] 完成：修了 {fixedUp.Count}，跳过 {skipped.Count}，失败 {failed.Count}。"
                + $"\n原始文件备份在：{backupDir}"
                + (fixedUp.Count > 0 ? $"\n修过：{string.Join("、", fixedUp)}" : "")
                + (failed.Count > 0 ? $"\n失败：{string.Join("、", failed)}" : ""));
        }

        /// <summary>这片子是不是这个版本的转换处理过的。读不出来就当没处理过，宁可多问一次。</summary>
        private static bool IsAlreadyNormalized(string ffprobe, string path)
        {
            string args = $"-v error -show_entries format_tags=comment "
                        + $"-of default=nw=1:nk=1 \"{path}\"";

            return Run(ffprobe, args, out string stdout, out _)
                && stdout.Trim() == ProcessedMarker;
        }

        /// <summary>
        /// 量整片的亮度上下界。一次解码扫完，signalstats 逐帧吐 YMIN/YMAX，这里取全片的极值。
        /// </summary>
        private static bool MeasureLumaRange(string ffmpeg, string path, out int yMin, out int yMax)
        {
            yMin = int.MaxValue;
            yMax = int.MinValue;

            // 用 -i 正常喂文件，不走 lavfi 的 movie= 源——那条路要对路径里的冒号、反斜杠、
            // 引号自己做转义，中文名和空格一多就容易出错。
            string args = $"-v error -i \"{path}\" "
                        + "-vf \"signalstats,metadata=print:file=-\" -f null -";

            if (!Run(ffmpeg, args, out string stdout, out _))
                return false;

            foreach (string line in stdout.Split('\n'))
            {
                int eq = line.IndexOf('=');
                if (eq < 0)
                    continue;

                string key = line.Substring(0, eq).Trim();
                if (!int.TryParse(line.Substring(eq + 1).Trim(), out int value))
                    continue;

                if (key.EndsWith("YMIN"))
                    yMin = Mathf.Min(yMin, value);
                else if (key.EndsWith("YMAX"))
                    yMax = Mathf.Max(yMax, value);
            }

            return yMin != int.MaxValue && yMax != int.MinValue;
        }

        /// <summary>
        /// 重编一遍：满范围压进有限范围，标签一并写死。原始文件先挪进备份目录。
        ///
        /// 帧率不碰——原始输出是 24fps，重采样只会多一层插值糊。
        /// </summary>
        private static bool Normalize(
            string ffmpeg, string path, string backupDir, int yMin, int yMax)
        {
            string name = Path.GetFileName(path);
            string temp = path + ".normalizing.mp4";

            string args =
                $"-y -i \"{path}\" "
                + "-vf \"scale=in_range=full:out_range=limited:"
                + "in_color_matrix=bt709:out_color_matrix=bt709\" "
                + $"-c:v libx264 -crf {Crf} -preset slow -pix_fmt yuv420p "
                + "-colorspace bt709 -color_primaries bt709 -color_trc bt709 -color_range tv "
                // 上面那几个只写进容器；primaries 和 trc 落不到码流的 VUI 里（实测出来是
                // unknown）。这一道把五项全部写死在 H.264 码流自己身上，解码器读的就是它。
                + "-bsf:v \"h264_metadata=colour_primaries=1:transfer_characteristics=1"
                + ":matrix_coefficients=1:video_full_range_flag=0\" "
                // 片子带音轨，原样搬过去——这一步只管画面。
                + "-c:a copy -movflags +faststart "
                + $"-metadata comment=\"{ProcessedMarker}\" "
                + $"\"{temp}\"";

            if (!Run(ffmpeg, args, out _, out string stderr))
            {
                Debug.LogError($"[CutsceneVideo] {name}：重编失败。\n{stderr}");
                if (File.Exists(temp))
                    File.Delete(temp);
                return false;
            }

            // 备份只认第一次：重复跑的时候，目录里躺着的那份才是真原件，别让处理过的版本
            // 把它盖掉。
            string backup = Path.Combine(backupDir, name);
            if (File.Exists(backup))
                File.Delete(path);
            else
                File.Move(path, backup);

            // 原地替换，.meta 不动，GUID 和场上引用都保住。
            File.Move(temp, path);

            Debug.Log($"[CutsceneVideo] {name}：亮度 {yMin}–{yMax} → 已压回有限范围并标 bt709/tv。");
            return true;
        }

        private static bool Run(string exe, string args, out string stdout, out string stderr)
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var proc = Process.Start(psi);
            if (proc == null)
            {
                stdout = string.Empty;
                stderr = "进程起不来";
                return false;
            }

            // 两个流都得先读干净再 WaitForExit：管道缓冲满了子进程会卡在写上，双方一起死等。
            stdout = proc.StandardOutput.ReadToEnd();
            stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            return proc.ExitCode == 0;
        }

        /// <summary>
        /// 找外部命令。从 Finder 启动的 Unity 继承的是 launchd 那份精简 PATH，Homebrew 的
        /// 目录多半不在里面，所以先按常见位置直接探。
        /// </summary>
        private static string? FindTool(string tool)
        {
            string[] candidates =
            {
                $"/opt/homebrew/bin/{tool}",
                $"/usr/local/bin/{tool}",
                $"/usr/bin/{tool}",
            };

            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate))
                    return candidate;
            }

            string? pathEnv = System.Environment.GetEnvironmentVariable("PATH");
            if (pathEnv == null)
                return null;

            foreach (string dir in pathEnv.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(dir))
                    continue;

                string candidate = Path.Combine(dir, tool);
                if (File.Exists(candidate))
                    return candidate;
            }

            return null;
        }
    }
}
