// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;

namespace NuGetCredentialProvider.Util
{
    /// <summary>
    /// Utility class for redacting sensitive information in logs.
    /// 
    /// Redaction Behavior:
    /// - Feed URLs: Controlled by ShouldRedact flag (set via -R flag or environment variable)
    /// - Passwords/Tokens: Controlled by ShouldRedact flag (set via -R flag or environment variable)
    /// </summary>
    public static class RedactionUtil
    {
        private static bool? shouldRedact;
        
        /// <summary>
        /// Gets or sets whether redaction should be enabled
        /// </summary>
        public static bool ShouldRedact
        {
            get
            {
                if (!shouldRedact.HasValue)
                {
                    // Check environment variable as fallback
                    shouldRedact = EnvUtil.GetRedactionEnabledFromEnvironment();
                }
                return shouldRedact.Value;
            }
            set
            {
                shouldRedact = value;
            }
        }

        /// <summary>
        /// Redacts a feed URL if redaction is enabled
        /// </summary>
        /// <param name="uri">The URI to potentially redact</param>
        /// <returns>The original URI string or a redacted version</returns>
        public static string RedactFeedUrl(Uri uri)
        {
            if (uri == null)
            {
                return null;
            }

            return RedactFeedUrl(uri.AbsoluteUri);
        }

        /// <summary>
        /// Redacts a feed URL if redaction is enabled
        /// </summary>
        /// <param name="uriString">The URI string to potentially redact</param>
        /// <returns>The original URI string or a redacted version</returns>
        public static string RedactFeedUrl(string uriString)
        {
            if (!ShouldRedact || string.IsNullOrEmpty(uriString))
            {
                return uriString;
            }

            // Redact all URLs when redaction is enabled
            // This prevents accidental exposure of any feed URLs, not just Azure Artifacts
            try
            {
                Uri uri = new Uri(uriString);
                // Preserve scheme for debugging context, redact the rest
                return $"{uri.Scheme}://[REDACTED_FEED_URL]";
            }
            catch
            {
                // If parsing fails, just return the redacted placeholder
                return Resources.RedactedFeedUrl;
            }
        }

        /// <summary>
        /// Redacts a password/token if redaction is enabled
        /// </summary>
        /// <param name="password">The password/token to potentially redact</param>
        /// <returns>The original password or a redacted version</returns>
        public static string RedactPassword(string password)
        {
            if (!ShouldRedact || string.IsNullOrEmpty(password))
            {
                return password;
            }

            return Resources.Redacted;
        }
    }
}
