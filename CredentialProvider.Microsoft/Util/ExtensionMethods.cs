// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Text;
using NuGet.Common;
using ILogger = NuGetCredentialProvider.Logging.ILogger;

namespace NuGetCredentialProvider.Util
{
    /// <summary>
    /// Represents the set of extension methods used by this project.
    /// </summary>
    internal static class ExtensionMethods
    {
        /// <summary>
        /// Writes a <see cref="LogLevel.Error"/> event message to the <see cref="ILogger"/> using the specified message.
        /// </summary>
        /// <param name="logger">A <see cref="ILogger"/> instance to write the message to.</param>
        /// <param name="message">The message.</param>
        public static void Error(this ILogger logger, string message)
        {
            logger.Log(LogLevel.Error, message);
        }

        /// <summary>
        /// Writes a <see cref="LogLevel.Warning"/> event message to the <see cref="ILogger"/> using the specified message.
        /// </summary>
        /// <param name="logger">A <see cref="ILogger"/> instance to write the message to.</param>
        /// <param name="message">The message.</param>
        public static void Warning(this ILogger logger, string message)
        {
            logger.Log(LogLevel.Warning, message);
        }

        /// <summary>
        /// Writes a <see cref="LogLevel.Minimal"/> event message to the <see cref="ILogger"/> using the specified message.
        /// </summary>
        /// <param name="logger">A <see cref="ILogger"/> instance to write the message to.</param>
        /// <param name="message">The message.</param>
        public static void Minimal(this ILogger logger, string message)
        {
            logger.Log(LogLevel.Minimal, message);
        }

        /// <summary>
        /// Writes a <see cref="LogLevel.Information"/> event message to the <see cref="ILogger"/> using the specified message.
        /// </summary>
        /// <param name="logger">A <see cref="ILogger"/> instance to write the message to.</param>
        /// <param name="message">The message.</param>
        public static void Info(this ILogger logger, string message)
        {
            logger.Log(LogLevel.Information, message);
        }

        /// <summary>
        /// Writes a <see cref="LogLevel.Verbose"/> event message to the <see cref="ILogger"/> using the specified message.
        /// </summary>
        /// <param name="logger">A <see cref="ILogger"/> instance to write the message to.</param>
        /// <param name="message">The message.</param>
        public static void Verbose(this ILogger logger, string message)
        {
            logger.Log(LogLevel.Verbose, message);
        }

        /// <summary>
        /// Converts the current <see cref="Uri"/> with just the host by discarding the other parts like path and querystrings.
        /// </summary>
        /// <param name="uri">The current <see cref="Uri"/> to convert.</param>
        /// <returns>A <see cref="Uri"/> with only the host.</returns>
        public static Uri ToHostOnly(this Uri uri)
        {
            return uri.Segments.Length > 1
                ? new Uri($"{uri.Scheme}://{uri.Host}")
                : uri;
        }

        /// <summary>
        /// Converts the current string to a JSON web access token (JWT) as a string.
        /// </summary>
        /// <param name="accessToken">The current access token as a string.</param>
        /// <returns>A JWT as a JSON string.</returns>
        public static string ToJsonWebTokenString(this string accessToken)
        {
            // Effictively this splits by '.' and converts from a base-64 encoded string.  Splitting creates new strings so this just calculates
            // a substring instead to reduce memory overhead.
            int start = accessToken.IndexOf(".", StringComparison.Ordinal) + 1;

            if (start < 0)
            {
                return null;
            }

            int length = accessToken.IndexOf(".", start, StringComparison.Ordinal) - start;

            return start > 0 && length < accessToken.Length
                ? Encoding.UTF8.GetString(
                    Convert.FromBase64String(
                        accessToken.Substring(start, length)
                            .PadRight(length + (length % 4), '=')))
                : null;
        }
    }
}