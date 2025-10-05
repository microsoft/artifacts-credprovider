#!/usr/bin/env bash
# DESCRIPTION: A simple shell script designed to fetch a version
# of the artifacts credential provider plugin and install it into $HOME/.nuget/plugins.
# Readme: https://github.com/Microsoft/artifacts-credprovider/blob/master/README.md

# Default version to install is the latest version.
# To install a release other than `latest`, set the `AZURE_ARTIFACTS_CREDENTIAL_PROVIDER_VERSION` environment
# variable to match the TAG NAME of a supported release, e.g. "v1.0.1".
# Releases: https://github.com/microsoft/artifacts-credprovider/releases

# SKIP_CERTIFICATE_CHECK: Set to "true" to skip SSL certificate validation (default: false)
# WARNING: This should ONLY be used in development and build environments where certificate
# validation fails due to:
#   - Corporate proxies that intercept SSL traffic
#   - Docker containers with incomplete certificate chains
#   - Build agents behind firewalls
# This is SAFE for build purposes when fetching packages from password-protected private NuGet feeds,
# as the feed authentication itself provides security. However, this is NOT RECOMMENDED for production
# runtime environments.
# Usage: SKIP_CERTIFICATE_CHECK=true ./installcredprovider.sh
SKIP_CERTIFICATE_CHECK="${SKIP_CERTIFICATE_CHECK:-false}"

REPO="Microsoft/artifacts-credprovider"
NUGET_PLUGIN_DIR="$HOME/.nuget/plugins"

# If a RuntimeID (RID) is set, install the self-contained version of the .NET 8 credential provider.
# To install a release with a specific runtime version set the `ARTIFACTS_CREDENTIAL_PROVIDER_RID` enviornment variable.
if [ ! -z ${ARTIFACTS_CREDENTIAL_PROVIDER_RID} ]; then
  echo "INFO: ARTIFACTS_CREDENTIAL_PROVIDER_RID variable set, defaulting to NET8 installation."

  # If the RID is osx-*, use the zip file otherwise use the tar.gz file.
  case ${ARTIFACTS_CREDENTIAL_PROVIDER_RID} in osx-* )
    FILE="Microsoft.Net8.${ARTIFACTS_CREDENTIAL_PROVIDER_RID}.NuGet.CredentialProvider.zip"
    ;;
    *)
    FILE="Microsoft.Net8.${ARTIFACTS_CREDENTIAL_PROVIDER_RID}.NuGet.CredentialProvider.tar.gz"
    ;;
  esac

  if [ -z ${USE_NET6_ARTIFACTS_CREDENTIAL_PROVIDER} ]; then
    echo "WARNING: The USE_NET6_ARTIFACTS_CREDENTIAL_PROVIDER variable is set, but ARTIFACTS_CREDENTIAL_PROVIDER_RID variable is defined. The NET8 version of the credential provider will be installed."
  fi

  # throw if version starts < 1.4.0. (self-contained not supported)
  case ${AZURE_ARTIFACTS_CREDENTIAL_PROVIDER_VERSION} in
    0.*|v0.*|1.0.*|v1.0.*|1.1.*|v1.1.*|1.2.*|v1.2.*|1.3.*|v1.3.*)
      echo "ERROR: To install NET8 cred provider using the ARTIFACTS_CREDENTIAL_PROVIDER_RID variable, version to be installed must be 1.4.0 or greater. Check your AZURE_ARTIFACTS_CREDENTIAL_PROVIDER_VERSION variable."
      exit 1
      ;;
  esac
