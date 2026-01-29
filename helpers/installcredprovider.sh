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
VERSION_NORMALIZED=$(echo "${AZURE_ARTIFACTS_CREDENTIAL_PROVIDER_VERSION}" | sed 's/^v//')

# Detect musl libc (e.g., Alpine Linux)
is_musl() {
  # Check ldd output for musl, suppress errors if ldd is not available
  if command -v ldd >/dev/null 2>&1 && ldd --version 2>&1 | grep -qi musl; then
    return 0
  fi
  return 1
}

set_runtime_identifier() {
  # Use uname -s for POSIX-compliant OS detection (OSTYPE is bash-specific)
  OS_NAME=$(uname -s)
  case "$OS_NAME" in
  Linux*)
    # musl-based systems (like Alpine) are not supported for self-contained builds
    if is_musl; then
      echo "WARNING: musl libc detected. Self-contained builds are not available for musl-based systems. The .NET 8 runtime-dependent version will be installed instead." >&2
      return
    fi
    RUNTIME_ID="linux"
    ;;
  Darwin*)
    RUNTIME_ID="osx"
    ;;
  CYGWIN* | MINGW* | MSYS*)
    RUNTIME_ID="win"
    ;;
  *)
    echo "WARNING: Unable to automatically detect a supported OS from '$OS_NAME'. The .NET 8 version will be installed by default. Please set the ARTIFACTS_CREDENTIAL_PROVIDER_RID environment variable to specify a runtime version." >&2
    return
    ;;
  esac

  arch=$(uname -m)
  case "$arch" in
    x86_64 | amd64)
      OS_ARCH="-x64"
      ;;
    aarch64 | arm64)
      OS_ARCH="-arm64"
      ;;
    *)
      echo "WARNING: Unable to automatically detect a supported CPU architecture from '$arch'. The .NET 8 version will be installed by default. Please set the ARTIFACTS_CREDENTIAL_PROVIDER_RID environment variable to specify a runtime version." >&2
      return
      ;;
  esac

  # Windows on ARM64 runs x64 binaries (similar to PowerShell logic)
  if [ "$OS_ARCH" = "-arm64" ] && [ "$RUNTIME_ID" = "win" ]; then
    RUNTIME_ID="${RUNTIME_ID}-x64"
  else
    RUNTIME_ID="${RUNTIME_ID}${OS_ARCH}"
  fi

  echo "INFO: Calculated artifacts-credprovider RuntimeIdentifier: $RUNTIME_ID"
}

# If a RuntimeID (RID) is set, install the self-contained version of the .NET 8 credential provider.
# To install a release with a specific runtime version set the `ARTIFACTS_CREDENTIAL_PROVIDER_RID` enviornment variable.
if [ ! -z ${ARTIFACTS_CREDENTIAL_PROVIDER_RID} ]; then
  echo "INFO: ARTIFACTS_CREDENTIAL_PROVIDER_RID variable set, defaulting to self-contained installation."

  # If the RID is osx-*, use the zip file otherwise use the tar.gz file.
  # Self-contained builds use RID without .Net8 prefix (v2.0.0+)
  case ${ARTIFACTS_CREDENTIAL_PROVIDER_RID} in osx-*)
    FILE="Microsoft.${ARTIFACTS_CREDENTIAL_PROVIDER_RID}.NuGet.CredentialProvider.zip"
    ;;
  *)
    FILE="Microsoft.${ARTIFACTS_CREDENTIAL_PROVIDER_RID}.NuGet.CredentialProvider.tar.gz"
    ;;
  esac

  if [ -z ${USE_NET6_ARTIFACTS_CREDENTIAL_PROVIDER} ]; then
    echo "WARNING: The USE_NET6_ARTIFACTS_CREDENTIAL_PROVIDER variable is set, but ARTIFACTS_CREDENTIAL_PROVIDER_RID variable is defined. The self-contained version of the credential provider will be installed."
  fi

  # throw if version starts < 1.4.0. (self-contained not supported)
  case ${VERSION_NORMALIZED} in
    0.* | 1.0.* | 1.1.* | 1.2.* | 1.3.*)
      echo "ERROR: To install NET8 cred provider using the ARTIFACTS_CREDENTIAL_PROVIDER_RID variable, version to be installed must be 1.4.0 or greater. Check your AZURE_ARTIFACTS_CREDENTIAL_PROVIDER_VERSION variable."
      exit 1
      ;;
  esac
