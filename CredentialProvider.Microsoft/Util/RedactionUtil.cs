// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;

namespace NuGetCredentialProvider.Util
{
    /// <summary>
    /// Utility class for redacting sensitive information in logs.
    /// </summary>
    public static class RedactionUtil
    {
        private static bool? shouldRedact;
        
        public static bool ShouldRedact
        {
            get => shouldRedact ?? (shouldRedact = EnvUtil.GetRedactionEnabledFromEnvironment()).Value;
            set => shouldRedact = value;
        }

        /// <summary>
        /// Redacts a feed URL if redaction is enabled
        /// </summary>
        public static string RedactFeedUrl(Uri uri) => 
            uri == null ? null : RedactFeedUrl(uri.AbsoluteUri);

        /// <summary>
        /// Redacts a feed URL if redaction is enabled
        /// </summary>
        public static string RedactFeedUrl(string uriString)
        {
            if (!ShouldRedact || string.IsNullOrEmpty(uriString))
            {
                return uriString;
            }

            try
            {
                var uri = new Uri(uriString);
                return $"{uri.Scheme}://[REDACTED_FEED_URL]";
            }
            catch
            {
                return Resources.RedactedFeedUrl;
            }
        }

        /// <summary>
        /// Redacts a password/token if redaction is enabled
        /// </summary>
        public static string RedactPassword(string password) =>
            !ShouldRedact || string.IsNullOrEmpty(password) ? password : Resources.Redacted;
    }
}
