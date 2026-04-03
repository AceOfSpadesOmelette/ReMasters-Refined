using System;
using System.Linq;

namespace ReMastersLib
{
    public class GameVersions
    {
        private readonly string[] Versions;
        private readonly string CurrentVersion;

        public GameVersions(string[] versionsList, string currentVersion)
        {
            Versions = versionsList;
            CurrentVersion = String.IsNullOrEmpty(currentVersion) ? Versions.Last() : currentVersion;
        }

        public string Current()
        {
            return CurrentVersion;
        }

        public string Previous()
        {
            return Previous(CurrentVersion);
        }

        public string Previous(string version)
        {
            return Versions.ElementAtOrDefault(Array.IndexOf(Versions, version) - 1);
        }
    }
}