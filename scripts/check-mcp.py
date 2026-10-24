#!/usr/bin/env python3
"""Read-only deployment smoke check. Never logs in, sends tokens or invokes tools."""
import argparse
import json
import sys
import urllib.error
import urllib.parse
import urllib.request


def check(issuer):
    parsed = urllib.parse.urlsplit(issuer)
    if parsed.scheme != "https" or not parsed.netloc or parsed.path not in ("", "/") or parsed.query or parsed.fragment or parsed.username:
        raise ValueError("Issuer must be an HTTPS origin without path, credentials, query or fragment.")
    issuer = issuer.rstrip("/")
    resource = issuer + "/mcp"

    def request(path, data=None):
        req = urllib.request.Request(issuer + path, data=data,
                                     headers={"Accept": "application/json, text/event-stream", "Content-Type": "application/json"})
        try:
            return urllib.request.urlopen(req, timeout=20)
        except urllib.error.HTTPError as error:
            return error

    with request("/.well-known/oauth-protected-resource/mcp") as response:
        if response.status != 200:
            raise ValueError("Protected resource metadata is unavailable (MCP disabled, wrong deployment or proxy).")
        metadata = json.load(response)
    if metadata.get("resource") != resource or issuer + "/" not in metadata.get("authorization_servers", []):
        raise ValueError("Resource metadata does not match the public issuer.")
    with request("/.well-known/openid-configuration") as response:
        discovery = json.load(response)
    if discovery.get("issuer") != issuer + "/" or "S256" not in discovery.get("code_challenge_methods_supported", []):
        raise ValueError("OAuth discovery issuer/PKCE configuration is invalid.")
    for field, path in (("authorization_endpoint", "/connect/authorize"), ("token_endpoint", "/connect/token"), ("revocation_endpoint", "/connect/revoke")):
        if discovery.get(field) != issuer + path:
            raise ValueError("Unexpected OAuth endpoint: " + field)
    with request("/mcp", json.dumps({"jsonrpc": "2.0", "id": 1, "method": "tools/list"}).encode()) as response:
        if response.status != 401 or "resource_metadata=" not in response.headers.get("WWW-Authenticate", ""):
            raise ValueError("MCP must reject an unauthenticated call with the resource metadata challenge.")
    print("OK: HTTPS discovery, public OAuth endpoints and unauthenticated MCP challenge.")
    print("Next: verify real Microsoft/ChatGPT login, permissions, refresh and revocation manually.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("issuer", help="Public HTTPS API origin")
    args = parser.parse_args()
    try:
        check(args.issuer)
    except (ValueError, KeyError, OSError) as error:
        print("MCP check failed: " + str(error), file=sys.stderr)
        sys.exit(1)
