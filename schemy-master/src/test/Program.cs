// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

﻿namespace test
{
    using System;
    using System.IO;
    using System.Reflection;
    using Schemy;

    class Program
    {
        static void Main(string[] args)
        {
            var interpreter = new Interpreter(fsAccessor: new ReadOnlyFileSystemAccessor());
            using (var reader = new StreamReader(File.OpenRead(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "tests.ss"))))
            {
                var result = interpreter.Evaluate(reader);
                if (result.Error != null)
                {
                    throw new InvalidOperationException(string.Format("Test Error: {0}", result.Error));
                }
            }

            AssertString(interpreter, "\"他说：\\\"你好\\\"。\"", "他说：\"你好\"。");
            AssertString(interpreter, "\"That's fine. 中文弯引号：“你好。”\"", "That's fine. 中文弯引号：“你好。”");
            AssertString(interpreter, "\"C:\\\\temp\\\\file\"", "C:\\temp\\file");
            AssertString(interpreter, "\"第一行\\n第二行\\t结束\"", "第一行\n第二行\t结束");

            AssertNumber(interpreter, "(+)", 0);
            AssertNumber(interpreter, "(+ 1 2 3)", 6);
            AssertNumber(interpreter, "(*)", 1);
            AssertNumber(interpreter, "(* 2 3 4)", 24);
            AssertNumber(interpreter, "(- 3)", -3);
            AssertNumber(interpreter, "(- 10 3 2)", 5);
            AssertNumber(interpreter, "(/ 4)", 0.25);
            AssertNumber(interpreter, "(/ 20 2 5)", 2);
            AssertError(interpreter, "(-)", "Unary/binary subtraction requires an argument.");
            AssertError(interpreter, "(/)", "Unary/binary division requires an argument.");

            var invalidEscape = interpreter.Evaluate("\"bad\\qescape\"");
            if (invalidEscape.Error == null)
                throw new InvalidOperationException("Unknown string escapes must fail.");

            Console.WriteLine("Tests were successful");
        }

        private static void AssertString(Interpreter interpreter, string source, string expected)
        {
            var result = interpreter.Evaluate(source);
            if (result.Error != null)
                throw new InvalidOperationException("String test failed: " + result.Error);
            if (!object.Equals(result.Result, expected))
                throw new InvalidOperationException(
                    string.Format("String test mismatch. Expected <{0}>, got <{1}>.", expected, result.Result));
        }

        private static void AssertNumber(Interpreter interpreter, string source, double expected)
        {
            var result = interpreter.Evaluate(source);
            if (result.Error != null)
                throw new InvalidOperationException("Number test failed: " + result.Error);
            double actual = Convert.ToDouble(result.Result);
            if (Math.Abs(actual - expected) > 0.0000001)
                throw new InvalidOperationException(
                    string.Format("Number test mismatch for {0}. Expected <{1}>, got <{2}>.", source, expected, result.Result));
        }

        private static void AssertError(Interpreter interpreter, string source, string message)
        {
            var result = interpreter.Evaluate(source);
            if (result.Error == null)
                throw new InvalidOperationException(message);
        }
    }
}
