// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCore.Common
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json.Nodes;
    using System.Threading.Tasks;
    using Jose;
    using Microsoft.WingetCreateCore.Common.Exceptions;
    using Microsoft.WingetCreateCore.Models;
    using Microsoft.WingetCreateCore.Models.DefaultLocale;
    using Microsoft.WingetCreateCore.Models.Installer;
    using Octokit;
    using Polly;

    /// <summary>
    /// Provides functionality for interacting a user's GitHub account.
    /// </summary>
    public class GitHub
    {
        private const string HeadMasterRef = "heads/master";
        private const string PRDescriptionRepoPath = ".github/PULL_REQUEST_TEMPLATE.md";
        private const string UserAgentName = "WingetCreate";
        private readonly GitHubClient github;
        private readonly string wingetRepoOwner;
        private readonly string wingetRepo;

        /// <summary>
        /// Initializes a new instance of the <see cref="GitHub"/> class.
        /// </summary>
        /// <param name="githubApiToken">GitHub access token.</param>
        /// <param name="wingetRepoOwner">Winget repository owner.</param>
        /// <param name="wingetRepo">Winget repository.</param>
        public GitHub(string githubApiToken, string wingetRepoOwner, string wingetRepo)
        {
            this.wingetRepoOwner = wingetRepoOwner;
            this.wingetRepo = wingetRepo;
            this.github = new GitHubClient(new ProductHeaderValue(UserAgentName));
            if (githubApiToken != null)
            {
                this.github.Credentials = new Credentials(githubApiToken, Octokit.AuthenticationType.Bearer);
            }
        }

        /// <summary>
        /// Gets an access token to use for GitHub operations performed from a GitHub app context.
        /// </summary>
        /// <param name="gitHubAppPrivateKeyPem">The private key for the GitHub app in PEM format.</param>
        /// <param name="gitHubAppId">The id for the GitHub app.</param>
        /// <param name="wingetRepoOwner">Winget repository owner.</param>
        /// <param name="wingetRepo">Winget repository.</param>
        /// <returns>GitHub app installation access token to use for GitHub operations.</returns>
        public static async Task<string> GetGitHubAppInstallationAccessToken(string gitHubAppPrivateKeyPem, int gitHubAppId, string wingetRepoOwner, string wingetRepo)
        {
            string jwtToken = GetJwtToken(gitHubAppPrivateKeyPem, gitHubAppId);

            var github = new GitHubClient(new ProductHeaderValue(UserAgentName));
            github.Credentials = new Credentials(jwtToken, Octokit.AuthenticationType.Bearer);

            var installation = await github.GitHubApps.GetRepositoryInstallationForCurrent(wingetRepoOwner, wingetRepo);
            var response = await github.GitHubApps.CreateInstallationToken(installation.Id);
            return response.Token;
        }

        /// <summary>
        /// Gets all app manifests in the repo.
        /// </summary>
        /// <param name="manifestRoot">The name of the ManifestRoot.</param>
        /// <returns>A list of <see cref="PublisherAppVersion"/>, each representing a single app manifest version.</returns>
        public async Task<IList<PublisherAppVersion>> GetAppVersions(string manifestRoot = Constants.WingetManifestRoot)
        {
            var reference = await this.github.Git.Reference.Get(this.wingetRepoOwner, this.wingetRepo, HeadMasterRef);
            var tree = await this.github.Git.Tree.GetRecursive(this.wingetRepoOwner, this.wingetRepo, reference.Object.Sha);
            return tree.Tree
                .Where(i => i.Path.StartsWith(manifestRoot + "/") && i.Type.Value == TreeType.Blob)
                .Select(i => new { i.Path, PathTokens = i.Path[manifestRoot.Length..].Split('/') })
                .Where(i => i.PathTokens.Length >= 3)
                .Select(i =>
                {
                    // Substring path will be in the form of
                    //      Microsoft/PowerToys/0.15.2.yaml, or
                    //      Microsoft/VisualStudio/Community/16.0.30011.22.yaml
                    string publisher = i.PathTokens[0];
                    string extension = Path.GetExtension(i.Path);
                    string version = i.PathTokens[^1].Replace(extension, string.Empty, StringComparison.OrdinalIgnoreCase);
                    string app = string.Join('.', i.PathTokens[1..^1]);
                    return new PublisherAppVersion(publisher, app, version, $"{publisher}.{app}", i.Path);
                })
                .ToList();
        }

        /// <summary>
        /// Obtains the latest manifest using the specified packageId.
        /// </summary>
        /// <param name="packageId">PackageId of the manifest to be retrieved.</param>
        /// <param name="version">Version of the manifest to be retrieved. Pass in null to retrieve the latest version.</param>
        /// <returns>Manifest as a string.</returns>
        public async Task<List<string>> GetManifestContentAsync(string packageId, string version = null)
        {
            List<string> validExtensions = new List<string> { ".yaml", ".json" };
            string versionDirectoryPath = await this.GetVersionDirectoryPath(packageId, version);

            if (string.IsNullOrEmpty(versionDirectoryPath))
            {
                throw new NotFoundException(nameof(version), System.Net.HttpStatusCode.NotFound);
            }

            var packageContents = (await this.github.Repository.Content.GetAllContents(this.wingetRepoOwner, this.wingetRepo, versionDirectoryPath))
                .Where(c => c.Type != ContentType.Dir && validExtensions.Any(ext => Path.GetExtension(c.Name).EqualsIC(ext)));

            // If all contents of version directory are directories themselves, user must've provided an invalid packageId.
            if (!packageContents.Any())
            {
                throw new NotFoundException(nameof(packageId), System.Net.HttpStatusCode.NotFound);
            }

            List<string> manifestContent = new List<string>();

            foreach (RepositoryContent content in packageContents)
            {
                string fileContent = await this.GetFileContentsAsync(content.Path);
                manifestContent.Add(fileContent);
            }

            return manifestContent;
        }

        /// <summary>
        /// Submits a pull request on behalf of the user.
        /// </summary>
        /// <param name="manifests">Wrapper object for manifest object models to be submitted in the PR.</param>
        /// <param name="submitToFork">Bool indicating whether or not to submit the PR via a fork.</param>
        /// <param name="manifestRoot">The manifest root name.</param>
        /// <param name="prTitle">Optional parameter specifying the title for the pull request.</param>
        /// <param name="shouldReplace">Optional parameter specifying whether the new submission should replace an existing manifest.</param>
        /// <param name="replaceVersion">Optional parameter specifying the version of the manifest to be replaced.</param>
        /// <returns>Pull request object.</returns>
        public Task<PullRequest> SubmitPullRequestAsync(Manifests manifests, bool submitToFork, string manifestRoot = Constants.WingetManifestRoot, string prTitle = null, bool shouldReplace = false, string replaceVersion = null)
        {
            Dictionary<string, string> contents = new Dictionary<string, string>();
            string id;
            string version;

            if (manifests.SingletonManifest != null)
            {
                id = manifests.SingletonManifest.PackageIdentifier;
                version = manifests.SingletonManifest.PackageVersion;
                contents.Add(manifests.SingletonManifest.PackageIdentifier, manifests.SingletonManifest.ToManifestString());
            }
            else
            {
                id = manifests.VersionManifest.PackageIdentifier;
                version = manifests.VersionManifest.PackageVersion;

                contents = manifests.LocaleManifests.ToDictionary(locale => $"{id}.locale.{locale.PackageLocale}", locale => locale.ToManifestString());

                contents.Add(id, manifests.VersionManifest.ToManifestString());
                contents.Add($"{id}.installer", manifests.InstallerManifest.ToManifestString());
                contents.Add($"{id}.locale.{manifests.DefaultLocaleManifest.PackageLocale}", manifests.DefaultLocaleManifest.ToManifestString());
            }

            return this.SubmitPRAsync(id, version, contents, submitToFork, manifestRoot, prTitle, shouldReplace, replaceVersion);
        }

        /// <summary>
        /// Gets the latest release tag name of winget-create.
        /// </summary>
        /// <returns>Latest release tag name.</returns>
        public async Task<string> GetLatestRelease()
        {
            var latestRelease = await this.github.Repository.Release.GetLatest("microsoft", "winget-create");
            return latestRelease.TagName;
        }

        /// <summary>
        /// Closes an open pull request and deletes its branch if not on forked repo.
        /// </summary>
        /// <param name="pullRequestId">The pull request number.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task ClosePullRequest(int pullRequestId)
        {
            // Close PR and delete its branch.
            await this.github.PullRequest.Update(this.wingetRepoOwner, this.wingetRepo, pullRequestId, new PullRequestUpdate() { State = ItemState.Closed });
            await this.DeletePullRequestBranch(pullRequestId);
        }

        /// <summary>
        /// Merges an open pull request.
        /// </summary>
        /// <param name="pullRequestId">The pull request number.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task MergePullRequest(int pullRequestId)
        {
            await this.github.PullRequest.Merge(this.wingetRepoOwner, this.wingetRepo, pullRequestId, new MergePullRequest());
            await this.DeletePullRequestBranch(pullRequestId);
        }

        /// <summary>
        /// Retrieves file contents from a specified GitHub path.
        /// </summary>
        /// <param name="path">GitHub path where the files should be retrieved from.</param>
        /// <returns>Contents from the specified GitHub path.</returns>
        public async Task<string> GetFileContentsAsync(string path)
        {
            var contents = (await this.github.Repository.Content.GetAllContents(this.wingetRepoOwner, this.wingetRepo, path))
                .Select(f => f.Content)
                .First();

            return contents;
        }

        /// <summary>
        /// Checks that the GitHub client can perform operations against the repo using the auth token.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task CheckAccess()
        {
            await this.github.Repository.Get(this.wingetRepoOwner, this.wingetRepo);
        }

        /// <summary>
        /// Recursively searches the repository for the provided package identifer to determine if it already exists.
        /// </summary>
        /// <param name="packageId">The package identifier.</param>
        /// <param name="manifestRoot">The manifest root name.</param>
        /// <returns>The exact matching package identifier or null if no match was found.</returns>
        public async Task<string> FindPackageId(string packageId, string manifestRoot = Constants.WingetManifestRoot)
        {
            string path = manifestRoot + '/' + $"{char.ToLowerInvariant(packageId[0])}";
            return await this.FindPackageIdRecursive(packageId.Split('.'), path, string.Empty, 0);
        }

        /// <summary>
        /// Uses the GitHub API to retrieve and populate metadata for manifests in the provided <see cref="Manifests"/> object.
        /// </summary>
        /// <param name="manifests">Wrapper object for manifest object models to be populated with GitHub metadata.</param>
        /// <param name="serializerFormat">The output format of the manifest serializer.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation. The task result is a boolean indicating whether metadata was successfully populated.</returns>
        public async Task<bool> PopulateGitHubMetadata(Manifests manifests, string serializerFormat)
        {
            // Only populate metadata if we have a valid GitHub token.
            if (this.github.Credentials.AuthenticationType != Octokit.AuthenticationType.Anonymous)
            {
                return await GitHubManifestMetadata.PopulateManifestMetadata(manifests, serializerFormat, this.github);
            }

            return false;
        }

        /// <summary>
        /// Generate a signed-JWT token for specified GitHub app, per instructions here: https://docs.github.com/en/developers/apps/authenticating-with-github-apps#authenticating-as-an-installation.
        /// </summary>
        /// <param name="gitHubAppPrivateKeyPem">The private key for the GitHub app in PEM format.</param>
        /// <param name="gitHubAppId">The id for the GitHub app.</param>
        /// <returns>Signed JWT token, expiring in 10 minutes.</returns>
        private static string GetJwtToken(string gitHubAppPrivateKeyPem, int gitHubAppId)
        {
            var rsa = RSA.Create();
            rsa.ImportFromPem(gitHubAppPrivateKeyPem);

            var payload = new
            {
                // issued at time, 60 seconds in the past to allow for clock drift
                iat = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds(),

                // JWT expiration time (10 minute maximum)
                exp = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds(),

                // GitHub App's identifier
                iss = gitHubAppId,
            };

            return JWT.Encode(payload, rsa, JwsAlgorithm.RS256);
        }

        private async Task<string> FindPackageIdRecursive(string[] packageId, string path, string exactPackageId, int index)
        {
            if (index == packageId.Length)
            {
                return exactPackageId.Trim('.');
            }

            var contents = await this.github.Repository.Content.GetAllContents(this.wingetRepoOwner, this.wingetRepo, path);
            string packageIdToken = packageId[index].ToLowerInvariant();

            foreach (RepositoryContent content in contents)
            {
                if (string.Equals(packageIdToken, content.Name.ToLowerInvariant()))
                {
                    path = path + '/' + content.Name;
                    exactPackageId = string.Join(".", exactPackageId, content.Name);
                    index++;
                    return await this.FindPackageIdRecursive(packageId, path, exactPackageId, index);
                }
            }

            return null;
        }

        private async Task<PullRequest> SubmitPRAsync(string packageId, string version, Dictionary<string, string> contents, bool submitToFork, string manifestRoot = Constants.WingetManifestRoot, string prTitle = null, bool shouldReplace = false, string replaceVersion = null)
        {
            bool createdRepo = false;
            Repository repo;

            if (submitToFork)
            {
                try
                {
                    var user = await this.github.User.Current();
                    repo = await this.github.Repository.Get(user.Login, this.wingetRepo);
                }
                catch (NotFoundException)
                {
                    repo = await this.github.Repository.Forks.Create(this.wingetRepoOwner, this.wingetRepo, new NewRepositoryFork());
                    createdRepo = true;
                }
            }
            else
            {
                repo = await this.github.Repository.Get(this.wingetRepoOwner, this.wingetRepo);
            }

            string newBranchName = $"{packageId}-{version}-{Guid.NewGuid()}".Replace(" ", string.Empty);
            string newBranchNameHeads = $"heads/{newBranchName}";

            if (string.IsNullOrEmpty(prTitle))
            {
                prTitle = $"{packageId} version {version}";
            }

            var upstreamMaster = await this.github.Git.Reference.Get(this.wingetRepoOwner, this.wingetRepo, HeadMasterRef);
            var upstreamMasterSha = upstreamMaster.Object.Sha;

            Reference newBranch = null;
            try
            {
                var retryPolicy = Policy
                    .Handle<ApiException>()
                    .Or<GenericSyncFailureException>()
                    .WaitAndRetryAsync(3, i => TimeSpan.FromSeconds(i));

                await retryPolicy.ExecuteAsync(async () =>
                {
                    // Related issue: https://github.com/microsoft/winget-create/issues/282
                    // There is a known issue where a reference is unable to be created if the fork is behind by too many commits.
                    // Always attempt to sync fork in order to mitigate the possibility of this scenario occurring.
                    if (submitToFork)
                    {
                        await this.UpdateForkedRepoWithUpstreamCommits(repo);
                    }

                    await this.github.Git.Reference.Create(repo.Id, new NewReference($"refs/{newBranchNameHeads}", upstreamMasterSha));
                });

                // Update from upstream branch master
                newBranch = await this.github.Git.Reference.Update(repo.Id, newBranchNameHeads, new ReferenceUpdate(upstreamMasterSha));
                var updatedSha = newBranch.Object.Sha;

                var nt = new NewTree { BaseTree = updatedSha };
                string appPath = Utils.GetAppManifestDirPath(packageId, version, manifestRoot, '/');

                foreach (KeyValuePair<string, string> item in contents)
                {
                    string file = $"{appPath}/{item.Key}{Serialization.ManifestSerializer.AssociatedFileExtension}";
                    nt.Tree.Add(new NewTreeItem { Path = file, Mode = "100644", Type = TreeType.Blob, Content = item.Value });
                }

                var newTree = await this.github.Git.Tree.Create(repo.Id, nt);

                var newCommit = new NewCommit(prTitle, newTree.Sha, updatedSha);
                var commit = await this.github.Git.Commit.Create(repo.Id, newCommit);

                await this.github.Git.Reference.Update(repo.Id, newBranchNameHeads, new ReferenceUpdate(commit.Sha));

                // Remove a previous manifest
                if (shouldReplace)
                {
                    await this.DeletePackageManifest(repo.Id, packageId, replaceVersion, newBranchName);
                }

                // Get latest description template from repo
                string description = await this.GetFileContentsAsync(PRDescriptionRepoPath);

                string targetBranch = submitToFork ? repo.Parent.DefaultBranch : repo.DefaultBranch;
                var newPullRequest = new NewPullRequest(prTitle, $"{repo.Owner.Login}:{newBranchName}", targetBranch) { Body = description };
                var pullRequest = await this.github.PullRequest.Create(this.wingetRepoOwner, this.wingetRepo, newPullRequest);

                return pullRequest;
            }
            catch (Exception)
            {
                // On error, cleanup created branch/repo before re-throwing
                if (createdRepo)
                {
                    try
                    {
                        await this.github.Repository.Delete(repo.Id);
                    }
                    catch (ForbiddenException)
                    {
                        // If we fail to delete the fork, the user did not provide a token with the "delete_repo" permission. Do nothing.
                    }
                }
                else if (newBranch != null)
                {
                    await this.github.Git.Reference.Delete(repo.Id, newBranch.Ref);
                }

                throw;
            }
        }

        private async Task<string> GetVersionDirectoryPath(string packageId, string manifestRoot = Constants.WingetManifestRoot, string version = null)
        {
            string appPath = Utils.GetAppManifestDirPath(packageId, string.Empty, manifestRoot, '/');
            var contents = await this.github.Repository.Content.GetAllContents(this.wingetRepoOwner, this.wingetRepo, appPath);
            string directory;

            if (string.IsNullOrEmpty(version))
            {
                // Get the latest version directory
                directory = contents
                    .Where(c => c.Type == ContentType.Dir)
                    .OrderByDescending(c => c.Name, new VersionComparer())
                    .Select(c => c.Path)
                    .FirstOrDefault();
            }
            else
            {
                // Get the specified version directory
                directory = contents
                    .Where(c => c.Type == ContentType.Dir && c.Name.EqualsIC(version))
                    .Select(c => c.Path)
                    .FirstOrDefault();
            }

            return directory;
        }

        private async Task DeletePackageManifest(long forkRepoId, string packageId, string version, string branchName)
        {
            string versionDirectoryPath = await this.GetVersionDirectoryPath(packageId, version);

            if (string.IsNullOrEmpty(versionDirectoryPath))
            {
                throw new NotFoundException(nameof(version), System.Net.HttpStatusCode.NotFound);
            }

            // Get all files in the version directory
            var versionDirectoryContents = await this.github.Repository.Content.GetAllContents(forkRepoId, versionDirectoryPath);

            // Delete files from the new branch in the forked repository
            foreach (var file in versionDirectoryContents)
            {
                var fileContent = await this.github.Repository.Content.GetAllContentsByRef(forkRepoId, file.Path, branchName);
                await this.github.Repository.Content.DeleteFile(forkRepoId, file.Path, new DeleteFileRequest($"Delete {file.Path}", fileContent[0].Sha, branchName));
            }
        }

        /// <summary>
        /// Checks if the provided forked repository is behind on upstream commits and updates the default branch with the fetched commits. Update can only be a fast-forward update.
        /// </summary>
        /// <param name="forkedRepo"><see cref="Repository"/>Forked repository to be updated.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        private async Task UpdateForkedRepoWithUpstreamCommits(Repository forkedRepo)
        {
            var upstream = forkedRepo.Parent;
            var compareResult = await this.github.Repository.Commit.Compare(upstream.Id, upstream.DefaultBranch, $"{forkedRepo.Owner.Login}:{forkedRepo.DefaultBranch}");

            if (compareResult.BehindBy > 0)
            {
                // Octokit .NET doesn't support sync fork endpoint, so we make a direct call to the GitHub API.
                // Tracking issue for the request: https://github.com/octokit/octokit.net/issues/2989
                HttpClient httpClient = new HttpClient();

                // API reference: https://docs.github.com/en/rest/branches/branches?apiVersion=2022-11-28#sync-a-fork-branch-with-the-upstream-repository
                var url = $"https://api.github.com/repos/{forkedRepo.Owner.Login}/{forkedRepo.Name}/merge-upstream";

                // Headers
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", this.github.Credentials.Password);
                httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
                httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
                httpClient.DefaultRequestHeaders.UserAgent.TryParseAdd(Constants.ProgramName);

                // Payload
                JsonObject jsonObject = new JsonObject { { "branch", forkedRepo.DefaultBranch } };
                var content = new StringContent(jsonObject.ToString(), Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync(url, content);

                // 409 status code
                if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    throw new BranchMergeConflictException();
                }

                // 422 status code
                if (response.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity)
                {
                    throw new GenericSyncFailureException();
                }

                // The API doesn't document another error code. If this fails, a generic HttpRequestException is thrown.
                response.EnsureSuccessStatusCode();
            }
        }

        private async Task DeletePullRequestBranch(int pullRequestId)
        {
            // Delete branch if it's not on a forked repo.
            var pullRequest = await this.github.PullRequest.Get(this.wingetRepoOwner, this.wingetRepo, pullRequestId);
            if (pullRequest.Base.Repository.Id == pullRequest.Head.Repository.Id)
            {
                string newBranchNameHeads = $"heads/{pullRequest.Head.Ref}";
                await this.github.Git.Reference.Delete(this.wingetRepoOwner, this.wingetRepo, newBranchNameHeads);
            }
        }

        private static class GitHubManifestMetadata
        {
            public static async Task<bool> PopulateManifestMetadata(Manifests manifests, string serializerFormat, GitHubClient client)
            {
                // Get owner and repo from the installer manifest
                GitHubUrlMetadata? metadata = GetMetadataFromGitHubUrl(manifests.InstallerManifest);

                if (metadata == null)
                {
                    // Could not populate GitHub metadata.
                    return false;
                }

                string owner = metadata.Value.Owner;
                string repo = metadata.Value.Repo;
                string tag = metadata.Value.ReleaseTag;

                var githubRepo = await client.Repository.Get(owner, repo);
                var githubRelease = await client.Repository.Release.Get(owner, repo, tag);

                // License
                if (string.IsNullOrEmpty(manifests.DefaultLocaleManifest.License))
                {
                    // License will only ever be empty in new command flow
                    manifests.DefaultLocaleManifest.License = githubRepo.License?.SpdxId ?? githubRepo.License?.Name;
                }

                // ShortDescription
                if (string.IsNullOrEmpty(manifests.DefaultLocaleManifest.ShortDescription))
                {
                    // ShortDescription will only ever be empty in new command flow
                    manifests.DefaultLocaleManifest.ShortDescription = githubRepo.Description;
                }

                // PackageUrl
                if (string.IsNullOrEmpty(manifests.DefaultLocaleManifest.PackageUrl))
                {
                    manifests.DefaultLocaleManifest.PackageUrl = githubRepo.HtmlUrl;
                }

                // PublisherUrl
                if (string.IsNullOrEmpty(manifests.DefaultLocaleManifest.PublisherUrl))
                {
                    manifests.DefaultLocaleManifest.PublisherUrl = githubRepo.Owner.HtmlUrl;
                }

                // PublisherSupportUrl
                if (string.IsNullOrEmpty(manifests.DefaultLocaleManifest.PublisherSupportUrl) && githubRepo.HasIssues)
                {
                    manifests.DefaultLocaleManifest.PublisherSupportUrl = $"{githubRepo.HtmlUrl}/issues";
                }

                // Tags
                // 16 is the maximum number of tags allowed in the manifest
                manifests.DefaultLocaleManifest.Tags ??= githubRepo.Topics?.Take(count: 16).ToList();

                // ReleaseNotesUrl
                if (string.IsNullOrEmpty(manifests.DefaultLocaleManifest.ReleaseNotesUrl))
                {
                    manifests.DefaultLocaleManifest.ReleaseNotesUrl = githubRelease.HtmlUrl;
                }

                // ReleaseDate
                SetReleaseDate(manifests, serializerFormat, githubRelease);

                // Documentations
                if (manifests.DefaultLocaleManifest.Documentations == null && githubRepo.HasWiki)
                {
                    manifests.DefaultLocaleManifest.Documentations = new List<Documentation>
                    {
                        new()
                        {
                            DocumentLabel = "Wiki",
                            DocumentUrl = $"{githubRepo.HtmlUrl}/wiki",
                        },
                    };
                }

                return true;
            }

            private static void SetReleaseDate(Manifests manifests, string serializerFormat, Release githubRelease)
            {
                DateTimeOffset? releaseDate = githubRelease.PublishedAt;
                if (releaseDate == null)
                {
                    return;
                }

                switch (serializerFormat.ToLower())
                {
                    case "yaml":
                        manifests.InstallerManifest.ReleaseDateTime = releaseDate.Value.ToString("yyyy-MM-dd");
                        break;
                    case "json":
                        manifests.InstallerManifest.ReleaseDate = releaseDate;
                        break;
                }
            }

            private static GitHubUrlMetadata? GetMetadataFromGitHubUrl(InstallerManifest installerManifest)
            {
                // Get all GitHub URLs from the installer manifest
                List<string> gitHubUrls = installerManifest.Installers
                    .Where(x => x.InstallerUrl.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase))
                    .Select(x => x.InstallerUrl)
                    .ToList();

                if (gitHubUrls.Count != installerManifest.Installers.Count)
                {
                    // No GitHub URLs found OR not all manifest InstallerUrls are GitHub URLs.
                    return null;
                }

                string domainTrimmed = gitHubUrls.First().Replace("https://github.com/", string.Empty);
                string[] parts = domainTrimmed.Split("/");
                string owner = parts[0];
                string repo = parts[1];
                string tag = domainTrimmed.Replace($"{owner}/{repo}/releases/download/", string.Empty).Split("/")[0];

                // Check if all GitHub URLs have the same owner, repo and tag
                if (gitHubUrls.Any(x => !x.StartsWith($"https://github.com/{owner}/{repo}/releases/download/{tag}", StringComparison.OrdinalIgnoreCase)))
                {
                    return null;
                }

                return new GitHubUrlMetadata(owner, repo, tag);
            }

            private record struct GitHubUrlMetadata(string Owner, string Repo, string ReleaseTag);
        }
    }
}
