using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;

using CommandLine;

using Pastel;

namespace PackageUninstaller
{
    internal static class Program
    {
        [Verb("list", HelpText = "Show installed packages.")]
        private class ListOptions
        {
            [Option("volume", Required = false, Default = "/",
                    HelpText = "Only show packages installed on this volume.")]
            public string Volume { get; set; }
        }

        [Verb("remove", HelpText = "Remove files and directories installed by packages.")]
        private class RemoveOptions
        {
            [Option("id", SetName = "Packages", Required = true,
                    HelpText = "Operate on these package IDs.")]
            public IEnumerable<string>? Packages { get; set; }

            [Option("regex", SetName = "SinglePackage", Required = true,
                    HelpText = "Or operate on packages matching a regex expression.")]
            public string? RegEx { get; set; }
        }

        [Verb("interactive", true, HelpText = "Enter keyboard interactive mode.")]
        private class InteractiveOptions { }

        internal static int Main(string[] args)
        {
            Parser.Default.ParseArguments<ListOptions, RemoveOptions, InteractiveOptions>(args)
                  .WithParsed<ListOptions>(ListPackages)
                  .WithParsed<RemoveOptions>(UninstallPackages)
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
            Console.WriteLine("Run 'pkguninst' for usage and help.");
        }

        private static void RunInteractively(InteractiveOptions options) { }

        private static void UninstallPackages(RemoveOptions options) { }

        private static void ListPackages(ListOptions options)
        {
            if (!options.Volume.StartsWith('/'))
            {
                options.Volume = "/Volumes/" + options.Volume;
            }

            if (!Directory.Exists(options.Volume))
            {
                Console.WriteLine(
                    "Volume is not mounted, has been removed, or its name is invalid.".Pastel(Color.OrangeRed));
                return;
            }

            var pkgutil = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "pkgutil",
                    Arguments = $"--pkgs --volume \"{options.Volume}\"",
                    RedirectStandardOutput = true,
                }
            };
            pkgutil.Start();

            var packages = new List<string>();
            var output = pkgutil.StandardOutput;
            while (!output.EndOfStream)
            {
                packages.Add(output.ReadLine());
            }

            packages.Sort();

            Console.WriteLine();
            Console.WriteLine("Volume:");
            Console.WriteLine(options.Volume.Pastel(Color.LightSeaGreen));
            if (packages.Count == 0)
            {
                Console.WriteLine("No packages found on specified volume.".Pastel(Color.OrangeRed));
                return;
            }

            Console.WriteLine("Installed Packages:");
            Console.WriteLine(string.Join(Environment.NewLine, packages).Pastel(Color.LightSeaGreen));
            Console.WriteLine();
        }
    }
}
