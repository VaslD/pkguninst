using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using CommandLine;

using Pastel;

using Sharprompt;

namespace PackageUninstaller
{
    internal static partial class Program
    {
        [Verb("remove", HelpText = "Remove files and directories installed by packages.")]
        private class RemoveOptions : IVolumeBasedOptions
        {
            [Option("volume", Required = false, Default = "/",
                    HelpText = "Only remove packages from this volume.")]
            public string Volume { get; set; }

            [Option("id", SetName = "Packages", Required = true,
                    HelpText = "Operate on these package IDs.")]
            public IEnumerable<string>? Packages { get; set; }

            [Option("regex", SetName = "SinglePackage", Required = true,
                    HelpText = "Or operate on packages matching a regex expression.")]
            public string? RegEx { get; set; }

            [Option('f', "force", Default = false,
                    HelpText = "Do not check if a package is provided by Apple.")]
            public bool IsForced { get; set; }

            [Option('q', "quiet", Default = false,
                    HelpText = "Do not prompt for final user confirmation.")]
            public bool QuietRemoval { get; set; }
        }

        private static bool IsSIPEnabled()
        {
            var csrutil = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "csrutil",
                    Arguments = "status",
                    RedirectStandardOutput = true,
                }
            };
            csrutil.Start();

            var output = csrutil.StandardOutput;
            if (!output.EndOfStream)
            {
                var status = output.ReadLine();
                if (status?.StartsWith("System Integrity Protection", StringComparison.Ordinal) != true)
                {
                    return false;
                }

                return status.Contains("status: enabled", StringComparison.Ordinal);
            }

