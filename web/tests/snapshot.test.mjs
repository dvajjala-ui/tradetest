import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { escapeHtml, safeUrl, parseSnapshot, validateApiUrl } from '../src/safe.mjs';

const fixture = JSON.parse(await readFile(new URL('../public/snapshot.json', import.meta.url), 'utf8'));
test('C# snapshot renders under the supported schema', () => {
  assert.equal(parseSnapshot(fixture).dataKind, 'Synthetic');
  assert.ok(fixture.replay.trades.length > 0);
});
test('partial, incompatible and nonfinite reports are rejected before replacement', () => {
  for (const report of [{}, { ...fixture, schemaVersion: 'v0' }, { ...fixture, research: { ...fixture.research, facts: [null] } }, { ...fixture, replay: { ...fixture.replay, netPnl: Infinity } }]) assert.throws(() => parseSnapshot(report));
});
test('document content and links cannot inject markup or executable schemes', () => {
  assert.equal(escapeHtml('<img src=x onerror="alert(1)">'), '&lt;img src=x onerror=&quot;alert(1)&quot;&gt;');
  assert.equal(safeUrl('javascript:alert(1)'), '#');
  assert.equal(safeUrl('https://name:secret@example.com'), '#');
  assert.equal(safeUrl('https://example.com/report'), 'https://example.com/report');
});
test('remote API URLs require HTTPS and do not carry credentials or query tokens', () => {
  assert.equal(validateApiUrl('http://127.0.0.1:5080/'), 'http://127.0.0.1:5080');
  assert.throws(() => validateApiUrl('http://public.example'));
  assert.throws(() => validateApiUrl('https://service.example?token=secret'));
  assert.throws(() => validateApiUrl('https://name:secret@service.example'));
});
test('recorded AI imports cannot mislabel mode, override rules, or change totals without matching cases', () => {
  assert.equal(parseSnapshot(fixture).aiEvaluation.continuedCount, 2);
  for (const mutate of [
    value => { value.aiEvaluation.mode = 'LIVE'; },
    value => { value.aiEvaluation.knownRecordedChargeUsd = -1; },
    value => { value.aiEvaluation.continuedCount = 9; },
    value => { value.aiEvaluation.cases[0].review.assessment.modelId = 'other-model'; },
    value => { value.aiEvaluation.cases[0].rulesEligible = false; },
    value => { value.aiEvaluation.cases[4].review.allowsFurtherReview = true; },
    value => { value.aiEvaluation.cases[0].recordedFilterCandidateNetPnlRupees = 999; },
    value => { value.aiEvaluation.cases[0].review.usage = null; },
    value => { value.aiEvaluation.cases[0].review.assessment.decision = 'Buy'; }
  ]) {
    const changed = structuredClone(fixture); mutate(changed);
    assert.throws(() => parseSnapshot(changed));
  }
});
