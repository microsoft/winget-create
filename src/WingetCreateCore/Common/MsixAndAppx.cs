// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

#pragma warning disable SA1649 // File name should match first type name

namespace Microsoft.WingetCreateCore.Common
{
    using System;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using Microsoft.WingetCreateCore.Models.Installer;

    /// <summary>
    /// Class to handle MSIX and APPX packages and extract metadata from them.
    /// </summary>
    public class MsixOrAppxPackage
    {
        /// <summary>
        /// Appx Manifest file name.
        /// </summary>
        public const string ManifestFile = "AppxManifest.xml";

        /// <summary>
        /// Appx Bundle Manifest file name.
        /// </summary>
        public const string BundleManifestFile = "AppxMetadata/AppxBundleManifest.xml";

        /// <summary>
        /// Appx Signature file name.
        /// </summary>
        public const string SignatureFile = "AppxSignature.p7x";

        /// <summary>
        /// Gets metadata of APPX/MSIX package or bundle.
        /// </summary>
        public Metadata Information { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MsixOrAppxPackage"/> class.
        /// </summary>
        /// <param name="path">Path to the MSIX/APPX package or bundle.</param>
        public MsixOrAppxPackage(string path)
        {
            using ZipArchive zipArchive = new(File.OpenRead(path), ZipArchiveMode.Read);

            using MemoryStream appxSignatureStream = new MemoryStream();
            zipArchive.GetEntry(SignatureFile).Open().CopyTo(appxSignatureStream);

            string appxManifestXml = new StreamReader(zipArchive.GetEntry(ManifestFile).Open()).ReadToEnd();
            var appxManifest = new System.Xml.XmlDocument();
            appxManifest.LoadXml(appxManifestXml);

            // /Package/Identity
            var identityNode = appxManifest.SelectSingleNode("/*[local-name()='Package']/*[local-name()='Identity']");

            // /Package/Properties
            var propertiesNode = appxManifest.SelectSingleNode("/*[local-name()='Package']/*[local-name()='Properties']");

            // /Package/Dependencies/TargetDeviceFamily
            var targetDeviceFamilyNode = appxManifest.SelectSingleNode("/*[local-name()='Package']/*[local-name()='Dependencies']/*[local-name()='TargetDeviceFamily']");

            // Generate the hash part of the package family name
            var publisherSha256 = SHA256.HashData(Encoding.Unicode.GetBytes(identityNode.Attributes["Publisher"].Value));
            var binaryString = string.Concat(publisherSha256.Take(8).Select(c => Convert.ToString(c, 2).PadLeft(8, '0'))) + '0'; // representing 65-bits = 13 * 5
            var encodedPublisherId = string.Concat(Enumerable.Range(0, binaryString.Length / 5).Select(i => "0123456789ABCDEFGHJKMNPQRSTVWXYZ".Substring(Convert.ToInt32(binaryString.Substring(i * 5, 5), 2), 1)));

            this.Information = new Metadata
            {
                SignatureSha256 = GetSignatureSha256(appxSignatureStream),
                PackageFamilyName = $"{identityNode.Attributes["Name"]?.Value}_{encodedPublisherId.ToLower()}",
                PackageVersion = identityNode.Attributes["Version"]?.Value,
                Architecture = identityNode.Attributes["ProcessorArchitecture"]?.Value,
                PackageName = propertiesNode.SelectSingleNode("*[local-name()='DisplayName']")?.InnerText,
                Publisher = propertiesNode.SelectSingleNode("*[local-name()='PublisherDisplayName']")?.InnerText,
                ShortDescription = propertiesNode.SelectSingleNode("*[local-name()='Description']")?.InnerText,
                MinimumOSVersion = targetDeviceFamilyNode.Attributes["MinVersion"]?.Value,
                Platforms = [Enum.Parse<Platform>(targetDeviceFamilyNode.Attributes["Name"]?.Value.Replace('.', '_'))],
            };
        }

        /// <summary>
        /// Gets signature sha256 of APPX/MSIX package or bundle.
        /// </summary>
        /// <param name="appxSignatureP7x_stream">Stream of the AppxSignature.p7x file.</param>
        /// <returns>Signature sha256 of the package.</returns>
        public static string GetSignatureSha256(MemoryStream appxSignatureP7x_stream)
        {
            return BitConverter.ToString(SHA256.HashData(appxSignatureP7x_stream.ToArray())).Replace("-", string.Empty);
        }

        /// <summary>
        /// Structure to hold the metadata of the package.
        /// </summary>
        public struct Metadata
        {
            /// <summary>
            /// Gets signature of MSIX/APPX package or bundle.
            /// </summary>
            public string SignatureSha256;

            /// <summary>
            /// Gets the package family name.
            /// </summary>
            public string PackageFamilyName;

            /// <summary>
            /// Gets the package version (Identity#Version).
            /// </summary>
            public string PackageVersion;

            /// <summary>
            /// Gets the package architecture (Identity#ProcessorArchitecture).
            /// </summary>
            public string Architecture;

            /// <summary>
            /// Gets the package name (Properties/DisplayName).
            /// </summary>
            public string PackageName;

            /// <summary>
            /// Gets the publisher name (Properties/PublisherDisplayName).
            /// </summary>
            public string Publisher;

            /// <summary>
            /// Gets the description of the package (Properties/Description).
            /// </summary>
            public string ShortDescription;

            /// <summary>
            /// Gets the minimum OS version required to run the package (Dependencies/TargetDeviceFamily#MinVersion).
            /// </summary>
            public string MinimumOSVersion;

            /// <summary>
            /// Gets the platform(s) supported by the package (Dependencies/TargetDeviceFamily#Name).
            /// </summary>
            public Platform[] Platforms;
        }
    }
}
