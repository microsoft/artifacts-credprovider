#!/usr/bin/env bash
# DESCRIPTION: A simple shell script designed to fetch a version
# of the artifacts credential provider plugin and install it into $HOME/.nuget/plugins.
# Default version to install is the latest version.
# To install a specific version, call the script with the TAG NAME of the version,
# e.g. "installcredprovider.sh v0.1.28". Find the tag name of the version from https://github.com/microsoft/artifacts-credprovider/releases
# More: https://github.com/Microsoft/artifacts-credprovider/blob/master/README.md

REPO="Microsoft/artifacts-credprovider"
FILE="Microsoft.NuGet.CredentialProvider.tar.gz"
NUGET_PLUGIN_DIR="$HOME/.nuget/plugins"

# If no arguments, install latest version
if [ -z "$1" ]; then
  # URL pattern to get latest documented at https://help.github.com/en/articles/linking-to-releases as of 2019-03-29
  URI="https://github.com/$REPO/releases/latest/download/$FILE"
else
  # browser_download_url from https://api.github.com/repos/Microsoft/artifacts-credprovider/releases/latest
  URI="https://github.com/$REPO/releases/download/$1/$FILE"
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
curl -H "Accept: application/octet-stream" \
     -s \
     -S \
     -L \
     "$URI" | tar xz -C "$HOME/.nuget/" "plugins/netcore"

echo "INFO: credential provider netcore plugin extracted to $HOME/.nuget/"