            return false;
        }

        #region Filter Packages

        private static string[] FilterPackages(string[] packages, string[] subset)
        {
            var filtered = new List<string>(subset.Length);
            foreach (var package in packages)
            {
                if (subset.Contains(package))
                {
                    filtered.Add(package);
                }
            }

            return filtered.ToArray();
        }

        private static string[] FilterPackages(string[] packages, Regex regex)
        {
            var filtered = new List<string>(packages.Length);
            foreach (var package in packages)
            {
                if (regex.IsMatch(package))
                {
                    filtered.Add(package);
                }
            }

            return filtered.ToArray();
        }

        #endregion

        #region Get Package Contents

        private static (string[] Files, string[] Directories) InspectPackage(string id)
        {
            var pkgutil = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "pkgutil",
                    Arguments = $"--only-files --files {id}",
                    RedirectStandardOutput = true,
                }
            };
            pkgutil.Start();

            var files = new List<string>();
            var output = pkgutil.StandardOutput;
            while (!output.EndOfStream)
            {
                var path = output.ReadLine();
                if (string.IsNullOrWhiteSpace(path)) continue;

                if (IsRootedInApplication(path))
                {
                    path = "/Applications/" + path;
                }
                else if (!Path.IsPathRooted(path))
                {
                    path = "/" + path;
                }

                if (File.Exists(path))
                {
                    files.Add(path);
                }
            }

            var filesArray = files.ToArray();
            Array.Sort(filesArray);

            pkgutil = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "pkgutil",
                    Arguments = $"--only-dirs --files {id}",
                    RedirectStandardOutput = true,
                }
            };
            pkgutil.Start();

            var dirs = new List<string>();
            output = pkgutil.StandardOutput;
            while (!output.EndOfStream)
            {
                var path = output.ReadLine();
                if (string.IsNullOrWhiteSpace(path)) continue;

                if (IsRootedInApplication(path))
                {
                    path = "/Applications/" + path;
                }
                else if (!Path.IsPathRooted(path))
                {
                    path = "/" + path;
                }

                if (Directory.Exists(path))
                {
                    dirs.Add(path);
                }
            }

            var dirsArray = dirs.ToArray();
            Array.Sort(dirsArray);
            Array.Reverse(dirsArray);

            return (filesArray, dirsArray);
        }

        #endregion

        #region Delete Files and Folders

        private static void RemoveFiles(string id, string[] paths)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "Package Uninstaller", id);
            Directory.CreateDirectory(tempDir);

            var moved = new Dictionary<string, string>();
            try
            {
                foreach (var path in paths)
                {
                    var fromPath = path;
                    var toPath = Path.Combine(tempDir, fromPath.TrimStart('/'));
                    try
                    {
                        var parent = Path.GetDirectoryName(toPath);
                        if (!Directory.Exists(parent)) Directory.CreateDirectory(parent);
                    }
                    catch
                    {
                        // Ignored.
                    }

                    File.Move(fromPath, toPath);
                    moved[toPath] = fromPath;
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine();
                Console.WriteLine(exception.Message.Pastel(Color.OrangeRed));
                Console.WriteLine();

                if (exception is DirectoryNotFoundException)
                {
                    Console.WriteLine("This is likely a bug, please file a report to @VaslD.".Pastel(Color.Cyan));
                    Console.WriteLine();
                }

                Console.WriteLine("Error encountered, restoring deleted files...".Pastel(Color.Orange));
                var failedCount = 0;
                foreach (var (key, value) in moved)
                {
                    try
                    {
                        File.Move(key, value);
                    }
                    catch
                    {
                        failedCount += 1;
                    }
                }

                if (failedCount > 0)
                {
                    Console.WriteLine($"{failedCount} files cannot be restored.");
                    Console.WriteLine($"Backup saved at: {tempDir}".Pastel(Color.Orange));
                    Console.WriteLine();
                }
                else
                {
                    try
                    {
                        Directory.Delete(tempDir, true);
                    }
                    catch
                    {
                        // Ignored.
                    }
                }

                throw;
            }

            try
            {
                Directory.Delete(tempDir, true);
            }
            catch
            {
                // Ignored.
            }
        }

        private static void RemoveDirectories(string[] directories)
        {
            var failed = new List<string>();
            foreach (var directory in directories)
            {
                try
                {
                    Directory.Delete(directory, false);
                }
                catch
                {
                    failed.Add(directory);
                }
            }

            Console.WriteLine();
            Console.WriteLine("These directories are not removed:");
            Console.WriteLine(string.Join(Environment.NewLine, failed).Pastel(Color.Orange));
            Console.WriteLine("This is not an error because they may be used by other packages.");
            Console.WriteLine();
        }

        private static void RemoveReceipt(string id)
        {
            var pkgutil = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "pkgutil",
                    Arguments = $"--forget {id}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                }
            };
            pkgutil.Start();
            pkgutil.WaitForExit();

            if (pkgutil.ExitCode == 0) return;
            Console.WriteLine($"Cannot unregister receipt for '{id}'.".Pastel(Color.Orange));
            Console.WriteLine("All files installed by this package are deleted.");
        }

        private static bool RemovePackage(string id, bool forced, bool quietly)
        {
            if (id.StartsWith("com.apple.pkg.", StringComparison.OrdinalIgnoreCase) &&
                !forced)
            {
                Console.WriteLine();
                Console.WriteLine("This package was added by Apple or macOS installer.");
                Console.WriteLine("With SIP disabled, you must remove it with 'force' flag.".Pastel(Color.OrangeRed));
                return false;
            }

            Console.WriteLine();
            Console.WriteLine($"Removing package '{id}'...");

            var (files, folders) = InspectPackage(id);
            if (files.Length > 0)
            {
                Console.WriteLine();
                Console.WriteLine(string.Join(Environment.NewLine, files));
                Console.WriteLine($"{files.Length} file(s) will be permanently deleted!".Pastel(Color.OrangeRed));
                Console.WriteLine();

                var shouldContinue = quietly || Prompt.Confirm("Continue?");
                if (!shouldContinue) return false;

                try
                {
                    RemoveFiles(id, files);
                }
                catch (UnauthorizedAccessException)
                {
                    Console.WriteLine();
                    Console.WriteLine("You must be root to remove this package!".Pastel(Color.OrangeRed));
                    return false;
                }
            }

            if (folders.Length > 0)
            {
                Console.WriteLine();
                Console.WriteLine(string.Join(Environment.NewLine, folders));
                Console.WriteLine(
                    $"{folders.Length} folder(s) will be permanently deleted when empty!".Pastel(Color.OrangeRed));
                Console.WriteLine();

                var shouldContinue = quietly || Prompt.Confirm("Continue?");
                if (!shouldContinue) return false;

                try
                {
                    RemoveDirectories(folders);
                }
                catch (UnauthorizedAccessException)
                {
                    Console.WriteLine();
                    Console.WriteLine("You must be root to remove this package!".Pastel(Color.OrangeRed));
                    return false;
                }
            }

            RemoveReceipt(id);

            return true;
        }

        #endregion

        private static void RemovePackages(RemoveOptions options)
        {
            if (getuid() != 0 && geteuid() != 0)
            {
                Console.WriteLine("It is recommended to run this tool as root!".Pastel(Color.OrangeRed));
            }

            var sip = IsSIPEnabled();
            if (sip)
            {
                options.IsForced = true;
                Console.WriteLine();
                Console.WriteLine("System Integrity Protection is enabled.".Pastel(Color.Green));
                Console.WriteLine("Critical packages bundled with macOS cannot be removed.");
                Console.WriteLine("You may remove Apple-provided packages without using 'force' flag.");
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine("System Integrity Protection is disabled.".Pastel(Color.OrangeRed));
                Console.WriteLine("Critical packages bundled with macOS can now be removed.");
                Console.WriteLine("You must supply 'force' flag to remove Apple-provided packages.");
                Console.WriteLine();
            }

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
            if (options.Packages != null && options.Packages.Any())
            {
                packages = FilterPackages(packages, options.Packages.ToArray());
            }
            else if (!string.IsNullOrEmpty(options.RegEx))
            {
                try
                {
                    var regex = new Regex(options.RegEx);
                    packages = FilterPackages(packages, regex);
                }
                catch (ArgumentException)
                {
                    Console.WriteLine("RegEx is invalid.".Pastel(Color.OrangeRed));
                    return;
                }
            }
            else
            {
                Console.WriteLine(
                    "You must supply at least one package ID, or a single regex.".Pastel(Color.OrangeRed));
                return;
            }

            if (packages.Length == 0)
            {
                Console.WriteLine("No packages found matching given conditions.".Pastel(Color.Orange));
                return;
            }

            Console.WriteLine();
            Console.WriteLine(string.Join(Environment.NewLine, packages));
            Console.WriteLine($"{packages.Length} package(s) will be removed.".Pastel(Color.Orange));
            Console.WriteLine();

            var shouldContinue = options.QuietRemoval || Prompt.Confirm("Continue?", true);
            if (!shouldContinue)
            {
                Console.WriteLine("User cancelled removal operation.");
                return;
            }

            foreach (var package in packages)
            {
                if (RemovePackage(package, options.IsForced, options.QuietRemoval))
                {
                    Console.WriteLine($"Package '{package}' was removed!".Pastel(Color.Green));
                }
                else
                {
                    Console.WriteLine($"Package '{package}' was not removed!".Pastel(Color.OrangeRed));
                }
            }
        }
    }
}
