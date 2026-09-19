# Installing Lapper (dev/alpha)

Lapper is two pieces: a **Windows desktop app** (installed from the MSIX in
this bundle) and a **local backend** (Node.js, holds the OpenAI key — the
desktop app never talks to OpenAI directly). Both must be running.

## 1. Install the desktop app

From this bundle folder, in an **elevated** PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File .\Install-Lapper.ps1
```

The script does two things: trusts the dev signing certificate
(`Lapper-Dev.cer` → Local Machine → Trusted People — needed once per
machine because dev builds are self-signed) and installs `Lapper.msix`.
Verify downloads against `SHA256SUMS.txt` if you want
(`Get-FileHash .\Lapper.msix`).

To update later: install the new bundle's MSIX the same way. To uninstall:
Settings → Apps → Lapper → Uninstall (the certificate can be removed from
`certlm.msc` → Trusted People).

## 2. Run the backend

Prerequisite: Node.js 24 LTS. From a clone of this repository:

```powershell
cd backend
npm ci
Copy-Item .env.example .env
# open .env and set OPENAI_API_KEY=sk-...
npm run build
npm start
```

The server listens on `http://127.0.0.1:3000` (the desktop app's default
backend URL). Leave it running. Without an API key it still runs, but
orientation requests answer "AI is not configured".

`npm run dev` works too if you prefer watch mode.

## 3. Use Lapper

1. Start **Lapper** from the Start menu. It lives in the system tray with a
   floating pill on screen.
2. Focus any window and press **Ctrl+Alt+L** (or click the pill).
3. A compact card appears with a one-sentence orientation of what you're
   looking at, then facts and suggested actions (copy, read aloud, draft…).
4. **Esc** or clicking elsewhere dismisses the card. Right-click the tray
   icon for settings (shortcut, backend URL, excluded apps) and exit.

## Troubleshooting

- **"AI is not configured"** — set `OPENAI_API_KEY` in `backend/.env` and
  restart the backend.
- **"Can't reach the Lapper backend"** — the backend isn't running, or the
  backend URL in Lapper's settings doesn't match it. Default is
  `http://127.0.0.1:3000/`. Non-localhost URLs must be `https://`.
- **Add-AppxPackage error 0x800B0100 / certificate errors** — the
  certificate step didn't run; re-run `Install-Lapper.ps1` as Administrator.
- **Nothing happens on Ctrl+Alt+L in a specific app** — password managers
  and elevation prompts are excluded by design; Lapper also declines when a
  password field is focused.

## What Lapper sends and stores

Screen text is read locally (UI Automation, then local OCR only if needed),
secret-shaped content is redacted on-device, and only that redacted text is
sent to **your own backend** over the URL you configured. Screenshots never
leave the machine and are never written to disk. Neither the backend nor
the app logs screen content, questions or answers.
