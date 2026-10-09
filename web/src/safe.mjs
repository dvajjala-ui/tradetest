/** @param {unknown} value */
export function escapeHtml(value) {
  return String(value).replace(/[&<>"']/g, char => /** @type {Record<string, string>} */ ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[char]);
}

/** @param {string} value */
export function safeUrl(value) {
  try { const url = new URL(value); return ['https:', 'http:'].includes(url.protocol) && !url.username && !url.password ? url.href : '#'; }
  catch { return '#'; }
}

/** @param {unknown} value @param {string} name @returns {Record<string, any>} */
function record(value, name) {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) throw new Error(`${name} must be an object.`);
  return /** @type {Record<string, any>} */ (value);
}
/** @param {Record<string, any>} value @param {string[]} strings @param {string[]} numbers */
function fields(value, strings = [], numbers = []) {
  for (const key of strings) if (typeof value[key] !== 'string' || value[key].length > 20000) throw new Error(`Invalid ${key}.`);
  for (const key of numbers) if (typeof value[key] !== 'number' || !Number.isFinite(value[key])) throw new Error(`Invalid ${key}.`);
}
/** @param {unknown} value @param {string} name @param {(row: Record<string, any>) => void} validate */
function rows(value, name, validate) {
  if (!Array.isArray(value) || value.length > 50000) throw new Error(`Invalid ${name} list.`);
  for (const item of value) validate(record(item, name));
}
/** @param {Record<string, any>} value */
function evaluation(value) {
  fields(value, ['evidenceNote'], ['sessions', 'noTradeSessions', 'trades', 'grossPnl', 'tradingCosts', 'netPnl', 'fixedCapitalReturnPercent']);
  if (value.winRatePercent !== null && (typeof value.winRatePercent !== 'number' || !Number.isFinite(value.winRatePercent))) throw new Error('Invalid win rate.');
}
/** @param {Record<string, any>} research */
function researchFields(research) {
  fields(research, ['asOf', 'packetHash']);
  rows(research.facts, 'facts', row => fields(row, ['factId', 'documentId', 'securityId', 'claim', 'firstKnownAt', 'verification']));
  rows(research.metrics, 'metrics', row => fields(row, ['securityId', 'kind', 'firstKnownAt', 'sourceFactId', 'verification'], ['value']));
  rows(research.documents, 'documents', row => fields(row, ['documentId', 'securityId', 'publisher', 'sourceUrl', 'publishedAt', 'firstKnownAt', 'licenceId', 'contentSha256']));
  rows(research.companies, 'companies', row => { fields(row, ['securityId', 'modelVersion'], ['score']); for (const key of ['evidenceFactIds', 'riskFlags']) if (!Array.isArray(row[key]) || row[key].some((/** @type {unknown} */ item) => typeof item !== 'string')) throw new Error(`Invalid ${key}.`); });
}
/** Validates every field used for rendering before changing the active report. @param {unknown} input @returns {import('./types.ts').Snapshot} */
export function parseSnapshot(input) {
  const value = record(input, 'Snapshot');
  fields(value, ['schemaVersion', 'generatedAt', 'dataKind', 'mode', 'evidenceNote']);
  if (value.schemaVersion !== 'tradetest-dashboard-v1' || !Number.isFinite(Date.parse(value.generatedAt))) throw new Error('Unsupported snapshot version or generation date.');
  rows(value.inputs, 'inputs', row => fields(row, ['file', 'sha256']));
  rows(value.bars, 'bars', row => fields(row, ['securityId', 'startsAt', 'endsAt'], ['open', 'high', 'low', 'close', 'volume']));
  const replay = record(value.replay, 'Replay');
  fields(replay, ['strategyVersion', 'costModelVersion'], ['initialCash', 'candidateCount', 'riskRejectedCount', 'submittedOrders', 'netPnl', 'netReturnPercent', 'maxClosedEquityDrawdownPercent']);
  rows(replay.trades, 'trades', row => { fields(row, ['securityId', 'enteredAt', 'exitedAt', 'exitReason'], ['quantity', 'entryPrice', 'exitPrice', 'grossPnl', 'netPnl']); fields(record(row.costs, 'Costs'), [], ['total']); });
  rows(replay.events, 'events', row => fields(row, ['type', 'occurredAt', 'json']));
  const study = record(value.intradayStudy, 'Study');
  fields(study, ['evidenceNote']);
  for (const key of ['training', 'validation', 'holdout', 'stressedHoldout']) evaluation(record(study[key], key));
  const walk = record(value.walkForward, 'Walk forward');
  fields(walk, ['evidenceNote']);
  for (const key of ['combinedOutOfSample', 'finalHoldout', 'stressedFinalHoldout']) evaluation(record(walk[key], key));
  rows(walk.folds, 'folds', row => { fields(row, ['trainingStartsAt', 'validationStartsAt', 'evaluationStartsAt', 'evaluationEndExclusive'], ['number']); for (const key of ['training', 'validation', 'evaluation']) evaluation(record(row[key], key)); });
  const long = record(value.longTerm, 'Long term');
  fields(long, ['strategyVersion', 'evidenceNote'], ['initialCapital', 'finalCapital', 'totalNetReturnPercent', 'benchmarkTriReturnPercent', 'maximumRebalanceDrawdownPercent']);
  if (typeof long.pointInTimeUniverseSupplied !== 'boolean') throw new Error('Invalid universe evidence flag.');
  if (long.investableBenchmark != null) fields(record(long.investableBenchmark, 'Fund benchmark'), ['name', 'evidenceNote'], ['netReturnPercent', 'tradingCostsRupees', 'observations', 'maximumObservedDrawdownPercent']);
  rows(long.periods, 'periods', row => { fields(row, ['decisionAt', 'nextDecisionAt'], ['grossReturnPercent', 'costPercent', 'netReturnPercent', 'benchmarkTriReturnPercent']); if (!Array.isArray(row.securities) || row.securities.some((/** @type {unknown} */ id) => typeof id !== 'string')) throw new Error('Invalid securities.'); });
  const research = record(value.research, 'Research');
  researchFields(research);
  const performance = record(value.performance, 'Performance');
  fields(performance, ['CheckedAtUtc', 'Method', 'Caveat']);
  const comparisons = record(performance.Comparisons, 'Comparisons');
  for (const key of ['replay', 'import']) fields(record(comparisons[key], key), [], ['MedianBeforeMs', 'MedianAfterMs', 'ElapsedReductionPercent', 'ThroughputMultiplier']);
  rows(value.gates, 'gates', row => fields(row, ['id', 'title', 'state', 'detail']));
  if (value.returnAdjustments != null) {
    const adjustments = record(value.returnAdjustments, 'Return adjustments');
    fields(adjustments, ['builderVersion', 'inputSha256', 'evidenceNote']);
    rows(adjustments.prices, 'adjusted prices', row => {
      fields(row, ['securityId', 'closeAt', 'firstKnownAt'], ['adjustedTotalReturnClose']);
      if (typeof row.isTerminal !== 'boolean' || row.terminalReason !== null && typeof row.terminalReason !== 'string' || !Array.isArray(row.sourceEvidenceIds) || row.sourceEvidenceIds.some((/** @type {unknown} */ id) => typeof id !== 'string')) throw new Error('Invalid terminal outcome or return citations.');
    });
  }
  if (value.rankingPerformance != null) {
    const ranking = record(value.rankingPerformance, 'Ranking performance');
    fields(ranking, ['Caveat'], ['MedianIndexBuildMs', 'MedianIndexedWithBuildMs']);
    fields(record(ranking.Workload, 'Ranking workload'), [], ['Companies', 'MetricRows', 'QueriesPerTrial']);
    fields(record(ranking.Comparison, 'Ranking comparison'), [], ['MedianBeforeMs', 'MedianAfterMs', 'ElapsedReductionPercent', 'ThroughputMultiplier']);
  }
  return /** @type {import('./types.ts').Snapshot} */ (value);
}

/** @param {unknown} input @returns {import('./types.ts').CompanyResearch} */
export function parseCompanyResearch(input) {
  const value = record(input, 'Company report');
  fields(value, ['schemaVersion', 'securityId', 'query', 'hash']);
  researchFields(value);
  if (value.schemaVersion !== 'tradetest-company-v1' || !value.securityId.trim() || value.securityId.length > 128 || value.query.length > 500 ||
      !/^[a-f0-9]{64}$/.test(value.hash) || !/^[a-f0-9]{64}$/.test(value.packetHash) || !Number.isFinite(Date.parse(value.asOf))) throw new Error('Invalid company report identity, date or hash.');
  const asOf = Date.parse(value.asOf);
  const facts = new Map(value.facts.map((/** @type {Record<string, any>} */ f) => [f.factId, f]));
  const documents = new Map(value.documents.map((/** @type {Record<string, any>} */ d) => [d.documentId, d]));
  if (facts.size !== value.facts.length || documents.size !== value.documents.length || value.companies.length > 1) throw new Error('Duplicate evidence or companies in the report.');
  for (const list of [value.facts, value.metrics, value.documents, value.companies]) for (const row of list) if (row.securityId !== value.securityId) throw new Error('Company evidence belongs to a different security.');
  for (const row of [...value.facts, ...value.metrics, ...value.documents]) if (!Number.isFinite(Date.parse(row.firstKnownAt)) || Date.parse(row.firstKnownAt) > asOf) throw new Error('Evidence is invalid or became known after this report date.');
  for (const doc of value.documents) if (!/^[a-f0-9]{64}$/.test(doc.contentSha256) || !Number.isFinite(Date.parse(doc.publishedAt)) || Date.parse(doc.publishedAt) > Date.parse(doc.firstKnownAt) || safeUrl(doc.sourceUrl) === '#') throw new Error('Invalid document provenance.');
  for (const fact of value.facts) {
    const doc = documents.get(fact.documentId);
    if (fact.verification !== 'Verified' || !doc || Date.parse(fact.firstKnownAt) < Date.parse(doc.firstKnownAt)) throw new Error('Fact has no valid dated source.');
  }
  for (const metric of value.metrics) {
    const fact = facts.get(metric.sourceFactId);
    if (metric.verification !== 'Verified' || !fact || Date.parse(metric.firstKnownAt) < Date.parse(fact.firstKnownAt)) throw new Error('Metric has no valid dated fact.');
  }
  for (const company of value.companies) if (company.evidenceFactIds.some((/** @type {string} */ id) => !facts.has(id))) throw new Error('Screen score cites missing facts.');
  if (!Array.isArray(value.matchingDocumentIds) || value.matchingDocumentIds.some((/** @type {unknown} */ id) => typeof id !== 'string' || !documents.has(id))) throw new Error('Invalid matching source IDs.');
  return /** @type {import('./types.ts').CompanyResearch} */ (value);
}

/** @param {string} input */
export function validateApiUrl(input) {
  const url = new URL(input);
  const local = ['localhost', '127.0.0.1', '[::1]'].includes(url.hostname);
  if ((!local && url.protocol !== 'https:') || !['https:', 'http:'].includes(url.protocol) || url.username || url.password || url.search || url.hash) throw new Error('Use an HTTPS service URL, or HTTP on localhost.');
  return url.href.replace(/\/$/, '');
}
