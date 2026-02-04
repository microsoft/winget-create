// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCore.Common
{
    /// <summary>
    /// Constants used in this library.
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// Microsoft copyright statement.
        /// </summary>
        public const string MicrosoftCopyright = "Copyright (c) Microsoft Corporation. All rights reserved.";

        /// <summary>
        /// Root directory of the winget-pkgs manifests repository.
        /// </summary>
        public const string WingetManifestRoot = "manifests";

        /// <summary>
        /// Root directory of the winget-pkgs fonts repository.
        /// </summary>
        public const string WingetFontRoot = "fonts";

        /// <summary>
        /// Client Id for the WingetCreate GitHub OAuth app.
        /// </summary>
        public const string GitHubOAuthClientId = "7799527e58dca9b4d33c";

        /// <summary>
        /// App Id for the Winget-Create GitHub App.
        /// </summary>
        public const int GitHubAppId = 965520;

        /// <summary>
        /// Link to the GitHub releases page for the winget-create tool.
        /// </summary>
        public const string GitHubReleasesUrl = "https://github.com/microsoft/winget-create/releases";

        /// <summary>
        /// Program name of the app.
        /// </summary>
        public const string ProgramName = "wingetcreate";

        /// <summary>
        /// Link to the privacy statement for the winget-create tool.
        /// </summary>
        public const string PrivacyStatementUrl = "https://aka.ms/winget-create-privacy";

        /// <summary>
        /// Link to the license for the winget-create tool.
        /// </summary>
        public const string LicenseUrl = "https://aka.ms/winget-create-license";

        /// <summary>
        /// Link to the notice file containing third party attributions for the winget-create tool.
        /// </summary>
        public const string ThirdPartyNoticeUrl = "https://aka.ms/winget-create-3rdPartyNotice";

        /// <summary>
        /// Link to the GitHub repository for the winget-create tool.
        /// </summary>
        public const string HomePageUrl = "https://aka.ms/winget-create";

        /// <summary>
        /// Represents the subdirectory name of the user's local app data folder where the tool stores its debug data.
        /// </summary>
        public const string DiagnosticOutputDirectoryFolderName = "DiagOutputDir";

        /// <summary>
        /// The url path to the manifest documentation site.
        /// </summary>
        public const string ManifestDocumentationUrl = "https://aka.ms/winget-manifest-schema";
    }
}
