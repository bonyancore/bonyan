# Intelligence service (Python, FastAPI)

Extraction, transcription, copilot and forecasts. It never writes business records directly;
it returns suggestions through the contracts in `contracts/intelligence`.

- `app/api/`: internal endpoints (extract, transcribe)
- `app/pipelines/`: transcribe, extract, resolve, score
- `app/schemas/`: models generated from `contracts/intelligence`
- `evals/`: golden sets and release gates
- `tests/`

Agents suggest, humans confirm. See Backend Engineering Handbook BE-08 and spec 14.
No dependencies or code added yet.
