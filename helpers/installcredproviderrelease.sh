#!/usr/bin/env bash
# DESCRIPTION: This script downloads a specific version of the installcredprovider.sh script
# from a GitHub release and executes it, handling validation of the script via checksum.
# Readme: https://github.com/Microsoft/artifacts-credprovider/blob/master/README.md

# Use environment variables:
# To specify a version, set the AZURE_ARTIFACTS_CREDENTIAL_PROVIDER_VERSION environment variable
# For runtime identifier, set ARTIFACTS_CREDENTIAL_PROVIDER_RID environment variable
# To use .NET 6, set USE_NET6_ARTIFACTS_CREDENTIAL_PROVIDER=true
# To use .NET 8, set USE_NET8_ARTIFACTS_CREDENTIAL_PROVIDER=true

set -e

INSTALL_SCRIPT="installcredprovider.sh"
RELEASE_BASE_URL="https://api.github.com/repos/microsoft/artifacts-credprovider/releases"

# Process version - if not set, use latest
if [ -z "${AZURE_ARTIFACTS_CREDENTIAL_PROVIDER_VERSION}" ] || [ "${AZURE_ARTIFACTS_CREDENTIAL_PROVIDER_VERSION}" = "latest" ]; then
  RELEASE_DOWNLOAD_URL="$RELEASE_BASE_URL/latest/download"
  echo "No version specified, using latest release."
else
  VERSION="${AZURE_ARTIFACTS_CREDENTIAL_PROVIDER_VERSION}"
  # Validate version format
  if ! [[ "${VERSION}" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[a-zA-Z0-9]+(\.[a-zA-Z0-9]+)*)?$ ]]; then
    echo "ERROR: Invalid version format. Please use format #.#.# (e.g., 1.4.1)"
    exit 1
  fi
  
  # For versions 0.x and 1.x, use the last published 1.x version for backward compatibility
  if [[ "${VERSION}" == 0.* ]] || [[ "${VERSION}" == 1.* ]]; then
    echo "INFO: Using version 1.4.1 for installation script as the minimum supported version"
    VERSION="1.4.1"
  fi

  # Attach 'v' prefix if not present
  TAG_VERSION="$VERSION"
  if [[ "${VERSION}" != v* ]]; then
    TAG_VERSION="v${AZURE_ARTIFACTS_CREDENTIAL_PROVIDER_VERSION}"
  fi
  
  RELEASE_DOWNLOAD_URL="$RELEASE_BASE_URL/download/v${VERSION}"
  echo "Fetching tagged release: ${VERSION}"
fi

INSTALL_URL="${RELEASE_DOWNLOAD_URL}/installcredprovider.sh"
echo "Fetching versioned release install script at: ${INSTALL_URL}"

# Check if we need to validate with checksum
SHOULD_VALIDATE=false
CHECKSUM_URL=""

if [[ "${VERSION}" != 0.* ]] && [[ "${VERSION}" != 1.* ]]; then
  # Extract the assets URL to find the checksum file using the normalized data
  ASSETS_URL=$(echo "${NORMALIZED_RELEASE_DATA}" | grep -o "\"assets_url\":\"[^\"]*\"" | head -1 | awk -F'"' '{print $4}')
  
  if [ ! -z "${ASSETS_URL}" ]; then
    # Get the assets data
    ASSETS_DATA=$(curl -s -H "Accept: application/json" "${ASSETS_URL}")
    
    # Normalize assets data too
    NORMALIZED_ASSETS_DATA=$(echo "${ASSETS_DATA}" | tr -d '\r\n' | sed 's/[[:space:]]*\([{}[\],]\)[[:space:]]*/\1/g')
    
    # Look for sha256 checksum file
    CHECKSUM_URL=$(echo "${NORMALIZED_ASSETS_DATA}" | grep -o "\"browser_download_url\":\"[^\"]*artifacts-credprovider-sha256.txt\"" | head -1 | awk -F'"' '{print $4}')
    
    if [ ! -z "${CHECKSUM_URL}" ]; then
      SHOULD_VALIDATE=true
      echo "Found checksum file at: ${CHECKSUM_URL}"
    else
      echo "WARNING: Could not find SHA256 checksum file, proceeding without validation"
    fi
  fi
