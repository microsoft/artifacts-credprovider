using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace NuGetCredentialProvider.Cancellation
{
    public class CancellationTokenSourceRegistry
    {
        public static CancellationTokenSourceRegistry Instance { get; } = new CancellationTokenSourceRegistry();

        private readonly ConditionalWeakTable<CancellationTokenSource, CancellationTokenSourceInfo> knownSources = new ConditionalWeakTable<CancellationTokenSource, CancellationTokenSourceInfo>();

        public CancellationTokenSourceInfo GetOrAdd(CancellationTokenSource cts)
        {
            var info = knownSources.GetValue(cts, _ => new CancellationTokenSourceInfo());
            if (!cts.IsCancellationRequested && info.TrySetIsInitialized())
            {
                cts.Token.Register(OnCancel, info);
            }

            return info;
        }

        private static void OnCancel(object obj)
        {
            var info = (CancellationTokenSourceInfo)obj;
            info.CancelingBecause(new CancelReason("[registered cancellation handler]", Environment.StackTrace));
        }
    }
}
