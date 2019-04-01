#!/bin/sh
# DESCRIPTION: A simple shell script designed to fetch the latest version
# of the artifacts credential provider plugin for dotnet and
# install it into $HOME/.nuget/plugins.
# SEE: https://github.com/Microsoft/artifacts-credprovider/blob/master/README.md

GITHUB="https://github.com"
REPO="Microsoft/artifacts-credprovider"
FILE="Microsoft.NuGet.CredentialProvider.tar.gz"
VERSION="latest"
# URL pattern documented at https://help.github.com/en/articles/linking-to-releases as of 2019-03-29
RELEASEURL="$GITHUB/$REPO/releases/$VERSION/download/$FILE"
NUGET_PLUGIN_DIR="$HOME/.nuget/plugins"

# Ensure plugin directory exists
if [[ ! -e "${NUGET_PLUGIN_DIR}" ]]; then
  echo "INFO: Creating the nuget plugin directory (i.e. ${NUGET_PLUGIN_DIR}). "
  if ! mkdir -p "${NUGET_PLUGIN_DIR}"; then
      echo "ERROR: Unable to create nuget plugins directory (i.e. ${NUGET_PLUGIN_DIR})."
      exit 1
  fi
fi

echo "Downloading from $RELEASEURL"

# Extract netcore from the .tar.gz into the plugin directory

#Fetch the file
curl -H "Accept: application/octet-stream" \
     -s \
     -S \
     -L \
     "$RELEASEURL" | tar xz -C "$HOME/.nuget/" "plugins/netcore"

echo "INFO: credential provider netcore plugin extracted to $HOME/.nuget/"
