from __future__ import absolute_import

import os
import requests
import subprocess
import sys
import warnings
import json

from subprocess import Popen

if not hasattr(Popen, "__enter__"):
    # Handle Python 2.x not making Popen a context manager
    class Popen(Popen):
        def __enter__(self):
            return self

        def __exit__(self, ex_type, ex_value, ex_tb):
            pass

try:
    from urllib.parse import urlsplit
except ImportError:
    from urlparse import urlsplit

# A wrapper similar as https://github.com/microsoft/artifacts-keyring
class CredentialProvider(object):
    _NON_INTERACTIVE_VAR_NAME = "ARTIFACTS_CONDA_NONINTERACTIVE_MODE"

    def __init__(self):
        if sys.platform.startswith("win"):
            tool_path = os.path.join(
                os.path.abspath(os.environ['UserProfile']),
                ".nuget",
                "plugins",
                "netfx",
                "CredentialProvider.Microsoft",
                "CredentialProvider.Microsoft.exe",
            )
            self.exe = [tool_path]
        else:
            try:
                sys_version = tuple(int(i) for i in
                    subprocess.check_output(["dotnet", "--version"]).decode().strip().partition("-")[0].split("."))
                get_runtime_path = lambda: "dotnet"
            except Exception as e:
                message = (
                    "Unable to find dependency dotnet, please manually install"
                    " the .NET SDK and ensure 'dotnet' is in your PATH. Error: "
                )
                raise Exception(message + str(e))

            tool_path = os.path.join(
                os.path.abspath(os.environ['HOME']),
                ".nuget",
                "plugins",
                "netcore",
                "CredentialProvider.Microsoft",
                "CredentialProvider.Microsoft.dll",
            )
            self.exe = [get_runtime_path(), "exec", tool_path]

        if not os.path.exists(tool_path):
            raise RuntimeError("Unable to find credential provider in the expected path: " + tool_path)


    def get_credentials(self, url):
        # Public feed short circuit: return nothing if the endpoint is public (can authenticate without credentials).
        if self._can_authenticate(url, None):
            return None, None

        # Getting credentials with IsRetry=false; the credentials may come from the cache
        username, password = self._get_credentials_from_credential_provider(url, is_retry=False)
        
        # Do not attempt to validate if the credentials could not be obtained
        if username is None or password is None:
            return username, password

        # Make sure the credentials are still valid (i.e. not expired)
        if self._can_authenticate(url, (username, password)):
            return username, password

        # The cached credentials are expired; get fresh ones with IsRetry=true
        return self._get_credentials_from_credential_provider(url, is_retry=True)

    def _can_authenticate(self, url, auth):
        response = requests.get(url, auth=auth)

        return response.status_code < 500 and \
            response.status_code != 401 and \
            response.status_code != 403


    def _get_credentials_from_credential_provider(self, url, is_retry):
        non_interactive = self._NON_INTERACTIVE_VAR_NAME in os.environ and \
            os.environ[self._NON_INTERACTIVE_VAR_NAME] and \
            str(os.environ[self._NON_INTERACTIVE_VAR_NAME]).lower() == "true"

        proc = Popen(
            self.exe + [
                "-Uri", url,
                "-IsRetry", str(is_retry),
                "-NonInteractive", str(non_interactive),
                "-CanShowDialog", str(non_interactive),
                "-OutputFormat", "Json"
            ],
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE
        )

        # Read all standard error first, which may either display
        # errors from the credential provider or instructions
        # from it for Device Flow authentication.
        for stderr_line in iter(proc.stderr.readline, b''):
            line = stderr_line.decode("utf-8", "ignore")
            sys.stderr.write(line)
            sys.stderr.flush()

        proc.wait()

        if proc.returncode != 0:
            stderr = proc.stderr.read().decode("utf-8", "ignore")
            raise RuntimeError("Failed to get credentials: process with PID {pid} exited with code {code}; additional error message: {error}"
                .format(pid=proc.pid, code=proc.returncode, error=stderr))

        try:
            # stdout is expected to be UTF-8 encoded JSON, so decoding errors are not ignored here.
            payload = proc.stdout.read().decode("utf-8")
        except ValueError:
            raise RuntimeError("Failed to get credentials: the Credential Provider's output could not be decoded using UTF-8.")

        try:
            parsed = json.loads(payload)
            return parsed["Username"], parsed["Password"]
        except ValueError:
            raise RuntimeError("Failed to get credentials: the Credential Provider's output could not be parsed as JSON.")

class ArtifactsKeyringBackend():
    SUPPORTED_NETLOC = (
        "pkgs.dev.azure.com",
        "pkgs.visualstudio.com",
        "pkgs.codedev.ms",
        "pkgs.vsts.me"
    )
    _PROVIDER = CredentialProvider

    priority = 9.9

    def get_credential(self, service, username):
        try:
            parsed = urlsplit(service)
        except Exception as exc:
            warnings.warn(str(exc))
            return None

        netloc = parsed.netloc.rpartition("@")[-1]

        if netloc is None or not netloc.endswith(self.SUPPORTED_NETLOC):
            return None

        provider = self._PROVIDER()

        username, password = provider.get_credentials(service)

        if username and password:
            return password

result = sys.stdin.buffer.read()
jsonResult = json.loads(result)
resultUrl = jsonResult['channel_alias']['scheme'] + "://" + jsonResult['channel_alias']['location']
cred = ArtifactsKeyringBackend()
token = cred.get_credential(resultUrl,None)
print(token) # pipe the result back to the shell script