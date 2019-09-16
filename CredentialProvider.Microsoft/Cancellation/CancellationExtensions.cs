using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace NuGetCredentialProvider.Cancellation
{
    public static class CancellationExtensions
    {
        public static void Register(this CancellationTokenSource cts, string name)
        {
            CancellationTokenSourceRegistry.Instance.GetOrAdd(cts).RecordRegistration(new RegistrationInfo(name, Environment.StackTrace));
        }

        public static void Cancel(this CancellationTokenSource cts, string reason)
        {
            CancellationTokenSourceRegistry.Instance.GetOrAdd(cts).CancelingBecause(new CancelReason(reason, Environment.StackTrace));
            cts.Cancel();
        }

        private static Regex NewLineRegex = new Regex("\r\n|\r|\n", RegexOptions.Compiled);

        private static IEnumerable<CancellationTokenSource> GetLinkedCancellationTokenSources(this CancellationTokenSource cts)
        {
            // TODO: degrade gracefully

#if NETCOREAPP
            // gross, rummaging around in the internals of corefx
            var linked1CancellationTokenSourceType = typeof(CancellationTokenSource).GetNestedType(
                "Linked1CancellationTokenSource",
                BindingFlags.NonPublic);

            var linked1CancellationTokenSourceReg1 =
                linked1CancellationTokenSourceType?.GetField("_reg1", BindingFlags.Instance | BindingFlags.NonPublic);

            var linked2CancellationTokenSourceType = typeof(CancellationTokenSource).GetNestedType(
                "Linked2CancellationTokenSource",
                BindingFlags.NonPublic);

            var linked2CancellationTokenSourceReg1 =
                linked2CancellationTokenSourceType?.GetField("_reg1", BindingFlags.Instance | BindingFlags.NonPublic);
            var linked2CancellationTokenSourceReg2 =
                linked2CancellationTokenSourceType?.GetField("_reg2", BindingFlags.Instance | BindingFlags.NonPublic);

            var linkedNCancellationTokenSourceType = typeof(CancellationTokenSource).GetNestedType(
                "LinkedNCancellationTokenSource",
                BindingFlags.NonPublic);

            var linkedNCancellationTokenSourceLinkingRegistrations =
                linkedNCancellationTokenSourceType?.GetField("m_linkingRegistrations", BindingFlags.Instance | BindingFlags.NonPublic);

            var cancellationTokenRegistrationTokenProperty = typeof(CancellationTokenRegistration).GetProperty("Token");

            var ctsType = cts.GetType();
            if (ctsType == typeof(CancellationTokenSource))
            {
                yield break;
            }
            else if (ctsType == linked1CancellationTokenSourceType)
            {
                var registration = (CancellationTokenRegistration) linked1CancellationTokenSourceReg1.GetValue(cts);
                yield return GetSourceFromRegistration(registration);
            }
            else if (ctsType == linked2CancellationTokenSourceType)
            {
                var registration1 = (CancellationTokenRegistration)linked2CancellationTokenSourceReg1.GetValue(cts);
                yield return GetSourceFromRegistration(registration1);

                var registration2 = (CancellationTokenRegistration)linked2CancellationTokenSourceReg2.GetValue(cts);
                yield return GetSourceFromRegistration(registration2);
            }
            else if (ctsType == linkedNCancellationTokenSourceType)
            {
                var registrations =
                    (CancellationTokenRegistration[])linkedNCancellationTokenSourceLinkingRegistrations.GetValue(cts);

                foreach (var registration in registrations)
                {
                    yield return GetSourceFromRegistration(registration);
                }
            }

            CancellationTokenSource GetSourceFromRegistration(CancellationTokenRegistration reg)
            {
                var token = (CancellationToken)cancellationTokenRegistrationTokenProperty.GetValue(reg);
                return token.GetSource();
            }

#elif NETFRAMEWORK
            // gross, rummaging around in the internals of netfx

            var cancellationTokenSourceLinkingRegistrations =
                typeof(CancellationTokenSource).GetField("m_linkingRegistrations", BindingFlags.Instance | BindingFlags.NonPublic);
            var cancellationTokenRegistrationCallbackInfoField = typeof(CancellationTokenRegistration).GetField("m_callbackInfo", BindingFlags.Instance | BindingFlags.NonPublic);
            var cancellationCallbackInfoType = typeof(CancellationTokenSource).Assembly.GetType("System.Threading.CancellationCallbackInfo");
            var cancellationCallbackInfoCancellationTokenSourceField = cancellationCallbackInfoType?.GetField("CancellationTokenSource", BindingFlags.Instance | BindingFlags.NonPublic);

            var registrations =
                (CancellationTokenRegistration[])cancellationTokenSourceLinkingRegistrations.GetValue(cts);

            if(registrations is null)
            {
                // m_linkingRegistrations is only set if it is used
                yield break;
            }

            foreach(var registration in registrations)
            {
                var callbackInfo = cancellationTokenRegistrationCallbackInfoField.GetValue(registration);
                yield return (CancellationTokenSource)cancellationCallbackInfoCancellationTokenSourceField.GetValue(callbackInfo);
            }


#else
            yield break;
#endif

        }

        public static string DumpDiagnostics(this CancellationTokenSource cts)
        {
            var sb = new StringBuilder();
            sb.AppendLine("==== CancellationTokenSource Diagnostics ====");
            sb.AppendLine($"This CancellationTokenSource [{RuntimeHelpers.GetHashCode(cts):X}]");
            DumpDiagnosticsCore(sb, cts, ImmutableList<int>.Empty, "");
            sb.AppendLine("==== End CancellationTokenSource Diagnostics ====");
            return sb.ToString();
        }

        private static void DumpDiagnosticsCore(
            StringBuilder sb,
            CancellationTokenSource node,
            ImmutableList<int> path,
            string indent)
        {
            if (node == null || node.Token == CancellationToken.None)
            {
                sb.AppendLine($"{indent}[CancellationToken.None]");
                return;
            }

            sb.AppendLine($"{indent}IsCancellationRequested: {node.IsCancellationRequested}");

            var info = CancellationTokenSourceRegistry.Instance.GetOrAdd(node);
            sb.AppendLine($"{indent}Is registered for notifications: {info.IsInitialized}");
            sb.AppendLine($"{indent}Registrations:");
            var stackTracePrefix = $"{indent}  ";
            foreach (var infoRegistration in info.Registrations)
            {
                sb.AppendLine($"{indent}- {infoRegistration.Name}");
                sb.AppendStackTraceLines(stackTracePrefix, infoRegistration.StackTrace);
            }

            sb.AppendLine($"{indent}Cancellation reasons:");
            foreach (var cancelReason in info.CancelReasons)
            {
                sb.AppendLine($"{indent}- {cancelReason.Reason}");
                sb.AppendStackTraceLines(stackTracePrefix, cancelReason.StackTrace);
            }

            var linkedSources = GetLinkedCancellationTokenSources(node);
            foreach (var (linkedSource, index) in linkedSources.Select((x, idx) => (x, idx)))
            {
                var newIndent = indent + "    ";
                var newPath = path.Add(index);
                sb.AppendLine($"{newIndent}---- Linked CancellationTokenSource {string.Join(".", newPath)} [{RuntimeHelpers.GetHashCode(linkedSource):X}]");
                DumpDiagnosticsCore(sb, linkedSource, newPath, newIndent);
            }
        }

        private static void AppendStackTraceLines(this StringBuilder sb, string prefix, string text)
        {
            foreach (var line in NewLineRegex.Split(text))
            {
                sb.AppendLine($"{prefix}{line.TrimStart()}");
            }
        }

        private static readonly FieldInfo NetCoreSourceField = typeof(CancellationToken).GetField(
            "_source",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo NetFxSourceField = typeof(CancellationToken).GetField(
            "m_source",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo SourceField = NetFxSourceField ?? NetCoreSourceField;

        private static readonly Func<object, object> ReflectGetSourceFromToken = SourceField.GetValue;

        private static CancellationTokenSource GetSource(this CancellationToken token)
        {
            return (CancellationTokenSource)ReflectGetSourceFromToken(token);
        }

        public static void EnsureSourceRegistered(this CancellationToken token, string name)
        {
            token.GetSource()?.Register("via EnsureSourceRegistered: " + name);
        }

        public static string DumpDiagnostics(this CancellationToken token)
        {
            // DumpDiagnostics is OK with nulls
            return token.GetSource().DumpDiagnostics();
        }


    }
}