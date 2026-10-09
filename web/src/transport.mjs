/** Downloads bounded JSON with a deadline; tokens stay in headers and redirects are rejected.
 * @param {string} url @param {string} token @param {number} maxBytes @returns {Promise<unknown>} */
export async function fetchJson(url, token = '', maxBytes = 2 * 1024 * 1024) {
  const response = await fetch(url, { signal: AbortSignal.timeout(10000), headers: token ? { Authorization: 'Bearer ' + token } : {}, credentials: 'omit', redirect: 'error', referrerPolicy: 'no-referrer' });
  if (!response.ok || !response.headers.get('content-type')?.toLowerCase().includes('application/json')) {
    await response.body?.cancel();
    if (!response.ok) throw new Error(response.status === 401 ? 'The service rejected the access token.' : `Report request failed (${response.status}).`);
    throw new Error('The service did not return a JSON report.');
  }
  if (!response.body) throw new Error('The service returned an empty report.');
  const reader = response.body.getReader(), decoder = new TextDecoder('utf-8', { fatal: true });
  const chunks = []; let bytes = 0;
  try {
    while (true) {
      const { done, value } = await reader.read();
      if (done) break;
      bytes += value.byteLength;
      if (bytes > maxBytes) throw new Error('The service report exceeds the 2 MB limit.');
      chunks.push(decoder.decode(value, { stream: true }));
    }
    chunks.push(decoder.decode());
    return JSON.parse(chunks.join(''));
  } catch (error) { await reader.cancel().catch(() => {}); throw error; }
  finally { reader.releaseLock(); }
}
