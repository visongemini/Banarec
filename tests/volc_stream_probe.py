"""One-shot manual probe for Volcengine bidirectional streaming ASR.

This script never prints credentials or request headers. It reads the two legacy
console credentials from the explicitly configured env file, streams an existing
16 kHz PCM WAV in 200 ms chunks, and prints only a redacted connection outcome,
service code, and transcript.
"""

from __future__ import annotations

import argparse
import asyncio
import gzip
import json
import re
import struct
import uuid
import wave
from pathlib import Path

import websockets
from websockets.exceptions import InvalidStatus


ENDPOINT = "wss://openspeech.bytedance.com/api/v3/sauc/bigmodel"
RESOURCES = ("volc.seedasr.sauc.duration", "volc.bigasr.sauc.duration")


def load_credentials(path: Path) -> tuple[str, str]:
    values: dict[str, str] = {}
    for line in path.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        key, value = line.split("=", 1)
        if key.strip() in {"VOLC_ASR_APP_ID", "VOLC_ASR_ACCESS_TOKEN"}:
            values[key.strip()] = value.strip().strip('"').strip("'")
    try:
        app_id = values["VOLC_ASR_APP_ID"]
        token = values["VOLC_ASR_ACCESS_TOKEN"]
    except KeyError as exc:
        raise RuntimeError("required ASR credential is missing") from exc
    if not app_id or not token:
        raise RuntimeError("required ASR credential is blank")
    return app_id, token


def frame(message_type: int, flags: int, serialization: int, compression: int, body: bytes) -> bytes:
    header = bytes((0x11, (message_type << 4) | flags, (serialization << 4) | compression, 0))
    payload = gzip.compress(body) if compression == 1 else body
    return header + struct.pack(">I", len(payload)) + payload


def parse_frame(data: bytes) -> tuple[int, int | None, dict]:
    if len(data) < 4:
        return -1, None, {}
    header_size = (data[0] & 0x0F) * 4
    message_type = data[1] >> 4
    flags = data[1] & 0x0F
    compression = data[2] & 0x0F
    offset = header_size
    if message_type == 0xF:
        if len(data) < offset + 8:
            return message_type, None, {}
        code = struct.unpack_from(">I", data, offset)[0]
        size = struct.unpack_from(">I", data, offset + 4)[0]
        raw = data[offset + 8 : offset + 8 + size]
        return message_type, code, decode_payload(raw, compression)
    if flags & 1:
        if len(data) < offset + 4:
            return message_type, None, {}
        offset += 4
    if len(data) < offset + 4:
        return message_type, None, {}
    size = struct.unpack_from(">I", data, offset)[0]
    raw = data[offset + 4 : offset + 4 + size]
    return message_type, 0, decode_payload(raw, compression)


def decode_payload(raw: bytes, compression: int) -> dict:
    try:
        if compression == 1:
            raw = gzip.decompress(raw)
        value = json.loads(raw.decode("utf-8")) if raw else {}
        return value if isinstance(value, dict) else {"value": value}
    except Exception:
        return {}


def transcript(payload: dict) -> str:
    result = payload.get("result") or {}
    if isinstance(result, list):
        result = result[-1] if result else {}
    if not isinstance(result, dict):
        return ""
    text = str(result.get("text") or "").strip()
    if text:
        return text
    utterances = result.get("utterances") or []
    return "".join(str(item.get("text") or "") for item in utterances if isinstance(item, dict)).strip()


def permission_mismatch(ws_status: str, code: int | str | None) -> bool:
    if ws_status in {"http-400", "http-401", "http-403"}:
        return True
    return code is not None and code != 0


def redact(value: object, app_id: str, token: str) -> str:
    text = str(value or "")
    for secret in (app_id, token):
        if secret:
            text = text.replace(secret, "[redacted]")
    return re.sub(r"(?i)(app[_ -]?id|access[_ -]?token)\s*[:=]\s*\S+", r"\1=[redacted]", text)[:500]


