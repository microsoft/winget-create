// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateUnitTests
{
    using System.Text.Json;
    using Microsoft.WingetCreateCLI;
    using Moq;
    using NUnit.Framework;
    using RestSharp;

    /// <summary>
    /// Unit tests for testing the OAuth login flow for GitHub.
    /// </summary>
    public class OAuthTests
    {
        private const string FailedToObtainAccessTokenString = "Failed to obtain access token.";

        private const string TokenResponseFailedToDeserialize = "TokenResponse object failed to deserialize.";

        /// <summary>
        /// Unit test that mocks the response of the RestRequest such that it returns a TokenResponse.
        /// </summary>
        [Test]
        public void GetTokenAsyncMockTest()
        {
            Mock<IRestClient> mockClient = new Mock<IRestClient>();
            mockClient.Setup(x => x.ExecutePostAsync(It.IsAny<RestRequest>(), default))
                .ReturnsAsync(GenerateTokenResponse());

            var response = GitHubOAuth.GetTokenAsync(mockClient.Object, new GitHubOAuth.DeviceAuthorizationResponse()).Result;
            Assert.That(response.GetType(), Is.EqualTo(typeof(GitHubOAuth.TokenResponse)), TokenResponseFailedToDeserialize);
            Assert.That(response.AccessToken, Is.Not.Null.And.Not.Empty, FailedToObtainAccessTokenString);
        }

        private static RestResponse GenerateTokenResponse()
        {
            GitHubOAuth.TokenResponse tokenResponse = new GitHubOAuth.TokenResponse();
            tokenResponse.AccessToken = "A_FAKE_TOKEN";
            tokenResponse.TokenType = "bearer";
            tokenResponse.Scope = "user";
            return SerializeToRestResponse(tokenResponse);
        }

        private static RestResponse SerializeToRestResponse(object model)
        {
            var responseContent = JsonSerializer.Serialize(model);
            RestResponse restResponse = new RestResponse();
            restResponse.Content = responseContent;
            return restResponse;
        }
    }
}
