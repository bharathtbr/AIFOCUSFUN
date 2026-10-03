"""
Step 1: turn Admin → "AI training data" exports into clean train/test splits.

    python prepare.py exports/*.jsonl

Checks every example, drops broken or duplicate ones, holds back a test split the model never trains on,
and prints a short quality report (with a warning when there isn't enough data yet).
"""

from __future__ import annotations

import argparse
import json
import random
from collections import Counter
from pathlib import Path

from focusfun_training.common import final_answer, read_jsonl, write_jsonl

RECOMMENDED_MINIMUM = 300


def problems(example: dict) -> list[str]:
    """Why an example can't be used (empty list = fine)."""
    messages = example.get("messages")
    if not isinstance(messages, list) or len(messages) < 3:
        return ["needs system, user and assistant messages"]
    if messages[0].get("role") != "system" or messages[1].get("role") != "user":
        return ["must start with system then user"]
    if messages[-1].get("role") != "assistant" or messages[-1].get("tool_calls"):
        return ["must end with the assistant's final answer"]
    try:
        answer = final_answer(example)
    except (json.JSONDecodeError, TypeError):
        return ["final answer is not valid JSON"]
    words = answer.get("words") if isinstance(answer, dict) else None
    if not words:
        return ["final answer has no words (nothing was kept)"]
    if any(not isinstance(w, dict) or not str(w.get("word", "")).strip() for w in words):
        return ["a kept word is empty"]
    for m in messages:
        for call in m.get("tool_calls") or []:
            try:
                json.loads(call["function"]["arguments"])
            except (KeyError, TypeError, json.JSONDecodeError):
                return ["a web_search call has unreadable arguments"]
    return []


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("exports", nargs="+", type=Path, help="JSONL files downloaded from Admin → AI training data")
    parser.add_argument("--out", type=Path, default=Path("data"), help="where train.jsonl and test.jsonl go")
    parser.add_argument("--test-share", type=float, default=0.1, help="share held back for evaluation")
    parser.add_argument("--seed", type=int, default=42)
    args = parser.parse_args()

    examples: dict[str, dict] = {}
    rejected: Counter[str] = Counter()
    for path in args.exports:
        for ex in read_jsonl(path):
            reasons = problems(ex)
            if reasons:
                rejected[reasons[0]] += 1
                continue
            # Later exports repeat earlier calls; keep one copy per call id.
            key = str(ex.get("metadata", {}).get("id") or json.dumps(ex["messages"], sort_keys=True))
            examples[key] = ex

    rows = list(examples.values())
    random.Random(args.seed).shuffle(rows)
    n_test = max(1, round(len(rows) * args.test_share)) if len(rows) >= 2 else 0
    test, train = rows[:n_test], rows[n_test:]

    write_jsonl(args.out / "train.jsonl", train)
    write_jsonl(args.out / "test.jsonl", test)

    searches = sum(any(m.get("tool_calls") for m in ex["messages"]) for ex in rows)
    words = [w for ex in rows for w in final_answer(ex)["words"]]
    hindi = sum(bool(w.get("hindi")) for w in words)
    telugu = sum(bool(w.get("telugu")) for w in words)
    print(f"usable examples: {len(rows)}  (train {len(train)}, test {len(test)})")
    for reason, count in rejected.most_common():
        print(f"  skipped {count}: {reason}")
    if rows:
        print(f"examples with web searches: {searches} ({searches / len(rows):.0%})")
        print(f"kept words: {len(words)}  with Hindi {hindi / max(1, len(words)):.0%}, with Telugu {telugu / max(1, len(words)):.0%}")
    if len(rows) < RECOMMENDED_MINIMUM:
        print(f"WARNING: {len(rows)} examples is below the ~{RECOMMENDED_MINIMUM} recommended for a useful fine-tune. "
              "Fine for testing the pipeline; keep reviewing in Admin before a real training run.")


if __name__ == "__main__":
    main()
