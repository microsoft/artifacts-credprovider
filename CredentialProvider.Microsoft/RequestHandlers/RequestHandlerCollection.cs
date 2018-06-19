// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Collections.Concurrent;
using NuGet.Protocol.Plugins;

namespace NuGetCredentialProvider.RequestHandlers
{
    /// <summary>
    /// Represents a collection of NuGet plug-in request handlers.
    /// </summary>
    /// <remarks>This custom collection is used instead of <see cref="RequestHandlers"/> because it inherits
    /// <see cref="ConcurrentDictionary{TKey,TValue}"/> which allows for the initializer syntax.</remarks>
    internal class RequestHandlerCollection : ConcurrentDictionary<MessageMethod, IRequestHandler>, IRequestHandlers
    {
        public void Add(MessageMethod method, IRequestHandler handler)
        {
            TryAdd(method, handler);
        }

        public void AddOrUpdate(MessageMethod method, Func<IRequestHandler> addHandlerFunc, Func<IRequestHandler, IRequestHandler> updateHandlerFunc)
        {
            AddOrUpdate(method, messageMethod => addHandlerFunc(), (messageMethod, requestHandler) => updateHandlerFunc(requestHandler));
        }

        public bool TryGet(MessageMethod method, out IRequestHandler requestHandler)
        {
            return TryGetValue(method, out requestHandler);
        }

        public bool TryRemove(MessageMethod method)
        {
            return TryRemove(method, out IRequestHandler _);
        }
    }
}