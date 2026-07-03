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
    }
}
