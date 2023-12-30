﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCore.Common
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Threading.Tasks;
    using Jose;
    using Microsoft.WingetCreateCore.Common.Exceptions;
    using Microsoft.WingetCreateCore.Models;
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
                this.github.Credentials = new Credentials(githubApiToken, AuthenticationType.Bearer);
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
            github.Credentials = new Credentials(jwtToken, AuthenticationType.Bearer);

            var installation = await github.GitHubApps.GetRepositoryInstallationForCurrent(wingetRepoOwner, wingetRepo);
            var response = await github.GitHubApps.CreateInstallationToken(installation.Id);
            return response.Token;
        }

        /// <summary>
        /// Gets all app manifests in the repo.
        /// </summary>
        /// <returns>A list of <see cref="PublisherAppVersion"/>, each representing a single app manifest version.</returns>
        public async Task<IList<PublisherAppVersion>> GetAppVersions()
        {
            var reference = await this.github.Git.Reference.Get(this.wingetRepoOwner, this.wingetRepo, HeadMasterRef);
            var tree = await this.github.Git.Tree.GetRecursive(this.wingetRepoOwner, this.wingetRepo, reference.Object.Sha);
            return tree.Tree
                .Where(i => i.Path.StartsWith(Constants.WingetManifestRoot + "/") && i.Type.Value == TreeType.Blob)
                .Select(i => new { i.Path, PathTokens = i.Path[Constants.WingetManifestRoot.Length..].Split('/') })
                .Where(i => i.PathTokens.Length >= 3)
                .Select(i =>
                {
                    // Substring path will be in the form of
                    //      Microsoft/PowerToys/0.15.2.yaml, or
                    //      Microsoft/VisualStudio/Community/16.0.30011.22.yaml
                    string publisher = i.PathTokens[0];
                    string version = i.PathTokens[^1].Replace(".yaml", string.Empty, StringComparison.OrdinalIgnoreCase);
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
            string versionDirectoryPath = await this.GetVersionDirectoryPath(packageId, version);

            if (string.IsNullOrEmpty(versionDirectoryPath))
            {
                throw new NotFoundException(nameof(version), System.Net.HttpStatusCode.NotFound);
            }

            var packageContents = (await this.github.Repository.Content.GetAllContents(this.wingetRepoOwner, this.wingetRepo, versionDirectoryPath))
                .Where(c => c.Type != ContentType.Dir && Path.GetExtension(c.Name).EqualsIC(".yaml"));

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
        /// <param name="prTitle">Optional parameter specifying the title for the pull request.</param>
        /// <param name="shouldReplace">Optional parameter specifying whether the new submission should replace an existing manifest.</param>
        /// <param name="replaceVersion">Optional parameter specifying the version of the manifest to be replaced.</param>
        /// <returns>Pull request object.</returns>
        public Task<PullRequest> SubmitPullRequestAsync(Manifests manifests, bool submitToFork, string prTitle = null, bool shouldReplace = false, string replaceVersion = null)
        {
            Dictionary<string, string> contents = new Dictionary<string, string>();
            string id;
            string version;

            if (manifests.SingletonManifest != null)
            {
                id = manifests.SingletonManifest.PackageIdentifier;
                version = manifests.SingletonManifest.PackageVersion;
                contents.Add(manifests.SingletonManifest.PackageIdentifier, manifests.SingletonManifest.ToYaml());
            }
            else
            {
                id = manifests.VersionManifest.PackageIdentifier;
                version = manifests.VersionManifest.PackageVersion;

                contents = manifests.LocaleManifests.ToDictionary(locale => $"{id}.locale.{locale.PackageLocale}", locale => locale.ToYaml());

                contents.Add(id, manifests.VersionManifest.ToYaml());
                contents.Add($"{id}.installer", manifests.InstallerManifest.ToYaml());
                contents.Add($"{id}.locale.{manifests.DefaultLocaleManifest.PackageLocale}", manifests.DefaultLocaleManifest.ToYaml());
            }

            return this.SubmitPRAsync(id, version, contents, submitToFork, prTitle, shouldReplace, replaceVersion);
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
        /// <returns>The exact matching package identifier or null if no match was found.</returns>
        public async Task<string> FindPackageId(string packageId)
        {
            string path = Constants.WingetManifestRoot + '/' + $"{char.ToLowerInvariant(packageId[0])}";
            return await this.FindPackageIdRecursive(packageId.Split('.'), path, string.Empty, 0);
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

        private async Task<PullRequest> SubmitPRAsync(string packageId, string version, Dictionary<string, string> contents, bool submitToFork, string prTitle = null, bool shouldReplace = false, string replaceVersion = null)
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
            bool forkSyncAttempted = false;

            try
            {
                var retryPolicy = Policy
                    .Handle<ApiException>()
                    .Or<NonFastForwardException>()
                    .WaitAndRetryAsync(3, i => TimeSpan.FromSeconds(i));

                await retryPolicy.ExecuteAsync(async () =>
                {
                    // Related issue: https://github.com/microsoft/winget-create/issues/282
                    // There is a known issue where a reference is unable to be created if the fork is behind by too many commits.
                    // Always attempt to sync fork during first execution in order to mitigate the possibility of this scenario occurring.
                    // If the fork is behind by too many commits, syncing will also fail with a NotFoundException.
                    // Updating the fork can fail if it is a non-fast forward update, but this should not be blocking as pull request submission can still proceed.
                    // If creating a reference fails, that means syncing the fork also failed, therefore the user will need to manually sync their repo regardless.
                    if (!forkSyncAttempted && submitToFork)
                    {
                        forkSyncAttempted = true;
                        await this.UpdateForkedRepoWithUpstreamCommits(repo);
                    }

                    await this.github.Git.Reference.Create(repo.Id, new NewReference($"refs/{newBranchNameHeads}", upstreamMasterSha));
                });

                // Update from upstream branch master
                newBranch = await this.github.Git.Reference.Update(repo.Id, newBranchNameHeads, new ReferenceUpdate(upstreamMasterSha));
                var updatedSha = newBranch.Object.Sha;

                var nt = new NewTree { BaseTree = updatedSha };
                string appPath = Utils.GetAppManifestDirPath(packageId, version, '/');

                foreach (KeyValuePair<string, string> item in contents)
                {
                    string file = $"{appPath}/{item.Key}.yaml";
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

        private async Task<string> GetVersionDirectoryPath(string packageId, string version = null)
        {
            string appPath = Utils.GetAppManifestDirPath(packageId, string.Empty, '/');
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

            // Check to ensure that the update is only a fast-forward update.
            if (compareResult.BehindBy > 0)
            {
                int commitsAheadBy = compareResult.AheadBy;
                if (commitsAheadBy > 0)
                {
                    throw new NonFastForwardException(commitsAheadBy);
                }

                var upstreamBranchReference = await this.github.Git.Reference.Get(upstream.Id, $"heads/{upstream.DefaultBranch}");
                await this.github.Git.Reference.Update(forkedRepo.Id, $"heads/{forkedRepo.DefaultBranch}", new ReferenceUpdate(upstreamBranchReference.Object.Sha));
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
    }
}
