# SKAgent Replay App

Week9 standalone replay UI for:

- recent runs
- run detail with timeline, prompt, steps, and memory summary
- daily suggestion replay entry

## Local dev

```bash
npm install
npm run dev
npm run build
```

By default the Vite dev server proxies `/api/*` to `http://127.0.0.1:5192`.

Override the host target when needed:

```bash
SKAGENT_HOST=http://127.0.0.1:5192 npm run dev
```

If you prefer the browser to call the Host directly instead of using the Vite proxy:

```bash
VITE_API_BASE_URL=https://127.0.0.1:7108 npm run dev
```

The Host now allows local Replay UI origins through CORS, but your browser still needs to trust the ASP.NET development certificate for `https://127.0.0.1:7108`.

## Manual checks

After starting the Host and the Vite app, use:

- `http://127.0.0.1:4179/runs`
- `http://127.0.0.1:4179/runs/245cc2f5b1de4daa965d9eebcd36a3dc`
- `http://127.0.0.1:4179/suggestions`
