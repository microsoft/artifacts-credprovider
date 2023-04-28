// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System.Net.Http.Headers;
using Microsoft.Identity.Client;

namespace Microsoft.Artifacts.Authentication;

public class MsalHttpClientFactory : IMsalHttpClientFactory
{
    private readonly HttpClient httpClient;

    public MsalHttpClientFactory(HttpClient httpClient)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

        var userAgent = this.httpClient.DefaultRequestHeaders.UserAgent;
        userAgent.Add(ProgramProduct);
        userAgent.Add(ProgramComment);
        userAgent.Add(ClrProduct);
        userAgent.Add(ClrComment);
    }

    public static ProductInfoHeaderValue ProgramProduct =>
        new ProductInfoHeaderValue(PlatformInformation.GetProgramName(), PlatformInformation.GetProgramVersion());

    public static ProductInfoHeaderValue ProgramComment =>
        new ProductInfoHeaderValue($"({PlatformInformation.GetOSType()}; {PlatformInformation.GetCpuArchitecture()}; {PlatformInformation.GetOsDescription()})");

    public static ProductInfoHeaderValue ClrProduct =>
        new ProductInfoHeaderValue("CLR", PlatformInformation.GetClrVersion());

    public static ProductInfoHeaderValue ClrComment =>
        new ProductInfoHeaderValue($"({PlatformInformation.GetClrFramework()}; {PlatformInformation.GetClrRuntime()}; {PlatformInformation.GetClrDescription()})");

    // Produces a value similar to the following:
    // CredentialProvider.Microsoft/1.0.4+aae4981de95d543b7935811c05474e393dd9e144 (Windows; X64; Microsoft Windows 10.0.19045) CLR/6.0.16 (.NETCoreApp,Version=v6.0; win10-x64; .NET 6.0.16)
    public static IEnumerable<ProductInfoHeaderValue> UserAgent =>
        Array.AsReadOnly(new[]
        {
            ProgramProduct,
            ProgramComment,
            ClrProduct,
            ClrComment
        });

    public HttpClient GetHttpClient()
    {
        return httpClient;
    }
}
