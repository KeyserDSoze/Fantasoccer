#!/usr/bin/env python3
"""Build a filtered, verified Blob-only historical archive; never modify the sources.

The complete private ZIP and PostgreSQL dump are deliberately not inputs. Only
manifest-listed blobs can enter the public archive. Table, push and OAuth data
are excluded before copying, and populated credential fields fail closed.
"""
import argparse
import collections
import copy
import hashlib
import json
import pathlib
import re
import shutil
import zipfile


EXCLUDED = frozenset({
    "notification", "pushnotification", "pushnotificationlocks", "pushnotificationsent",
    "mcp-oauth", "mcp-keys",
})
ASSETS = frozenset({"images", "logos", "cardimages"})
SECRET_FIELD = re.compile(
    r"^(?:access_?token|refresh_?token|client_?secret|private_?key|password|"
    r"account_?key|sharedaccesssignature|deviceproof|deviceproofhash|p256dh|auth)$", re.I)
SECRET_TEXT = re.compile(
    rb"-----BEGIN (?:RSA |EC |ENCRYPTED )?PRIVATE KEY-----|<RSAKeyValue>|"
    rb"(?:DefaultEndpointsProtocol=|AccountKey=|SharedAccessSignature=)|"
    rb"https://(?:fcm\.googleapis\.com|[^/\s\"]*\.notify\.windows\.com)/", re.I)


def blob_file(root, container, name):
    digest = hashlib.sha256((container + "\0" + name).encode()).hexdigest()
    return root / "blobs" / (digest + ".bin")


def credential_fields(value, findings=None):
    findings = collections.Counter() if findings is None else findings
    if isinstance(value, dict):
        # RSA private components can be stored as ordinary JSON, not PEM.
        if "Modulus" in value and any(value.get(k) for k in ("D", "P", "Q", "DP", "DQ", "InverseQ")):
            findings["private-rsa"] += 1
        for key, child in value.items():
            if SECRET_FIELD.fullmatch(key) and child not in (None, "", [], {}):
                findings["credential-field"] += 1
            credential_fields(child, findings)
    elif isinstance(value, list):
        for child in value:
            credential_fields(child, findings)
    elif isinstance(value, str):
        if SECRET_TEXT.search(value.encode()):
            findings["credential-text"] += 1
        # Legacy wrappers frequently serialize their Value as another JSON string.
        if value.lstrip().startswith(("{", "[")):
            try:
                credential_fields(json.loads(value), findings)
            except ValueError:
                pass
    return findings


def filtered_snapshot(source, destination):
    manifest = json.loads((source / "manifest.json").read_text())
    if not manifest.get("complete") or manifest.get("failureCode"):
        raise ValueError("Source snapshot is incomplete")
    result = copy.deepcopy(manifest)
    result["scope"] = "blob-current-state"
    result["tables"] = []
    result["containers"] = [c for c in result["containers"] if c["name"] not in EXCLUDED]
    result["exclusions"] = list(dict.fromkeys(result.get("exclusions", []) + ["Tables"] +
        ["PublicArchiveContainer:" + name for name in sorted(EXCLUDED)]))
    if credential_fields(result):
        raise ValueError("Credential fields found in filtered manifest")
    destination.mkdir()
    (destination / "blobs").mkdir()
    count = 0
    for container in result["containers"]:
        for descriptor in container["blobs"]:
            source_file = blob_file(source, container["name"], descriptor["name"])
            raw = source_file.read_bytes()
            if len(raw) != descriptor["length"] or hashlib.sha256(raw).hexdigest() != descriptor["sha256"]:
                raise ValueError("Source blob checksum mismatch")
            if container["name"] not in ASSETS:
                findings = collections.Counter()
                if SECRET_TEXT.search(raw):
                    findings["credential-text"] += 1
                try:
                    credential_fields(json.loads(raw), findings)
                except ValueError:
                    pass
                if findings:
                    # Never print blob names or values: names can contain emails.
                    raise ValueError(f"Credential scan failed in container {container['name']}: {dict(findings)}")
            shutil.copyfile(source_file, blob_file(destination, container["name"], descriptor["name"]))
            count += 1
    (destination / "manifest.json").write_text(json.dumps(result, indent=2) + "\n")
    (destination / ".migration.lock").touch()
    return {"containers": len(result["containers"]), "blobs": count, "tables": 0,
            "sourceCompletedAtUtc": manifest["completedAtUtc"], "sourceQuiesced": manifest["sourceQuiesced"]}


def build(import_source, final_source, output):
    # Refuse overwrite/reuse of a previously prepared public directory.
    output.mkdir()
    snapshots = output / "snapshots"
    snapshots.mkdir()
    summaries = {}
    for label, source in (("migration-import-snapshot", import_source), ("legacy-final-snapshot", final_source)):
        summaries[label] = filtered_snapshot(source, snapshots / label)
    readme = (
        "# Public legacy history — 2026-11-01\n\n"
        "Two filtered Blob snapshots: the migration import and the final legacy state.\n"
        "All Table Storage, push notification containers, mcp-oauth and mcp-keys are excluded.\n"
        "No PostgreSQL dump, environment files or unlisted source files are included.\n"
        "This is a partial historical archive, not a complete authentication backup.\n"
        "Other blobs are byte-for-byte copies with original SHA-256 in each manifest.\n"
        "It still contains real fantasy-football data, including names and emails.\n"
    )
    archive = output / "2026-11-01.zip"
    with zipfile.ZipFile(archive, "x", compression=zipfile.ZIP_DEFLATED, compresslevel=6) as z:
        z.writestr("README.md", readme)
        for f in sorted(snapshots.rglob("*")):
            if f.is_file():
                z.write(f, f.relative_to(snapshots))
    with zipfile.ZipFile(archive) as z:
        if z.testzip() is not None:
            raise ValueError("ZIP checksum verification failed")
        if any("tables" in pathlib.PurePosixPath(name).parts or name.endswith(".dump") for name in z.namelist()):
            raise ValueError("Unexpected private data in ZIP")
    with archive.open("rb") as stream:
        digest = hashlib.file_digest(stream, "sha256").hexdigest()
    summary = {"date": "2026-11-01", "archive": archive.name, "edition": "public-filtered-blob-only",
               "excludedContainers": sorted(EXCLUDED), "tableStorageIncluded": False,
               "postgresDumpIncluded": False, "snapshots": summaries,
               "bytes": archive.stat().st_size, "sha256": digest, "zipVerified": True,
               "credentialScanPassed": True, "originalPrivateArchiveModified": False}
    (output / "2026-11-01-public.json").write_text(json.dumps(summary, indent=2) + "\n")
    (output / "2026-11-01.zip.sha256").write_text(digest + "  " + archive.name + "\n")
    print(json.dumps(summary, indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--import-snapshot", type=pathlib.Path, required=True)
    parser.add_argument("--final-snapshot", type=pathlib.Path, required=True)
    parser.add_argument("--output", type=pathlib.Path, required=True)
    args = parser.parse_args()
    build(args.import_snapshot, args.final_snapshot, args.output)