fi

# Download and validate the script content
echo "Fetching install script from ${INSTALL_URL}..."

# Get the script content without writing to disk
SCRIPT_CONTENT=$(curl -s -S -L "${INSTALL_URL}")
if [ -z "${SCRIPT_CONTENT}" ]; then
  echo "ERROR: Failed to download install script content"
  exit 1
fi

# Validate the script if checksum is available
if [ "${SHOULD_VALIDATE}" = true ] && [ ! -z "${CHECKSUM_URL}" ]; then
  echo "Validating script content with SHA256 checksum"
  
  # Download the checksum file content directly into memory
  echo "Fetching checksum file from ${CHECKSUM_URL}..."
  CHECKSUM_CONTENT=$(curl -s -S -L "${CHECKSUM_URL}")
  if [ -z "${CHECKSUM_CONTENT}" ]; then
    echo "ERROR: Failed to download checksum file"
    exit 1
  fi
  
  # Extract expected hash for the install script
  # Normalize the content to handle different formats
  NORMALIZED_CHECKSUM_CONTENT=$(echo "${CHECKSUM_CONTENT}" | tr -d '\r')
  EXPECTED_HASH=$(echo "${NORMALIZED_CHECKSUM_CONTENT}" | grep "${INSTALL_SCRIPT}" | awk '{print $1}')
  if [ -z "${EXPECTED_HASH}" ]; then
    echo "WARNING: Could not find hash for ${INSTALL_SCRIPT} in checksum file, proceeding without validation"
  else
    # Calculate actual hash from the script content
    if command -v shasum >/dev/null 2>&1; then
      ACTUAL_HASH=$(echo "${SCRIPT_CONTENT}" | shasum -a 256 | awk '{print $1}')
    elif command -v sha256sum >/dev/null 2>&1; then
      ACTUAL_HASH=$(echo "${SCRIPT_CONTENT}" | sha256sum | awk '{print $1}')
    else
      echo "WARNING: No SHA256 utility available, skipping validation"
      ACTUAL_HASH=""
    fi
    
    # Compare hashes
    if [ ! -z "${ACTUAL_HASH}" ] && [ "${ACTUAL_HASH}" != "${EXPECTED_HASH}" ]; then
      echo "ERROR: SHA256 checksum validation failed!"
      echo "Expected: ${EXPECTED_HASH}"
      echo "Actual: ${ACTUAL_HASH}"
      exit 1
    fi
  fi
fi

# Execute the script content directly
echo "Executing install script..."

# Build parameter string for passing to the install script
PARAM_STRING=""

# Pass runtime identifier if set
if [ ! -z "${ARTIFACTS_CREDENTIAL_PROVIDER_RID}" ]; then
  PARAM_STRING="${PARAM_STRING} -RuntimeIdentifier ${ARTIFACTS_CREDENTIAL_PROVIDER_RID}"
fi

# Pass .NET version settings if provided
if [ "${USE_NET6_ARTIFACTS_CREDENTIAL_PROVIDER}" = "true" ]; then
  PARAM_STRING="${PARAM_STRING} -InstallNet6"
fi

if [ "${USE_NET8_ARTIFACTS_CREDENTIAL_PROVIDER}" = "true" ]; then
  PARAM_STRING="${PARAM_STRING} -InstallNet8"
fi

# Execute the script with parameters
if [ ! -z "${PARAM_STRING}" ]; then
  echo "Passing parameters: ${PARAM_STRING}"
fi

RESULT=0
eval "${SCRIPT_CONTENT} ${PARAM_STRING}"
RESULT=$?

echo "Installation completed with exit code: ${RESULT}"
exit ${RESULT}
