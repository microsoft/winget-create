// Copyright (c) Microsoft. All rights reserved.
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
            "wix",
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
        /// Parses a package for available metadata including Version, Publisher, Name, Descripion, License, etc.
        /// </summary>
        /// <param name="path">Path to package file. </param>
        /// <param name="url">Installer url. </param>
        /// <param name="manifests">Wrapper object for manifest object models.</param>
        /// <returns>True if package was successfully parsed and metadata extracted, false otherwise.</returns>
        public static bool ParsePackage(
            string path,
            string url,
            Manifests manifests)
        {
            VersionManifest versionManifest = manifests.VersionManifest = new VersionManifest();

            // TODO: Remove once default is set in schema
            versionManifest.DefaultLocale = "en-US";

            InstallerManifest installerManifest = manifests.InstallerManifest = new InstallerManifest();
            DefaultLocaleManifest defaultLocaleManifest = manifests.DefaultLocaleManifest = new DefaultLocaleManifest();

            var versionInfo = FileVersionInfo.GetVersionInfo(path);

            var installer = new Installer();
            installer.InstallerUrl = url;
            installer.InstallerSha256 = GetFileHash(path);
            installer.Architecture = GetMachineType(path)?.ToString().ToEnumOrDefault<InstallerArchitecture>() ?? InstallerArchitecture.Neutral;
            installerManifest.Installers.Add(installer);

            defaultLocaleManifest.PackageVersion ??= versionInfo.FileVersion?.Trim() ?? versionInfo.ProductVersion?.Trim();
            defaultLocaleManifest.Publisher ??= versionInfo.CompanyName?.Trim();
            defaultLocaleManifest.PackageName ??= versionInfo.ProductName?.Trim();
            defaultLocaleManifest.ShortDescription ??= versionInfo.FileDescription?.Trim();
            defaultLocaleManifest.License ??= versionInfo.LegalCopyright?.Trim();

            if (ParseExeInstallerType(path, installer) ||
                ParseMsix(path, manifests) ||
                ParseMsi(path, installer, manifests))
            {
                if (!string.IsNullOrEmpty(defaultLocaleManifest.PackageVersion))
                {
                    versionManifest.PackageVersion = installerManifest.PackageVersion = RemoveInvalidCharsFromString(defaultLocaleManifest.PackageVersion);
                }

                string packageIdPublisher = defaultLocaleManifest.Publisher?.Remove(" ").Trim('.') ?? $"<{nameof(defaultLocaleManifest.Publisher)}>";
                string packageIdName = defaultLocaleManifest.PackageName?.Remove(" ").Trim('.') ?? $"<{nameof(defaultLocaleManifest.PackageName)}>";
                versionManifest.PackageIdentifier = $"{RemoveInvalidCharsFromString(packageIdPublisher)}.{RemoveInvalidCharsFromString(packageIdName)}";
                installerManifest.PackageIdentifier = defaultLocaleManifest.PackageIdentifier = versionManifest.PackageIdentifier;
                return true;
            }
            else
            {
                return false;
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
        /// Update InstallerManifest's Installer nodes based on specified package file path.
        /// </summary>
        /// <param name="installerManifest"><see cref="InstallerManifest"/> to update.</param>
        /// <param name="installerUrl">InstallerUrl where installer can be downloaded.</param>
        /// <param name="packageFile">Path to package to extract metadata from.</param>
        public static void UpdateInstallerNodes(InstallerManifest installerManifest, string installerUrl, string packageFile)
        {
            string installerSha256 = GetFileHash(packageFile);
            foreach (var installer in installerManifest.Installers)
            {
                installer.InstallerSha256 = installerSha256;
                installer.InstallerUrl = installerUrl;

                // If installer is an MSI, update its ProductCode
                var updatedInstaller = new Installer();
                if (ParseMsi(packageFile, updatedInstaller, null))
                {
                    installer.ProductCode = updatedInstaller.ProductCode;
                }
            }

            GetAppxMetadataAndSetInstallerProperties(packageFile, installerManifest);
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

        private static bool ParseExeInstallerType(string path, Installer installer)
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

                installer.InstallerType = KnownInstallerResourceNames.Contains(installerType) ? installerType.ToEnumOrDefault<InstallerType>() : InstallerType.Exe;

                return true;
            }
            catch (Win32Exception)
            {
                // Installer doesn't have a resource header
                return false;
            }
        }

        private static bool ParseMsi(string path, Installer installer, Manifests manifests)
        {
            DefaultLocaleManifest defaultLocaleManifest = manifests?.DefaultLocaleManifest;

            try
            {
                using (var database = new QDatabase(path, Deployment.WindowsInstaller.DatabaseOpenMode.ReadOnly))
                {
                    installer.InstallerType = InstallerType.Msi;

                    var properties = database.Properties.ToList();

                    if (defaultLocaleManifest != null)
                    {
                        defaultLocaleManifest.PackageVersion ??= properties.FirstOrDefault(p => p.Property == "ProductVersion")?.Value;
                        defaultLocaleManifest.PackageName ??= properties.FirstOrDefault(p => p.Property == "ProductName")?.Value;
                        defaultLocaleManifest.Publisher ??= properties.FirstOrDefault(p => p.Property == "Manufacturer")?.Value;
                    }

                    installer.ProductCode = properties.FirstOrDefault(p => p.Property == "ProductCode")?.Value;

                    string archString = database.SummaryInfo.Template.Split(';').First();
                    installer.Architecture = archString.ToEnumOrDefault<InstallerArchitecture>() ?? InstallerArchitecture.Neutral;

                    if (installer.InstallerLocale == null)
                    {
                        string languageString = properties.FirstOrDefault(p => p.Property == "ProductLanguage")?.Value;

                        if (int.TryParse(languageString, out int lcid))
                        {
                            try
                            {
                                installer.InstallerLocale = new CultureInfo(lcid).Name;
                            }
                            catch (Exception ex) when (ex is ArgumentOutOfRangeException || ex is CultureNotFoundException)
                            {
                                // If the lcid value is invalid, do nothing.
                            }
                        }
                    }
                }

                return true;
            }
            catch (Deployment.WindowsInstaller.InstallerException)
            {
                // Binary wasn't an MSI, skip
                return false;
            }
        }

        private static bool ParseMsix(string path, Manifests manifests)
        {
            InstallerManifest installerManifest = manifests.InstallerManifest;
            DefaultLocaleManifest defaultLocaleManifest = manifests.DefaultLocaleManifest;

            AppxMetadata metadata = GetAppxMetadataAndSetInstallerProperties(path, installerManifest);
            if (metadata == null)
            {
                // Binary wasn't an MSIX, skip
                return false;
            }

            installerManifest.Installers.ForEach(i => i.InstallerType = InstallerType.Msix);
            defaultLocaleManifest.PackageVersion = metadata.Version?.ToString();
            defaultLocaleManifest.PackageName ??= metadata.DisplayName;
            defaultLocaleManifest.Publisher ??= metadata.PublisherDisplayName;
            defaultLocaleManifest.ShortDescription ??= GetApplicationProperty(metadata, "Description");

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

            installer.MinimumOSVersion = SetInstallerStringPropertyIfNeeded(installerManifest.MinimumOSVersion, appxMetadata.MinOSVersion?.ToString());
            installer.PackageFamilyName = SetInstallerStringPropertyIfNeeded(installerManifest.PackageFamilyName, appxMetadata.PackageFamilyName);

            // We have to fixup the Platform string first, and then remove anything that fails to parse.
            var platformValues = appxMetadata.TargetDeviceFamiliesMinVersions.Keys
                .Select(k => k.Replace('.', '_').ToEnumOrDefault<Platform>())
                .Where(p => p != null)
                .Select(p => p.Value)
                .ToList();
            installer.Platform = SetInstallerListPropertyIfNeeded(installerManifest.Platform, platformValues);
        }

        private static string SetInstallerStringPropertyIfNeeded(string rootProperty, string valueToSet)
        {
            return valueToSet == rootProperty ? null : valueToSet;
        }

        private static List<T> SetInstallerListPropertyIfNeeded<T>(List<T> rootProperty, List<T> valueToSet)
        {
            return rootProperty != null && new HashSet<T>(rootProperty).SetEquals(valueToSet) ? null : valueToSet;
        }

        private static AppxMetadata GetAppxMetadataAndSetInstallerProperties(string path, InstallerManifest installerManifest)
        {
            try
            {
                var installers = installerManifest.Installers;
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

                var firstInstaller = installers.First();

                // Remove installer nodes which have no matching architecture in msix/bundle
                installers.RemoveAll(i => !appxMetadatas.Any(m => m.Architecture.EqualsIC(i.Architecture.ToString())));

                foreach (var appxMetadata in appxMetadatas)
                {
                    InstallerArchitecture appxArchitecture = appxMetadata.Architecture.ToEnumOrDefault<InstallerArchitecture>() ?? InstallerArchitecture.Neutral;
                    var matchingInstaller = installers.SingleOrDefault(i => i.Architecture == appxArchitecture);
                    if (matchingInstaller == null)
                    {
                        matchingInstaller = CloneInstaller(firstInstaller);
                        installers.Add(matchingInstaller);
                    }

                    SetInstallerPropertiesFromAppxMetadata(appxMetadata, matchingInstaller, installerManifest);
                }

                installers.ForEach(i => i.SignatureSha256 = signatureSha256);

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
