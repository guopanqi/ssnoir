function assetKey(pathname: string): string | null {
  let decoded: string;
  try {
    decoded = decodeURIComponent(pathname);
  } catch {
    return null;
  }

  const key = decoded.replace(/^\/+/, "") || "index.html";
  if (key.includes("\0") || key.split("/").some((part) => part === "" || part === "." || part === "..")) {
    return null;
  }
  return key;
}

function responseHeaders(object: R2Object, key: string): Headers {
  const headers = new Headers();
  object.writeHttpMetadata(headers);
  headers.set("etag", object.httpEtag);
  headers.set("accept-ranges", "bytes");
  headers.set("x-content-type-options", "nosniff");
  headers.set(
    "cache-control",
    key === "index.html"
      ? "no-store, no-cache, must-revalidate"
      : "public, max-age=31536000, immutable",
  );
  return headers;
}

function applyRangeHeaders(headers: Headers, object: R2Object): number | null {
  if (!object.range) {
    headers.set("content-length", object.size.toString());
    return null;
  }

  const range = object.range;
  let start: number;
  let length: number;
  if ("suffix" in range) {
    length = Math.min(range.suffix, object.size);
    start = object.size - length;
  } else {
    start = range.offset ?? 0;
    length = range.length ?? object.size - start;
  }
  headers.set("content-length", length.toString());
  headers.set("content-range", `bytes ${start}-${start + length - 1}/${object.size}`);
  return 206;
}

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    if (request.method !== "GET" && request.method !== "HEAD") {
      return new Response("Method not allowed", {
        status: 405,
        headers: { allow: "GET, HEAD" },
      });
    }

    const key = assetKey(new URL(request.url).pathname);
    if (!key) {
      return new Response("Not found", { status: 404 });
    }

    const object = await env.ASSETS.get(key, {
      onlyIf: request.headers,
      range: request.headers,
    });
    if (!object) {
      return new Response("Not found", { status: 404 });
    }

    const headers = responseHeaders(object, key);
    if (!("body" in object)) {
      return new Response(null, { status: 304, headers });
    }

    const rangeStatus = applyRangeHeaders(headers, object);
    return new Response(request.method === "HEAD" ? null : object.body, {
      status: rangeStatus ?? 200,
      headers,
    });
  },
} satisfies ExportedHandler<Env>;
