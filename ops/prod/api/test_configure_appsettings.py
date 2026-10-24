import base64
import os
import subprocess
import tempfile
import unittest
from unittest.mock import patch

from configure_appsettings import EXISTING_SETTINGS, build_settings, main, validate_certificate


class DeploymentSettingsTests(unittest.TestCase):
    def setUp(self):
        self.env = {name: "existing-secret" for name in EXISTING_SETTINGS}
        self.env.update({
            "MCP_ENABLED": "true", "MCP_ISSUER": "https://apifc.azurewebsites.net",
            "MCP_FRONTEND_URL": "https://fanta.zone/", "MCP_KEY_RING_REVIEWED": "true",
            "MCP_DATA_PROTECTION_KEY_PATH": "/existing/persistent/keyring",
            "MCP_CERTIFICATE_PFX_BASE64": "test-pfx", "MCP_CERTIFICATE_PASSWORD": "test-password",
        })

    @patch("configure_appsettings.validate_certificate", return_value="test-pfx")
    def test_dynamic_registration_does_not_require_client_variables(self, validate):
        settings = build_settings(self.env, {})
        self.assertEqual("true", settings["Mcp__Enabled"])
        self.assertEqual("test-pfx", settings["Mcp__CertificateBase64"])
        self.assertEqual("", settings["Mcp__CertificatePath"])
        self.assertEqual("/existing/persistent/keyring", settings["Mcp__DataProtectionKeyPath"])
        self.assertFalse(any("Clients" in key for key in settings))
        validate.assert_called_once()

    def test_disabled_feature_needs_no_new_secrets(self):
        env = {name: "existing-secret" for name in EXISTING_SETTINGS}
        env["MCP_ENABLED"] = "false"
        self.assertEqual("false", build_settings(env, {})["Mcp__Enabled"])

    def test_key_ring_review_is_mandatory(self):
        self.env.pop("MCP_KEY_RING_REVIEWED")
        with self.assertRaisesRegex(ValueError, "MCP_KEY_RING_REVIEWED"):
            build_settings(self.env, {})

    def test_does_not_silently_create_new_key_ring(self):
        self.env.pop("MCP_DATA_PROTECTION_KEY_PATH")
        with self.assertRaisesRegex(ValueError, "exactly one"):
            build_settings(self.env, {})

    @patch("configure_appsettings.validate_certificate", return_value="test-pfx")
    def test_existing_reviewed_azure_store_is_preserved(self, _):
        self.env.pop("MCP_DATA_PROTECTION_KEY_PATH")
        settings = build_settings(self.env, {"Mcp__DataProtectionBlobName": "existing-migrated-keyring.xml"})
        self.assertEqual("existing-migrated-keyring.xml", settings["Mcp__DataProtectionBlobName"])

    def test_cannot_mix_key_stores(self):
        with self.assertRaisesRegex(ValueError, "exactly one"):
            build_settings(self.env, {"Mcp__DataProtectionBlobName": "another.xml"})

    def test_issuer_cannot_be_insecure_or_contain_credentials(self):
        for issuer in ("http://api.example", "https://user:secret@api.example", "https://api.example/path"):
            with self.subTest(issuer=issuer):
                self.env["MCP_ISSUER"] = issuer
                with self.assertRaises(ValueError):
                    build_settings(self.env, {})

    @patch("configure_appsettings.azure", return_value="[]")
    def test_preflight_failure_never_writes_azure(self, azure):
        self.env.update({"API_RESOURCE_GROUP": "test", "API_APP_NAME": "test", "MCP_KEY_RING_REVIEWED": "false"})
        with patch.dict(os.environ, self.env, clear=True):
            with self.assertRaises(ValueError):
                main()
        self.assertEqual(1, azure.call_count)
        self.assertIn("list", azure.call_args.args[0])

    def test_pfx_and_password_are_verified_without_exposing_them(self):
        with tempfile.TemporaryDirectory() as directory:
            key, cert, pfx = (os.path.join(directory, name) for name in ("key.pem", "cert.pem", "cert.pfx"))
            password = "test-only-random-password"
            subprocess.run(["openssl", "req", "-x509", "-newkey", "rsa:2048", "-nodes", "-keyout", key,
                            "-out", cert, "-days", "1", "-subj", "/CN=test"], check=True, capture_output=True)
            subprocess.run(["openssl", "pkcs12", "-export", "-out", pfx, "-inkey", key, "-in", cert,
                            "-passout", "env:TEST_PFX_PASSWORD"], check=True, capture_output=True,
                           env={**os.environ, "TEST_PFX_PASSWORD": password})
            with open(pfx, "rb") as file:
                encoded = base64.b64encode(file.read()).decode()
            env = {"MCP_CERTIFICATE_PFX_BASE64": encoded, "MCP_CERTIFICATE_PASSWORD": password}
            self.assertEqual(encoded, validate_certificate(env))
            certificate_only = os.path.join(directory, "no-key.pfx")
            subprocess.run(["openssl", "pkcs12", "-export", "-nokeys", "-out", certificate_only, "-in", cert,
                            "-passout", "env:TEST_PFX_PASSWORD"], check=True, capture_output=True,
                           env={**os.environ, "TEST_PFX_PASSWORD": password})
            with open(certificate_only, "rb") as file:
                with self.assertRaisesRegex(ValueError, "private key"):
                    validate_certificate({**env, "MCP_CERTIFICATE_PFX_BASE64": base64.b64encode(file.read()).decode()})
            env["MCP_CERTIFICATE_PASSWORD"] = "wrong-password"
            with self.assertRaises(ValueError) as error:
                validate_certificate(env)
            self.assertNotIn(password, str(error.exception))
            self.assertNotIn(encoded, str(error.exception))
            env["MCP_CERTIFICATE_PFX_BASE64"] = "not base64!"
            with self.assertRaises(ValueError):
                validate_certificate(env)


if __name__ == "__main__":
    unittest.main()
