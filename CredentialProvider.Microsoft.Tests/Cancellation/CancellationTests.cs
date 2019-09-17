using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGet.Protocol.Cancellation;

namespace CredentialProvider.Microsoft.Tests.Cancellation
{
    [TestClass]
    public class CancellationTests
    {
        [TestMethod]
        public void CancellationDiagnosticsDemo()
        {
            var cts1 = new CancellationTokenSource();
            var cts2 = new CancellationTokenSource();
            var cts3 = CancellationTokenSource.CreateLinkedTokenSource(cts1.Token, cts2.Token);
            var cts4 = CancellationTokenSource.CreateLinkedTokenSource(cts3.Token);
            cts1.Register("foo");
            cts1.Token.EnsureSourceRegistered("cts1");

            cts1.Cancel();
            Console.WriteLine(cts4.DumpDiagnostics());
            Console.WriteLine(cts1.Token.DumpDiagnostics());
        }

    }
}
