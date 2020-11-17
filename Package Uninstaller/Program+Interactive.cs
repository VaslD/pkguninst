using System;
using System.Drawing;
using System.IO;
using System.Linq;

using CommandLine;

using Pastel;

using Sharprompt;

namespace PackageUninstaller
{
    internal static partial class Program
    {
        [Verb("interactive", true, HelpText = "Enter keyboard interactive mode.")]
        private class InteractiveOptions
        {
            [Option('f', "force", Default = false,
                    HelpText = "Allow removing Apple-provided packages.")]
            public bool IsForced { get; set; }
        }

        private static string[] GetVolumes()
        {
            var path = new DirectoryInfo("/Volumes");
            if (!path.Exists) return Array.Empty<string>();

            return path.EnumerateDirectories().Select(x => x.FullName).ToArray();
        }

        private static string PromptForVolume(string[] volumes)
        {
            var volume = "/";
            Console.WriteLine();
            if (volumes.Length > 1)
            {
                Console.WriteLine("↑/↓ change selection | ←/→ page | ⏎ confirm".Pastel(Color.Gray));
                volume = Prompt.Select("Start by choosing a volume", volumes);
            }
            else if (volumes.Length == 1)
            {
                volume = volumes.First();
            }

            return volume;
        }

        private static void PromptForPackage(string volume, InteractiveOptions options)
        {
            Console.WriteLine();
            var packages = ListPackages(volume);

            Console.WriteLine("↑/↓ change selection | ←/→ page | ⏎ confirm".Pastel(Color.Gray));
            var package = Prompt.Select("Choose a package to remove", packages,
                                        5);

            if (package.StartsWith("com.apple.", StringComparison.OrdinalIgnoreCase))
            {
                if (!options.IsForced)
                {
                    Console.WriteLine(
                        "You cannot remove Apple-provided packages without 'force' flag.".Pastel(Color.OrangeRed));
                    return;
                }

                Console.WriteLine("Double check that you want to remove an Apple-provided package.");
                Console.WriteLine("This package may be required by macOS.");
                if (!Prompt.Confirm("Continue?")) return;
            }

            if (RemovePackage(package, true, false))
            {
                Console.WriteLine($"Package '{package}' was removed!".Pastel(Color.Green));
            }
            else
            {
                Console.WriteLine($"Package '{package}' was not removed!".Pastel(Color.OrangeRed));
            }
        }

        private static void RunInteractively(InteractiveOptions options)
        {
            if (getuid() != 0 && geteuid() != 0)
            {
                Console.WriteLine("Interactive mode must be started as root.".Pastel(Color.OrangeRed));
                return;
            }

            var volumes = GetVolumes();
            var volume = PromptForVolume(volumes);

            var shouldContinue = true;
            while (shouldContinue)
            {
                PromptForPackage(volume, options);

                Console.WriteLine();
                var nextStep = Prompt.Select("What would you like to do next",
                                             new[]
                                             {
                                                 "1. Change volume to operate on.",
                                                 "2. Remove another package.",
                                                 "3. Exit"
                                             });
                switch (nextStep[0])
                {
                case '1':
                {
                    volume = PromptForVolume(volumes);
                    break;
                }
                case '2':
                {
                    continue;
                }
                case '3':
                {
                    shouldContinue = false;
                    break;
                }
                }
            }
        }
    }
}
