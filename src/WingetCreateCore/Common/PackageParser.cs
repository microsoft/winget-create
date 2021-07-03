﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCore
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using System.Xml;
    using Microsoft.Deployment.WindowsInstaller.Linq;
    using Microsoft.Msix.Utils;
    using Microsoft.Msix.Utils.AppxPackaging;
    using Microsoft.Msix.Utils.AppxPackagingInterop;
    using Microsoft.WingetCreateCore.Common;
    using Microsoft.WingetCreateCore.Models;
    using Microsoft.WingetCreateCore.Models.DefaultLocale;
    using Microsoft.WingetCreateCore.Models.Installer;
    using Microsoft.WingetCreateCore.Models.Version;
    using Newtonsoft.Json;
    using Vestris.ResourceLib;

    /// <summary>
    /// Provides functionality for a parsing and extracting relevant metadata from a given package.
    /// </summary>
    public static class PackageParser
    {
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
        }

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
        /// Parses packages for available metadata including Version, Publisher, Name, Descripion, License, etc.
        /// </summary>
        /// <param name="paths">Path(s) to package files. </param>
        /// <param name="urls">Installer urls. </param>
        /// <param name="manifests">Wrapper object for manifest object models.</param>
        /// <returns>True if packages were successfully parsed and metadata extracted, false otherwise.</returns>
        public static bool ParsePackages(
            IEnumerable<string> paths,
            IEnumerable<string> urls,
            Manifests manifests)
        {
            VersionManifest versionManifest = manifests.VersionManifest = new VersionManifest();

            // TODO: Remove once default is set in schema
            versionManifest.DefaultLocale = "en-US";

            InstallerManifest installerManifest = manifests.InstallerManifest = new InstallerManifest();
            DefaultLocaleManifest defaultLocaleManifest = manifests.DefaultLocaleManifest = new DefaultLocaleManifest();

            foreach (var package in paths.Zip(urls, (path, url) => (path, url)))
            {
                if (!ParsePackage(package.path, package.url, manifests))
                {
                    return false;
                }
            }

            return true;
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

            if (!response.IsSuccessStatusCode)
            {
                string message = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException(message, null, response.StatusCode);
            }

            string urlFile = Path.GetFileName(url.Split('?').Last());
            string contentDispositionFile = response.Content.Headers.ContentDisposition?.FileName?.Trim('"');
            string targetFile = Path.Combine(Path.GetTempPath(), contentDispositionFile ?? urlFile);
            long? downloadSize = response.Content.Headers.ContentLength;

            if (downloadSize > maxDownloadSize)
            {
                string invalidDataExceptionMessage = $"URL points to file larger than the maximum size of {maxDownloadSize / 1024 / 1024}MB";
                throw new InvalidDataException(invalidDataExceptionMessage);
            }

            if (!File.Exists(targetFile) || new FileInfo(targetFile).Length != downloadSize)
            {
                File.Delete(targetFile);
                using var targetFileStream = File.OpenWrite(targetFile);
                var contentStream = await response.Content.ReadAsStreamAsync();
                await contentStream.CopyToAsync(targetFileStream);
            }

            return targetFile;
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
        /// Update InstallerManifest's Installer nodes based on specified package file paths.
        /// </summary>
        /// <param name="installerManifest"><see cref="InstallerManifest"/> to update.</param>
        /// <param name="installerUrls">InstallerUrls where installers can be downloaded.</param>
        /// <param name="paths">Paths to packages to extract metadata from.</param>
        /// <param name="installerMismatch">If set, the failure was due to an installer count or type mismatch.</param>
        /// <param name="installerMissingMatch">If populated, all packages were successfully parsed, but at least one without a match in the existing manifest.</param>
        /// <returns>True if update succeeded, false otherwise.</returns>
        public static bool UpdateInstallerNodes(InstallerManifest installerManifest, IEnumerable<string> installerUrls, IEnumerable<string> paths, out bool installerMismatch, out Installer installerMissingMatch)
        {
            var newPackages = paths.Zip(installerUrls, (path, url) => (path, url)).ToList();
            var newInstallers = new List<Installer>();
            var existingInstallers = new List<Installer>(installerManifest.Installers);
            installerMismatch = false;
            installerMissingMatch = null;

            foreach (var (path, url) in newPackages)
            {
                if (!ParsePackageAndGenerateInstallerNodes(path, url, newInstallers, null))
                {
                    return false;
                }
            }

            // We only allow updating manifests with the same package count
            if (newInstallers.Count != existingInstallers.Count)
            {
                installerMismatch = true;
                return false;
            }

            // Update previous installers with parsed data from downloaded packages
            foreach (var newInstaller in newInstallers)
            {
                // Find a match for the current installer in the list of installers remaining to be updated.
                var matchingExistingInstaller = existingInstallers.SingleOrDefault(i =>
                    (i.InstallerType ?? installerManifest.InstallerType) == newInstaller.InstallerType &&
                    i.Architecture == newInstaller.Architecture);

                // If we can't find a match in the remaining existing packages, there must be a mismatch between the old manifest and the URLs provided
                if (matchingExistingInstaller == null)
                {
                    installerMismatch = true;
                    installerMissingMatch = newInstaller;
                    return false;
                }
                else
                {
                    existingInstallers.Remove(matchingExistingInstaller);
                }

                matchingExistingInstaller.InstallerUrl = newInstaller.InstallerUrl;
                matchingExistingInstaller.InstallerSha256 = newInstaller.InstallerSha256;
                matchingExistingInstaller.SignatureSha256 = newInstaller.SignatureSha256;
                matchingExistingInstaller.ProductCode = newInstaller.ProductCode;
                matchingExistingInstaller.MinimumOSVersion = newInstaller.MinimumOSVersion;
                matchingExistingInstaller.PackageFamilyName = newInstaller.PackageFamilyName;
                matchingExistingInstaller.Platform = newInstaller.Platform;
            }

            return true;
        }

        /// <summary>
        /// Parses a package for available metadata including Version, Publisher, Name, Descripion, License, etc.
        /// </summary>
        /// <param name="path">Path to package file. </param>
        /// <param name="url">Installer url. </param>
        /// <param name="manifests">Wrapper object for manifest object models.</param>
        /// <returns>True if package was successfully parsed and metadata extracted, false otherwise.</returns>
        private static bool ParsePackage(
            string path,
            string url,
            Manifests manifests)
        {
            VersionManifest versionManifest = manifests.VersionManifest;
            InstallerManifest installerManifest = manifests.InstallerManifest;
            DefaultLocaleManifest defaultLocaleManifest = manifests.DefaultLocaleManifest;

            var versionInfo = FileVersionInfo.GetVersionInfo(path);

            defaultLocaleManifest.PackageVersion ??= versionInfo.FileVersion?.Trim() ?? versionInfo.ProductVersion?.Trim();
            defaultLocaleManifest.Publisher ??= versionInfo.CompanyName?.Trim();
            defaultLocaleManifest.PackageName ??= versionInfo.ProductName?.Trim();
            defaultLocaleManifest.ShortDescription ??= versionInfo.FileDescription?.Trim();
            defaultLocaleManifest.License ??= versionInfo.LegalCopyright?.Trim();

            if (ParsePackageAndGenerateInstallerNodes(path, url, installerManifest.Installers, manifests))
            {
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

        private static bool ParsePackageAndGenerateInstallerNodes(string path, string url, List<Installer> installers, Manifests manifests)
        {
            var installer = new Installer();
            installer.InstallerUrl = url;
            installer.InstallerSha256 = GetFileHash(path);
            installer.Architecture = GetMachineType(path)?.ToString().ToEnumOrDefault<InstallerArchitecture>() ?? InstallerArchitecture.Neutral;

            return ParseExeInstallerType(path, installer, installers) ||
                ParseMsix(path, installer, manifests, installers) ||
                ParseMsi(path, installer, manifests, installers);
        }

        /// <summary>
        /// Computes the SHA256 hash value for the specified byte array.
        /// </summary>
        /// <param name="buffer">The input to compute the hash code for.</param>
        /// <returns>The computed SHA256 hash string.</returns>
        private static string HashBytes(byte[] buffer)
        {
            using var hasher = SHA256.Create();
            return BitConverter.ToString(hasher.ComputeHash(buffer)).Remove("-");
        }

        private static string HashAppxFile(IAppxFile signatureFile)
        {
            var signatureBytes = StreamUtils.ReadStreamToByteArray(signatureFile.GetStream());
            return HashBytes(signatureBytes);
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

                        return machineType;
                    }
                }
            }

            return null;
        }

        private static bool ParseExeInstallerType(string path, Installer baseInstaller, List<Installer> installers)
        {
            try
            {
                ManifestResource rc = new ManifestResource();
                rc.LoadFrom(path);
                string installerType = rc.Manifest.DocumentElement
                    .GetElementsByTagName("description")
                    .Cast<XmlNode>()
                    .FirstOrDefault()?
                    .InnerText?
                    .Split(' ').First()
                    .ToLowerInvariant();

                if (installerType.EqualsIC("wix"))
                {
                    // See https://github.com/microsoft/winget-create/issues/26, a Burn installer is an exe-installer produced by the WiX toolset.
                    baseInstaller.InstallerType = InstallerType.Burn;
                }
                else if (KnownInstallerResourceNames.Contains(installerType))
                {
                    // If it's a known exe installer type, set as appropriately
                    baseInstaller.InstallerType = installerType.ToEnumOrDefault<InstallerType>();
                }
                else
                {
                    baseInstaller.InstallerType = InstallerType.Exe;
                }

                installers.Add(baseInstaller);

                return true;
            }
            catch (Win32Exception)
            {
                // Installer doesn't have a resource header
                return false;
            }
        }

        private static bool ParseMsi(string path, Installer baseInstaller, Manifests manifests, List<Installer> installers)
        {
            DefaultLocaleManifest defaultLocaleManifest = manifests?.DefaultLocaleManifest;

            try
            {
                using (var database = new QDatabase(path, Deployment.WindowsInstaller.DatabaseOpenMode.ReadOnly))
                {
                    baseInstaller.InstallerType = InstallerType.Msi;

                    var properties = database.Properties.ToList();

                    if (defaultLocaleManifest != null)
                    {
                        defaultLocaleManifest.PackageVersion ??= properties.FirstOrDefault(p => p.Property == "ProductVersion")?.Value;
                        defaultLocaleManifest.PackageName ??= properties.FirstOrDefault(p => p.Property == "ProductName")?.Value;
                        defaultLocaleManifest.Publisher ??= properties.FirstOrDefault(p => p.Property == "Manufacturer")?.Value;
                    }

                    baseInstaller.ProductCode = properties.FirstOrDefault(p => p.Property == "ProductCode")?.Value;

                    string archString = database.SummaryInfo.Template.Split(';').First();

                    archString = archString.EqualsIC("Intel") ? "x86" : archString.EqualsIC("Intel64") ? "x64" : archString;

                    baseInstaller.Architecture = archString.ToEnumOrDefault<InstallerArchitecture>() ?? InstallerArchitecture.Neutral;

                    if (baseInstaller.InstallerLocale == null)
                    {
                        string languageString = properties.FirstOrDefault(p => p.Property == "ProductLanguage")?.Value;

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
                }

                installers?.Add(baseInstaller);

                return true;
            }
            catch (Deployment.WindowsInstaller.InstallerException)
            {
                // Binary wasn't an MSI, skip
                return false;
            }
        }

        private static bool ParseMsix(string path, Installer baseInstaller, Manifests manifests, List<Installer> installers)
        {
            InstallerManifest installerManifest = manifests?.InstallerManifest;
            DefaultLocaleManifest defaultLocaleManifest = manifests?.DefaultLocaleManifest;

            AppxMetadata metadata = GetAppxMetadataAndSetInstallerProperties(path, installerManifest, baseInstaller, installers);
            if (metadata == null)
            {
                // Binary wasn't an MSIX, skip
                return false;
            }

            if (defaultLocaleManifest != null)
            {
                defaultLocaleManifest.PackageVersion ??= metadata.Version?.ToString();
                defaultLocaleManifest.PackageName ??= metadata.DisplayName;
                defaultLocaleManifest.Publisher ??= metadata.PublisherDisplayName;
                defaultLocaleManifest.ShortDescription ??= GetApplicationProperty(metadata, "Description");
            }

            return true;
        }

        private static string GetApplicationProperty(AppxMetadata appxMetadata, string propertyName)
        {
            IAppxManifestApplicationsEnumerator enumerator = appxMetadata.AppxReader.GetManifest().GetApplications();

            while (enumerator.GetHasCurrent())
            {
                IAppxManifestApplication application = enumerator.GetCurrent();

                try
                {
                    application.GetStringValue(propertyName, out string value);
                    return value;
                }
                catch (ArgumentException)
                {
                    // Property not found on this node, continue
                }

                enumerator.MoveNext();
            }

            return null;
        }

        private static Installer CloneInstaller(Installer installer)
        {
            string json = JsonConvert.SerializeObject(installer);
            return JsonConvert.DeserializeObject<Installer>(json);
        }

        private static void SetInstallerPropertiesFromAppxMetadata(AppxMetadata appxMetadata, Installer installer, InstallerManifest installerManifest)
        {
            installer.Architecture = appxMetadata.Architecture.ToEnumOrDefault<InstallerArchitecture>() ?? InstallerArchitecture.Neutral;

            installer.MinimumOSVersion = SetInstallerStringPropertyIfNeeded(installerManifest?.MinimumOSVersion, appxMetadata.MinOSVersion?.ToString());
            installer.PackageFamilyName = SetInstallerStringPropertyIfNeeded(installerManifest?.PackageFamilyName, appxMetadata.PackageFamilyName);

            // We have to fixup the Platform string first, and then remove anything that fails to parse.
            var platformValues = appxMetadata.TargetDeviceFamiliesMinVersions.Keys
                .Select(k => k.Replace('.', '_').ToEnumOrDefault<Platform>())
                .Where(p => p != null)
                .Select(p => p.Value)
                .ToList();
            installer.Platform = SetInstallerListPropertyIfNeeded(installerManifest?.Platform, platformValues);
        }

        private static string SetInstallerStringPropertyIfNeeded(string rootProperty, string valueToSet)
        {
            return valueToSet == rootProperty ? null : valueToSet;
        }

        private static List<T> SetInstallerListPropertyIfNeeded<T>(List<T> rootProperty, List<T> valueToSet)
        {
            return rootProperty != null && new HashSet<T>(rootProperty).SetEquals(valueToSet) ? null : valueToSet;
        }

        private static AppxMetadata GetAppxMetadataAndSetInstallerProperties(string path, InstallerManifest installerManifest, Installer baseInstaller, List<Installer> installers)
        {
            try
            {
                var appxMetadatas = new List<AppxMetadata>();
                string signatureSha256;

                try
                {
                    // Check if package is an MsixBundle
                    var bundle = new AppxBundleMetadata(path);

                    IAppxFile signatureFile = bundle.AppxBundleReader.GetFootprintFile(APPX_BUNDLE_FOOTPRINT_FILE_TYPE.APPX_BUNDLE_FOOTPRINT_FILE_TYPE_SIGNATURE);
                    signatureSha256 = HashAppxFile(signatureFile);

                    // Only create installer nodes for non-resource packages
                    foreach (var childPackage in bundle.ChildAppxPackages.Where(p => p.PackageType == PackageType.Application))
                    {
                        var appxFile = bundle.AppxBundleReader.GetPayloadPackage(childPackage.RelativeFilePath);
                        appxMetadatas.Add(new AppxMetadata(appxFile.GetStream()));
                    }
                }
                catch (COMException)
                {
                    // Check if package is an Msix
                    var appxMetadata = new AppxMetadata(path);
                    appxMetadatas.Add(appxMetadata);
                    IAppxFile signatureFile = appxMetadata.AppxReader.GetFootprintFile(APPX_FOOTPRINT_FILE_TYPE.APPX_FOOTPRINT_FILE_TYPE_SIGNATURE);
                    signatureSha256 = HashAppxFile(signatureFile);
                }

                baseInstaller.SignatureSha256 = signatureSha256;
                baseInstaller.InstallerType = InstallerType.Msix;

                // Add installer nodes for MSIX installers
                foreach (var appxMetadata in appxMetadatas)
                {
                    var msixInstaller = CloneInstaller(baseInstaller);
                    installers.Add(msixInstaller);

                    SetInstallerPropertiesFromAppxMetadata(appxMetadata, msixInstaller, installerManifest);
                }

                return appxMetadatas.First();
            }
            catch (COMException)
            {
                // Binary wasn't an MSIX
                return null;
            }
        }

        private static string RemoveInvalidCharsFromString(string value)
        {
            return Regex.Replace(value, InvalidCharacters, string.Empty);
        }
    }
}
