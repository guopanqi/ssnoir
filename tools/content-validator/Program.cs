using System;
using SSNoir.Testing;
using SSNoir.Core;

namespace SSNoir
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                if (args.Length > 0 && args[0] == "--validate")
                {
                    if (args.Length > 2 || (args.Length == 2 && args[1] != "en" && args[1] != "zh-CN"))
                        throw new ArgumentException("Usage: --validate [zh-CN|en]");
                    GameLanguage.Current = args.Length == 2 ? args[1] : GameLanguage.Chinese;
                    GameTester.ValidateContent();
                    return;
                }

                if (args.Length == 1 && args[0] == "--test-theatre")
                {
                    TheatreChecks.Run();
                    return;
                }

                if (args.Length > 0 && args[0] == "--test-saveload")
                {
                    GameTester.TestSaveLoad();
                    return;
                }

                if (args.Length > 0 && args[0] == "--test-odds")
                {
                    GameTester.TestFateStrip();
                    return;
                }

                if (args.Length > 0 && args[0] == "--test-round-transition")
                {
                    GameTester.TestRoundTransition();
                    return;
                }

                // 交锋试跑：无头跑完一场，打出逐回合流水。
                if (args.Length > 0 && args[0] == "--playtest")
                {
                    SSNoir.Playtest.PlaytestRunner.Run(args);
                    return;
                }

                if (args.Length > 0 && args[0] == "--session")
                {
                    SSNoir.Session.SessionRunner.Run(args);
                    return;
                }

                if (args.Length == 3 && args[0] == "--theatre-export")
                {
                    TheatreExporter.Run(args[1], args[2]);
                    return;
                }

                if (args.Length == 3 && args[0] == "--stage-export")
                {
                    SSNoir.StagePreview.StageExporter.Run(args[1], args[2]);
                    return;
                }

                if (args.Length == 2 && args[0] == "--replay")
                {
                    SSNoir.Session.SessionRunner.Replay(args[1]);
                    return;
                }

                if (args.Length > 0 && args[0] == "--session-baseline")
                {
                    SSNoir.Session.SessionRunner.Baseline(args);
                    return;
                }

                if (args.Length > 1 && args[0] == "--session-report")
                {
                    SSNoir.Session.SessionRunner.Report(args[1..]);
                    return;
                }

                Console.Error.WriteLine(
                    "用法：./run --validate | --test-saveload | --test-odds | --test-round-transition | --test-theatre | " +
                    "--playtest <入场表达式或场景名> [选项] | --session <world|场景名|入场表达式> [选项] " +
                    "| --session-baseline <交锋入口> --output <记录.json> | --session-report <记录.json> [...] | --replay <记录.json> " +
                    "| --theatre-export <Scheme 表达式> <输出.json> | --stage-export <Scheme 表达式> <输出.json>");
                Environment.Exit(2);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                System.Environment.Exit(1);
            }
        }
    }
}
