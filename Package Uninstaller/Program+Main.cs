using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;

using CommandLine;

using Pastel;

namespace PackageUninstaller
{
    internal static partial class Program
    {
        [DllImport("libc")]
        public static extern uint getuid();

        [DllImport("libc")]
        public static extern uint geteuid();

        internal static int Main(string[] args)
        {
            Parser.Default.ParseArguments<ListOptions, RemoveOptions, InteractiveOptions>(args)
                  .WithParsed<ListOptions>(ListPackages)
                  .WithParsed<RemoveOptions>(RemovePackages)
                  .WithParsed<InteractiveOptions>(RunInteractively)
                  .WithNotParsed(ReportError);
            return 0;
        }

        private static void ReportError(IEnumerable<Error> errors)
        {
            var firstError = errors.FirstOrDefault()?.Tag;
            if (firstError == ErrorType.HelpRequestedError ||
                firstError == ErrorType.HelpVerbRequestedError ||
                firstError == ErrorType.VersionRequestedError)
            {
                return;
            }

            Console.WriteLine(string.Join(", ", errors).Pastel(Color.OrangeRed));
            Console.WriteLine();
            Console.WriteLine("Run this tool with no argument for usage.");
        }
    }
}
