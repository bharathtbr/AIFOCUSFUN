"""
Step 2: LoRA fine-tune a base model on data/train.jsonl.

    python train.py --base Qwen/Qwen2.5-1.5B-Instruct

Trains a small adapter (not the whole model) on the assistant turns only â€” the web searches it should run and the
admin-corrected answer â€” so it doesn't waste capacity memorising prompts. Uses 4-bit QLoRA on a CUDA GPU.
"""

from __future__ import annotations

import argparse
import json
import time
from pathlib import Path

import torch
from transformers import (
    AutoModelForCausalLM,
    AutoTokenizer,
    BitsAndBytesConfig,
    DataCollatorForSeq2Seq,
    Trainer,
    TrainingArguments,
)
from peft import LoraConfig, get_peft_model, prepare_model_for_kbit_training

from focusfun_training.common import WEB_SEARCH_TOOL, read_jsonl, to_chat_messages

IGNORE = -100


def tokenize(example: dict, tokenizer, max_length: int) -> dict | None:
    """Token ids for the whole conversation, with labels only on assistant turns (everything else is ignored in the loss)."""
    messages = to_chat_messages(example["messages"])
    tools = [WEB_SEARCH_TOOL]
    full = tokenizer.apply_chat_template(messages, tools=tools, tokenize=True)
    labels = [IGNORE] * len(full)

    for i, message in enumerate(messages):
        if message["role"] != "assistant":
            continue
        start = len(tokenizer.apply_chat_template(messages[:i], tools=tools, tokenize=True, add_generation_prompt=True))
        end = len(tokenizer.apply_chat_template(messages[: i + 1], tools=tools, tokenize=True))
        labels[start:end] = full[start:end]

    full, labels = full[:max_length], labels[:max_length]
    if all(label == IGNORE for label in labels):
        return None  # the answer didn't fit; skip rather than train on nothing
    return {"input_ids": full, "attention_mask": [1] * len(full), "labels": labels}


def load_split(path: Path, tokenizer, max_length: int) -> list[dict]:
    rows, skipped = [], 0
    for ex in read_jsonl(path):
        row = tokenize(ex, tokenizer, max_length)
        if row is None:
            skipped += 1
        else:
            rows.append(row)
    if skipped:
        print(f"{path.name}: skipped {skipped} example(s) longer than --max-length {max_length}")
    return rows


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--base", default="Qwen/Qwen2.5-1.5B-Instruct", help="Hugging Face base model (needs a chat template with tool support)")
    parser.add_argument("--data", type=Path, default=Path("data"))
    parser.add_argument("--out", type=Path, default=Path("out/adapter"))
    parser.add_argument("--epochs", type=float, default=3)
    parser.add_argument("--max-steps", type=int, default=-1, help="stop early (for a quick pipeline test)")
    parser.add_argument("--lr", type=float, default=2e-4)
    parser.add_argument("--rank", type=int, default=16, help="LoRA rank: higher learns more, needs more memory")
    parser.add_argument("--batch", type=int, default=1)
    parser.add_argument("--grad-accum", type=int, default=8)
    parser.add_argument("--max-length", type=int, default=3072)
    parser.add_argument("--no-4bit", action="store_true", help="train in 16-bit (needs more GPU memory)")
    args = parser.parse_args()

    cuda = torch.cuda.is_available()
    if not cuda:
        print("No CUDA GPU found: training on CPU works only for tiny test runs.")
    bf16 = cuda and torch.cuda.is_bf16_supported()

    tokenizer = AutoTokenizer.from_pretrained(args.base)
    if tokenizer.pad_token is None:
        tokenizer.pad_token = tokenizer.eos_token

    train = load_split(args.data / "train.jsonl", tokenizer, args.max_length)
    test = load_split(args.data / "test.jsonl", tokenizer, args.max_length) if (args.data / "test.jsonl").exists() else []
    if not train:
        raise SystemExit("No training examples. Run prepare.py first.")
    print(f"training on {len(train)} examples, evaluating on {len(test)}")

    quant = None
    if cuda and not args.no_4bit:
        quant = BitsAndBytesConfig(
            load_in_4bit=True,
            bnb_4bit_quant_type="nf4",
            bnb_4bit_use_double_quant=True,
            bnb_4bit_compute_dtype=torch.bfloat16 if bf16 else torch.float16,
        )
    model = AutoModelForCausalLM.from_pretrained(
        args.base,
        quantization_config=quant,
        dtype=torch.bfloat16 if bf16 else (torch.float16 if cuda else torch.float32),
        device_map="auto" if cuda else None,
    )
    if quant is not None:
        model = prepare_model_for_kbit_training(model, use_gradient_checkpointing=True)
    model = get_peft_model(model, LoraConfig(
        r=args.rank, lora_alpha=args.rank * 2, lora_dropout=0.05, target_modules="all-linear", task_type="CAUSAL_LM",
    ))
    model.print_trainable_parameters()

    trainer = Trainer(
        model=model,
        args=TrainingArguments(
            output_dir=str(args.out.parent / "checkpoints"),
            num_train_epochs=args.epochs,
            max_steps=args.max_steps,
            per_device_train_batch_size=args.batch,
            per_device_eval_batch_size=args.batch,
            gradient_accumulation_steps=args.grad_accum,
            learning_rate=args.lr,
            lr_scheduler_type="cosine",
            warmup_ratio=0.05,
            logging_steps=5,
            eval_strategy="epoch" if test and args.max_steps < 0 else "no",
            save_strategy="epoch" if args.max_steps < 0 else "no",
            save_total_limit=2,
            bf16=bf16,
            fp16=cuda and not bf16,
            gradient_checkpointing=cuda,
            report_to=[],
        ),
        train_dataset=train,
        eval_dataset=test or None,
        data_collator=DataCollatorForSeq2Seq(tokenizer, padding=True, label_pad_token_id=IGNORE),
    )

    started = time.time()
    result = trainer.train()
    metrics = {"train_loss": result.training_loss}
    if test:
        metrics["eval_loss"] = trainer.evaluate()["eval_loss"]

    args.out.mkdir(parents=True, exist_ok=True)
    model.save_pretrained(args.out)
    tokenizer.save_pretrained(args.out)
    (args.out / "train_meta.json").write_text(json.dumps({
        "base_model": args.base,
        "train_examples": len(train),
        "test_examples": len(test),
        "epochs": args.epochs,
        "max_steps": args.max_steps,
        "lora_rank": args.rank,
        "learning_rate": args.lr,
        "four_bit": quant is not None,
        "minutes": round((time.time() - started) / 60, 1),
        **metrics,
    }, indent=2), encoding="utf-8")
    print(f"adapter saved to {args.out}  {metrics}")


if __name__ == "__main__":
    main()