# .NET 6 is the legacy installation, attempt to install only when explicitly set.
elif [ ! -z ${USE_NET6_ARTIFACTS_CREDENTIAL_PROVIDER} ] && [ ${USE_NET6_ARTIFACTS_CREDENTIAL_PROVIDER} != "false" ] && [ ${USE_NET8_ARTIFACTS_CREDENTIAL_PROVIDER} != "true" ]; then
  FILE="Microsoft.Net6.NuGet.CredentialProvider.tar.gz"

  # throw if version starts with 0. (net6 not supported)
  case ${VERSION_NORMALIZED} in
    0.*)
      echo "ERROR: To install .NET 6 cred provider using the USE_NET6_ARTIFACTS_CREDENTIAL_PROVIDER variable, version to be installed must be 1.0.0 or greater. Check your AZURE_ARTIFACTS_CREDENTIAL_PROVIDER_VERSION variable."
      exit 1
      ;;
  esac
# The .NET 8 install is the default installation, attempt to install unless set to false.
# If .NET 8 variable is set, install the .NET 8 version of the credential provider even if USE_NET6_ARTIFACTS_CREDENTIAL_PROVIDER is true.
elif [ -z ${USE_NET8_ARTIFACTS_CREDENTIAL_PROVIDER} ] || [ ${USE_NET8_ARTIFACTS_CREDENTIAL_PROVIDER} != "false" ]; then
  if [ ! -z ${ARTIFACTS_CREDENTIAL_PROVIDER_NON_SC} ] && [ ${ARTIFACTS_CREDENTIAL_PROVIDER_NON_SC} != "false" ]; then
    # Default to the full zip file if ARTIFACTS_CREDENTIAL_PROVIDER_NON_SC is specified.
    # Runtime-dependent builds still use Net8 prefix
    FILE="Microsoft.Net8.NuGet.CredentialProvider.tar.gz"
  else
    # Get the correct runtime identifier for the self-contained version.
    # Self-contained builds use RID without .Net8 prefix (v2.0.0+)
    set_runtime_identifier

    # If RUNTIME_ID is not set (e.g., musl detected or unsupported OS/arch), fall back to runtime-dependent version
    if [ -z "${RUNTIME_ID}" ]; then
      FILE="Microsoft.Net8.NuGet.CredentialProvider.tar.gz"
    else
      case "${RUNTIME_ID}" in
      osx-*)
        FILE="Microsoft.${RUNTIME_ID}.NuGet.CredentialProvider.zip"
        ;;
      *)
        FILE="Microsoft.${RUNTIME_ID}.NuGet.CredentialProvider.tar.gz"
        ;;
      esac
    fi
  fi

  # throw if version starts < 1.3.0. (net8 not supported)
  case ${VERSION_NORMALIZED} in
    0.* | 1.0.* | 1.1.* | 1.2.*)
      echo "ERROR: To install NET8 cred provider using the USE_NET8_ARTIFACTS_CREDENTIAL_PROVIDER variable, version to be installed must be 1.3.0 or greater. Check your AZURE_ARTIFACTS_CREDENTIAL_PROVIDER_VERSION variable."
      exit 1
      ;;
  esac
# If .NET 6 or .NET 8 isn't being downloaded, fall back to the .NET Framework 4.8.1 version.
else
  FILE="Microsoft.NetFx48.NuGet.CredentialProvider.tar.gz"
fi

# If AZURE_ARTIFACTS_CREDENTIAL_PROVIDER_VERSION is set, install the version specified, otherwise install latest
if [ ! -z ${VERSION_NORMALIZED} ] && [ ${VERSION_NORMALIZED} != "latest" ]; then
  # browser_download_url from https://api.github.com/repos/Microsoft/artifacts-credprovider/releases/latest
  URI="https://github.com/$REPO/releases/download/v${VERSION_NORMALIZED}/$FILE"
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

# Remove existing content from Microsoft credential provider to ensure clean installation
if [ -d "${NUGET_PLUGIN_DIR}/netcore/CredentialProvider.Microsoft" ]; then
  echo "INFO: Removing existing Microsoft credential provider for clean installation"
  rm -rf "${NUGET_PLUGIN_DIR}/netcore/CredentialProvider.Microsoft"
fi

echo "INFO: Downloading from $URI"
# Extract netcore from the .tar.gz into the plugin directory

# Fetch the file
if ! curl -H "Accept: application/octet-stream" \
  -s \
  -S \
  -L \
  "$URI" | tar xz -C "$HOME/.nuget/" "plugins/netcore"; then
  exit 1
fi

echo "INFO: credential provider netcore plugin extracted to $HOME/.nuget/"
