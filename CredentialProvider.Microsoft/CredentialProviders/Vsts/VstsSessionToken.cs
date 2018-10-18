// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

using System;
using System.Runtime.Serialization;

namespace NuGetCredentialProvider.CredentialProviders.Vsts
{
    [DataContract]
    public class VstsSessionToken
    {
        [DataMember]
        public string DisplayName { get; set; }

        [DataMember]
        public string Scope { get; set; }

        [DataMember]
        public DateTime ValidTo { get; set; }

        [DataMember]
        public string Token { get; set; }
    }
}
