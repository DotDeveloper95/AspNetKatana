﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;

namespace Microsoft.Owin.Infrastructure
{
    /// <summary>
    /// This handles cookies that are limited by per cookie length. It breaks down long cookies for responses, and reassembles them
    /// from requests.
    /// </summary>
    public class ChunkingCookieManager : ICookieManager
    {
        /// <summary>
        /// Creates a new instance of ChunkingCookieManager.
        /// </summary>
        public ChunkingCookieManager()
        {
            ChunkSize = 4090;
            ThrowForPartialCookies = true;
        }

        /// <summary>
        /// The maximum size of cookie to send back to the client. If a cookie exceeds this size it will be broken down into multiple
        /// cookies. Set this value to null to disable this behavior. The default is 4090 characters, which is supported by all
        /// common browsers.
        ///
        /// Note that browsers may also have limits on the total size of all cookies per domain, and on the number of cookies per domain.
        /// </summary>
        public int? ChunkSize { get; set; }

        /// <summary>
        /// Throw if not all chunks of a cookie are available on a request for re-assembly.
        /// </summary>
        public bool ThrowForPartialCookies { get; set; }

        // Parse the "chunks:XX" to determine how many chunks there should be.
        private static int ParseChunksCount(string value)
        {
            if (value != null && value.StartsWith("chunks:", StringComparison.Ordinal))
            {
                string chunksCountString = value.Substring("chunks:".Length);
                int chunksCount;
                if (int.TryParse(chunksCountString, System.Globalization.NumberStyles.None, CultureInfo.InvariantCulture, out chunksCount))
                {
                    return chunksCount;
                }
            }
            return 0;
        }

        /// <summary>
        /// Get the reassembled cookie. Non chunked cookies are returned normally.
        /// Cookies with missing chunks just have their "chunks:XX" header returned.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="key"></param>
        /// <returns>The reassembled cookie, if any, or null.</returns>
        public string GetRequestCookie(IOwinContext context, string key)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            RequestCookieCollection requestCookies = context.Request.Cookies;
            string value = requestCookies[key];
            int chunksCount = ParseChunksCount(value);
            if (chunksCount > 0)
            {
                bool quoted = false;
                string[] chunks = new string[chunksCount];
                for (int chunkId = 1; chunkId <= chunksCount; chunkId++)
                {
                    string chunk = requestCookies[key + "C" + chunkId.ToString(CultureInfo.InvariantCulture)];
                    if (chunk == null)
                    {
                        if (ThrowForPartialCookies)
                        {
                            int totalSize = 0;
                            for (int i = 0; i < chunkId - 1; i++)
                            {
                                totalSize += chunks[i].Length;
                            }
                            throw new FormatException(
                                string.Format(CultureInfo.CurrentCulture, Resources.Exception_ImcompleteChunkedCookie, chunkId - 1, chunksCount, totalSize));
                        }
                        // Missing chunk, abort by returning the original cookie value. It may have been a false positive?
                        return value;
                    }
                    if (IsQuoted(chunk))
                    {
                        // Note: Since we assume these cookies were generated by our code, then we can assume that if one cookie has quotes then they all do.
                        quoted = true;
                        chunk = RemoveQuotes(chunk);
                    }
                    chunks[chunkId - 1] = chunk;
                }
                string merged = string.Join(string.Empty, chunks);
                if (quoted)
                {
                    merged = Quote(merged);
                }
                return merged;
            }
            return value;
        }

