#!/usr/bin/env python3
"""Validate before changing Azure; never print settings or put secrets in CLI args."""
import base64
import json
import os
import subprocess
import sys
import tempfile
from urllib.parse import urlsplit


EXISTING_SETTINGS = {
    "API_STORAGE_CONNECTION_STRING": "ConnectionStrings__Storage",
    "API_APPINSIGHTS_CONNECTION_STRING": "ConnectionStrings__AppInsights",
    "MICROSOFT_CLIENT_SECRET": "SocialLogin__Microsoft__ClientSecret",
    "OPENAI_API_KEY": "OpenAi__ApiKey",
    "ENCRYPTION_KEY": "Encryption__Key",
    "ENCRYPTION_IV": "Encryption__IV",
}


def required(env, name):
    value = env.get(name, "")
    if not value:
        raise ValueError("Missing GitHub production environment value: " + name)
    return value


def validate_certificate(env):
    try:
        encoded = "".join(required(env, "MCP_CERTIFICATE_PFX_BASE64").split())
        if len(encoded) > 24000:
            raise ValueError("PFX is too large for an App Service environment setting.")
        certificate = base64.b64decode(encoded, validate=True)
    except (ValueError, base64.binascii.Error):
        raise ValueError("MCP_CERTIFICATE_PFX_BASE64 must contain a valid, reasonably sized Base64 PFX.") from None
    password = required(env, "MCP_CERTIFICATE_PASSWORD")
    result = subprocess.run(
        ["openssl", "pkcs12", "-nodes", "-passin", "env:MCP_CERTIFICATE_PASSWORD"],
        input=certificate, capture_output=True,
        env={**os.environ, "MCP_CERTIFICATE_PASSWORD": password}, check=False,
    )
    # Decrypted material is captured only in memory, never printed or written.
    if result.returncode or b"PRIVATE KEY-----" not in result.stdout or b"CERTIFICATE-----" not in result.stdout:
        raise ValueError("Cannot open the MCP PFX with a private key: verify the certificate and its export password.")
    validity = subprocess.run(["openssl", "x509", "-checkend", "0", "-noout"],
                              input=result.stdout, capture_output=True, check=False)
    if validity.returncode:
        raise ValueError("MCP certificate is expired or invalid.")
    return encoded


def build_settings(env, current):
    settings = {setting: required(env, name) for name, setting in EXISTING_SETTINGS.items()}
    enabled = required(env, "MCP_ENABLED").lower()
    if enabled not in ("true", "false"):
        raise ValueError("MCP_ENABLED must be true or false.")
    settings["Mcp__Enabled"] = enabled
    if enabled == "false":
        return settings

    for name in ("MCP_ISSUER", "MCP_FRONTEND_URL"):
        uri = urlsplit(required(env, name))
        if uri.scheme != "https" or not uri.hostname or uri.username is not None or uri.password is not None or uri.query or uri.fragment:
            raise ValueError(name + " must be an HTTPS URL without credentials, query or fragment.")
        if name == "MCP_ISSUER" and uri.path not in ("", "/"):
            raise ValueError("MCP_ISSUER must be a root HTTPS origin.")

    if env.get("MCP_KEY_RING_REVIEWED", "").lower() != "true":
        raise ValueError("Before enabling MCP, preserve/migrate the existing Data Protection keys and application discriminator; then set MCP_KEY_RING_REVIEWED=true.")
    path = env.get("MCP_DATA_PROTECTION_KEY_PATH") or current.get("Mcp__DataProtectionKeyPath", "")
    blob = env.get("MCP_DATA_PROTECTION_BLOB_NAME") or current.get("Mcp__DataProtectionBlobName", "")
    if bool(path) == bool(blob):
        raise ValueError("Configure exactly one reviewed MCP_DATA_PROTECTION_KEY_PATH or MCP_DATA_PROTECTION_BLOB_NAME (or the matching existing Azure setting). No new key ring is created automatically.")
    settings.update({
        "Mcp__Issuer": env["MCP_ISSUER"],
        "Mcp__FrontendUrl": env["MCP_FRONTEND_URL"],
        "Mcp__CertificateBase64": validate_certificate(env),
        "Mcp__CertificatePassword": env["MCP_CERTIFICATE_PASSWORD"],
        "Mcp__CertificatePath": "",
        "Mcp__DataProtectionKeyPath": path,
        "Mcp__DataProtectionBlobName": blob,
    })
    # No client ID/secret/callback settings: clients register themselves via RFC 7591.
    return settings


def azure(arguments):
    result = subprocess.run(["az", *arguments, "--only-show-errors"], capture_output=True, text=True, check=False)
    if result.returncode:
        # Azure errors can include arguments/settings: do not copy them into Actions logs.
        raise ValueError("Azure configuration command failed. Check the deployment identity permissions and App Service configuration securely.")
    return result.stdout


def main():
    target = ["--resource-group", required(os.environ, "API_RESOURCE_GROUP"),
              "--name", required(os.environ, "API_APP_NAME")]
    current = json.loads(azure(["webapp", "config", "appsettings", "list", *target,
        "--query", "[?name=='Mcp__DataProtectionKeyPath'||name=='Mcp__DataProtectionBlobName'].{name:name,value:value}", "--output", "json"]))
    settings = build_settings(os.environ, {item["name"]: item["value"] for item in current})
    # Private temporary file, removed even on failure; not part of the publish artifact.
    with tempfile.TemporaryDirectory(prefix="fantasoccer-settings-") as directory:
        path = os.path.join(directory, "settings.json")
        with open(path, "w", opener=lambda p, flags: os.open(p, flags, 0o600)) as file:
            json.dump(settings, file)
        azure(["webapp", "config", "appsettings", "set", *target, "--settings", "@" + path, "--output", "none"])
    print("App Service settings configured; no secret values logged.")


if __name__ == "__main__":
    try:
        main()
    except (ValueError, OSError) as error:
        print("Deployment preflight failed: " + str(error), file=sys.stderr)
        sys.exit(1)
