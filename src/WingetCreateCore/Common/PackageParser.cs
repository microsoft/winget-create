// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCore
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Net.Http;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using System.Xml;
    using System.Xml.Linq;
    using AsmResolver.PE;
    using AsmResolver.PE.Win32Resources;
    using AsmResolver.PE.Win32Resources.Version;
    using Microsoft.WingetCreateCore.Common;
    using Microsoft.WingetCreateCore.Common.Exceptions;
    using Microsoft.WingetCreateCore.Common.Msi;
    using Microsoft.WingetCreateCore.Models;
    using Microsoft.WingetCreateCore.Models.DefaultLocale;
    using Microsoft.WingetCreateCore.Models.Installer;
    using Microsoft.WingetCreateCore.Models.Version;
    using Newtonsoft.Json;

    /// <summary>
    /// Provides functionality for a parsing and extracting relevant metadata from a given package.
    /// </summary>
    public static class PackageParser
    {
        /// <summary>
        /// The default path where downloaded installers are stored.
        /// </summary>
        public static readonly string DefaultInstallerDownloadPath = Path.Combine(Path.GetTempPath(), Constants.ProgramName);

        private const string InvalidCharacters = "©|®";

        private static readonly string[] KnownInstallerResourceNames = new[]
        {
            "inno",
            "nullsoft",
        };

        private static HttpClient httpClient = new HttpClient();

        private enum MachineType
        {
            X86 = 0x014c,
            X64 = 0x8664,
            Arm = 0x01c0,
            Armv7 = 0x01c4,
            Arm64 = 0xaa64,
        }

        private enum CompatibilitySet
        {
            None,
            Exe,
            Msi,
            Msix,
        }

        /// <summary>
        /// Gets or sets the path in the %TEMP% directory where installers are downloaded to.
        /// </summary>
        public static string InstallerDownloadPath { get; set; } = DefaultInstallerDownloadPath;

        /// <summary>
        /// Sets the HttpMessageHandler used for the static HttpClient.
        /// </summary>
        /// <param name="httpMessageHandler">Optional HttpMessageHandler to override default HttpClient behavior.</param>
        public static void SetHttpMessageHandler(HttpMessageHandler httpMessageHandler)
        {
            httpClient.Dispose();
            httpClient = httpMessageHandler != null ? new HttpClient(httpMessageHandler) : new HttpClient();
        }

        /// <summary>
        /// Parses packages for available metadata including Version, Publisher, Name, Description, License, etc.
        /// </summary>
        /// <param name="installerMetadataList">List of <see cref="InstallerMetadata"/>.</param>
        /// <param name="manifests">Wrapper object for manifest object models.</param>
        public static void ParsePackages(List<InstallerMetadata> installerMetadataList, Manifests manifests)
        {
            manifests.VersionManifest = new VersionManifest();
            manifests.InstallerManifest = new InstallerManifest();
            manifests.DefaultLocaleManifest = new DefaultLocaleManifest();
            List<string> parseFailedInstallerUrls = new List<string>();

            foreach (var installerMetadata in installerMetadataList)
            {
                if (!ParsePackage(installerMetadata, manifests))
                {
                    parseFailedInstallerUrls.Add(installerMetadata.InstallerUrl);
                }
            }

            if (parseFailedInstallerUrls.Any())
            {
                throw new ParsePackageException(parseFailedInstallerUrls);
            }
        }

        /// <summary>
        /// Download file at specified URL to temp directory, unless it's already present.
        /// </summary>
        /// <param name="url">The URL of the file to be downloaded.</param>
        /// <param name="maxDownloadSize">The maximum file size in bytes to download.</param>
        /// <returns>Path of downloaded, or previously downloaded, file.</returns>
        public static async Task<string> DownloadFileAsync(string url, long? maxDownloadSize = null)
        {
            var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);

            int redirectCount = 0;
            while (response.StatusCode == System.Net.HttpStatusCode.Redirect && redirectCount < 2)
            {
                var redirectUri = response.Headers.Location;
                response = await httpClient.GetAsync(redirectUri, HttpCompletionOption.ResponseHeadersRead);
                redirectCount++;
            }

            if (!response.IsSuccessStatusCode)
            {
                string message = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException(message, null, response.StatusCode);
            }

            long? downloadSize = response.Content.Headers.ContentLength;

            if (downloadSize > maxDownloadSize)
            {
                throw new DownloadSizeExceededException(maxDownloadSize.Value);
            }

            string urlFile = Path.GetFileName(url.Split('?').Last());
            string contentDispositionFile = response.Content.Headers.ContentDisposition?.FileName?.Trim('"');
            string requestUrlFileName = Path.GetFileName(response.RequestMessage?.RequestUri?.ToString());

            if (!Directory.Exists(InstallerDownloadPath))
            {
                Directory.CreateDirectory(InstallerDownloadPath);
            }

            // If no relevant filename can be obtained for the installer download, use a temporary filename as last option.
            string targetFileName = contentDispositionFile.NullIfEmpty() ?? urlFile.NullIfEmpty() ?? requestUrlFileName.NullIfEmpty() ?? Path.GetTempFileName();
            string targetFile = GetNumericFilename(Path.Combine(InstallerDownloadPath, targetFileName));
            using var targetFileStream = File.OpenWrite(targetFile);
            var contentStream = await response.Content.ReadAsStreamAsync();

            /*
             * There seems to be a difference between the test environments and a users environment
             * On the users environment, the stream cannot seek and will always have position 0 when
             * the response content is read into it. In the unit test environment, the stream can
             * seek and will not reset the position when the response content is read in. This logic
             * should always ensure that the position is reset to 0 regardless of the environment.
            */
            if (contentStream.CanSeek)
            {
                contentStream.Position = 0;
            }

            await contentStream.CopyToAsync(targetFileStream);

            return targetFile;
        }

        /// <summary>
        /// When creating a file, inserts a number into the desired filename when other files
        /// with the same name exist in the target directory.
        /// </summary>
        /// <param name="desiredPath">The path where the new file would be created.</param>
        /// <returns>The path where the new file should be created.</returns>
        public static string GetNumericFilename(string desiredPath)
        {
            string fileName = Path.GetFileNameWithoutExtension(desiredPath);
            string fileExt = Path.GetExtension(desiredPath);
            string fileDir = Path.GetDirectoryName(desiredPath);
            DirectoryInfo dir = new DirectoryInfo(fileDir);
            FileInfo[] existingFiles = dir.GetFiles(fileName + "*" + fileExt);
            return existingFiles.Length == 0 ? Path.Combine(fileDir, fileName + fileExt) : Path.Combine(fileDir, fileName + " (" + existingFiles.Length.ToString() + ")" + fileExt);
        }

        /// <summary>
        /// Computes the SHA256 hash of a file given its file path.
        /// </summary>
        /// <param name="path">Path to file to be hashed.</param>
        /// <returns>The computed SHA256 hash string.</returns>
        public static string GetFileHash(string path)
        {
            using Stream stream = File.OpenRead(path);
            using var hasher = SHA256.Create();
            return BitConverter.ToString(hasher.ComputeHash(stream)).Remove("-");
        }

        /// <summary>
        /// Update InstallerManifest's Installer nodes using the provided list of InstallerMetadata objects.
        /// </summary>
        /// <param name="installerMetadataList">List of <see cref="InstallerMetadata"/>.</param>
        /// <param name="installerManifest"><see cref="InstallerManifest"/> to update.</param>
        public static void UpdateInstallerNodesAsync(List<InstallerMetadata> installerMetadataList, InstallerManifest installerManifest)
        {
            var existingInstallers = new List<Installer>(installerManifest.Installers);
            List<Installer> unmatchedInstallers = new List<Installer>();
            List<Installer> multipleMatchedInstallers = new List<Installer>();
            List<string> parseFailedInstallerUrls = new List<string>();

            foreach (var installerMetadata in installerMetadataList)
            {
                if (!ParsePackageAndGenerateInstallerNodes(installerMetadata, null))
                {
                    parseFailedInstallerUrls.Add(installerMetadata.InstallerUrl);
                }

                // In case of multiple nested installers in the archive, we expect the new installers to have duplicates
                // Remove these duplicates to avoid multiple matches.
                installerMetadata.NewInstallers = installerMetadata.NewInstallers.Distinct().ToList();
            }

            int numOfNewInstallers = installerMetadataList.Sum(x => x.NewInstallers.Count);

            if (parseFailedInstallerUrls.Any())
            {
                throw new ParsePackageException(parseFailedInstallerUrls);
            }

            // We only allow updating manifests with the same package count
            if (numOfNewInstallers != existingInstallers.Count)
            {
                throw new InvalidOperationException();
            }

            Dictionary<Installer, Installer> installerMatchDict = new Dictionary<Installer, Installer>();

            // Update previous installers with parsed data from downloaded packages
            foreach (var installerUpdate in installerMetadataList)
            {
                foreach (var newInstaller in installerUpdate.NewInstallers)
                {
                    // Update DisplayVersion for each AppsAndFeaturesEntry
                    if (!string.IsNullOrEmpty(installerUpdate.DisplayVersion))
                    {
                        if (newInstaller.AppsAndFeaturesEntries != null)
                        {
                            newInstaller.AppsAndFeaturesEntries[0].DisplayVersion = installerUpdate.DisplayVersion;
                        }
                        else
                        {
                            newInstaller.AppsAndFeaturesEntries = new List<AppsAndFeaturesEntry>
                            {
                                new AppsAndFeaturesEntry
                                {
                                    DisplayVersion = installerUpdate.DisplayVersion,
                                },
                            };
                        }
                    }

                    // if the installerUpdate does not have a binary or url architecture specified, then just use what is specified in the installer.
                    Installer existingInstallerMatch = FindInstallerMatch(
                        newInstaller,
                        existingInstallers,
                        installerManifest,
                        installerUpdate,
                        ref unmatchedInstallers,
                        ref multipleMatchedInstallers);

                    // If a match is found, add match to dictionary and remove for list of existingInstallers
                    if (existingInstallerMatch != null)
                    {
                        // Remove the nested installers from the new installer that are not present in the existing installer.
                        if (newInstaller.NestedInstallerFiles != null && existingInstallerMatch.NestedInstallerFiles != null)
                        {
                            var matchedFiles = newInstaller.NestedInstallerFiles
                                .Where(nif =>
                                {
                                    var fileName = Path.GetFileName(nif.RelativeFilePath);

                                    // If the flow reaches here, there's guaranteed to be a matching file name
                                    // Any mismatches would've been detected earlier in the update flow.
                                    return existingInstallerMatch.NestedInstallerFiles.Any(eif =>
                                        Path.GetFileName(eif.RelativeFilePath) == fileName);
                                })
                                .ToList();

                            newInstaller.NestedInstallerFiles = matchedFiles;
                        }

                        installerMatchDict.Add(existingInstallerMatch, newInstaller);
                        existingInstallers.Remove(existingInstallerMatch);
                    }
                    else
                    {
                        continue;
                    }
                }
            }

            if (unmatchedInstallers.Any() || multipleMatchedInstallers.Any())
            {
                bool isArchitectureOverride = installerMetadataList.Any(x => x.OverrideArchitecture.HasValue);
                throw new InstallerMatchException(multipleMatchedInstallers, unmatchedInstallers, isArchitectureOverride);
            }
            else
            {
                foreach (var existingInstaller in installerMatchDict.Keys)
                {
                    UpdateInstallerMetadata(existingInstaller, installerMatchDict[existingInstaller]);
                }
            }
        }

        /// <summary>
        /// Parses the package for relevant metadata and and updates the metadata of the provided installer node.
        /// </summary>
        /// <param name="installer">Installer node.</param>
        /// <param name="filePath">Path to package file.</param>
        /// <param name="url">Installer url.</param>
        /// <param name="archivePath">Path to archive file containing the installer. Required if the installer type is Zip.</param>
        /// <returns>Boolean indicating whether the package parse was successful.</returns>
        public static bool ParsePackageAndUpdateInstallerNode(Installer installer, string filePath, string url, string archivePath = null)
        {
            // Guard clause to ensure that the archivePath is provided if the installer type is Zip.
            if (installer.InstallerType == InstallerType.Zip && string.IsNullOrEmpty(archivePath))
            {
                return false;
            }

            List<Installer> newInstallers = new List<Installer>();
            bool parseResult = ParseExeInstallerType(filePath, installer, newInstallers) ||
                ParseMsix(filePath, installer, null, newInstallers) ||
                ParseMsi(filePath, installer, null, newInstallers);

            if (!parseResult || !newInstallers.Any())
            {
                return false;
            }

            Installer newInstaller = newInstallers.First();

            if (newInstallers.Count > 1)
            {
                // For multiple installers in an AppxBundle, use the existing architecture to avoid matching conflicts.
                newInstaller.Architecture = installer.Architecture;
            }
            else
            {
                // For a single installer, detect the architecture. If no architecture is detected, default to architecture from existing manifest.
                newInstaller.Architecture = GetArchFromUrl(url) ?? GetMachineType(filePath)?.ToString().ToEnumOrDefault<Architecture>() ?? installer.Architecture;
            }

            newInstaller.InstallerUrl = url;
            newInstaller.InstallerSha256 = string.IsNullOrEmpty(archivePath) ? GetFileHash(filePath) : GetFileHash(archivePath);
            UpdateInstallerMetadata(installer, newInstallers.First());
            return true;
        }

        /// <summary>
        /// Creates a new Installer object that is a copy of the provided Installer.
        /// </summary>
        /// <param name="installer">Installer object to be cloned.</param>
        /// <returns>A new cloned Installer object.</returns>
        public static Installer CloneInstaller(Installer installer)
        {
            string json = JsonConvert.SerializeObject(installer);
            return JsonConvert.DeserializeObject<Installer>(json);
        }

        /// <summary>
        /// Finds an existing installer that matches the new installer by checking the installerType and the following:
        /// 1. Matching based on architecture specified as an override if present.
        /// 2. Matching based on architecture detected from URL string if present.
        /// 3. If no singular match is found based on architecture, use scope to narrow down the match results if a scope override is present.
        /// </summary>
        /// <param name="newInstaller">New installer to be matched.</param>
        /// <param name="existingInstallers">List of existing installers to be matched.</param>
        /// <param name="installerManifest">Installer manifest.</param>
        /// <param name="installerMetadata">Helper class for storing an installer's metadata information.</param>
        /// <param name="unmatchedInstallers">List of unmatched installers.</param>
        /// <param name="multipleMatchedInstallers">List of installers with multiple matches..</param>
        /// <returns>The installer match from the list of existing installers.</returns>
        private static Installer FindInstallerMatch(
            Installer newInstaller,
            List<Installer> existingInstallers,
            InstallerManifest installerManifest,
            InstallerMetadata installerMetadata,
            ref List<Installer> unmatchedInstallers,
            ref List<Installer> multipleMatchedInstallers)
        {
            // If we can find an exact match by comparing the installerType and the override architecture, then return match.
            // Otherwise, continue and try matching based on arch detected from url and binary detection, as the user could be trying overwrite with a new architecture.
            var installerTypeMatches = existingInstallers.Where(
                i => (i.InstallerType ?? installerManifest.InstallerType) == newInstaller.InstallerType);

            // If there are no exact installerType matches, check if there is a compatible installerType that can be matched.
            if (!installerTypeMatches.Any())
            {
                installerTypeMatches = existingInstallers.Where(
                i => IsCompatibleInstallerType(i.InstallerType ?? installerManifest.InstallerType, newInstaller.InstallerType));
            }

            // Match installers using the installer architecture with the following priority: OverrideArchitecture > UrlArchitecture > BinaryArchitecture.
            IEnumerable<Installer> architectureMatches;
            if (installerMetadata.OverrideArchitecture.HasValue)
            {
                architectureMatches = installerTypeMatches.Where(i => i.Architecture == installerMetadata.OverrideArchitecture);
            }
            else
            {
                if (installerMetadata.UrlArchitecture.HasValue)
                {
                    architectureMatches = installerTypeMatches.Where(i => i.Architecture == installerMetadata.UrlArchitecture);
                }
                else
                {
                    var binaryArchitecture = installerMetadata.BinaryArchitecture ?? newInstaller.Architecture;
                    architectureMatches = installerTypeMatches.Where(i => i.Architecture == binaryArchitecture);
                }
            }

            int architectureMatchesCount = architectureMatches.Count();
            if (architectureMatchesCount == 1)
            {
                return architectureMatches.Single();
            }
            else if (architectureMatchesCount == 0)
            {
                unmatchedInstallers.Add(newInstaller);
            }
            else
            {
                // If there are multiple architecture matches, use scope to further narrow down the matches (if present).
                IEnumerable<Installer> scopeMatches;
                if (installerMetadata.OverrideScope.HasValue)
                {
                    scopeMatches = architectureMatches.Where(i => i.Scope == installerMetadata.OverrideScope);
                }
                else
                {
                    scopeMatches = architectureMatches;
                }

                int scopeMatchesCount = scopeMatches.Count();
                if (scopeMatchesCount == 1)
                {
                    return scopeMatches.Single();
                }
                else if (scopeMatchesCount == 0)
                {
                    unmatchedInstallers.Add(newInstaller);
                }
                else
                {
                    multipleMatchedInstallers.Add(newInstaller);
                }
            }

            return null;
        }

        /// <summary>
        /// Updates the metadata from an existing installer node with the metadata from a new installer node.
        /// </summary>
        /// <param name="existingInstaller">Existing installer node.</param>
        /// <param name="newInstaller">New installer node.</param>
        private static void UpdateInstallerMetadata(Installer existingInstaller, Installer newInstaller)
        {
            existingInstaller.Architecture = newInstaller.Architecture;
            existingInstaller.InstallerUrl = newInstaller.InstallerUrl;
            existingInstaller.InstallerSha256 = newInstaller.InstallerSha256;
            existingInstaller.SignatureSha256 = newInstaller.SignatureSha256;

            // If the newInstaller field value is null, we default to using the existingInstaller field value.
            existingInstaller.ProductCode = newInstaller.ProductCode ?? existingInstaller.ProductCode;
            existingInstaller.MinimumOSVersion = newInstaller.MinimumOSVersion ?? existingInstaller.MinimumOSVersion;
            existingInstaller.PackageFamilyName = newInstaller.PackageFamilyName ?? existingInstaller.PackageFamilyName;
            existingInstaller.NestedInstallerFiles = newInstaller.NestedInstallerFiles ?? existingInstaller.NestedInstallerFiles;
            existingInstaller.Platform = newInstaller.Platform ?? existingInstaller.Platform;

            if (existingInstaller.AppsAndFeaturesEntries != null && newInstaller.AppsAndFeaturesEntries != null)
            {
                foreach (var existingAppsAndFeaturesEntry in existingInstaller.AppsAndFeaturesEntries)
                {
                    if (existingAppsAndFeaturesEntry.DisplayName != null && newInstaller.AppsAndFeaturesEntries.FirstOrDefault().DisplayName != null)
                    {
                        existingAppsAndFeaturesEntry.DisplayName = newInstaller.AppsAndFeaturesEntries.FirstOrDefault().DisplayName;
                    }

                    if (existingAppsAndFeaturesEntry.DisplayVersion != null && newInstaller.AppsAndFeaturesEntries.FirstOrDefault().DisplayVersion != null)
                    {
                        existingAppsAndFeaturesEntry.DisplayVersion = newInstaller.AppsAndFeaturesEntries.FirstOrDefault().DisplayVersion;
                    }

                    if (existingAppsAndFeaturesEntry.Publisher != null && newInstaller.AppsAndFeaturesEntries.FirstOrDefault().Publisher != null)
                    {
                        existingAppsAndFeaturesEntry.Publisher = newInstaller.AppsAndFeaturesEntries.FirstOrDefault().Publisher;
                    }

                    if (existingAppsAndFeaturesEntry.ProductCode != null && newInstaller.AppsAndFeaturesEntries.FirstOrDefault().ProductCode != null)
                    {
                        existingAppsAndFeaturesEntry.ProductCode = newInstaller.AppsAndFeaturesEntries.FirstOrDefault().ProductCode;
                    }

                    if (existingAppsAndFeaturesEntry.UpgradeCode != null && newInstaller.AppsAndFeaturesEntries.FirstOrDefault().UpgradeCode != null)
                    {
                        existingAppsAndFeaturesEntry.UpgradeCode = newInstaller.AppsAndFeaturesEntries.FirstOrDefault().UpgradeCode;
                    }

                    // Break on first match to avoid setting DisplayVersion for all entries.
                    // We do not support updating multiple DisplayVersions under the same installer.
                    break;
                }
            }
        }

        /// <summary>
        /// Parses a package for available metadata including Version, Publisher, Name, Description, License, etc.
        /// </summary>
        /// <param name="installerMetadata">Helper class for storing an installer's metadata information.</param>
        /// <param name="manifests">Wrapper object for manifest object models.</param>
        /// <returns>True if package was successfully parsed and metadata extracted, false otherwise.</returns>
        private static bool ParsePackage(InstallerMetadata installerMetadata, Manifests manifests)
        {
            VersionManifest versionManifest = manifests.VersionManifest;
            InstallerManifest installerManifest = manifests.InstallerManifest;
            DefaultLocaleManifest defaultLocaleManifest = manifests.DefaultLocaleManifest;

            try
            {
                var versionInfoResource = VersionInfoResource.FromDirectory(PEImage.FromFile(installerMetadata.PackageFile).Resources);
                var versionInfo = versionInfoResource.GetChild<StringFileInfo>("StringFileInfo").Tables.First();

                defaultLocaleManifest.PackageVersion ??= versionInfo["FileVersion"]?.Trim() ?? versionInfo["ProductVersion"]?.Trim();
                defaultLocaleManifest.Publisher ??= versionInfo["CompanyName"]?.Trim();
                defaultLocaleManifest.PackageName ??= versionInfo["ProductName"]?.Trim();
                defaultLocaleManifest.ShortDescription ??= versionInfo["FileDescription"]?.Trim();
                defaultLocaleManifest.Description ??= versionInfo["Comments"]?.Trim();
                defaultLocaleManifest.Copyright ??= versionInfo["LegalCopyright"]?.Trim();
            }
            catch (Exception ex) when (ex is BadImageFormatException || ex is KeyNotFoundException)
            {
                // do nothing
            }

            if (ParsePackageAndGenerateInstallerNodes(installerMetadata, manifests))
            {
                // Add range of new installers generates from parsing the package.
                manifests.InstallerManifest.Installers.AddRange(installerMetadata.NewInstallers);

                if (!string.IsNullOrEmpty(defaultLocaleManifest.PackageVersion))
                {
                    versionManifest.PackageVersion = installerManifest.PackageVersion = RemoveInvalidCharsFromString(defaultLocaleManifest.PackageVersion);
                }

                string packageIdPublisher = defaultLocaleManifest.Publisher?.Remove(" ").Trim('.') ?? $"<{nameof(defaultLocaleManifest.Publisher)}>";
                string packageIdName = defaultLocaleManifest.PackageName?.Remove(" ").Trim('.') ?? $"<{nameof(defaultLocaleManifest.PackageName)}>";
                versionManifest.PackageIdentifier ??= $"{RemoveInvalidCharsFromString(packageIdPublisher)}.{RemoveInvalidCharsFromString(packageIdName)}";
                installerManifest.PackageIdentifier = defaultLocaleManifest.PackageIdentifier = versionManifest.PackageIdentifier;
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Parses a package for relevant metadata and generates a new installer node for each installer file (MSIX can have multiple installers).
        /// </summary>
        /// <param name="installerMetadata">Helper class for storing an installer's metadata information.</param>
        /// <param name="manifests">Manifests object model.</param>
        /// <returns>Boolean value indicating whether the function was successful or not.</returns>
        private static bool ParsePackageAndGenerateInstallerNodes(InstallerMetadata installerMetadata, Manifests manifests)
        {
            var url = installerMetadata.InstallerUrl;
            var packageFile = installerMetadata.PackageFile;
            var newInstallers = installerMetadata.NewInstallers;
            var baseInstaller = new Installer();
            baseInstaller.InstallerUrl = url;
            baseInstaller.InstallerSha256 = GetFileHash(packageFile);

            List<string> installerPaths = new List<string>();

            if (installerMetadata.IsZipFile)
            {
                baseInstaller.InstallerType = InstallerType.Zip;
                baseInstaller.NestedInstallerFiles = new List<NestedInstallerFile>();
                List<string> relativeFilePaths = installerMetadata.NestedInstallerFiles.Select(i => i.RelativeFilePath).Distinct().ToList();

                foreach (NestedInstallerFile nestedInstallerFile in installerMetadata.NestedInstallerFiles)
                {
                    // Skip adding duplicate NestedInstallerFile object.
                    if (baseInstaller.NestedInstallerFiles.Any(i =>
                        i.RelativeFilePath == nestedInstallerFile.RelativeFilePath &&
                        i.PortableCommandAlias == nestedInstallerFile.PortableCommandAlias))
                    {
                        continue;
                    }

                    baseInstaller.NestedInstallerFiles.Add(new NestedInstallerFile
                    {
                        RelativeFilePath = nestedInstallerFile.RelativeFilePath,
                        PortableCommandAlias = nestedInstallerFile.PortableCommandAlias,
                    });
                }

                // Number of installer paths should be equal to the distinct relative file paths.
                foreach (var relativeFilePath in relativeFilePaths)
                {
                    installerPaths.Add(Path.Combine(installerMetadata.ExtractedDirectory, relativeFilePath));
                }
            }
            else
            {
                installerPaths.Add(packageFile);
            }

            Architecture? nestedArchitecture = null;
            bool parseMsixResult = false;

            // There will only be multiple installer paths if there are multiple nested portable installers in an zip archive.
            foreach (string path in installerPaths)
            {
                bool parseResult = ParseExeInstallerType(path, baseInstaller, newInstallers) ||
                    (parseMsixResult = ParseMsix(path, baseInstaller, manifests, newInstallers)) ||
                    ParseMsi(path, baseInstaller, manifests, newInstallers);

                if (!parseResult)
                {
                    return false;
                }

                // Check if the detected architectures are the same for all nested portables (exe).
                if (nestedArchitecture.HasValue && nestedArchitecture != baseInstaller.Architecture)
                {
                    installerMetadata.MultipleNestedInstallerArchitectures = true;
                }

                nestedArchitecture = baseInstaller.Architecture;
            }

            // Only capture architecture if installer is non-msix as the architecture for msix installers is deterministic
            if (!parseMsixResult)
            {
                var urlArchitecture = installerMetadata.UrlArchitecture = GetArchFromUrl(baseInstaller.InstallerUrl);
                installerMetadata.BinaryArchitecture = baseInstaller.Architecture;

                var overrideArch = installerMetadata.OverrideArchitecture;

                if (overrideArch.HasValue)
                {
                    baseInstaller.Architecture = overrideArch.Value;
                }
                else if (urlArchitecture.HasValue)
                {
                    baseInstaller.Architecture = urlArchitecture.Value;
                }
            }

            return true;
        }

        /// <summary>
        /// Performs a regex match to determine the installer architecture based on the url string.
        /// </summary>
        /// <param name="url">Installer url string.</param>
        /// <returns>Installer architecture enum.</returns>
        private static Architecture? GetArchFromUrl(string url)
        {
            List<Architecture> archMatches = new List<Architecture>();

            // Arm must only be checked if arm64 check fails, otherwise it'll match for arm64 too
            if (Regex.Match(url, "arm64|aarch64(ec)?", RegexOptions.IgnoreCase).Success)
            {
                archMatches.Add(Architecture.Arm64);
            }
            else if (Regex.Match(url, @"\barm\b|armv[567]|\baarch\b", RegexOptions.IgnoreCase).Success)
            {
                archMatches.Add(Architecture.Arm);
            }

            if (Regex.Match(url, "x64|winx?64|_64|64-?bit|ia64|amd64", RegexOptions.IgnoreCase).Success)
            {
                archMatches.Add(Architecture.X64);
            }

            // x86 must only be checked if the URL doesn't match an x86_64 like pattern, which is for x64
            if (Regex.Match(url, "x86(-|_)x?64", RegexOptions.IgnoreCase).Success)
            {
                archMatches.Add(Architecture.X64);
            }
            else if (Regex.Match(url, @"x86|win32|winx86|_86|32-?bit|ia32|i[3456]86|\b[3456]86\b", RegexOptions.IgnoreCase).Success)
            {
                archMatches.Add(Architecture.X86);
            }

            archMatches = archMatches.Distinct().ToList();
            return archMatches.Count == 1 ? archMatches.Single() : null;
        }

        private static MachineType? GetMachineType(string binary)
        {
            using (FileStream stream = File.OpenRead(binary))
            using (BinaryReader bw = new BinaryReader(stream))
            {
                const ushort executableMagicNumber = 0x5a4d;
                const int peMagicNumber = 0x00004550;    // "PE\0\0"

                stream.Seek(0, SeekOrigin.Begin);
                int magicNumber = bw.ReadUInt16();
                bool isExecutable = magicNumber == executableMagicNumber;

                if (isExecutable)
                {
                    stream.Seek(60, SeekOrigin.Begin);
                    int headerOffset = bw.ReadInt32();

                    stream.Seek(headerOffset, SeekOrigin.Begin);
                    int signature = bw.ReadInt32();
                    bool isPortableExecutable = signature == peMagicNumber;

                    if (isPortableExecutable)
                    {
                        MachineType machineType = (MachineType)bw.ReadUInt16();

                        return GetCompatibleMachineType(machineType);
                    }
                }
            }

            return null;
        }

        private static MachineType GetCompatibleMachineType(MachineType type)
        {
            switch (type)
            {
                case MachineType.Armv7:
                    return MachineType.Arm;
                default:
                    return type;
            }
        }

        /// <summary>
        /// Checks if the provided installerTypes are compatible.
        /// </summary>
        /// <param name="type1">First InstallerType.</param>
        /// <param name="type2">Second InstallerType.</param>
        /// <returns>A boolean value indicating whether the installerTypes are compatible.</returns>
        private static bool IsCompatibleInstallerType(InstallerType? type1, InstallerType? type2)
        {
            if (!type1.HasValue || !type2.HasValue)
            {
                return false;
            }

            InstallerType installerType1 = type1.Value;
            InstallerType installerType2 = type2.Value;

            if (installerType1 == installerType2)
            {
                return true;
            }

            CompatibilitySet set1 = GetCompatibilitySet(installerType1);
            CompatibilitySet set2 = GetCompatibilitySet(installerType2);

            if (set1 == CompatibilitySet.None || set2 == CompatibilitySet.None)
            {
                return false;
            }

            return set1 == set2;
        }

        private static CompatibilitySet GetCompatibilitySet(InstallerType type)
        {
            switch (type)
            {
                case InstallerType.Inno:
                case InstallerType.Nullsoft:
                case InstallerType.Exe:
                case InstallerType.Burn:
                // Portable is included as a compatible installer type since
                // they are detected as 'exe' installers. This is to ensure
                // updating a portable manifest is supported.
                case InstallerType.Portable:
                    return CompatibilitySet.Exe;
                case InstallerType.Wix:
                case InstallerType.Msi:
                    return CompatibilitySet.Msi;
                case InstallerType.Msix:
                case InstallerType.Appx:
                    return CompatibilitySet.Msix;
                default:
                    return CompatibilitySet.None;
            }
        }

        private static bool ParseExeInstallerType(string path, Installer baseInstaller, List<Installer> newInstallers)
        {
            try
            {
                InstallerType? installerTypeEnum;
                var directory = PEImage.FromFile(path).Resources
                    .GetDirectory(ResourceType.Manifest)
                    .GetDirectory(ResourceType.Cursor);
                var data = directory.GetData(directory.Entries.First().Id);
                var manifestXml = new XmlDocument();

                // The first character is not a valid XML character, so we skip it.
                manifestXml.LoadXml(data.CreateReader().ReadUtf8String().ToString()[1..]);
                string installerType = manifestXml.DocumentElement
                    .GetElementsByTagName("description")
                    .Cast<XmlNode>()
                    .FirstOrDefault()?
                    .InnerText?
                    .Split(' ').First()
                    .ToLowerInvariant();

                if (installerType.EqualsIC("wix"))
                {
                    // See https://github.com/microsoft/winget-create/issues/26, a Burn installer is an exe-installer produced by the WiX toolset.
                    installerTypeEnum = InstallerType.Burn;
                }
                else if (KnownInstallerResourceNames.Contains(installerType))
                {
                    // If it's a known exe installer type, set as appropriately
                    installerTypeEnum = installerType.ToEnumOrDefault<InstallerType>();
                }
                else
                {
                    installerTypeEnum = (baseInstaller.InstallerType == InstallerType.Portable ||
                            baseInstaller.NestedInstallerType == NestedInstallerType.Portable) ?
                            InstallerType.Portable : InstallerType.Exe;
                }

                SetInstallerType(baseInstaller, installerTypeEnum.Value);

                baseInstaller.Architecture = GetMachineType(path)?.ToString().ToEnumOrDefault<Architecture>() ?? Architecture.Neutral;

                newInstallers.Add(baseInstaller);

                return true;
            }
            catch (Exception ex) when (ex is BadImageFormatException || ex is KeyNotFoundException)
            {
                return false;
            }
        }

        private static bool ParseMsi(string path, Installer baseInstaller, Manifests manifests, List<Installer> newInstallers)
        {
            DefaultLocaleManifest defaultLocaleManifest = manifests?.DefaultLocaleManifest;

            Msi msiInfo = new Msi(path);

            // Subject is a mandatory property in an MSI, equals to ProductName in the Property table
            // https://learn.microsoft.com/en-us/windows/win32/msi/summary-property-descriptions
            if (msiInfo.Information.Subject == null)
            {
                // Binary isn't a valid MSI or wasn't an MSI, skip
                return false;
            }

            Msi.MsiTable propertyTable = msiInfo.Tables.Where(table => table.Name == "Property").First();

            bool isWix = msiInfo.Information.TableNames.Any(table_name => table_name.ToLower().Contains("wix")) ||
                propertyTable.Rows.Any(row => row[0].ToLower().Contains("wix") || row[1].ToLower().Contains("wix")) ||
                msiInfo.Information.CreatingApplication.ToLower().Contains("wix") ||
                msiInfo.Information.CreatingApplication.ToLower().Contains("windows installer xml");

            SetInstallerType(baseInstaller, isWix ? InstallerType.Wix : InstallerType.Msi);

            if (defaultLocaleManifest != null)
            {
                defaultLocaleManifest.PackageVersion ??= propertyTable.Rows.Where(row => row[0] == "\"ProductVersion\"").First()[1].Trim('"');
                defaultLocaleManifest.PackageName ??= propertyTable.Rows.Where(row => row[0] == "\"ProductName\"").First()[1].Trim('"');
                defaultLocaleManifest.Publisher ??= propertyTable.Rows.Where(row => row[0] == "\"Manufacturer\"").First()[1].Trim('"');
            }

            baseInstaller.ProductCode = propertyTable.Rows.Where(row => row[0] == "\"ProductCode\"").First()[1].Trim('"');
            baseInstaller.AppsAndFeaturesEntries = new List<AppsAndFeaturesEntry>
                {
                    new AppsAndFeaturesEntry
                    {
                        DisplayName = propertyTable.Rows.Where(row => row[0] == "\"ProductName\"").First()[1].Trim('"'),
                        DisplayVersion = propertyTable.Rows.Where(row => row[0] == "\"ProductVersion\"").First()[1].Trim('"'),
                        Publisher = propertyTable.Rows.Where(row => row[0] == "\"Manufacturer\"").First()[1].Trim('"'),
                        ProductCode = propertyTable.Rows.Where(row => row[0] == "\"ProductCode\"").First()[1].Trim('"'),
                        UpgradeCode = propertyTable.Rows.Where(row => row[0] == "\"UpgradeCode\"").First()[1].Trim('"'),
                    },
                };

            string archString = msiInfo.Information.Architecture;

            archString = archString.EqualsIC("Intel") ? "x86" :
                archString.EqualsIC("Intel64") ? "x64" :
                archString.EqualsIC("Arm64") ? "Arm64" :
                archString.EqualsIC("Arm") ? "Arm" : archString;

            baseInstaller.Architecture = archString.ToEnumOrDefault<Architecture>() ?? Architecture.Neutral;

            if (baseInstaller.InstallerLocale == null)
            {
                string languageString = propertyTable.Rows.Where(row => row[0] == "\"ProductLanguage\"").First()[1].Trim('"');

                if (int.TryParse(languageString, out int lcid))
                {
                    try
                    {
                        baseInstaller.InstallerLocale = new CultureInfo(lcid).Name;
                    }
                    catch (Exception ex) when (ex is ArgumentOutOfRangeException || ex is CultureNotFoundException)
                    {
                        // If the lcid value is invalid, do nothing.
                    }
                }
            }

            newInstallers?.Add(baseInstaller);

            return true;
        }

        private static bool ParseMsix(string path, Installer baseInstaller, Manifests manifests, List<Installer> newInstallers)
        {
            InstallerManifest installerManifest = manifests?.InstallerManifest;
            DefaultLocaleManifest defaultLocaleManifest = manifests?.DefaultLocaleManifest;

            MsixOrAppxPackage.Metadata metadata = GetAppxMetadataAndSetInstallerProperties(path, installerManifest, baseInstaller, newInstallers);
            if (metadata.Equals(default(MsixOrAppxPackage.Metadata)))
            {
                // Binary wasn't an MSIX, skip
                return false;
            }

            if (defaultLocaleManifest != null)
            {
                defaultLocaleManifest.PackageVersion ??= metadata.PackageVersion;
                defaultLocaleManifest.PackageName ??= metadata.PackageName;
                defaultLocaleManifest.Publisher ??= metadata.Publisher;
                defaultLocaleManifest.ShortDescription ??= metadata.ShortDescription;
            }

            return true;
        }

        private static string SetInstallerStringPropertyIfNeeded(string rootProperty, string valueToSet)
        {
            return valueToSet == rootProperty ? null : valueToSet;
        }

        private static List<T> SetInstallerListPropertyIfNeeded<T>(List<T> rootProperty, List<T> valueToSet)
        {
            return rootProperty != null && new HashSet<T>(rootProperty).SetEquals(valueToSet) ? null : valueToSet;
        }

        private static MsixOrAppxPackage.Metadata GetAppxMetadataAndSetInstallerProperties(string path, InstallerManifest installerManifest, Installer baseInstaller, List<Installer> installers)
        {
            // Create a folder with the same name as the downloaded installer.
            // This will be used to extract individual MSIX/APPX packages from an MSIX/APPX bundle.
            string installerFileWithoutExtension = Path.GetFileNameWithoutExtension(path);
            if (!Directory.Exists(installerFileWithoutExtension))
            {
                Directory.CreateDirectory(installerFileWithoutExtension);
            }

            try
            {
                var appxMetadatas = new List<MsixOrAppxPackage.Metadata>();
                string signatureSha256;

                try
                {
                    ZipArchive zipArchive = new(File.OpenRead(path), ZipArchiveMode.Read);

                    // Check if package is an MsixBundle
                    string appxBundleManifestXml = new StreamReader(zipArchive.GetEntry(MsixOrAppxPackage.BundleManifestFile).Open()).ReadToEnd();
                    var appxBundleManifest = new XmlDocument();
                    appxBundleManifest.LoadXml(appxBundleManifestXml);

                    using MemoryStream appxSignatureStream = new MemoryStream();
                    zipArchive.GetEntry(MsixOrAppxPackage.SignatureFile).Open().CopyTo(appxSignatureStream);
                    signatureSha256 = MsixOrAppxPackage.GetSignatureSha256(appxSignatureStream);

                    // /Bundle/Packages/Package[@Type='application'] -> FileName
                    var packages = appxBundleManifest
                        .SelectNodes("/*[local-name()='Bundle']/*[local-name()='Packages']/*[local-name()='Package']")
                        .Cast<XmlNode>()
                        .Where(p => p.Attributes["Type"].Value == "application")
                        .Select(p => p.Attributes["FileName"].Value);

                    // Only create installer nodes for non-resource packages
                    foreach (var childPackage in packages)
                    {
                        // Ignore stub packages.
                        if (childPackage.StartsWith("AppxMetadata\\Stub", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        string extractPath = Path.Combine(installerFileWithoutExtension, childPackage);
                        zipArchive.GetEntry(childPackage).ExtractToFile(extractPath, true);
                        appxMetadatas.Add(new MsixOrAppxPackage(extractPath).Information);
                    }
                }
                catch
                {
                    // Check if package is an Msix
                    MsixOrAppxPackage.Metadata appxMetadata = new MsixOrAppxPackage(path).Information;
                    appxMetadatas.Add(appxMetadata);
                    signatureSha256 = appxMetadata.SignatureSha256;
                }

                baseInstaller.SignatureSha256 = signatureSha256;
                SetInstallerType(baseInstaller, InstallerType.Msix);

                // Add installer nodes for MSIX installers
                foreach (var appxMetadata in appxMetadatas)
                {
                    var msixInstaller = CloneInstaller(baseInstaller);

                    msixInstaller.Architecture = appxMetadata.Architecture.ToEnumOrDefault<Architecture>() ?? Architecture.Neutral;

                    msixInstaller.MinimumOSVersion = SetInstallerStringPropertyIfNeeded(installerManifest?.MinimumOSVersion, appxMetadata.MinimumOSVersion);
                    msixInstaller.PackageFamilyName = SetInstallerStringPropertyIfNeeded(installerManifest?.PackageFamilyName, appxMetadata.PackageFamilyName);
                    msixInstaller.Platform = SetInstallerListPropertyIfNeeded(installerManifest?.Platform, [.. appxMetadata.Platforms]);

                    installers.Add(msixInstaller);
                }

                return appxMetadatas.First();
            }
            catch
            {
                // Binary wasn't an MSIX
                return default;
            }
            finally
            {
                // Delete the folder created to extract individual MSIX/APPX packages.
                // This is done to prevent additional disk space being consumed by
                // individual MSIX/APPX packages extracted from an MSIX/APPX bundle.
                if (Directory.Exists(installerFileWithoutExtension))
                {
                    Directory.Delete(installerFileWithoutExtension, true);
                }
            }
        }

        private static void SetInstallerType(Installer baseInstaller, InstallerType installerType)
        {
            if (baseInstaller.InstallerType.IsArchiveType())
            {
                baseInstaller.NestedInstallerType = (NestedInstallerType)Enum.Parse(typeof(NestedInstallerType), installerType.ToString());
            }
            else
            {
                baseInstaller.InstallerType = installerType;
            }
        }

        private static string RemoveInvalidCharsFromString(string value)
        {
            return Regex.Replace(value, InvalidCharacters, string.Empty);
        }
    }
}
