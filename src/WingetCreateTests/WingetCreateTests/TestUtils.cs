// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading;
    using System.Threading.Tasks;
    using CommandLine;
    using Microsoft.WingetCreateCLI.Commands;
    using Microsoft.WingetCreateCore;
    using Microsoft.WingetCreateUnitTests.Models;
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

        /// <summary>
        /// Creates copies of the specified resource file. If multiple copies are requested, the new files will be named with a numeric suffix.
        /// </summary>
        /// <param name="resource">Name of the resource file to copy.</param>
        /// <param name="numberOfCopies">Number of copies to create.</param>
        /// <param name="newResourceName">Optional new name for the copied resource file.</param>
        /// <returns>List of paths to the newly created files.</returns>
        public static List<string> CreateResourceCopy(string resource, int numberOfCopies = 1, string newResourceName = null)
        {
            string originalResourcePath = GetTestFile(resource);
            string newResourcePath = originalResourcePath;
            if (!string.IsNullOrEmpty(newResourceName))
            {
                newResourcePath = Path.Combine(Path.GetDirectoryName(originalResourcePath), newResourceName);
            }

            List<string> copyPaths = new();
            for (int i = 0; i < numberOfCopies; i++)
            {
                string copyPath = PackageParser.GetNumericFilename(newResourcePath);
                File.Copy(originalResourcePath, copyPath);
                copyPaths.Add(copyPath);
            }

            return copyPaths;
        }

        /// <summary>
        /// Adds files to an existing test zip archive.
        /// </summary>
        /// <param name="zipResourceName">Name of the zip resource file.</param>
        /// <param name="filePaths">List of paths for files to be included in the zip archive.</param>
        public static void AddFilesToZip(string zipResourceName, List<string> filePaths)
        {
            string zipPath = GetTestFile(zipResourceName);
            using (ZipArchive zipArchive = ZipFile.Open(zipPath, ZipArchiveMode.Update))
            {
                foreach (string file in filePaths)
                {
                    var fileInfo = new FileInfo(file);
                    zipArchive.CreateEntryFromFile(fileInfo.FullName, fileInfo.Name);
                }
            } // The zipArchive is automatically closed and disposed here
        }

        /// <summary>
        /// Removes files from an existing test zip archive.
        /// </summary>
        /// <param name="zipResourceName">Name of the zip resource file.</param>
        /// <param name="fileNames">List of file names to be removed from the zip archive.</param>
        public static void RemoveFilesFromZip(string zipResourceName, List<string> fileNames)
        {
            string zipPath = GetTestFile(zipResourceName);
            using (ZipArchive zipArchive = ZipFile.Open(zipPath, ZipArchiveMode.Update))
            {
                foreach (string fileName in fileNames)
                {
                    zipArchive.GetEntry(fileName)?.Delete();
                }
            } // ZipArchive is automatically closed and disposed here
        }

        /// <summary>
        /// Delete test resources from cache directory.
        /// </summary>
        /// <param name="testFileNames">Name of the test files to delete.</param>
        public static void DeleteCachedFiles(List<string> testFileNames)
        {
            foreach (string fileName in testFileNames)
            {
                File.Delete(Path.Combine(PackageParser.InstallerDownloadPath, fileName));
            }
        }

        /// <summary>
        /// Execute the DSC command.
        /// </summary>
        /// <param name="args">The arguments to pass to the DSC command.</param>
        /// <returns>Result of executing the DSC command.</returns>
        public static async Task<DscExecuteResult> ExecuteDscCommandAsync(List<string> args)
        {
            var sw = new StringWriter();
            Console.SetOut(sw);
            var executeResult = await Parser.Default.ParseArguments<DscCommand>(args).Value.Execute();
            var output = sw.ToString();
            return new(executeResult, output);
        }
    }
}
