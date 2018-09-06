#!/usr/bin/env bash
# DESCRIPTION: A simple shell script designed to fetch the latest version
# of the artifacts credential provider plugin for dotnet and
# install it into $HOME/.nuget/plugins.
# SEE: https://github.com/Microsoft/artifacts-credprovider/blob/master/README.md



# TOKEN is only necessary if the REPO is private or you need
# a prerelease version.
#TOKEN=
#-H "Authorization: token $TOKEN" \

GITHUB_API="https://api.github.com"
REPO="Microsoft/artifacts-credprovider"
FILE="Microsoft.NuGet.CredentialProvider.tar.gz"
VERSION="latest"
RELEASEURL="$GITHUB_API/repos/$REPO/releases/$VERSION"
NUGET_PLUGIN_DIR="$HOME/.nuget/plugins"

function get_release() {
  curl -H "Accept: application/vnd.github.v3.raw" \
       -s \
       $@
}

function get_download_uri() {
  echo $@ | python -c "\
import json,sys; \
obj=json.load(sys.stdin)['assets']; \
print filter(lambda x: x['name'] == '$FILE', obj)[0]['url'];" 2>&1
}

# Get the release JSON
RELEASE=$( get_release $RELEASEURL )
if [[ -z "${RELEASE}" ]]; then
  echo "ERROR: Unable to fetch release information from $RELEASEURL"
  exit 1
else
  # Extract the url from the release JSON
  URI=$( get_download_uri $RELEASE )
  if [ $? -ne 0 ]; then
    echo "ERROR: Unable to find url in JSON response. Response: $RELEASE"
    exit 1
  fi
fi

# Ensure plugin directory exists
if [[ ! -e ${NUGET_PLUGIN_DIR} ]]; then
  echo "INFO: Creating the nuget plugin directory (i.e. ${NUGET_PLUGIN_DIR}). "
  if ! mkdir -p ${NUGET_PLUGIN_DIR}; then
      echo "ERROR: Unable to create nuget plugins directory (i.e. ${NUGET_PLUGIN_DIR})."
      exit 1
  fi
fi

# Extract netcore from the .tar.gz into the plugin directory

#Fetch the file
curl -H "Accept: application/octet-stream" \
     -s \
     -L \
     $URI | tar xz -C $HOME/.nuget/ plugins/netcore

echo "INFO: credential provider netcore plugin extracted to $HOME/.nuget/"