# If .NET 8 variable is set, install the .NET 8 version of the credential provider even if USE_NET6_ARTIFACTS_CREDENTIAL_PROVIDER is true.
elif [ ! -z ${USE_NET8_ARTIFACTS_CREDENTIAL_PROVIDER} ] && [ ${USE_NET8_ARTIFACTS_CREDENTIAL_PROVIDER} != "false" ]; then
  # Default to the full zip file since ARTIFACTS_CREDENTIAL_PROVIDER_RID is not specified.
  FILE="Microsoft.Net8.NuGet.CredentialProvider.tar.gz"

  if [ -z ${USE_NET6_ARTIFACTS_CREDENTIAL_PROVIDER} ]; then
    echo "WARNING: The USE_NET6_ARTIFACTS_CREDENTIAL_PROVIDER variable is set, but USE_NET8_ARTIFACTS_CREDENTIAL_PROVIDER variable is true. The NET8 version of the credential provider will be installed."
  fi

  # throw if version starts < 1.3.0. (net8 not supported)
  case ${AZURE_ARTIFACTS_CREDENTIAL_PROVIDER_VERSION} in
    0.*|v0.*|1.0.*|v1.0.*|1.1.*|v1.1.*|1.2.*|v1.2.*)
      echo "ERROR: To install NET8 cred provider using the USE_NET8_ARTIFACTS_CREDENTIAL_PROVIDER variable, version to be installed must be 1.3.0 or greater. Check your AZURE_ARTIFACTS_CREDENTIAL_PROVIDER_VERSION variable."
      exit 1
      ;;
  esac
# .NET 6 is the default installation, attempt to install unless set to false.
elif [ -z ${USE_NET6_ARTIFACTS_CREDENTIAL_PROVIDER} ] || [ ${USE_NET6_ARTIFACTS_CREDENTIAL_PROVIDER} != "false" ]; then
  FILE="Microsoft.Net6.NuGet.CredentialProvider.tar.gz"

  # throw if version starts with 0. (net6 not supported)
  case ${AZURE_ARTIFACTS_CREDENTIAL_PROVIDER_VERSION} in
    0.*|v0.*)
      echo "ERROR: To install NET6 cred provider using the USE_NET6_ARTIFACTS_CREDENTIAL_PROVIDER variable, version to be installed must be 1.0.0 or greater. Check your AZURE_ARTIFACTS_CREDENTIAL_PROVIDER_VERSION variable."
      exit 1
      ;;
  esac
# If .NET 6 is disabled and .NET 8 isn't explicitly enabled, fall back to the legacy .NET Framework.
else
  echo "WARNING: The .Net Framework 3.1 version of the Credential Provider is deprecated and will be removed in the next major release. Please migrate to the .Net Framework 4.8 or .Net Core versions."
  FILE="Microsoft.NuGet.CredentialProvider.tar.gz"
fi

# If AZURE_ARTIFACTS_CREDENTIAL_PROVIDER_VERSION is set, install the version specified, otherwise install latest
if [ ! -z ${AZURE_ARTIFACTS_CREDENTIAL_PROVIDER_VERSION} ] && [ ${AZURE_ARTIFACTS_CREDENTIAL_PROVIDER_VERSION} != "latest" ]; then
  # browser_download_url from https://api.github.com/repos/Microsoft/artifacts-credprovider/releases/latest
  URI="https://github.com/$REPO/releases/download/${AZURE_ARTIFACTS_CREDENTIAL_PROVIDER_VERSION}/$FILE"
else
  # URL pattern to get latest documented at https://help.github.com/en/articles/linking-to-releases as of 2019-03-29
  URI="https://github.com/$REPO/releases/latest/download/$FILE"
fi

# Ensure plugin directory exists
if [ ! -d "${NUGET_PLUGIN_DIR}" ]; then
  echo "INFO: Creating the nuget plugin directory (i.e. ${NUGET_PLUGIN_DIR}). "
  if ! mkdir -p "${NUGET_PLUGIN_DIR}"; then
      echo "ERROR: Unable to create nuget plugins directory (i.e. ${NUGET_PLUGIN_DIR})."
      exit 1
  fi
fi

echo "Downloading from $URI"
# Extract netcore from the .tar.gz into the plugin directory

# Build curl command with optional certificate check skip
CURL_OPTS=(-H "Accept: application/octet-stream" -s -S -L)
if $SKIP_CERTIFICATE_CHECK; then
  echo "WARNING: Certificate validation is disabled. This should only be used in development/build environments."
  CURL_OPTS+=(-k)
fi

#Fetch the file
if ! curl "${CURL_OPTS[@]}" "$URI" | tar xz -C "$HOME/.nuget/" "plugins/netcore"; then
  exit 1
fi

echo "INFO: credential provider netcore plugin extracted to $HOME/.nuget/"
