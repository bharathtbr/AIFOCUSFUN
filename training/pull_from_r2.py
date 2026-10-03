"""
Run on the model server at startup: fetch the latest published model from R2 and load it into Ollama.

    python pull_from_r2.py                    # latest version -> ./models, then `ollama create focusfun-words`
    python pull_from_r2.py --version v1       # a specific version
    python pull_from_r2.py --no-ollama        # just download (e.g. for vLLM with --include-merged uploads)

Skips the download when that version is already on disk with matching checksums, so restarts are quick.
Credentials: R2_ENDPOINT, R2_BUCKET, R2_ACCESS_KEY_ID, R2_SECRET_ACCESS_KEY (and optional R2_PREFIX) environment variables.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import subprocess
from pathlib import Path

from focusfun_training.common import r2_client_and_bucket


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--name", default="focusfun-words")
    parser.add_argument("--version", help="default: the version in latest.json")
    parser.add_argument("--dest", type=Path, default=Path("models"))
    parser.add_argument("--no-ollama", action="store_true")
    args = parser.parse_args()

    client, bucket, prefix = r2_client_and_bucket()
    root = f"{prefix}models/{args.name}/"
    version = args.version or json.loads(client.get_object(Bucket=bucket, Key=f"{root}latest.json")["Body"].read())["version"]
    target = args.dest / args.name / version

    metadata = json.loads(client.get_object(Bucket=bucket, Key=f"{root}{version}/metadata.json")["Body"].read())
    # Ollama uses the GGUF + Modelfile; vLLM (--no-ollama) uses the merged safetensors.
    wanted = metadata.get("merged_files" if args.no_ollama else "files", {})
    if not wanted:
        kind = "merged safetensors (export, then upload with --include-merged)" if args.no_ollama else "GGUF (export with --llama-cpp)"
        raise SystemExit(f"{args.name} {version} has no {kind}")
    have = (target / "metadata.json").exists() and all(
        (target / f).exists() and sha256(target / f) == info["sha256"] for f, info in wanted.items())
    if have:
        print(f"{args.name} {version} already on disk")
    else:
        tmp = target.with_name(version + ".partial")
        shutil.rmtree(tmp, ignore_errors=True)
        tmp.mkdir(parents=True)
        for name in [*wanted, "metadata.json"]:
            print(f"downloading {name} ...")
            (tmp / name).parent.mkdir(parents=True, exist_ok=True)
            client.download_file(bucket, f"{root}{version}/{name}", str(tmp / name))
        for name, info in wanted.items():
            if sha256(tmp / name) != info["sha256"]:
                raise SystemExit(f"checksum mismatch for {name}; not installing {version}")
        shutil.rmtree(target, ignore_errors=True)
        tmp.rename(target)

    (args.dest / args.name / "current.txt").write_text(version, encoding="utf-8")
    if not args.no_ollama:
        subprocess.run(["ollama", "create", args.name, "-f", str(target / "Modelfile")], check=True)
        print(f"ollama model '{args.name}' now serves {version}")


if __name__ == "__main__":
    main()