def response_error(response: object, app_id: str, token: str) -> tuple[str | None, str]:
    headers = getattr(response, "headers", {}) or {}
    service_code = headers.get("X-Api-Status-Code")
    message = headers.get("X-Api-Message") or ""
    body = getattr(response, "body", b"") or b""
    if isinstance(body, bytes):
        body = body.decode("utf-8", errors="replace")
    try:
        payload = json.loads(body) if body else {}
        if isinstance(payload, dict):
            service_code = service_code or payload.get("code") or payload.get("error_code")
            message = message or payload.get("message") or payload.get("msg") or payload.get("error") or ""
    except Exception:
        message = message or body
    return service_code, redact(message, app_id, token)


async def probe(resource: str, pcm: bytes, app_id: str, token: str) -> tuple[str, int | str | None, str, str]:
    headers = {
        "X-Api-App-Key": app_id,
        "X-Api-Access-Key": token,
        "X-Api-Resource-Id": resource,
        "X-Api-Connect-Id": str(uuid.uuid4()),
    }
    try:
        async with websockets.connect(
            ENDPOINT,
            additional_headers=headers,
            open_timeout=15,
            close_timeout=5,
            max_size=4 * 1024 * 1024,
        ) as socket:
            request = {
                "user": {"uid": "BanaStudio-probe"},
                "audio": {"format": "pcm", "codec": "raw", "rate": 16000, "bits": 16, "channel": 1},
                "request": {
                    "model_name": "bigmodel",
                    "enable_itn": True,
                    "enable_punc": True,
                    "enable_ddc": True,
                    "show_utterances": True,
                    "enable_nonstream": False,
                    "corpus": {
                        "context": json.dumps(
                            {"hotwords": [{"word": "琵琶雅礼"}, {"word": "睡蕉"}]},
                            ensure_ascii=False,
                        )
                    },
                },
            }
            await socket.send(frame(0x1, 0, 1, 1, json.dumps(request, ensure_ascii=False).encode("utf-8")))
            latest = ""
            last_code: int | None = None
            done = asyncio.Event()

            async def receive() -> None:
                nonlocal latest, last_code
                try:
                    async for message in socket:
                        if not isinstance(message, bytes):
                            continue
                        message_type, code, payload = parse_frame(message)
                        last_code = code
                        value = transcript(payload)
                        if value:
                            latest = value
                        if message_type == 0xF or (message[1] & 0x0F) in (2, 3):
                            done.set()
                            return
                finally:
                    done.set()

            receiver = asyncio.create_task(receive())
            chunk_size = 16000 * 2 * 200 // 1000
            chunks = [pcm[index : index + chunk_size] for index in range(0, len(pcm), chunk_size)]
            for index, chunk in enumerate(chunks):
                final = index == len(chunks) - 1
                await socket.send(frame(0x2, 2 if final else 0, 0, 1, chunk))
                if not final:
                    await asyncio.sleep(0.2)
            try:
                await asyncio.wait_for(done.wait(), timeout=15)
            except asyncio.TimeoutError:
                pass
            if not receiver.done():
                receiver.cancel()
            await asyncio.gather(receiver, return_exceptions=True)
            return "connected", last_code, latest, ""
    except InvalidStatus as exc:
        response = getattr(exc, "response", None)
        status = getattr(response, "status_code", None)
        service_code, message = response_error(response, app_id, token)
        return "http-" + str(status or "error"), service_code, "", message
    except Exception as exc:
        return "connect-error", None, "", redact(exc, app_id, token)


async def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("wav", type=Path)
    parser.add_argument("--env-file", type=Path, required=True)
    args = parser.parse_args()
    app_id, token = load_credentials(args.env_file)
    with wave.open(str(args.wav), "rb") as source:
        if (source.getnchannels(), source.getsampwidth(), source.getframerate()) != (1, 2, 16000):
            raise RuntimeError("probe WAV must be mono 16-bit 16 kHz PCM")
        pcm = source.readframes(source.getnframes())
    for index, resource in enumerate(RESOURCES):
        ws_status, code, text, message = await probe(resource, pcm, app_id, token)
        print(f"resource={resource} ws={ws_status} code={code if code is not None else 'none'} message={message or 'none'} text={text}")
        if index == 0 and permission_mismatch(ws_status, code):
            continue
        return 0 if ws_status == "connected" and code == 0 and bool(text) else 1
    return 1


if __name__ == "__main__":
    raise SystemExit(asyncio.run(main()))
