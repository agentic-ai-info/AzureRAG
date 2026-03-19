#!/usr/bin/env python3

import argparse
import json
import sys
from pathlib import Path
from typing import Iterator
from urllib import error, request


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Chunk a text file and send each chunk to the RAG embeddings API."
    )
    parser.add_argument(
        "file",
        help="Path to the file to embed.",
    )
    parser.add_argument(
        "--api-base-url",
        default="http://localhost:5001",
        help="Base URL of the RAG API. Default: http://localhost:5001",
    )
    parser.add_argument(
        "--chunk-size",
        type=int,
        default=1000,
        help="Chunk size in characters. Default: 1000",
    )
    parser.add_argument(
        "--overlap",
        type=int,
        default=200,
        help="Overlap between chunks in characters. Default: 200",
    )
    parser.add_argument(
        "--source",
        default=None,
        help="Optional source label stored in metadata. Defaults to the file path.",
    )
    return parser.parse_args()


def validate_args(args: argparse.Namespace) -> None:
    if args.chunk_size <= 0:
        raise ValueError("--chunk-size must be greater than 0")
    if args.overlap < 0:
        raise ValueError("--overlap cannot be negative")
    if args.overlap >= args.chunk_size:
        raise ValueError("--overlap must be smaller than --chunk-size")


def chunk_text(text: str, chunk_size: int, overlap: int) -> Iterator[str]:
    start = 0
    step = chunk_size - overlap

    while start < len(text):
        end = min(start + chunk_size, len(text))
        yield text[start:end]
        start += step


def post_embedding(api_base_url: str, text: str, metadata: dict) -> dict:
    payload = json.dumps({"text": text, "metadata": metadata}).encode("utf-8")
    req = request.Request(
        url=f"{api_base_url.rstrip('/')}/embeddings",
        data=payload,
        headers={"Content-Type": "application/json"},
        method="POST",
    )

    with request.urlopen(req) as response:
        body = response.read().decode("utf-8")
        return json.loads(body)


def main() -> int:
    try:
        args = parse_args()
        validate_args(args)
    except ValueError as exc:
        print(f"Argument error: {exc}", file=sys.stderr)
        return 2

    file_path = Path(args.file)
    if not file_path.is_file():
        print(f"File not found: {file_path}", file=sys.stderr)
        return 1

    text = file_path.read_text(encoding="utf-8")
    source = args.source or str(file_path)

    chunk_count = 0
    created_ids: list[int] = []

    for chunk_index, chunk in enumerate(chunk_text(text, args.chunk_size, args.overlap), start=1):
        metadata = {
            "source": source,
            "chunk": chunk_index,
            "chunk_size": args.chunk_size,
            "overlap": args.overlap,
        }

        try:
            response = post_embedding(args.api_base_url, chunk, metadata)
        except error.HTTPError as exc:
            body = exc.read().decode("utf-8", errors="replace")
            print(f"HTTP error for chunk {chunk_index}: {exc.code} {body}", file=sys.stderr)
            return 1
        except error.URLError as exc:
            print(f"Connection error for chunk {chunk_index}: {exc.reason}", file=sys.stderr)
            return 1

        chunk_count += 1
        created_id = response.get("id")
        if isinstance(created_id, int):
            created_ids.append(created_id)
        print(f"Embedded chunk {chunk_index}: id={created_id}")

    print(f"Completed embedding {chunk_count} chunk(s) from {file_path}")
    if created_ids:
        print(f"Created document ids: {', '.join(str(doc_id) for doc_id in created_ids)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())