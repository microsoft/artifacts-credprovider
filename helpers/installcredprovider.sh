#!/usr/bin/env bash
# DESCRIPTION: A simple shell script designed to fetch a version
# of the artifacts credential provider plugin and install it into $HOME/.nuget/plugins.
# Readme: https://github.com/Microsoft/artifacts-credprovider/blob/master/README.md

# Default version to install is the latest version.
# To install a release other than `latest`, set the `AZURE_ARTIFACTS_CREDENTIAL_PROVIDER_VERSION` environment
# variable to match the TAG NAME of a supported release, e.g. "v1.0.1".
# Releases: https://github.com/microsoft/artifacts-credprovider/releases

REPO="Microsoft/artifacts-credprovider"
NUGET_PLUGIN_DIR="$HOME/.nuget/plugins"

# .NET 6 is the default installation, attempt to install unless set to false.
if [ -z ${USE_NET6_ARTIFACTS_CREDENTIAL_PROVIDER} ] || [ ${USE_NET6_ARTIFACTS_CREDENTIAL_PROVIDER} != "false" ]; then
  FILE="Microsoft.Net6.NuGet.CredentialProvider.tar.gz"

  # throw if version starts with 0. (net6 not supported)
  case ${AZURE_ARTIFACTS_CREDENTIAL_PROVIDER_VERSION} in 
    0.*|v0.*)
      echo "ERROR: To install NET6 cred provider using the USE_NET6_ARTIFACTS_CREDENTIAL_PROVIDER variable, version to be installed must be 1.0.0 or greater. Check your AZURE_ARTIFACTS_CREDENTIAL_PROVIDER_VERSION variable."
      exit 1
      ;;
  esac
# Don't attempt to install .NET 8 without a set variable.
elif [ ! -z ${USE_NET8_ARTIFACTS_CREDENTIAL_PROVIDER} ] && [ ${USE_NET8_ARTIFACTS_CREDENTIAL_PROVIDER} != "false" ]; then
  FILE="Microsoft.Net8.NuGet.CredentialProvider.tar.gz"

  # throw if version starts < 1.3.0. (net8 not supported)
  case ${AZURE_ARTIFACTS_CREDENTIAL_PROVIDER_VERSION} in 
    0.*|v0.*|1.0.*|v1.0.*|1.1.*|v1.1.*|1.2.*|v1.2.*)
      echo "ERROR: To install NET8 cred provider using the USE_NET8_ARTIFACTS_CREDENTIAL_PROVIDER variable, version to be installed must be 1.3.0 or greater. Check your AZURE_ARTIFACTS_CREDENTIAL_PROVIDER_VERSION variable."
      exit 1
      ;;
  esac
# If .NET 6 is disabled and .NET 8 isn't explicitly enabled, fall back to the legacy .NET Framework.
else
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

#Fetch the file
if ! curl -H "Accept: application/octet-stream" \
     -s \
     -S \
     -L \
     "$URI" | tar xz -C "$HOME/.nuget/" "plugins/netcore"; then
        exit 1
fi

echo "INFO: credential provider netcore plugin extracted to $HOME/.nuget/"
