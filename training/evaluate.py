"""
Step 3: score a model on the held-back test split before trusting it.

    python evaluate.py --adapter out/adapter            # the fine-tuned model
    python evaluate.py --base Qwen/Qwen2.5-1.5B-Instruct --base-only   # the untrained base, for comparison

For each test example the model gets the same system prompt and question. When it asks to web_search, it is handed
the results recorded for that example (offline replay), so runs are repeatable and cost nothing.
Scores: valid JSON, overlap with the words the admin kept, rejected words it still suggests, searches made,
and Hindi/Telugu agreement with the admin's spelling. Writes out/report.json.
"""

from __future__ import annotations

import argparse
import json
import re
from pathlib import Path
from statistics import mean

import torch
from transformers import AutoModelForCausalLM, AutoTokenizer

from focusfun_training.common import WEB_SEARCH_TOOL, final_answer, read_jsonl, to_chat_messages

MAX_SEARCHES = 5
TOOL_CALL = re.compile(r"<tool_call>\s*(\{.*?\})\s*</tool_call>", re.DOTALL)


def load(base: str | None, adapter: Path | None):
    cuda = torch.cuda.is_available()
    dtype = torch.bfloat16 if cuda and torch.cuda.is_bf16_supported() else (torch.float16 if cuda else torch.float32)
    if adapter is not None:
        base = base or json.loads((adapter / "train_meta.json").read_text(encoding="utf-8"))["base_model"]
    tokenizer = AutoTokenizer.from_pretrained(adapter or base)
    model = AutoModelForCausalLM.from_pretrained(base, dtype=dtype, device_map="auto" if cuda else None)
    if adapter is not None:
        from peft import PeftModel
        model = PeftModel.from_pretrained(model, adapter)
    model.eval()
    return tokenizer, model, base


def generate(tokenizer, model, messages: list[dict], max_new_tokens: int) -> str:
    ids = tokenizer.apply_chat_template(messages, tools=[WEB_SEARCH_TOOL], add_generation_prompt=True, return_tensors="pt").to(model.device)
    with torch.no_grad():
        out = model.generate(ids, max_new_tokens=max_new_tokens, do_sample=False, pad_token_id=tokenizer.pad_token_id or tokenizer.eos_token_id)
    return tokenizer.decode(out[0, ids.shape[1]:], skip_special_tokens=True).strip()


def run_example(tokenizer, model, example: dict, max_new_tokens: int) -> tuple[str, list[str]]:
    """Plays one conversation: tool calls get the example's recorded search results, in order."""
    recorded = [m["content"] for m in example["messages"] if m["role"] == "tool"]
    messages = to_chat_messages(example["messages"][:2])
    queries: list[str] = []
    for _ in range(MAX_SEARCHES + 1):
        reply = generate(tokenizer, model, messages, max_new_tokens)
        call = TOOL_CALL.search(reply)
        if call is None or len(queries) >= MAX_SEARCHES:
            return reply, queries
        try:
            parsed = json.loads(call.group(1))
            query = str(parsed.get("arguments", {}).get("query", ""))
        except (json.JSONDecodeError, AttributeError):
            return reply, queries
        queries.append(query)
        results = recorded[len(queries) - 1] if len(queries) <= len(recorded) else "[]"
        messages.append({"role": "assistant", "content": "", "tool_calls": [{"type": "function", "function": {"name": "web_search", "arguments": {"query": query}}}]})
        messages.append({"role": "tool", "content": results})
    return reply, queries


def parse_answer(reply: str) -> dict | None:
    start, end = reply.find("{"), reply.rfind("}")
    if start < 0 or end <= start:
        return None
    try:
        answer = json.loads(reply[start:end + 1])
    except json.JSONDecodeError:
        return None
    return answer if isinstance(answer, dict) and isinstance(answer.get("words"), list) else None


def score(example: dict, reply: str, queries: list[str]) -> dict:
    kept = {w["word"].strip().lower(): w for w in final_answer(example)["words"]}
    rejected = {w.strip().lower() for w in example.get("metadata", {}).get("rejected", [])}
    answer = parse_answer(reply)
    if answer is None:
        return {"valid_json": 0.0, "precision": 0.0, "recall": 0.0, "rejected_hits": 0, "searches": len(queries), "hindi": None, "telugu": None}

    suggested = {str(w.get("word", "")).strip().lower(): w for w in answer["words"] if isinstance(w, dict)}
    overlap = [w for w in suggested if w in kept]

    def agreement(field: str) -> float | None:
        pairs = [(suggested[w].get(field) or "", kept[w].get(field) or "") for w in overlap if kept[w].get(field)]
        return mean(float(a.strip() == b.strip()) for a, b in pairs) if pairs else None

    return {
        "valid_json": 1.0,
        "precision": len(overlap) / len(suggested) if suggested else 0.0,
        "recall": len(overlap) / len(kept) if kept else 0.0,
        "rejected_hits": sum(w in rejected for w in suggested),
        "searches": len(queries),
        "hindi": agreement("hindi"),
        "telugu": agreement("telugu"),
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--adapter", type=Path, help="LoRA adapter from train.py")
    parser.add_argument("--base", help="base model (read from the adapter when omitted)")
    parser.add_argument("--base-only", action="store_true", help="score the base model without the adapter")
    parser.add_argument("--test", type=Path, default=Path("data/test.jsonl"))
    parser.add_argument("--out", type=Path, default=Path("out/report.json"))
    parser.add_argument("--limit", type=int, default=0, help="only the first N examples (quick check)")
    parser.add_argument("--max-new-tokens", type=int, default=1200)
    args = parser.parse_args()
    if not args.base_only and args.adapter is None:
        parser.error("pass --adapter, or --base with --base-only")

    tokenizer, model, base = load(args.base, None if args.base_only else args.adapter)
    examples = list(read_jsonl(args.test))[: args.limit or None]
    rows = []
    for n, ex in enumerate(examples, 1):
        reply, queries = run_example(tokenizer, model, ex, args.max_new_tokens)
        rows.append(score(ex, reply, queries))
        print(f"[{n}/{len(examples)}] {rows[-1]}")

    def avg(key: str) -> float | None:
        values = [r[key] for r in rows if r[key] is not None]
        return round(mean(values), 3) if values else None

    summary = {
        "model": base if args.base_only else str(args.adapter),
        "base_model": base,
        "examples": len(rows),
        "valid_json": avg("valid_json"),
        "precision": avg("precision"),
        "recall": avg("recall"),
        "rejected_hits_per_answer": avg("rejected_hits"),
        "searches_per_answer": avg("searches"),
        "hindi_agreement": avg("hindi"),
        "telugu_agreement": avg("telugu"),
    }
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(json.dumps({"summary": summary, "examples": rows}, indent=2), encoding="utf-8")
    print(json.dumps(summary, indent=2))


if __name__ == "__main__":
    main()
