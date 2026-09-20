# BanaStudio ASR integration notes

Date: 2026-09-20

## Account and credential handling

Testing used an authorized local ASR fixture outside the repository.
Its README identifies the existing account as the legacy speech-console application, using `VOLC_ASR_APP_ID` and `VOLC_ASR_ACCESS_TOKEN`.
Credentials are read from an explicitly selected local configuration file. Do not copy it into the repository, logs, test output, or distributable files.
Application credential storage must use Windows DPAPI for the current user.

## Verified service behavior

- The existing account can use the flash file-recognition endpoint with resource `volc.bigasr.auc_turbo`.
- A short fixed test fixture was recognized successfully through the production C# `FlashAsrClient`, returning service status `20000000` and `琵琶雅礼今天卖了3单香氛礼盒。` when the proper noun was included as a hotword.
- The same fixed sample without the hotword returned `琵琶牙里今天卖了3单香氛礼盒。` through the reference client. This is an observed sample result, not an accuracy guarantee.
- The bidirectional WebSocket handshake with `volc.seedasr.sauc.duration` returned HTTP 400: resource not allowed.
- The handshake with `volc.bigasr.sauc.duration` returned HTTP 403: requested resource not granted.
- These failures occurred before audio frames were sent. Streaming account entitlement has not been granted for the tested resources; streaming recognition cannot be reported as live-tested successfully.

## Product behavior

The first usable configuration uses **极速识别（说完出字）**. The app also exposes **实时流式** as a separate selection. Do not silently fall back between the two.
Both support press-to-toggle and press-and-hold interaction, final transcript delivery, clipboard retention, and a local lexicon.
Flash mode produces text after recording ends. Streaming mode can show interim text only after the account has been enabled for a suitable streaming resource.

## Authoritative references

- Current bidirectional API: https://docs.volcengine.com/docs/DoubaoVoice/bidirectional-streaming-automatic-speech-recognition-websocket?lang=zh
- Legacy detailed framing documentation: https://www.volcengine.com/docs/6561/1354869
- Microsoft waveIn callback restrictions: https://learn.microsoft.com/en-us/previous-versions/dd743849(v=vs.85)
- Microsoft waveInReset buffer-return behavior: https://learn.microsoft.com/en-us/windows/win32/api/mmeapi/nf-mmeapi-waveinreset

The implementation was checked against the complete official API pages. Reference caches remain outside the source repository.

## Verification scope

The live production Flash client test passed. The voice logic suite passed 31 cases. Native desktop checks passed actual TextBox insertion with clipboard retention, rejection after focus changes, cancellation, microphone capture, normal stop/drain and cancel. These component checks do not substitute for live streaming verification, which remains blocked by account entitlement.

See `VERIFICATION.md` for final build, UI, regression and package results, including what has not been tested. The existing installed Banarec process is preserved; creating a new installer does not mean it has been installed or published.
