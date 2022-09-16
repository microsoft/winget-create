// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.WingetCreateCore;
    using Moq;
    using Moq.Protected;

    /// <summary>
    /// Utils class for shared functionality among the test classes.
    /// </summary>
    public static class TestUtils
    {
        private static HttpMessageHandler httpMessageHandler;
        private static HttpResponseMessage httpResponseMessage;

        /// <summary>
        /// Initializes and sets up the infrastructure for mocking installer downloads, and sets the mock response content for each specified file.
        /// </summary>
        /// <param name="files">An array of files to generate mock response for.</param>
        public static void InitializeMockDownloads(params string[] files)
        {
            var handlerMock = new Mock<HttpMessageHandler>();

            foreach (var filename in files)
            {
                string url = $"https://fakedomain.com/{filename}";
                var httpResponse = new HttpResponseMessage(HttpStatusCode.OK);
                var content = new ByteArrayContent(File.ReadAllBytes(GetTestFile(Path.GetFileName(filename))));
                content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                httpResponse.Content = content;

                handlerMock
                    .Protected()

                    // Setup the PROTECTED method to mock
                    .Setup<Task<HttpResponseMessage>>(
                        "SendAsync",
                        ItExpr.Is<HttpRequestMessage>(x => x.RequestUri.AbsoluteUri == url),
                        ItExpr.IsAny<CancellationToken>())

                    // prepare the expected response of the mocked http call
                    .ReturnsAsync(httpResponse)
                    .Verifiable();
            }

            PackageParser.SetHttpMessageHandler(handlerMock.Object);
        }

        /// <summary>
        /// Initializes and sets up the infrastructure for mocking installer downloads.
        /// </summary>
        public static void InitializeMockDownload()
        {
            httpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK);

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()

                // Setup the PROTECTED method to mock
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())

                // prepare the expected response of the mocked http call
                .ReturnsAsync(httpResponseMessage)
                .Verifiable();

            httpMessageHandler = handlerMock.Object;
        }

        /// <summary>
        /// Sets the mock http response content.
        /// </summary>
        /// <param name="installerName">File name of the installer.</param>
        public static void SetMockHttpResponseContent(string installerName)
        {
            var content = new ByteArrayContent(File.ReadAllBytes(GetTestFile(installerName)));
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            httpResponseMessage.Content = content;
            PackageParser.SetHttpMessageHandler(httpMessageHandler);
        }

        /// <summary>
        /// Obtains the relative filepath of the resources test data directory.
        /// </summary>
        /// <param name="fileName">File name of the test file.</param>
        /// <returns>Full path of the test file.</returns>
        public static string GetTestFile(string fileName)
        {
            return Path.Combine(Environment.CurrentDirectory, "Resources", fileName);
        }

        /// <summary>
        /// Obtains the initial manifest content string from the provided manifest file name.
        /// </summary>
        /// <param name="manifestFileName">Manifest file name string.</param>
        /// <returns>List of manifest content strings.</returns>
        public static List<string> GetInitialManifestContent(string manifestFileName)
        {
            string testFilePath = GetTestFile(manifestFileName);
            var initialManifestContent = new List<string> { File.ReadAllText(testFilePath) };
            return initialManifestContent;
        }

        /// <summary>
        /// Obtains the initial manifest content string from the provided manifest directory name.
        /// </summary>
        /// <param name="manifestDirName">Manifest directory name string.</param>
        /// <returns>List of manifest content strings.</returns>
        public static List<string> GetInitialMultifileManifestContent(string manifestDirName)
        {
            string testDirPath = GetTestFile(manifestDirName);
            return Directory.GetFiles(testDirPath).Select(f => File.ReadAllText(f)).ToList();
        }

        /// <summary>
        /// Sets up mocking and downloads the given filename.
        /// </summary>
        /// <param name="filename">Filename to be mock downloaded.</param>
        /// <returns>Path to the mock downloaded file.</returns>
        public static string MockDownloadFile(string filename)
        {
            string url = $"https://fakedomain.com/{filename}";
            SetMockHttpResponseContent(filename);
            string downloadedPath = PackageParser.DownloadFileAsync(url).Result;
            return downloadedPath;
        }
    }
}
