"""
Step 4: package a trained adapter for serving.

    python export.py --adapter out/adapter --llama-cpp C:/tools/llama.cpp

Merges the LoRA adapter into the base model (safetensors, for vLLM) and, when a llama.cpp checkout is given,
converts it to a 4-bit GGUF with an Ollama Modelfile. Writes metadata.json with the base model, training details,
evaluation scores and file checksums, which upload_to_r2.py publishes alongside the model.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path

import torch
from peft import PeftModel
from transformers import AutoModelForCausalLM, AutoTokenizer

MODELFILE = """FROM ./model.gguf
PARAMETER temperature 0.3
PARAMETER num_ctx 8192
"""


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def describe(path: Path) -> dict:
    return {"bytes": path.stat().st_size, "sha256": sha256(path)}


def find_quantize(llama_cpp: Path) -> Path | None:
    for name in ("llama-quantize.exe", "llama-quantize"):
        for folder in (llama_cpp, llama_cpp / "build" / "bin", llama_cpp / "build" / "bin" / "Release"):
            if (folder / name).exists():
                return folder / name
    return None


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--adapter", type=Path, default=Path("out/adapter"))
    parser.add_argument("--out", type=Path, default=Path("out/package"))
    parser.add_argument("--report", type=Path, default=Path("out/report.json"), help="evaluate.py output to include")
    parser.add_argument("--llama-cpp", type=Path, help="llama.cpp folder (convert_hf_to_gguf.py + llama-quantize) for the Ollama GGUF")
    parser.add_argument("--quant", default="Q4_K_M", help="GGUF quantisation (Q4_K_M is a good size/quality balance)")
    args = parser.parse_args()

    meta = json.loads((args.adapter / "train_meta.json").read_text(encoding="utf-8"))
    base = meta["base_model"]
    if args.out.exists():
        shutil.rmtree(args.out)
    merged = args.out / "merged"

    print(f"merging {args.adapter} into {base} â€¦")
    model = AutoModelForCausalLM.from_pretrained(base, dtype=torch.float16)
    model = PeftModel.from_pretrained(model, args.adapter).merge_and_unload()
    model.save_pretrained(merged, safe_serialization=True)
    AutoTokenizer.from_pretrained(args.adapter).save_pretrained(merged)

    files = []
    if args.llama_cpp:
        converter = args.llama_cpp / "convert_hf_to_gguf.py"
        if not converter.exists():
            raise SystemExit(f"{converter} not found: clone https://github.com/ggml-org/llama.cpp")
        f16 = args.out / "model-f16.gguf"
        subprocess.run([sys.executable, str(converter), str(merged), "--outfile", str(f16), "--outtype", "f16"], check=True)
        quantize = find_quantize(args.llama_cpp)
        gguf = args.out / "model.gguf"
        if quantize:
            subprocess.run([str(quantize), str(f16), str(gguf), args.quant], check=True)
            f16.unlink()
        else:
            print("llama-quantize not found: keeping the 16-bit GGUF (about 4x larger). Download a llama.cpp release to quantise.")
            f16.rename(gguf)
        (args.out / "Modelfile").write_text(MODELFILE, encoding="utf-8")
        files += ["model.gguf", "Modelfile"]
    else:
        print("No --llama-cpp given: exported safetensors only (for vLLM). Add --llama-cpp for an Ollama GGUF.")

    report = json.loads(args.report.read_text(encoding="utf-8"))["summary"] if args.report.exists() else None
    metadata = {
        "name": "focusfun-words",
        "task": "word-suggestions",
        "base_model": base,
        "exported_at": datetime.now(timezone.utc).isoformat(timespec="seconds"),
        "training": meta,
        "evaluation": report,
        "gguf_quant": args.quant if "model.gguf" in files else None,
        # Ollama needs "files"; vLLM needs "merged_files" (uploaded only with upload_to_r2.py --include-merged).
        "files": {name: describe(args.out / name) for name in files},
        "merged_files": {p.relative_to(args.out).as_posix(): describe(p) for p in sorted(merged.rglob("*")) if p.is_file()},
    }
    (args.out / "metadata.json").write_text(json.dumps(metadata, indent=2), encoding="utf-8")
    print(f"package ready in {args.out}: {', '.join(files + ['merged/', 'metadata.json'])}")


if __name__ == "__main__":
    main()
