"""
Step 5: publish a packaged model to Cloudflare R2, versioned so you can roll back.

    python upload_to_r2.py out/package                 # uploads as the next version (v1, v2, …) and makes it latest
    python upload_to_r2.py out/package --no-promote    # upload only; promote later
    python upload_to_r2.py --promote v1                # roll back: point latest.json at an existing version
    python upload_to_r2.py out/package --dry-run       # show what would happen

Layout in the bucket (under the API's KeyPrefix, e.g. focusfun/):
    models/<name>/v1/model.gguf, Modelfile, metadata.json   [merged/… with --include-merged, for vLLM]
    models/<name>/latest.json                               {"version": "v1", ...}
Credentials come from R2_* environment variables or the API's appsettings.json (Storage section).
"""

from __future__ import annotations

import argparse
import json
import re
from datetime import datetime, timezone
from pathlib import Path

from boto3.s3.transfer import TransferConfig

from focusfun_training.common import r2_client_and_bucket

TRANSFER = TransferConfig(multipart_threshold=64 * 1024 * 1024, multipart_chunksize=64 * 1024 * 1024)


def versions(client, bucket: str, root: str) -> list[int]:
    found = set()
    for page in client.get_paginator("list_objects_v2").paginate(Bucket=bucket, Prefix=root, Delimiter="/"):
        for p in page.get("CommonPrefixes", []):
            if m := re.fullmatch(re.escape(root) + r"v(\d+)/", p["Prefix"]):
                found.add(int(m.group(1)))
    return sorted(found)


def write_latest(client, bucket: str, root: str, version: str, dry_run: bool) -> None:
    key = f"{root}{version}/metadata.json"
    if not dry_run:
        client.head_object(Bucket=bucket, Key=key)  # refuse to point latest at a version that isn't there
    latest = {"version": version, "updated_at": datetime.now(timezone.utc).isoformat(timespec="seconds")}
    print(f"latest.json -> {version}")
    if not dry_run:
        client.put_object(Bucket=bucket, Key=f"{root}latest.json", Body=json.dumps(latest).encode(), ContentType="application/json")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("package", nargs="?", type=Path, help="export.py output folder")
    parser.add_argument("--name", default="focusfun-words")
    parser.add_argument("--version", help="e.g. v3 (default: next free version)")
    parser.add_argument("--no-promote", action="store_true", help="don't update latest.json")
    parser.add_argument("--promote", metavar="VERSION", help="only point latest.json at an existing version (rollback)")
    parser.add_argument("--include-merged", action="store_true", help="also upload the safetensors model (for vLLM)")
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    client, bucket, prefix = r2_client_and_bucket()
    root = f"{prefix}models/{args.name}/"

    if args.promote:
        write_latest(client, bucket, root, args.promote, args.dry_run)
        return
    if args.package is None or not (args.package / "metadata.json").exists():
        parser.error("give the export.py package folder (it must contain metadata.json)")

    version = args.version or f"v{(max(versions(client, bucket, root), default=0) + 1)}"
    # Only the final model.gguf; a leftover 16-bit GGUF would double the upload.
    files = [p for p in args.package.iterdir() if p.is_file() and (p.suffix != ".gguf" or p.name == "model.gguf")]
    if args.include_merged:
        files += [p for p in (args.package / "merged").rglob("*") if p.is_file()]
    # Model files first, metadata last: a version only "exists" once its metadata.json is there.
    files.sort(key=lambda p: p.name == "metadata.json")

    for path in files:
        key = f"{root}{version}/{path.relative_to(args.package).as_posix()}"
        size_mb = path.stat().st_size / 1e6
        print(f"{'would upload' if args.dry_run else 'uploading'} {key} ({size_mb:.1f} MB)")
        if not args.dry_run:
            client.upload_file(str(path), bucket, key, Config=TRANSFER)

    if not args.no_promote:
        write_latest(client, bucket, root, version, args.dry_run)
    print(f"done: {args.name} {version}{' (dry run)' if args.dry_run else ''}")


if __name__ == "__main__":
    main()
