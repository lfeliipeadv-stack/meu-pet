namespace DesktopPet
{
    /// <summary>
    /// Online addresses used by the application, all in one place.
    /// Updates and the pet gallery come only from <see cref="RepoUrl"/>.
    /// </summary>
    internal static class AppInfo
    {
        /// <summary>GitHub owner of the repository.</summary>
        public const string RepoOwner = "lfeliipeadv-stack";
        /// <summary>GitHub repository.</summary>
        public const string RepoName = "meu-pet";
        /// <summary>Branch holding the pet gallery (Pets folder).</summary>
        public const string Branch = "master";

        /// <summary>Web page of the repository.</summary>
        public const string RepoUrl = "https://github.com/" + RepoOwner + "/" + RepoName;
        /// <summary>Pet gallery files (pets.json, folder/animations.xml, folder/icon.png …).</summary>
        public const string PetsRawUrl = "https://raw.githubusercontent.com/" + RepoOwner + "/" + RepoName + "/" + Branch + "/Pets/";
        /// <summary>Pet gallery as a web page.</summary>
        public const string PetsWebUrl = RepoUrl + "/tree/" + Branch + "/Pets";
        /// <summary>GitHub API: latest release.</summary>
        public const string LatestReleaseApi = "https://api.github.com/repos/" + RepoOwner + "/" + RepoName + "/releases/latest";
        /// <summary>Help page.</summary>
        public const string HelpUrl = RepoUrl + "#readme";
    }
}
