// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCore.Common
{
    using System;

    /// <summary>
    /// Wrapper class for utilizing WinGetUtil.dll functionality.
    /// </summary>
    public static class WinGetUtil
    {
        private const string DllName = @"WinGetUtil.dll";

        /// <summary>
        /// Validates the manifest is compliant.
        /// </summary>
        /// <param name="manifestPath">Manifest path.</param>
        /// <returns>Message from manifest validation.</returns>
        public static (string Succeeded, string FailureOrWarningMessage) ValidateManifest(string manifestPath)
        {
#if Windows
            WinGetValidateManifest(
                manifestPath,
                out bool succeeded,
                out string failureOrWarningMessage);

            return (succeeded.ToString(), failureOrWarningMessage);
#endif

            return (Constants.ManifestValidationUnavailable, string.Empty);
        }

        /// <summary>
        /// Compares two versions and returns the comparison result.
        /// </summary>
        /// <param name="versionA">Version A.</param>
        /// <param name="versionB">Version B.</param>
        /// <returns>Int representing the version comparison result.</returns>
        public static int CompareVersions(string versionA, string versionB)
        {
#if Windows
            int hr = WinGetCompareVersions(versionA, versionB, out int comparisonResult);
            Marshal.ThrowExceptionForHR(hr);
            return comparisonResult;
#endif

            // Since WinGetUtil.dll is not available on non-Windows platforms, we will a simple version comparison.
            // First, try to parse the versions SemVer. If it fails, we will use string comparison.
            return Version.TryParse(versionA, out Version versionAObj) && Version.TryParse(versionB, out Version versionBObj)
                ? versionAObj.CompareTo(versionBObj)
                : string.Compare(versionA, versionB, StringComparison.OrdinalIgnoreCase);
        }

#if Windows
        /// <summary>
        /// Validates a given manifest. Returns a bool for validation result and
        /// a string representing validation errors if validation failed.
        /// </summary>
        /// <param name="manifestPath">Path to manifest file.</param>
        /// <param name="succeeded">Out bool is validation succeeded.</param>
        /// <param name="failureMessage">Out string failure message, if any.</param>
        /// <returns>HRESULT.</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern IntPtr WinGetValidateManifest(
            string manifestPath,
            [MarshalAs(UnmanagedType.U1)] out bool succeeded,
            [MarshalAs(UnmanagedType.BStr)] out string failureMessage);

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern int WinGetCompareVersions(
            string versionA,
            string versionB,
            out int comparisonResult);
#endif
    }
}
