using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;

namespace NuGetCredentialProvider.Cancellation
{
    public class CancellationTokenSourceInfo
    {
        public IReadOnlyList<CancelReason> CancelReasons { get; private set; } = ImmutableList<CancelReason>.Empty;

        public void CancelingBecause(CancelReason reason)
        {
            CancelReasons = CancelReasons.ToImmutableList().Add(reason);
        }

        public IReadOnlyList<RegistrationInfo> Registrations { get; private set; } = ImmutableList<RegistrationInfo>.Empty;

        public void RecordRegistration(RegistrationInfo info)
        {
            Registrations = Registrations.ToImmutableList().Add(info);
        }

        public bool IsInitialized => isInitialized != 0;

        // Interlocked.CompareExchange doesn't support bool
        private int isInitialized;
        // returns true the first time it is called on this object, and false thereafter
        internal bool TrySetIsInitialized()
        {
            return Interlocked.CompareExchange(ref isInitialized, 1, 0) == 0;
        }
    }
}