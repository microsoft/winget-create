// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI
{
    using System;
    using System.Runtime.InteropServices;
    using System.Text;
    using Windows.Win32;
    using Windows.Win32.Foundation;
    using Windows.Win32.Security.Credentials;

    /// <summary>
    /// Provides functionality for caching and retrieving the GitHub OAuth token.
    /// </summary>
    public static class TokenHelper
    {
        // Windows credentials manager
        private const string CredTargetName = "git:https://aka.ms/winget-create";
        private const string CredUserName = "Personal Access Token";

        // Environment variable
        private const string TokenEnvironmentVariable = "WINGET_CREATE_GITHUB_TOKEN";

        /// <summary>
        /// Deletes the token.
        /// </summary>
        /// <returns>True if the token was deleted, false otherwise.</returns>
        public static bool Delete()
        {
            return PInvoke.CredDelete(CredTargetName, CRED_TYPE.CRED_TYPE_GENERIC);
        }

        /// <summary>
        /// Reads the token.
        /// </summary>
        /// <param name="token">Output token.</param>
        /// <returns>True if the token was read, false otherwise.</returns>
        public static unsafe bool TryRead(out string token) => TryReadFromEnvironmentVariable(out token) || TryReadFromCredentialManager(out token);

        /// <summary>
        /// Writes the token.
        /// </summary>
        /// <param name="token">Token to be cached.</param>
        /// <returns>True if the token was written, false otherwise.</returns>
        public static unsafe bool Write(string token)
        {
            var tokenBytes = Encoding.Unicode.GetBytes(token);
            fixed (byte* tokenBytesPtr = tokenBytes)
            {
                var credTargetNamePtr = Marshal.StringToHGlobalUni(CredTargetName);
                var credUserNamePtr = Marshal.StringToHGlobalUni(CredUserName);

                try
                {
                    var credential = new CREDENTIALW
                    {
                        Type = CRED_TYPE.CRED_TYPE_GENERIC,
                        TargetName = new PWSTR(credTargetNamePtr),
                        UserName = new PWSTR(credUserNamePtr),
                        CredentialBlobSize = (uint)tokenBytes.Length,
                        CredentialBlob = tokenBytesPtr,
                        Persist = CRED_PERSIST.CRED_PERSIST_LOCAL_MACHINE,
                    };

                    return PInvoke.CredWrite(&credential, /* None */ 0);
                }
                finally
                {
                    Marshal.FreeHGlobal(credTargetNamePtr);
                    Marshal.FreeHGlobal(credUserNamePtr);
                }
            }
        }

        /// <summary>
        /// Tries to read the token from the Windows credentials manager.
        /// </summary>
        /// <param name="token">Output token.</param>
        /// <returns>True if the token was read, false otherwise.</returns>
        private static unsafe bool TryReadFromCredentialManager(out string token)
        {
            if (PInvoke.CredRead(CredTargetName, CRED_TYPE.CRED_TYPE_GENERIC, out CREDENTIALW* credentialObject) && credentialObject != null)
            {
                try
                {
                    var accessTokenInBytes = new byte[credentialObject->CredentialBlobSize];
                    Marshal.Copy((IntPtr)credentialObject->CredentialBlob, accessTokenInBytes, 0, accessTokenInBytes.Length);
                    token = Encoding.Unicode.GetString(accessTokenInBytes);
                    return true;
                }
                finally
                {
                    PInvoke.CredFree(credentialObject);
                }
            }

            token = null;
            return false;
        }

        /// <summary>
        /// Tries to read the token from the environment variable.
        /// </summary>
        /// <param name="token">Output token.</param>
        /// <returns>True if the token was read, false otherwise.</returns>
        private static bool TryReadFromEnvironmentVariable(out string token)
        {
            var envToken = Environment.GetEnvironmentVariable(TokenEnvironmentVariable);
            if (!string.IsNullOrEmpty(envToken))
            {
                token = envToken;
                return true;
            }

            token = null;
            return false;
        }
    }
}
