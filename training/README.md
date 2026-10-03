# Training Focus Fun's own model

This folder fine-tunes an open model to do what Claude does in the Admin "Find words" job: decide what to search, then return age-appropriate words with Hindi and Telugu forms. The finished model is published to Cloudflare R2, and the model server pulls it from there.

```
Admin export (.jsonl) → prepare.py → train.py → evaluate.py → export.py → upload_to_r2.py → R2 → pull_from_r2.py (model server) → Ollama/vLLM ← C# API
```

## Before a real run

- **Enough data.** Aim for ~300+ reviewed examples. `prepare.py` warns below that. One example is one "Find words" run that you reviewed in Admin by adding words. Your Hindi and Telugu corrections are the most valuable part.
- **Terms.** You are training on Claude's outputs. Check Anthropic's commercial terms on using outputs to train models before training a model you'll use.
- **Hardware:**

  | Base model | GPU memory (4-bit QLoRA) | Where |
  | --- | --- | --- |
  | Qwen2.5-0.5B / 1.5B-Instruct | 4–8 GB | this laptop's RTX 4060 (8 GB) |
  | Qwen2.5-3B-Instruct | ~8–10 GB | RTX 4060 with `--max-length 2048`, or Colab T4 |
  | Qwen2.5-7B-Instruct | 16–24 GB | rented GPU (RunPod, Lambda, Vast.ai; about $0.40–$1/hour) or Colab |

  Start with 1.5B or 3B and move up only if the evaluation says it's needed. Qwen2.5 is the default because its chat template supports tool calls (`web_search`) and it handles JSON well. Telugu is weak in most general models, so watch `telugu_agreement` in the report.

## Setup (once)

```powershell
cd training
py -3.11 -m venv .venv
.\.venv\Scripts\python.exe -m pip install torch --index-url https://download.pytorch.org/whl/cu124
.\.venv\Scripts\python.exe -m pip install -r requirements.txt
.\.venv\Scripts\Activate.ps1
```

For the Ollama file (GGUF), also get llama.cpp:
- `git clone https://github.com/ggml-org/llama.cpp C:\tools\llama.cpp`, which provides `convert_hf_to_gguf.py`.
- Put `llama-quantize.exe` from a [llama.cpp release](https://github.com/ggml-org/llama.cpp/releases) zip into that folder. Use the `llama-bXXXX-bin-win-cpu-x64.zip` asset; not every release has one, so pick a recent release that does.
- The zip also has `llama-server.exe`, which serves the GGUF on an OpenAI-style endpoint for a quick local check before using Ollama: `llama-server -m out/package/model.gguf --jinja --port 8089`.

## Each training run

Run these from `training/` with the venv active.

1. **Export data:** Admin → AI training data → *Download for fine-tuning*. Save it in `training/exports/`.
2. **Prepare** (checks the data, splits it into train and test):
   ```powershell
   python prepare.py exports\*.jsonl
   ```
3. **Train:**
   ```powershell
   python train.py --base Qwen/Qwen2.5-1.5B-Instruct
   ```
   This writes `out/adapter` (the LoRA adapter plus `train_meta.json`). Useful options: `--epochs 3`, `--rank 16`, `--max-length 3072`, `--max-steps 20` for a quick test.
4. **Evaluate.** Always compare with the untrained base:
   ```powershell
   python evaluate.py --base Qwen/Qwen2.5-1.5B-Instruct --base-only --out out/report-base.json
   python evaluate.py --adapter out/adapter
   ```
   The report shows:
   - `valid_json`: how often it answers in the right format. Should be ~1.0.
   - `precision`: share of its words you had kept.
   - `recall`: share of your kept words it found.
   - `rejected_hits_per_answer`: words you'd dropped that it still suggests. Lower is better.
   - `searches_per_answer`: how many web searches it makes.
   - `hindi_agreement` / `telugu_agreement`: exact match with your spelling.

   When the model asks to search, it gets the recorded results from that example, so evaluation runs offline and repeatably. Publish only if it clearly beats the base and the previous version.
5. **Export:**
   ```powershell
   python export.py --adapter out/adapter --llama-cpp C:\tools\llama.cpp
   ```
   This writes `out/package`: `model.gguf` (4-bit) and `Modelfile` for Ollama, `merged/` (safetensors) for vLLM, and `metadata.json` with the training details, scores and checksums.
6. **Publish to R2:**
   ```powershell
   python upload_to_r2.py out/package --dry-run   # check first
   python upload_to_r2.py out/package             # uploads as the next version and makes it latest
   ```
   This uses the `Storage` settings in `backend/src/FocusFun.Api/appsettings.json`, or `R2_*` environment variables. Files land under `focusfun/models/focusfun-words/vN/`, and `latest.json` points at the live version.
   - **Roll back:** `python upload_to_r2.py --promote v1`.
   - Add `--include-merged` if the server runs vLLM.

## On the model server

Install Ollama and Python with `boto3`, copy `pull_from_r2.py` and `focusfun_training/`, set the R2 environment variables, then run this at startup (before Ollama serves requests):

```bash
export R2_ENDPOINT=https://<account>.r2.cloudflarestorage.com R2_BUCKET=<bucket> R2_ACCESS_KEY_ID=... R2_SECRET_ACCESS_KEY=...
python pull_from_r2.py        # downloads latest (skips if unchanged), verifies checksums, runs `ollama create focusfun-words`
```

The Focus Fun API then calls `http://<server>:11434/v1/chat/completions` with model `focusfun-words`, once the local provider is added to the API. Use a read-only R2 token on the server.

If tool calls don't work in Ollama, the GGUF's chat template wasn't recognised. Copy the `TEMPLATE` block from `ollama show qwen2.5 --modelfile` into the `Modelfile` and re-run `ollama create`.

## Testing the pipeline without real data

`sample/make_sample.py` writes 8 hand-made examples in the export format. They are for checking that the scripts run end to end, never for training a model you'll use:

```powershell
$env:PYTHONPATH = (Get-Location).Path
python sample/make_sample.py
python prepare.py sample/sample-export.jsonl --out data-sample --test-share 0.25
python train.py --base Qwen/Qwen2.5-0.5B-Instruct --data data-sample --out out-sample/adapter --max-steps 10
python evaluate.py --adapter out-sample/adapter --test data-sample/test.jsonl --out out-sample/report.json --max-new-tokens 400
python export.py --adapter out-sample/adapter --out out-sample/package --report out-sample/report.json
```
