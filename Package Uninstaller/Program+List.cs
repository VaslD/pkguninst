using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;

using CommandLine;

using Pastel;

namespace PackageUninstaller
{
    internal static partial class Program
    {
        [Verb("list", HelpText = "Show installed packages.")]
        private class ListOptions : IVolumeBasedOptions
        {
            [Option("volume", Required = false, Default = "/",
                    HelpText = "Only show packages installed on this volume.")]
            public string Volume { get; set; }
        }

        private static string[] ListPackages(string volume)
        {
            var pkgutil = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "pkgutil",
                    Arguments = $"--pkgs --volume \"{volume}\"",
                    RedirectStandardOutput = true,
                }
            };
            pkgutil.Start();

            var packages = new List<string>();
            var output = pkgutil.StandardOutput;
            while (!output.EndOfStream)
            {
                var line = output.ReadLine();
                if (string.IsNullOrWhiteSpace(line)) continue;

                packages.Add(line);
            }

            packages.Sort();
            return packages.ToArray();
        }

        private static void PrefixVolume(IVolumeBasedOptions options)
        {
            if (!options.Volume.StartsWith('/'))
            {
                options.Volume = "/Volumes/" + options.Volume;
            }

            if (!Directory.Exists(options.Volume))
            {
                throw new InvalidOperationException();
            }
        }

        private static void ListPackages(ListOptions options)
        {
            try
            {
                PrefixVolume(options);
            }
            catch (InvalidOperationException)
            {
                Console.WriteLine("Volume is not mounted, or its name is invalid.".Pastel(Color.OrangeRed));
                return;
            }

            var packages = ListPackages(options.Volume);

            Console.WriteLine();
            Console.WriteLine("Volume:");
            Console.WriteLine(options.Volume.Pastel(Color.LightSeaGreen));
            if (packages.Length == 0)
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
