using System;
using System.Diagnostics;

namespace PackageUninstaller
{
    internal static partial class Program
    {
        private interface IVolumeBasedOptions
        {
            public string Volume { get; set; }
        }

        private static string GetFirstDirectory(string path)
        {
            var separatorIndex = path.IndexOf('/');
            return separatorIndex <= 0 ? string.Empty : path.Substring(0, separatorIndex);
        }

        private static bool IsRootedInApplication(string path)
        {
            var firstDirectory = GetFirstDirectory(path);
            if (string.IsNullOrEmpty(firstDirectory)) return false;
            return firstDirectory.EndsWith(".app", StringComparison.OrdinalIgnoreCase);
        }
    }
}
