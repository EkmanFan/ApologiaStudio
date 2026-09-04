# LCGFT encoder — evaluation scripts

Prepared, **not executed**. They belong to the protocol in
`docs/knowledge-ingestion/genre-form-classifier-experimental-protocol-2026-09-04.md`.

The training spike itself lives outside this repository, in
`~/Documents/DeBERTa Spike/`. Nothing here writes into that directory: the
checkpoint is read, never modified.

## e1_calibrate_and_evaluate.py

Runs once, on a frozen checkpoint. Selects one threshold per label from the
validation split under three objectives — F1, precision ≥ 0.80, precision ≥ 0.90
— freezes them, then scores the test split exactly once.

`--device` defaults to `cpu` on purpose. Pass `cuda` only when no training run
holds the card.

```bash
python3 spikes/lcgft-encoder/e1_calibrate_and_evaluate.py \
  --dataset-dir "$HOME/Documents/DeBERTa Spike/dataset-v2" \
  --model-dir   "$HOME/Documents/DeBERTa Spike/runs/<frozen-run>/model" \
  --output-dir  "$HOME/Documents/DeBERTa Spike/runs/<frozen-run>/e1" \
  --checkpoint-note "epochs=<n> seed=<s>"
```

Writes `thresholds-frozen.json` — which E2 consumes and which is never edited
after E1 — and `e1-report.json`.
