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

# URL pattern to get latest documented at https://help.github.com/en/articles/linking-to-releases as of 2019-03-29
INSTALL_SCRIPT="installcredprovider.sh"
RELEASE_BASE_URL="https://github.com/microsoft/artifacts-credprovider/releases"
RELEASE_LATEST_DOWNLOAD_URL="https://github.com/microsoft/artifacts-credprovider/releases/latest/download"

# Process version - if not set, use latest
VERSION=$(echo "${AZURE_ARTIFACTS_CREDENTIAL_PROVIDER_VERSION}" | sed 's/^v//')
if [ -z "${AZURE_ARTIFACTS_CREDENTIAL_PROVIDER_VERSION}" ] || [ "${AZURE_ARTIFACTS_CREDENTIAL_PROVIDER_VERSION}" = "latest" ]; then
  DOWNLOAD_URL="${RELEASE_LATEST_DOWNLOAD_URL}"
  INSTALL_URL="${DOWNLOAD_URL}/${INSTALL_SCRIPT}"
  echo "No version specified, using latest release."
else
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

  # Attach 'v' prefix since it was removed during normalization
  TAG_VERSION="v${VERSION}"
  
  echo "Fetching tagged release: ${TAG_VERSION}"
  DOWNLOAD_URL="${RELEASE_BASE_URL}/download/${TAG_VERSION}"
  INSTALL_URL="${DOWNLOAD_URL}/${INSTALL_SCRIPT}"
fi

echo "Fetching versioned release install script at: ${INSTALL_URL}"

# Download and validate the script content
echo "Fetching install script from ${INSTALL_URL}..."

# Get the script content without writing to disk
SCRIPT_CONTENT=$(curl -s -S -L "${INSTALL_URL}")
if [ -z "${SCRIPT_CONTENT}" ]; then
  echo "ERROR: Failed to download install script content"
  exit 1
fi

# Check if we need to validate with checksum
SHOULD_VALIDATE=true
if [[ "${VERSION}" == 0.* ]] || [[ "${VERSION}" == 1.* ]]; then
  SHOULD_VALIDATE=false
  echo "Skipping checksum validation for version ${VERSION} as it is not available for 0.x and 1.x versions."
else
  CHECKSUM_URL="${DOWNLOAD_URL}/artifacts-credprovider-sha256.txt"
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

RESULT=0
eval "${SCRIPT_CONTENT}"
RESULT=$?

echo "Installation completed with exit code: ${RESULT}"
exit ${RESULT}
