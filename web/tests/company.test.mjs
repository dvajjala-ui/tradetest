import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { parseCompanyResearch } from '../src/safe.mjs';
import { fetchJson } from '../src/transport.mjs';

const snapshot = JSON.parse(await readFile(new URL('../public/snapshot.json', import.meta.url), 'utf8'));
const company = { ...snapshot.research, schemaVersion: 'tradetest-company-v1', securityId: 'SYNTH-ONE', query: '', hash: 'a'.repeat(64), matchingDocumentIds: [] };

test('company imports reject mixed identities, missing sources and future evidence', () => {
  assert.equal(parseCompanyResearch(company).facts.length, 7);
  for (const mutate of [
    value => { value.facts[0].securityId = 'OTHER'; },
    value => { value.facts[0].firstKnownAt = '2027-01-01T00:00:00Z'; },
    value => { value.documents = []; },
    value => { value.metrics[0].sourceFactId = 'missing'; },
    value => { value.facts[0].verification = 'Unverified'; },
    value => { value.matchingDocumentIds = ['missing']; },
    value => { value.documents[0].sourceUrl = 'javascript:alert(1)'; }
  ]) {
    const changed = structuredClone(company); mutate(changed);
    assert.throws(() => parseCompanyResearch(changed));
  }
});

test('bounded downloads stop oversized chunked responses and keep token out of the URL', async () => {
  const original = globalThis.fetch; let cancelled = false;
  try {
    globalThis.fetch = async (url, options) => {
      assert.equal(url, 'https://research.example/api/research/SYNTH');
      assert.equal(options.headers.Authorization, 'Bearer test-token');
      assert.equal(options.redirect, 'error');
      assert.equal(options.credentials, 'omit');
      return new Response(new ReadableStream({
        start(controller) { controller.enqueue(new Uint8Array(1024)); controller.enqueue(new Uint8Array(1024)); },
        cancel() { cancelled = true; }
      }), { headers: { 'content-type': 'application/json' } });
    };
    await assert.rejects(fetchJson('https://research.example/api/research/SYNTH', 'test-token', 1500), /exceeds/);
    assert.equal(cancelled, true);
  } finally { globalThis.fetch = original; }
});

test('download decoder handles Unicode across chunks and rejects HTML sign-in pages', async () => {
  const original = globalThis.fetch;
  try {
    const bytes = new TextEncoder().encode('{"label":"₹ investment"}');
    globalThis.fetch = async () => new Response(new ReadableStream({ start(controller) {
      for (const byte of bytes) controller.enqueue(new Uint8Array([byte]));
      controller.close();
    }}), { headers: { 'content-type': 'application/json; charset=utf-8' } });
    assert.deepEqual(await fetchJson('/report.json'), { label: '₹ investment' });
    globalThis.fetch = async () => new Response('<html>Sign in</html>', { headers: { 'content-type': 'text/html' } });
    await assert.rejects(fetchJson('/report.json'), /JSON report/);
  } finally { globalThis.fetch = original; }
});
