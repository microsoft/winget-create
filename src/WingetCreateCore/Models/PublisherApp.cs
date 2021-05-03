// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCore.Models
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using Newtonsoft.Json;

    /// <summary>
    /// A representation of an OWC publisher app.
    /// </summary>
    public class PublisherApp : IEquatable<PublisherApp>
    {
        /// <summary>
        /// Gets or sets the publisher name.
        /// </summary>
        public string Publisher { get; set; }

        /// <summary>
        /// Gets or sets the app name.
        /// </summary>
        public string App { get; set; }

        /// <summary>
        /// Gets or sets the PackageIdentifier.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Determines if the id of this publisher app is equal to that of another publisher app.
        /// </summary>
        /// <param name="other">Publisher app to be compared with.</param>
        /// <returns>Boolean indicated whether the ids are equivalent.</returns>
        public bool Equals([AllowNull] PublisherApp other)
        {
            return this.Id == other.Id;
        }

        /// <summary>
        /// Obtains the hash code of the id.
        /// </summary>
        /// <returns>Hash code of Id.</returns>
        public override int GetHashCode()
        {
            return this.Id.GetHashCode();
        }
    }

    /// <summary>
    /// A representation of an OWC publisher app with a defined version.
    /// </summary>
    [SuppressMessage("Microsoft.StyleCop.CSharp.MaintainabilityRules", "SA1402:FileMayOnlyContainASingleClass", Justification = "Just a POCO, keeping in same file makes for better readability")]
    public class PublisherAppVersion : PublisherApp
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PublisherAppVersion"/> class.
        /// </summary>
        /// <param name="publisher">The publisher name.</param>
        /// <param name="app">The app name.</param>
        /// <param name="version">The app version.</param>
        /// <param name="packageIdentifier">The package id.</param>
        /// <param name="path">The GitHub repo path.</param>
        public PublisherAppVersion(string publisher, string app, string version, string packageIdentifier, string path)
        {
            this.Publisher = publisher;
            this.App = app;
            this.Version = version;
            this.Id = packageIdentifier;
            this.Path = path;
        }

        /// <summary>
        /// Gets or sets the publisher app version.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the GitHub repo path.
        /// </summary>
        [JsonIgnore]
        public string Path { get; set; }
    }

    /// <summary>
    /// A representation of an OWC publisher app with a defined content and version.
    /// </summary>
    [SuppressMessage("Microsoft.StyleCop.CSharp.MaintainabilityRules", "SA1402:FileMayOnlyContainASingleClass", Justification = "Just a POCO, keeping in same file makes for better readability")]
    public class PublisherAppVersionContent : PublisherAppVersion
    {
        private PublisherAppVersionContent(string publisher, string app, string version, string packageIdentifier, string path)
            : base(publisher, app, version, packageIdentifier, path)
        {
        }

        /// <summary>
        /// Gets or sets the app content.
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// Returns an initialized PublisherAppVersionContent object.
        /// </summary>
        /// <param name="appVersion">App version.</param>
        /// <param name="content">App content.</param>
        /// <returns>PublisherAppVersionContent object.</returns>
        public static PublisherAppVersionContent FromAppVersion(PublisherAppVersion appVersion, string content)
        {
            return new PublisherAppVersionContent(appVersion.Publisher, appVersion.App, appVersion.Version, appVersion.Id, appVersion.Path) { Content = content };
        }
    }
}