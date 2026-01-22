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
RELEASE_API_URL="https://api.github.com/repos/microsoft/artifacts-credprovider/releases"
RELEASE_BASE_URL="https://github.com/microsoft/artifacts-credprovider/releases"

# Process version - if not set, use latest
VERSION=$(echo "${AZURE_ARTIFACTS_CREDENTIAL_PROVIDER_VERSION}" | sed 's/^v//')
if [ -z "${AZURE_ARTIFACTS_CREDENTIAL_PROVIDER_VERSION}" ] || [ "${AZURE_ARTIFACTS_CREDENTIAL_PROVIDER_VERSION}" = "latest" ]; then
  API_URL="${RELEASE_API_URL}/latest"
  INSTALL_URL="${RELEASE_BASE_URL}/latest/download/${INSTALL_SCRIPT}"
  echo "No version specified, using latest release."
else
  # Validate version format (POSIX-compliant)
  if ! echo "${VERSION}" | grep -qE '^[0-9]+\.[0-9]+\.[0-9]+(-[a-zA-Z0-9]+(\.[a-zA-Z0-9]+)*)?$'; then
    echo "ERROR: Invalid version format. Please use format #.#.# (e.g., 1.4.1)"
    exit 1
  fi
  
  # For versions 0.x and 1.x, use the last published 1.x version for backward compatibility
  case "${VERSION}" in
  0.* | 1.*)
    echo "Using installcredprovider script from version 1.4.1 to support 0.x and 1.x versions."
    VERSION="1.4.1"
    ;;
  esac

  # Attach 'v' prefix since it was removed during normalization
  TAG_VERSION="v${VERSION}"
  
  echo "Fetching tagged release: ${TAG_VERSION}"
  API_URL="${RELEASE_API_URL}/tags/${TAG_VERSION}"
  INSTALL_URL="${RELEASE_BASE_URL}/download/${TAG_VERSION}/${INSTALL_SCRIPT}"
fi

# Download and validate the script content
echo "Fetching versioned release install script from ${INSTALL_URL}"

# Get the script content without writing to disk
SCRIPT_CONTENT=$(curl -s -S -L "${INSTALL_URL}")
if [ -z "${SCRIPT_CONTENT}" ]; then
  echo "ERROR: Failed to download install script content"
  exit 1
fi

# Download and validate the checksums from GitHub API
echo "Fetching release metadata from ${API_URL}"

API_RESPONSE=$(curl -s -S -L "${API_URL}")
if [ -z "${API_RESPONSE}" ]; then
  echo "ERROR: Failed to download release metadata"
  exit 1
else
  EXPECTED_HASH=""
  DIGEST_VALUE=""

  # Extract digest using POSIX-compliant approach
  # First find the line with our asset name, then find the digest line that follows
  DIGEST_VALUE=$(echo "${API_RESPONSE}" | awk -v asset="${INSTALL_SCRIPT}" '
    /"name":.*"'"${INSTALL_SCRIPT}"'"/ { found=1; next }
    found && /"digest":/ {
      gsub(/.*"digest"[[:space:]]*:[[:space:]]*"?/, "")
      gsub(/"?[[:space:]]*,?$/, "")
      print
      exit
    }
    found && /"name":/ { exit }
  ')

  if [ -n "${DIGEST_VALUE}" ] && [ "${DIGEST_VALUE}" != "null" ]; then
    case "${DIGEST_VALUE}" in
    sha256:*)
      EXPECTED_HASH="${DIGEST_VALUE#sha256:}"
      ;;
    *)
      echo "WARNING: Invalid digest '${DIGEST_VALUE}', expected sha256: format"
      ;;
    esac
  fi

  if [ -z "${EXPECTED_HASH}" ]; then
    echo "WARNING: No digest available for ${INSTALL_SCRIPT} in release metadata, skipping validation"
  else
    echo "Validating script content with SHA256 checksum"

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
