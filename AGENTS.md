# Working Preferences

## Communication
- Reply in Russian when the request is in Russian.
- Be concise: the solution first, then a short explanation.
- Do not repeat the request and do not add unnecessary introductions.
- If the task is ambiguous, ask 1-5 clarifying questions instead of guessing.
- For complex changes, propose a plan first and wait for confirmation.

## Code style
- Write clean, readable code with meaningful variable and function names.
- Follow the Microsoft code style.
- Use strict typing where possible; avoid `any`.
- Prefer early returns over deep nesting.
- Add error handling; never ignore exceptions silently.
- Do not add comments that are obvious from the code.
- Follow the existing project style, even if it differs from your preferences.

## Git
- Use Conventional Commits:
  - `feat:`, `fix:`, `docs:`, `refactor:`, `test:`, `chore:`
- Format: `type(scope): short description`.
- Example: `feat(auth): add Google OAuth login`.
- Never commit secrets, `.env` files, tokens or personal data.

## Documentation
- Write documentation in English; keep the UI and user-facing texts localized through the i18n dictionaries.
- For README, state the purpose, quick start, main commands and known limitations.
- Update the documentation together with behavior changes.

## Testing
- For new functionality, propose unit tests.
- For .NET use xUnit.
- To fix a bug, first add a test that reproduces the problem.
- Do not change tests just to make them pass; explain why a change is justified.

## Security
- Never print or store secrets, API keys and passwords in code or logs.
- Validate user input.
- Use the least privilege necessary for services and IAM policies.

## Important rules
- Before starting work, make a plan and clarify every unclear or ambiguous point. Get confirmation from the user.
- When the work is done, say so explicitly and point out where to start.

## Tools
- Use the latest versions of the tools. If they are missing on the local machine, ask me.
- PowerShell 7.

## MollyNVR project (facts for agents)
- The architecture is agreed in `docs/architecture.md` — read it before large changes. The original specification is `Task.md` (in `.gitignore`, contains camera passwords: never commit it and never print credentials to the chat, code or logs).
- The solution is `MollyNvr.slnx` (.slnx format, .NET 10). Dependency rule: `Domain <- Application <- Infrastructure/Api`; vendor modules depend only on `Cameras.Abstractions`; `TreatWarningsAsErrors` is enabled for Domain/Application.
- Commands: build — `dotnet build MollyNvr.slnx`; tests — `dotnet test MollyNvr.slnx`; run the API — `dotnet run --project src/MollyNvr.Api` → http://localhost:9080.
- Docker: a single container — `docker compose up -d --build` (from the root; project name `mollynvr`; network — **bridge + published ports** `9080/tcp`, `8555/tcp+udp`; host networking is **not used**: in Docker Desktop on Windows it does not forward UDP into the container and WebRTC media does not pass — verified on M1).
- Ports: UI/API — `9080`; WebRTC media — `8555/tcp+udp` (published); inside the container go2rtc uses `127.0.0.1:1984` (API) and `127.0.0.1:8554` (internal RTSP). UI (React + Vite) is in `ui/`; Docker (one container, bridge) is in `docker/`.
- Work stages are in `docs/architecture.md` section 14 (M0-M6); stack: go2rtc + ffmpeg, SQLite/EF Core, xUnit, WebRTC/WHEP.
- Camera credentials are stored only encrypted (AES-GCM, key `/data/secret.key`); for debugging use environment variables or `appsettings.Development.json`, never git.
- UI: English is the default language, Russian is available in the settings; all strings go through the localization dictionaries (i18n).