        /// <summary>
        /// Appends a new response cookie to the Set-Cookie header. If the cookie is larger than the given size limit
        /// then it will be broken down into multiple cookies as follows:
        /// Set-Cookie: CookieName=chunks:3; path=/
        /// Set-Cookie: CookieNameC1=Segment1; path=/
        /// Set-Cookie: CookieNameC2=Segment2; path=/
        /// Set-Cookie: CookieNameC3=Segment3; path=/
        /// </summary>
        /// <param name="context"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="options"></param>
        public void AppendResponseCookie(IOwinContext context, string key, string value, CookieOptions options)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }
            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            bool domainHasValue = !string.IsNullOrEmpty(options.Domain);
            bool pathHasValue = !string.IsNullOrEmpty(options.Path);
            bool expiresHasValue = options.Expires.HasValue;

            string escapedKey = Uri.EscapeDataString(key);
            string prefix = escapedKey + "=";

            string suffix = string.Concat(
                !domainHasValue ? null : "; domain=",
                !domainHasValue ? null : options.Domain,
                !pathHasValue ? null : "; path=",
                !pathHasValue ? null : options.Path,
                !expiresHasValue ? null : "; expires=",
                !expiresHasValue ? null : options.Expires.Value.ToString("ddd, dd-MMM-yyyy HH:mm:ss ", CultureInfo.InvariantCulture) + "GMT",
                !options.Secure ? null : "; secure",
                !options.HttpOnly ? null : "; HttpOnly");

            value = value ?? string.Empty;
            bool quoted = false;
            if (IsQuoted(value))
            {
                quoted = true;
                value = RemoveQuotes(value);
            }
            string escapedValue = Uri.EscapeDataString(value);

            // Normal cookie
            IHeaderDictionary responseHeaders = context.Response.Headers;
            if (!ChunkSize.HasValue || ChunkSize.Value > prefix.Length + escapedValue.Length + suffix.Length + (quoted ? 2 : 0))
            {
                string setCookieValue = string.Concat(
                    prefix,
                    quoted ? Quote(escapedValue) : escapedValue,
                    suffix);
                responseHeaders.AppendValues(Constants.Headers.SetCookie, setCookieValue);
            }
            else if (ChunkSize.Value < prefix.Length + suffix.Length + (quoted ? 2 : 0) + 10)
            {
                // 10 is the minimum data we want to put in an individual cookie, including the cookie chunk identifier "CXX".
                // No room for data, we can't chunk the options and name
                throw new InvalidOperationException(Resources.Exception_CookieLimitTooSmall);
            }
            else
            {
                // Break the cookie down into multiple cookies.
                // Key = CookieName, value = "Segment1Segment2Segment2"
                // Set-Cookie: CookieName=chunks:3; path=/
                // Set-Cookie: CookieNameC1="Segment1"; path=/
                // Set-Cookie: CookieNameC2="Segment2"; path=/
                // Set-Cookie: CookieNameC3="Segment3"; path=/
                int dataSizePerCookie = ChunkSize.Value - prefix.Length - suffix.Length - (quoted ? 2 : 0) - 3; // Budget 3 chars for the chunkid.
                int cookieChunkCount = (int)Math.Ceiling(escapedValue.Length * 1.0 / dataSizePerCookie);

                responseHeaders.AppendValues(Constants.Headers.SetCookie, prefix + "chunks:" + cookieChunkCount.ToString(CultureInfo.InvariantCulture) + suffix);
                
                string[] chunks = new string[cookieChunkCount];
                int offset = 0;
                for (int chunkId = 1; chunkId <= cookieChunkCount; chunkId++)
                {
                    int remainingLength = escapedValue.Length - offset;
                    int length = Math.Min(dataSizePerCookie, remainingLength);
                    string segment = escapedValue.Substring(offset, length);
                    offset += length;

                    chunks[chunkId - 1] = string.Concat(
                                            escapedKey,
                                            "C",
                                            chunkId.ToString(CultureInfo.InvariantCulture),
                                            "=",
                                            quoted ? "\"" : string.Empty,
                                            segment,
                                            quoted ? "\"" : string.Empty,
                                            suffix);
                }
                responseHeaders.AppendValues(Constants.Headers.SetCookie, chunks);
            }
        }

        /// <summary>
        /// Deletes the cookie with the given key by setting an expired state. If a matching chunked cookie exists on
        /// the request, delete each chunk.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="key"></param>
        /// <param name="options"></param>
        public void DeleteCookie(IOwinContext context, string key, CookieOptions options)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }
            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            string escapedKey = Uri.EscapeDataString(key);
            List<string> keys = new List<string>();
            keys.Add(escapedKey + "=");

            string requestCookie = context.Request.Cookies[key];
            int chunks = ParseChunksCount(requestCookie);
            if (chunks > 0)
            {
                for (int i = 1; i <= chunks + 1; i++)
                {
                    string subkey = escapedKey + "C" + i.ToString(CultureInfo.InvariantCulture);
                    keys.Add(subkey + "=");
                }
            }

            bool domainHasValue = !string.IsNullOrEmpty(options.Domain);
            bool pathHasValue = !string.IsNullOrEmpty(options.Path);

            Func<string, bool> rejectPredicate;
            Func<string, bool> predicate = value => keys.Any(k => value.StartsWith(k, StringComparison.OrdinalIgnoreCase));
            if (domainHasValue)
            {
                rejectPredicate = value => predicate(value) && value.IndexOf("domain=" + options.Domain, StringComparison.OrdinalIgnoreCase) != -1;
            }
            else if (pathHasValue)
            {
                rejectPredicate = value => predicate(value) && value.IndexOf("path=" + options.Path, StringComparison.OrdinalIgnoreCase) != -1;
            }
            else
            {
                rejectPredicate = value => predicate(value);
            }

            IHeaderDictionary responseHeaders = context.Response.Headers;
            IList<string> existingValues = responseHeaders.GetValues(Constants.Headers.SetCookie);
            if (existingValues != null)
            {
                responseHeaders.SetValues(Constants.Headers.SetCookie, existingValues.Where(value => !rejectPredicate(value)).ToArray());
            }

            AppendResponseCookie(
                context,
                key,
                string.Empty,
                new CookieOptions
                {
                    Path = options.Path,
                    Domain = options.Domain,
                    Expires = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                });

            for (int i = 1; i <= chunks; i++)
            {
                AppendResponseCookie(
                    context, 
                    key + "C" + i.ToString(CultureInfo.InvariantCulture),
                    string.Empty,
                    new CookieOptions
                    {
                        Path = options.Path,
                        Domain = options.Domain,
                        Expires = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    });
            }
        }

        private static bool IsQuoted(string value)
        {
            return value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"';
        }

        private static string RemoveQuotes(string value)
        {
            return value.Substring(1, value.Length - 2);
        }

        private static string Quote(string value)
        {
            return '"' + value + '"';
        }
    }
}
