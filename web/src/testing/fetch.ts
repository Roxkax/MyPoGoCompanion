/** What the app sent to the API, read the same way for `fetch(url, init)` and `fetch(request)`. */
export interface SentRequest {
  url: string
  method: string
  headers: Headers
  /** The JSON body, parsed; undefined when there was none. */
  body: unknown
}

export async function readRequest(
  input: RequestInfo | URL,
  init?: RequestInit,
): Promise<SentRequest> {
  const request = new Request(input, init)
  const text = await request.text()
  return {
    url: request.url,
    method: request.method,
    headers: request.headers,
    body: text ? (JSON.parse(text) as unknown) : undefined,
  }
}
