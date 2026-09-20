using System;
using SSNoir.Testing;

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
                    GameTester.ValidateContent();
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

                Console.Error.WriteLine(
                    "用法：./run --validate | --test-saveload | --test-odds | --test-round-transition | " +
                    "--playtest <入场表达式或场景名> [选项]");
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
