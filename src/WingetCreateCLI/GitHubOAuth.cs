// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI
{
    using System;
    using System.ComponentModel;
    using System.IO;
#if WINDOWS
    using System.Security.Cryptography;
#endif
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using Microsoft.WingetCreateCLI.Logging;
    using Microsoft.WingetCreateCLI.Properties;
    using Microsoft.WingetCreateCLI.Telemetry;
    using Microsoft.WingetCreateCLI.Telemetry.Events;
    using Microsoft.WingetCreateCore.Common;
    using RestSharp;

    /// <summary>
    /// Provides functionality for launching the GitHub OAuth device flow for the WingetCreate CLI tool.
    /// </summary>
    public static class GitHubOAuth
    {
        private const string GitHubDeviceEndpoint = "https://github.com/login/device/code";
        private const string GitHubTokenEndpoint = "https://github.com/login/oauth/access_token";
        private const string GrantType = "urn:ietf:params:oauth:grant-type:device_code";
        private static readonly string TokenFile = Path.Combine(Common.LocalAppStatePath, "tokenCache.bin");

        /// <summary>
        /// Create byte array for additional entropy when using Protect method.
        /// </summary>
        private static readonly byte[] EntropyBytes = Encoding.UTF8.GetBytes(TokenFile);

        /// <summary>
        /// Deletes the cached token.
        /// </summary>
        public static void DeleteTokenCache()
        {
            File.Delete(TokenFile);
        }

        /// <summary>
        /// Reads and decrypts the cached token, if one exists.
        /// </summary>
        /// <returns>Decrypted cached token.</returns>
        public static string ReadTokenCache()
        {
            if (File.Exists(TokenFile))
            {
                var protectedBytes = File.ReadAllBytes(TokenFile);
                var bytes = protectedBytes;
#if WINDOWS
                bytes = ProtectedData.Unprotect(protectedBytes, EntropyBytes, DataProtectionScope.CurrentUser);
#endif
                return Encoding.UTF8.GetString(bytes);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Encrypts and writes the token to the file cache.
        /// </summary>
        /// <param name="token">Token to be cached.</param>
        public static void WriteTokenCache(string token)
        {
            var bytes = Encoding.UTF8.GetBytes(token);
#if WINDOWS
            bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(token), EntropyBytes, DataProtectionScope.CurrentUser);
#endif
            File.WriteAllBytes(TokenFile, bytes);
        }

        /// <summary>
        /// Sends a POST request to GitHub's authorization server to obtain a device code.
        /// </summary>
        /// <param name="client">RestClient.</param>
        /// <returns>DeviceAuthorizationResponse object containing device code.</returns>
        public static async Task<DeviceAuthorizationResponse> StartDeviceFlowAsync(IRestClient client)
        {
            // TODO: Shrink scopes request later when we eventually don't need access to private repo
            var request = new RestRequest(GitHubDeviceEndpoint)
                .AddJsonBody(new { client_id = Constants.GitHubOAuthClientId, scope = "repo" });

            return await client.PostAsync<DeviceAuthorizationResponse>(request);
        }

        /// <summary>
        /// Polls GitHub authorization server until access token is provided.
        /// </summary>
        /// <param name="client">RestClient.</param>
        /// <param name="authResponse">DeviceAuthorizationResponse object with device code.</param>
        /// <returns>GitHub access token.</returns>
        public static async Task<TokenResponse> GetTokenAsync(IRestClient client, DeviceAuthorizationResponse authResponse)
        {
            DeviceAuthorizationResponse authorizationResponse = authResponse;
            int pollingDelay = authorizationResponse.Interval;

            while (true)
            {
                var request = new RestRequest(GitHubTokenEndpoint)
                    .AddJsonBody(new { client_id = Constants.GitHubOAuthClientId, device_code = authorizationResponse.DeviceCode, grant_type = GrantType });

                var response = await client.ExecutePostAsync(request);

                if (response.Content.Contains("error"))
                {
                    var errorResponse = JsonSerializer.Deserialize<TokenErrorResponse>(response.Content);
                    switch (errorResponse.Error)
                    {
                        case "authorization_pending":
                            break;
                        case "slow_down":
                            pollingDelay += 5;
                            break;
                        case "expired_token":
                            authorizationResponse = await StartDeviceFlowAsync(client);
                            Logger.WarnLocalized(nameof(Resources.TokenExpired_Message));
                            Console.WriteLine();
                            Logger.InfoLocalized(nameof(Resources.EnterUserCode_Message), authorizationResponse.UserCode);
                            break;
                        default:
                            Logger.ErrorLocalized(nameof(Resources.GitHubFailedAuthorization_Message), errorResponse.Error, errorResponse.ErrorDescription);
                            TelemetryManager.Log.WriteEvent(new OAuthLoginEvent { IsSuccessful = false, Error = errorResponse.Error });
                            return null;
                    }

                    await Task.Delay(TimeSpan.FromSeconds(pollingDelay));
                }
                else
                {
                    TelemetryManager.Log.WriteEvent(new OAuthLoginEvent { IsSuccessful = true });
                    return JsonSerializer.Deserialize<TokenResponse>(response.Content);
                }
            }
        }

        /// <summary>
        /// Opens the provided URI in a browser.
        /// </summary>
        /// <param name="uri">URI to be opened by browser.</param>
        public static void OpenWebPage(string uri)
        {
            var psi = new System.Diagnostics.ProcessStartInfo();
            psi.UseShellExecute = true;
            psi.FileName = uri;

            try
            {
                System.Diagnostics.Process.Start(psi);
            }
            catch (Win32Exception e)
            {
                Logger.ErrorLocalized(nameof(Resources.BrowserFailedToLaunch_Error), e.Message);
            }
        }

        /// <summary>
        /// Models the device authorization response from GitHub.
        /// </summary>
        public class DeviceAuthorizationResponse
        {
            /// <summary>
            /// Gets or sets the code used to verify the device.
            /// </summary>
            [JsonPropertyName("device_code")]
            public string DeviceCode { get; set; }

            /// <summary>
            /// Gets or sets the user verification code to be entered in the browser.
            /// </summary>
            [JsonPropertyName("user_code")]
            public string UserCode { get; set; }

            /// <summary>
            /// Gets or sets the verification URL where the user needs to enter the user_code.
            /// </summary>
            [JsonPropertyName("verification_uri")]
            public string VerificationUri { get; set; }

            /// <summary>
            /// Gets or sets the number of seconds before the device or user code expires.
            /// </summary>
            [JsonPropertyName("expires_in")]
            public int ExpiresIn { get; set; }

            /// <summary>
            /// Gets or sets the minimum number of seconds that must pass before a new access token request can be made.
            /// </summary>
            [JsonPropertyName("interval")]
            public int Interval { get; set; }
        }

        /// <summary>
        /// Models the token response from GitHub if the user authentication is successful.
        /// </summary>
        public class TokenResponse
        {
            /// <summary>
            /// Gets or sets the GitHub access token.
            /// </summary>
            [JsonPropertyName("access_token")]
            public string AccessToken { get; set; }

            /// <summary>
            /// Gets or sets the token type.
            /// </summary>
            [JsonPropertyName("token_type")]
            public string TokenType { get; set; }

            /// <summary>
            /// Gets or sets the scope of the access token.
            /// </summary>
            [JsonPropertyName("scope")]
            public string Scope { get; set; }
        }

        /// <summary>
        /// Models the token response from GitHub if the user authentication is not yet successful.
        /// </summary>
        public class TokenErrorResponse
        {
            /// <summary>
            /// Gets or sets the error code or the response.
            /// </summary>
            [JsonPropertyName("error")]
            public string Error { get; set; }

            /// <summary>
            /// Gets or sets the error description.
            /// </summary>
            [JsonPropertyName("error_description")]
            public string ErrorDescription { get; set; }

            /// <summary>
            /// Gets or sets the error uri.
            /// </summary>
            [JsonPropertyName("error_uri")]
            public string ErrorUri { get; set; }
        }
    }
}
