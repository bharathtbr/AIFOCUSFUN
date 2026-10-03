"""
Writes sample/sample-export.jsonl: a handful of hand-made examples in the exact Admin export format.
For testing the pipeline only — never train a real model on it. Real data comes from Admin → AI training data.
"""

from __future__ import annotations

import json
import re
import textwrap
from pathlib import Path

from focusfun_training.common import REPO_ROOT, write_jsonl

WORD_FINDER = REPO_ROOT / "backend" / "src" / "FocusFun.Api" / "Ai" / "WordFinder.cs"

# (category, searches, kept words (word, hindi, telugu), rejected)
SAMPLES = [
    ("Fruits", ["common fruits names for kids picture dictionary"],
     [("apple", "सेब", "ఆపిల్"), ("mango", "आम", "మామిడి"), ("banana", "केला", "అరటి"), ("grapes", "अंगूर", "ద్రాక్ష"), ("orange", "संतरा", "నారింజ")],
     ["durian"]),
    ("Farm animals", ["farm animals list for kindergarten"],
     [("cow", "गाय", "ఆవు"), ("horse", "घोड़ा", "గుర్రం"), ("hen", "मुर्गी", "కోడి"), ("goat", "बकरी", "మేక")],
     ["alpaca"]),
    ("Wild animals", ["wild animals names for children"],
     [("lion", "शेर", "సింహం"), ("elephant", "हाथी", "ఏనుగు"), ("tiger", "बाघ", "పులి"), ("monkey", "बंदर", "కోతి")],
     []),
    ("Colours", ["basic colours words for preschool"],
     [("red", "लाल", "ఎరుపు"), ("blue", "नीला", "నీలం"), ("green", "हरा", "ఆకుపచ్చ"), ("yellow", "पीला", "పసుపు")],
     ["magenta"]),
    ("Body parts", ["body parts vocabulary for kids", "simple body part words age 6"],
     [("hand", "हाथ", "చేయి"), ("eye", "आँख", "కన్ను"), ("nose", "नाक", "ముక్కు"), ("ear", "कान", "చెవి")],
     ["elbow"]),
    ("Food", ["everyday food words for kids india"],
     [("rice", "चावल", "అన్నం"), ("milk", "दूध", "పాలు"), ("water", "पानी", "నీళ్ళు")],
     []),
    ("Shapes", [],
     [("circle", "गोला", "వృత్తం"), ("square", "वर्ग", "చతురస్రం"), ("star", "तारा", "నక్షత్రం")],
     ["rhombus"]),
    ("Birds", ["common birds names for children india"],
     [("parrot", "तोता", "చిలుక"), ("crow", "कौआ", "కాకి"), ("peacock", "मोर", "నెమలి")],
     ["albatross"]),
]


def system_prompt() -> str:
    source = WORD_FINDER.read_text(encoding="utf-8")
    match = re.search(r'SystemPrompt = """\r?\n(.*?)\r?\n\s*""";', source, re.DOTALL)
    if not match:
        raise SystemExit(f"couldn't find SystemPrompt in {WORD_FINDER}")
    return textwrap.dedent(match.group(1)).strip()


def example(n: int, category: str, searches: list[str], kept: list[tuple[str, str, str]], rejected: list[str], system: str) -> dict:
    prompt = f"Category: {category}\n\nFind {len(kept) + len(rejected)} new words.\n\nExisting words (don't repeat): none"
    messages: list[dict] = [{"role": "system", "content": system}, {"role": "user", "content": prompt}]
    for i, query in enumerate(searches, 1):
        messages.append({"role": "assistant", "content": "", "tool_calls": [
            {"id": f"search_{i}", "type": "function", "function": {"name": "web_search", "arguments": json.dumps({"query": query})}}]})
        messages.append({"role": "tool", "tool_call_id": f"search_{i}", "content": json.dumps([
            {"title": f"{category} for kids | sample result", "url": f"https://example.org/{category.lower().replace(' ', '-')}"}])})
    words = [{"word": w, "hindi": h, "telugu": t} for w, h, t in kept]
    messages.append({"role": "assistant", "content": json.dumps({"words": words, "sources": ["https://example.org/"]}, ensure_ascii=False)})
    return {"messages": messages, "metadata": {"id": f"sample-{n}", "task": "word-suggestions", "provider": "sample", "model": "hand-written", "rejected": rejected}}


def main() -> None:
    system = system_prompt()
    out = Path(__file__).with_name("sample-export.jsonl")
    count = write_jsonl(out, (example(n, *s, system=system) for n, s in enumerate(SAMPLES, 1)))
    print(f"wrote {count} sample examples to {out}")


if __name__ == "__main__":
    main()
