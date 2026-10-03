"""Shared helpers: reading the Admin export, the web_search tool definition, and R2 access."""

from __future__ import annotations

import json
import os
from pathlib import Path
from typing import Any, Iterable, Iterator

REPO_ROOT = Path(__file__).resolve().parents[2]
APPSETTINGS = REPO_ROOT / "backend" / "src" / "FocusFun.Api" / "appsettings.json"

# Must match the tool the C# local provider sends at inference time, so training and serving see the same format.
WEB_SEARCH_TOOL: dict[str, Any] = {
    "type": "function",
    "function": {
        "name": "web_search",
        "description": "Search the web. Returns a list of results with title and url.",
        "parameters": {
            "type": "object",
            "properties": {"query": {"type": "string", "description": "What to search for."}},
            "required": ["query"],
        },
    },
}


def read_jsonl(path: Path) -> Iterator[dict[str, Any]]:
    with path.open(encoding="utf-8") as f:
        for n, line in enumerate(f, 1):
            line = line.strip()
            if not line:
                continue
            try:
                yield json.loads(line)
            except json.JSONDecodeError as e:
                raise ValueError(f"{path}:{n}: not valid JSON ({e})") from e


def write_jsonl(path: Path, rows: Iterable[dict[str, Any]]) -> int:
    path.parent.mkdir(parents=True, exist_ok=True)
    count = 0
    with path.open("w", encoding="utf-8") as f:
        for row in rows:
            f.write(json.dumps(row, ensure_ascii=False) + "\n")
            count += 1
    return count


def to_chat_messages(messages: list[dict[str, Any]]) -> list[dict[str, Any]]:
    """
    Converts exported messages to the shape Hugging Face chat templates expect:
    tool-call arguments become dicts (the export stores them as JSON strings, OpenAI-style).
    """
    out = []
    for m in messages:
        m = dict(m)
        if m.get("tool_calls"):
            calls = []
            for call in m["tool_calls"]:
                fn = dict(call["function"])
                if isinstance(fn.get("arguments"), str):
                    fn["arguments"] = json.loads(fn["arguments"])
                calls.append({"type": "function", "function": fn})
            m["tool_calls"] = calls
            if not m.get("content"):
                m["content"] = ""
        out.append(m)
    return out


def final_answer(example: dict[str, Any]) -> dict[str, Any]:
    """The admin-corrected answer: the last assistant message, parsed."""
    return json.loads(example["messages"][-1]["content"])


def r2_client_and_bucket() -> tuple[Any, str, str]:
    """
    R2 credentials from environment variables (on a model server) or, failing that, the API's appsettings.json
    (on the dev machine), so there is one source of truth. Returns (boto3 client, bucket, key prefix).
    """
    import boto3
    from botocore.config import Config

    storage: dict[str, Any] = {}
    if APPSETTINGS.exists():
        storage = json.loads(APPSETTINGS.read_text(encoding="utf-8-sig")).get("Storage", {})

    def setting(env: str, key: str, default: str = "") -> str:
        return os.environ.get(env) or storage.get(key) or default

    endpoint = setting("R2_ENDPOINT", "ServiceUrl")
    bucket = setting("R2_BUCKET", "Bucket")
    prefix = setting("R2_PREFIX", "KeyPrefix", "focusfun/")
    if not endpoint or not bucket or endpoint.startswith("https://<"):
        raise SystemExit("R2 is not configured: set R2_ENDPOINT/R2_BUCKET/R2_ACCESS_KEY_ID/R2_SECRET_ACCESS_KEY or fill Storage in appsettings.json.")

    client = boto3.client(
        "s3",
        endpoint_url=endpoint,
        aws_access_key_id=setting("R2_ACCESS_KEY_ID", "AccessKeyId"),
        aws_secret_access_key=setting("R2_SECRET_ACCESS_KEY", "SecretAccessKey"),
        region_name="auto",
        # R2 does not support the newer default checksum headers.
        config=Config(request_checksum_calculation="when_required", response_checksum_validation="when_required"),
    )
    return client, bucket, prefix
