﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Globalization;
using System.IO;

namespace Microsoft.Coyote.IO
{
    /// <summary>
    /// Static class implementing error reporting methods.
    /// </summary>
    internal static class Error
    {
        /// <summary>
        /// If you play with Console.ForegroundColor then you should grab this lock in order
        /// to avoid color leakage (wrong color becomes set permanently).
        /// </summary>
        public static readonly object ColorLock = new object();

        /// <summary>
        /// Reports a generic error to the user.
        /// </summary>
        public static void Report(string format, params object[] args)
        {
            string message = string.Format(CultureInfo.InvariantCulture, format, args);
            Write(ConsoleColor.Red, "Error: ");
            Write(ConsoleColor.Yellow, message);
            Console.Error.WriteLine(string.Empty);
        }

        /// <summary>
        ///  Writes the specified string value to the output stream.
        /// </summary>
        private static void Write(ConsoleColor color, string value)
        {
            Write(Console.Error, color, value);
        }

        /// <summary>
        /// Writes with console color to the specified TextWriter.
        /// </summary>
        internal static void Write(TextWriter logger, ConsoleColor color, string value)
        {
            lock (ColorLock)
            {
                try
                {
                    Console.ForegroundColor = color;
                    logger.Write(value);
                }
                finally
                {
                    Console.ResetColor();
                }
            }
        }
    }
}
