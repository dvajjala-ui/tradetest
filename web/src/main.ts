import './style.css';
import { escapeHtml as e, parseSnapshot, parseCompanyResearch, safeUrl, validateApiUrl } from './safe.mjs';
import { fetchJson } from './transport.mjs';
import type { Snapshot, Bar, Evaluation, Benchmark, Research, CompanyResearch } from './types';

const app = document.querySelector<HTMLDivElement>('#app')!;
const views = ['overview', 'research', 'replay', 'studies', 'performance', 'deployment'] as const;
type View = typeof views[number];
const labels: Record<View, string> = { overview: 'Overview', research: 'Company research', replay: 'Intraday replay', studies: 'Strategy studies', performance: 'Performance', deployment: 'Deployment' };
let snapshot: Snapshot;
let reportSource = 'Bundled example';
let apiBase = '';
let apiDraft = '';
let apiToken = '';
let isLoading = false;
let loadVersion = 0;
let companyReport: CompanyResearch | null = null;
let companySource = '';
let researchRequest = { securityId: '', asOf: '', query: '' };
const activeResearch = (): Research => companyReport ?? snapshot.research;
let query = '';
let factsPage = 0;
const factsPageSize = 50;
let chosenSecurity = '';
let flash = '';
let flashError = false;

const icons: Record<string, string> = {
  overview: '<rect x="3" y="3" width="7" height="7" rx="1.5"/><rect x="14" y="3" width="7" height="7" rx="1.5"/><rect x="3" y="14" width="7" height="7" rx="1.5"/><rect x="14" y="14" width="7" height="7" rx="1.5"/>',
  research: '<path d="M4 19.5A2.5 2.5 0 0 1 6.5 17H20M6.5 3H20v19H6.5A2.5 2.5 0 0 1 4 19.5v-14A2.5 2.5 0 0 1 6.5 3Z"/><path d="M8 7h8M8 11h6"/>',
  replay: '<path d="M5 4v16M3 8h4M3 14h4M12 3v18M10 6h4M10 12h4M19 4v16M17 12h4M17 17h4"/>',
  studies: '<path d="M4 20h16M6 16V9M12 16V4M18 16v-5"/>',
  performance: '<path d="M4 17a9 9 0 1 1 16 0M12 13l5-5"/><circle cx="12" cy="13" r="1.5"/>',
  deployment: '<rect x="3" y="3" width="18" height="7" rx="2"/><rect x="3" y="14" width="18" height="7" rx="2"/><path d="M7 6.5h.01M7 17.5h.01M12 6.5h5M12 17.5h5"/>',
  arrow: '<path d="M7 17 17 7M7 7h10v10"/>',
  download: '<path d="M12 3v12m-5-5 5 5 5-5M4 17v4h16v-4"/>',
  refresh: '<path d="M20 7v5h-5M4 17v-5h5M6 7a7 7 0 0 1 12-1l2 2M4 16l2 2a7 7 0 0 0 12-1"/>',
  shield: '<path d="m12 3 8 3v6c0 5-8 9-8 9s-8-4-8-9V6Z"/><path d="m8 12 3 3 5-6"/>',
  search: '<circle cx="10.5" cy="10.5" r="6.5"/><path d="m16 16 5 5"/>'
};
const icon = (name: string) => `<svg viewBox="0 0 24 24" aria-hidden="true" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round">${icons[name] ?? icons.arrow}</svg>`;
const number = (value: number, digits = 2) => new Intl.NumberFormat('en-IN', { maximumFractionDigits: digits, minimumFractionDigits: digits }).format(value);
const rupees = (value: number) => `₹${number(value)}`;
const percent = (value: number) => `${value > 0 ? '+' : ''}${number(value)}%`;
const date = (value: string) => { const d = new Date(value); return Number.isFinite(d.valueOf()) ? new Intl.DateTimeFormat('en-IN', { day: 'numeric', month: 'short', year: 'numeric', timeZone: 'Asia/Kolkata' }).format(d) : value; };
const time = (value: string) => { const d = new Date(value); return Number.isFinite(d.valueOf()) ? new Intl.DateTimeFormat('en-IN', { hour: '2-digit', minute: '2-digit', hourCycle: 'h23', timeZone: 'Asia/Kolkata' }).format(d) : value; };
const tone = (value: number) => value < 0 ? 'negative' : 'positive';
const view = (): View => views.includes(location.hash.slice(1) as View) ? location.hash.slice(1) as View : 'overview';
const tag = (text: string, variant = '') => `<span class="tag ${variant}">${e(text)}</span>`;
const cardHead = (title: string, subtitle = '', action = '') => `<div class="card-head"><div><h2>${e(title)}</h2>${subtitle ? `<p>${e(subtitle)}</p>` : ''}</div>${action}</div>`;
const empty = (text: string) => `<div class="empty">${e(text)}</div>`;
const stat = (label: string, value: string, detail: string, valueClass = '') => `<article class="stat"><span class="eyebrow">${e(label)}</span><strong class="${valueClass}">${e(value)}</strong><p>${e(detail)}</p></article>`;

function priceChart(bars: Bar[]): string {
  if (!bars.length) return empty('No bars in this snapshot.');
  const lo = Math.min(...bars.map(b => b.low)), hi = Math.max(...bars.map(b => b.high));
  const range = Math.max(hi - lo, 0.01);
  const y = (price: number) => 205 - (price - lo) / range * 160;
  const x = (index: number) => 80 + index * 540 / Math.max(bars.length - 1, 1);
  const line = bars.map((b, i) => `${x(i)},${y(b.close)}`).join(' ');
  return `<svg class="chart" viewBox="0 0 700 260" role="img" aria-label="Synthetic five-minute closing prices, ${e(number(lo))} to ${e(number(hi))} rupees">
    ${[0, 1, 2, 3].map(i => { const price = hi - i * range / 3; return `<line x1="55" x2="650" y1="${y(price)}" y2="${y(price)}" class="grid-line"/><text x="12" y="${y(price) + 4}" class="chart-label">${e(number(price))}</text>`; }).join('')}
    <polygon points="80,220 ${line} ${x(bars.length - 1)},220" class="chart-area"/><polyline points="${line}" class="chart-line"/>
    ${bars.map((b, i) => `<line x1="${x(i)}" x2="${x(i)}" y1="${y(b.high)}" y2="${y(b.low)}" class="range-line"/><circle cx="${x(i)}" cy="${y(b.close)}" r="4" class="chart-dot"><title>${e(time(b.startsAt))} · close ${e(rupees(b.close))}, volume ${e(number(b.volume, 0))}</title></circle><text x="${x(i)}" y="247" text-anchor="middle" class="chart-label">${e(time(b.startsAt))}</text>`).join('')}
  </svg><div class="chart-legend"><span><i class="dot"></i>Close</span><span><i class="line-swatch"></i>High / low range</span><span>Asia/Kolkata · 5 minute bars</span></div>`;
}

function overview(): string {
  const { replay: r, research, walkForward: walk } = snapshot;
  return `<div class="intro"><div><span class="eyebrow">INDIA · TWO RESEARCH LANES</span><h1>Your research, in focus.</h1><p>Dated evidence. Deterministic decisions. Measured results.</p></div><a class="button" href="#research">Explore company evidence ${icon('arrow')}</a></div>
    <div class="stats-grid">${stat('Replay net P&L', rupees(r.netPnl), `${r.trades.length} synthetic trade · costs included`, tone(r.netPnl))}${stat('Evidence in packet', number(research.facts.length, 0), `${research.documents.length} source document · as of ${date(research.asOf)}`)}${stat('Evaluation sessions', number(walk.combinedOutOfSample.sessions, 0), `${walk.folds.length} walk-forward folds · final holdout separate`)}${stat('Execution mode', 'PAPER', 'Offline fixtures · broker disconnected')}</div>
    <div class="main-grid"><section class="card">${cardHead('Intraday replay', `${snapshot.bars[0]?.securityId ?? 'No instrument'} · ${snapshot.bars[0] ? date(snapshot.bars[0].startsAt) : 'No date'}`, '<a class="text-link" href="#replay">View session →</a>')}${priceChart(snapshot.bars)}<div class="card-bottom"><div><span class="eyebrow">Starting cash</span><strong>${e(rupees(r.initialCash))}</strong></div><div><span class="eyebrow">Risk rejected</span><strong>${r.riskRejectedCount}</strong></div><div><span class="eyebrow">Orders simulated</span><strong>${r.submittedOrders}</strong></div></div></section>
    <section class="card">${cardHead('Research readiness', 'Engineering progress and evidence still needed')}
      <div class="gates-compact">${snapshot.gates.slice(0, 5).map(g => `<div class="gate-row"><span class="gate-id">${e(g.id.toUpperCase())}</span><div><strong>${e(g.title)}</strong><span>${e(g.state)}</span></div><i class="status-dot ${g.state === 'Implemented' ? 'ready' : 'pending'}"></i></div>`).join('')}</div><a class="card-link" href="#deployment">Review deployment and next gates ${icon('arrow')}</a></section></div>
    <div class="two-grid"><section class="card">${cardHead('Company watchlist', 'A reproducible screen; scores are not expected returns', '<a class="text-link" href="#research">Open research →</a>')}<div class="table-wrap"><table><thead><tr><th>Company</th><th>Screen score</th><th>Evidence</th><th>Status</th></tr></thead><tbody>${research.companies.map(c => `<tr><td><strong>${e(c.securityId)}</strong><span class="cell-detail">Synthetic company</span></td><td>${e(number(c.score))}</td><td>${c.evidenceFactIds.length} facts</td><td>${tag(c.riskFlags.length ? 'Review flags' : 'Complete packet', c.riskFlags.length ? 'amber' : 'green')}</td></tr>`).join('')}</tbody></table></div></section>
    <section class="callout">${icon('shield')}<span class="eyebrow">GROUND RULE</span><h2>Evidence before activation.</h2><p>The rules and risk engine own the numeric decisions. AI assessments will need to earn their place through an offline evaluation.</p><a class="text-link" href="#studies">Inspect the baseline studies →</a></section></div>`;
}

const metricNames: Record<string, string> = { RevenueGrowth3YPercent: 'Revenue growth · 3 years', ReturnOnCapitalPercent: 'Return on capital', FreeCashFlowMarginPercent: 'Free cash flow margin', NetDebtToEbitda: 'Net debt / EBITDA', ShareDilution3YPercent: 'Share dilution · 3 years', Momentum12MPercent: 'Momentum · 12 months', AverageDailyTurnoverRupees: 'Average daily turnover' };

function researchView(): string {
  const research = activeResearch();
  const ids = [...new Set([...(companyReport ? [companyReport.securityId] : []), ...research.companies.map(c => c.securityId), ...research.facts.map(f => f.securityId)])];
  if (!ids.includes(chosenSecurity)) chosenSecurity = ids[0] ?? '';
  const company = research.companies.find(c => c.securityId === chosenSecurity);
  const metrics = research.metrics.filter(m => m.securityId === chosenSecurity);
  return `<div class="page-title"><div><span class="eyebrow">LONG-TERM RESEARCH</span><h1>Company evidence</h1><p>Visible as of ${e(date(research.asOf))}. Source dates stay attached to each claim.</p></div>${tag(companyReport ? 'User-supplied research' : 'Synthetic packet', companyReport ? 'amber' : 'green')}</div>
    ${researchControls()}
    <div class="research-toolbar"><label>Company <select id="company-select">${ids.map(id => `<option value="${e(id)}" ${id === chosenSecurity ? 'selected' : ''}>${e(id)}</option>`).join('')}</select></label><label class="search-box">${icon('search')}<input id="fact-search" type="search" value="${e(query)}" placeholder="Search claims or source IDs" aria-label="Search evidence" /></label></div>
    <div class="research-heading"><div class="company-avatar">${e(chosenSecurity.slice(0, 1) || '?')}</div><div><h2>${e(chosenSecurity || 'No company')}</h2><p>${companyReport ? 'User-supplied evidence' : 'Synthetic company'} · screen ${e(company?.modelVersion ?? 'not available')}</p></div><div class="score"><span class="eyebrow">SCREEN SCORE</span><strong>${company ? e(number(company.score)) : '—'}</strong></div></div>
    ${company?.riskFlags.length ? `<div class="notice">Review flags: ${company.riskFlags.map(e).join(', ')}</div>` : ''}
    <div class="metric-grid">${metrics.map(m => `<article class="metric"><span>${e(metricNames[m.kind] ?? m.kind)}</span><strong>${e(m.kind === 'AverageDailyTurnoverRupees' ? rupees(m.value) : m.kind === 'NetDebtToEbitda' ? number(m.value) + '×' : number(m.value) + '%')}</strong><a href="#fact-${encodeURIComponent(m.sourceFactId)}" data-fact="${e(m.sourceFactId)}">${e(m.sourceFactId)} ↗</a></article>`).join('')}</div>
    <section class="card">${cardHead('Claims and provenance', companyReport ? 'Verification labels come from the imported research; source review remains necessary.' : 'Verification labels here apply only to the synthetic fixture.')}<div id="fact-results">${factResults()}</div></section>
    <section class="card section-space">${cardHead('Source documents', `${research.documents.filter(d => d.securityId === chosenSecurity).length} documents · external links open separately`)}<div class="document-list">${research.documents.filter(d => d.securityId === chosenSecurity).map(d => `<article class="document"><div>${icon('research')}<div><h3>${e(d.publisher)}</h3><p>${e(d.documentId)} · published ${e(date(d.publishedAt))} · first known ${e(date(d.firstKnownAt))}</p></div><a class="button small" href="${e(safeUrl(d.sourceUrl))}" target="_blank" rel="noopener noreferrer">Source ${icon('arrow')}</a></div><dl><dt>Licence</dt><dd>${e(d.licenceId)}</dd><dt>SHA-256</dt><dd class="hash">${e(d.contentSha256)}</dd></dl></article>`).join('')}</div></section><p class="footnote">Packet hash <span class="hash">${e(research.packetHash)}</span>. A score is a screening result, not an estimate of future return.</p>`;
}

function researchControls(): string {
  const request = { securityId: researchRequest.securityId || chosenSecurity, asOf: researchRequest.asOf || activeResearch().asOf, query: researchRequest.query };
  return `<section class="card research-controls">${cardHead('Open dated company research', companyReport ? companySource : 'Use a local export or query your connected service.')}<div class="padded"><div class="button-row"><label class="button secondary">Open company report<input id="company-file" type="file" accept=".json,application/json" hidden /></label>${companyReport ? '<button class="button secondary" id="export-company">Export company report</button><button class="button secondary" id="restore-company">Restore example evidence</button>' : ''}</div>${apiBase ? `<form id="company-form" class="company-form"><label>Security ID<input name="securityId" maxlength="128" required value="${e(request.securityId)}" autocomplete="off" /></label><label>As of · ISO timestamp with time zone<input name="asOf" required value="${e(request.asOf)}" autocomplete="off" /></label><label>Find source documents<input name="query" maxlength="500" value="${e(request.query)}" placeholder="Revenue, debt, governance…" /></label><button class="button" type="submit" ${isLoading ? 'disabled' : ''}>Load company evidence</button></form>` : '<p class="footnote">Company files stay in browser memory. <a class="text-link" href="#deployment">Connect a read-only service →</a></p>'}${companyReport ? `<p class="footnote">Report hash <span class="hash">${e(companyReport.hash)}</span> · ${companyReport.matchingDocumentIds.length} sources matched the document query.</p>` : ''}</div></section>`;
}

function matchingFacts() {
  return activeResearch().facts.filter(f => f.securityId === chosenSecurity && `${f.claim} ${f.factId} ${f.documentId}`.toLowerCase().includes(query.toLowerCase()));
}
function factResults(): string {
  const facts = matchingFacts();
  if (!facts.length) return empty('No claims match this search.');
  const pages = Math.ceil(facts.length / factsPageSize); factsPage = Math.min(factsPage, pages - 1);
  const start = factsPage * factsPageSize, visible = facts.slice(start, start + factsPageSize);
  return `<div class="table-wrap"><table><thead><tr><th>Claim</th><th>First known</th><th>Source</th><th>Verification</th></tr></thead><tbody>${visible.map(f => `<tr id="fact-${e(f.factId)}"><td><strong class="claim">${e(f.claim)}</strong><span class="cell-detail">${e(f.factId)}</span></td><td class="nowrap">${e(date(f.firstKnownAt))}</td><td>${e(f.documentId)}</td><td>${tag(f.verification, f.verification === 'Verified' ? 'green' : 'amber')}</td></tr>`).join('')}</tbody></table></div><div class="table-pagination"><span>Showing ${e(number(start + 1, 0))}–${e(number(Math.min(start + factsPageSize, facts.length), 0))} of ${e(number(facts.length, 0))} claims</span>${pages > 1 ? `<div><button class="button small secondary" data-fact-page="-1" ${factsPage === 0 ? 'disabled' : ''}>Previous</button><span>Page ${factsPage + 1} of ${pages}</span><button class="button small secondary" data-fact-page="1" ${factsPage === pages - 1 ? 'disabled' : ''}>Next</button></div>` : ''}</div>`;
}

function replayView(): string {
  const r = snapshot.replay;
  return `<div class="page-title"><div><span class="eyebrow">DETERMINISTIC SIMULATION</span><h1>Intraday replay</h1><p>${e(r.strategyVersion)} · ${e(r.costModelVersion)} · fees, spread and slippage included</p></div>${tag('Offline / PAPER', 'green')}</div><div class="stats-grid">${stat('Gross P&L', rupees(r.trades.reduce((s, t) => s + t.grossPnl, 0)), 'Before statutory fees and brokerage')}${stat('Trading costs', rupees(r.trades.reduce((s, t) => s + t.costs.total, 0)), 'Spread and slippage are embedded in fills')}${stat('Net return', percent(r.netReturnPercent), `On ${rupees(r.initialCash)} starting cash`, tone(r.netReturnPercent))}${stat('Closed-equity drawdown', number(r.maxClosedEquityDrawdownPercent) + '%', 'Measured at closed trades only')}</div>
    <section class="card">${cardHead('Completed five-minute bars', 'Fills begin on a following bar; no same-bar signal fill.')}${priceChart(snapshot.bars)}</section>
    <section class="card section-space">${cardHead('Simulated trades', `${r.candidateCount} candidate · ${r.riskRejectedCount} risk rejected · ${r.submittedOrders} order intent`)}<div class="table-wrap"><table><thead><tr><th>Security / quantity</th><th>Entry</th><th>Exit</th><th>Exit rule</th><th>Costs</th><th>Net P&L</th></tr></thead><tbody>${r.trades.map(t => `<tr><td><strong>${e(t.securityId)}</strong><span class="cell-detail">${t.quantity} shares</span></td><td>${e(rupees(t.entryPrice))}<span class="cell-detail">${e(time(t.enteredAt))} IST</span></td><td>${e(rupees(t.exitPrice))}<span class="cell-detail">${e(time(t.exitedAt))} IST</span></td><td>${tag(t.exitReason)}</td><td>${e(rupees(t.costs.total))}</td><td class="${tone(t.netPnl)}">${e(rupees(t.netPnl))}</td></tr>`).join('')}</tbody></table></div>${!r.trades.length ? empty('No simulated trades for this session.') : ''}</section>
    <section class="card section-space">${cardHead('Decision timeline', 'Each candidate, risk decision, intent and fill is inspectable.')}<div class="timeline">${r.events.map((event, i) => `<details class="event"><summary><span class="event-number">${i + 1}</span><span><strong>${e(event.type.replaceAll('_', ' '))}</strong><small>${e(time(event.occurredAt))} IST</small></span><span class="event-expand">Inspect +</span></summary><pre>${e(formatEvent(event.json))}</pre></details>`).join('')}</div></section>`;
}
function formatEvent(value: string): string { try { return JSON.stringify(JSON.parse(value), null, 2); } catch { return value; } }
function evaluationRow(name: string, value: Evaluation): string { return `<tr><td><strong>${e(name)}</strong></td><td>${value.sessions}</td><td>${value.noTradeSessions}</td><td>${value.trades}</td><td>${e(rupees(value.tradingCosts))}</td><td class="${tone(value.netPnl)}">${e(rupees(value.netPnl))}</td><td>${e(percent(value.fixedCapitalReturnPercent))}</td></tr>`; }
function studiesView(): string {
  const { intradayStudy: s, walkForward: w, longTerm: l } = snapshot;
  const fund = l.investableBenchmark, adjustments = snapshot.returnAdjustments;
  return `<div class="page-title"><div><span class="eyebrow">FROZEN BASELINE · EXPLORATORY</span><h1>Strategy studies</h1><p>Chronological partitions, cost sensitivity and a separate final holdout.</p></div>${tag('Synthetic samples', 'amber')}</div>
    <section class="card">${cardHead('Intraday partitions', 'Fixed starting capital per session; no compounding.')}<div class="table-wrap"><table><thead><tr><th>Partition</th><th>Sessions</th><th>No trade</th><th>Trades</th><th>Costs</th><th>Net P&L</th><th>Return</th></tr></thead><tbody>${evaluationRow('Training', s.training)}${evaluationRow('Validation', s.validation)}${evaluationRow('Holdout', s.holdout)}${evaluationRow('Holdout · cost stress', s.stressedHoldout)}</tbody></table></div><p class="card-note">${e(s.evidenceNote)}</p></section>
    <section class="card section-space">${cardHead('Walk-forward evaluation', `${w.folds.length} expanding training windows · evaluation dates do not overlap`)}<div class="table-wrap"><table><thead><tr><th>Fold</th><th>Training sessions</th><th>Validation starts</th><th>Evaluation starts</th><th>Evaluation ends</th><th>Net P&L</th></tr></thead><tbody>${w.folds.map(f => `<tr><td>${f.number}</td><td>${f.training.sessions}</td><td>${e(date(f.validationStartsAt))}</td><td>${e(date(f.evaluationStartsAt))}</td><td>${e(date(f.evaluationEndExclusive))} exclusive</td><td class="${tone(f.evaluation.netPnl)}">${e(rupees(f.evaluation.netPnl))}</td></tr>`).join('')}</tbody></table></div><div class="three-grid padded">${stat('Combined evaluation', rupees(w.combinedOutOfSample.netPnl), `${w.combinedOutOfSample.sessions} sessions · ${w.combinedOutOfSample.trades} trades`)}${stat('Final holdout', rupees(w.finalHoldout.netPnl), `${w.finalHoldout.sessions} sessions · excluded from folds`)}${stat('Stressed final holdout', rupees(w.stressedFinalHoldout.netPnl), 'Wider spread and slippage assumptions')}</div><p class="card-note">${e(w.evidenceNote)}</p></section>
    <section class="card section-space">${cardHead('Long-term portfolio arithmetic', l.strategyVersion)}<div class="three-grid padded">${stat('Portfolio after costs', percent(l.totalNetReturnPercent), `${rupees(l.initialCapital)} → ${rupees(l.finalCapital)}`)}${stat('Synthetic TRI', percent(l.benchmarkTriReturnPercent), 'Research index; dividends reflected in its levels')}${stat('Fund / ETF after costs', fund ? percent(fund.netReturnPercent) : 'Not supplied', fund ? fund.name : 'Needs a net-expense total-return series')}</div><div class="table-wrap"><table><thead><tr><th>Decision date</th><th>Holdings</th><th>Gross return</th><th>Cost drag</th><th>Net return</th><th>TRI</th></tr></thead><tbody>${l.periods.map(p => `<tr><td>${e(date(p.decisionAt))}</td><td>${p.securities.map(e).join(', ') || 'Cash'}</td><td>${e(percent(p.grossReturnPercent))}</td><td>${e(number(p.costPercent))}%</td><td>${e(percent(p.netReturnPercent))}</td><td>${e(percent(p.benchmarkTriReturnPercent))}</td></tr>`).join('')}</tbody></table></div><p class="card-note">Universe history: ${l.pointInTimeUniverseSupplied ? 'provided' : 'not supplied'}. ${e(l.evidenceNote)}</p>${fund ? `<p class="card-note">Fund comparison: ${fund.observations} supplied observations · ${e(rupees(fund.tradingCostsRupees))} trading fees · ${e(number(fund.maximumObservedDrawdownPercent))}% observed drawdown. ${e(fund.evidenceNote)}</p>` : ''}</section>
    ${adjustments ? `<section class="card section-space">${cardHead('Corporate actions and terminal outcomes', 'Separate synthetic arithmetic check · forward return chain starts at 100')}<div class="table-wrap"><table><thead><tr><th>Close / settlement</th><th>Total-return level</th><th>Evidence</th><th>Outcome</th></tr></thead><tbody>${adjustments.prices.map(p => `<tr><td>${e(date(p.closeAt))}</td><td>${e(number(p.adjustedTotalReturnClose))}</td><td>${p.sourceEvidenceIds.map(e).join(', ')}</td><td>${tag(p.isTerminal ? p.terminalReason ?? 'Terminal' : 'Continuing', p.isTerminal ? 'amber' : 'green')}</td></tr>`).join('')}</tbody></table></div><p class="card-note">${e(adjustments.evidenceNote)}</p></section>` : ''}${aiEvaluationPanel()}`;
}

function aiEvaluationPanel(): string {
  const ai = snapshot.aiEvaluation;
  if (!ai) return '';
  return `<section class="card section-space" id="ai-evaluation">${cardHead('Recorded AI contract checks', `${ai.candidateCount} ${ai.dataKind.toLowerCase()} cases · no API calls`, '<button class="button small secondary" id="download-ai">Export evaluation</button>')}
    <p class="card-note">Frozen responses check evidence, citations and failure handling. They do not measure an AI model's trading skill. A continuation still requires deterministic risk review.</p>
    <div class="three-grid padded">${stat('Continued for review', number(ai.continuedCount, 0), `${ai.rulesEligibleCount} rules-eligible candidates`)}${stat('Invalid responses blocked', number(ai.invalidResponseCount, 0), 'Failed schema, citation or identifier checks')}${stat('Recorded charge metadata', '$' + number(ai.knownRecordedChargeUsd, 4), `${ai.unknownChargeCalls} replay calls lack valid billing metadata`)}</div>
    <div class="table-wrap"><table><thead><tr><th>Recorded case</th><th>Contract result</th><th>Assessment</th><th>Rules P&amp;L</th><th>Filtered P&amp;L</th></tr></thead><tbody>${ai.cases.slice(0, 50).map(c => `<tr><td><strong>${e(c.caseId)}</strong></td><td>${tag(c.review.status, c.review.status === 'Accepted' ? 'green' : 'amber')}</td><td>${e(c.review.assessment?.decision ?? 'Blocked')}</td><td>${e(rupees(c.rulesOnlyCandidateNetPnlRupees))}</td><td>${e(rupees(c.recordedFilterCandidateNetPnlRupees))}</td></tr>`).join('')}</tbody></table></div>
    <p class="card-note">Fixed independent candidate sizes, with supplied trading costs: rules ${e(rupees(ai.rulesOnlyCandidateNetPnlRupees))}; recorded filter ${e(rupees(ai.recordedFilterCandidateNetPnlRupees))}. Inference charges are separate USD metadata; replay and this page incur no inference charges. These supplied totals are arithmetic checks, not portfolio returns or forecasts.${ai.cases.length > 50 ? ' Showing the first 50 cases; export includes every case.' : ''}</p>
    <details class="padded"><summary>Evaluation versions and limits</summary><p>Model ${e(ai.modelId)} · prompt ${e(ai.promptVersion)} · plan ${e(ai.planId)}</p><p>${ai.limitations.map(e).join(' ')}</p><p class="footnote">Dataset <span class="hash">${e(ai.datasetHash)}</span><br/>Recordings <span class="hash">${e(ai.recordingsHash)}</span></p></details></section>`;
}

function comparison(title: string, benchmark: Benchmark, detail: string, allocationUnit = 'replay'): string {
  const ratio = Math.max(2, Math.min(100, benchmark.MedianAfterMs / benchmark.MedianBeforeMs * 100));
  return `<section class="card">${cardHead(title, detail)}<div class="comparison-summary"><strong>${e(number(benchmark.ThroughputMultiplier))}×</strong><span>throughput on this workload</span>${tag(`${number(benchmark.ElapsedReductionPercent)}% less elapsed time`, 'green')}</div><div class="bar-comparison"><div><span>Before</span><progress max="100" value="100" aria-label="Baseline elapsed time"></progress><strong>${e(number(benchmark.MedianBeforeMs))} ms</strong></div><div><span>After</span><progress max="100" value="${ratio}" aria-label="Optimized elapsed time"></progress><strong>${e(number(benchmark.MedianAfterMs))} ms</strong></div></div>${benchmark.AllocationReductionPercent !== undefined ? `<div class="allocation"><span>Allocation per ${e(allocationUnit)}</span><strong>${e(number(benchmark.MedianAllocationBeforeBytes ?? 0, 0))} → ${e(number(benchmark.MedianAllocationAfterBytes ?? 0, 0))} bytes</strong><small>${e(number(benchmark.AllocationReductionPercent))}% reduction</small></div>` : ''}</section>`;
}
function performanceView(): string {
  const p = snapshot.performance;
  const ranking = snapshot.rankingPerformance;
  return `<div class="page-title"><div><span class="eyebrow">MEASURED LOCAL COMPUTATION</span><h1>Performance</h1><p>Three separate Release runs. Median elapsed time; workload limits below.</p></div>${tag(date(p.CheckedAtUtc))}</div><div class="two-grid">${comparison('Incremental replay', p.Comparisons.replay, '5,000 synthetic flat sessions · 75 five-minute bars each')}${comparison('Atomic research import', p.Comparisons.import, '5,000 documents + 5,000 facts + 5,000 metrics')}</div>
    ${ranking ? `<div class="section-space">${comparison('Dated company ranking', ranking.Comparison, `${number(ranking.Workload.Companies, 0)} companies · ${number(ranking.Workload.MetricRows, 0)} metric rows · ${ranking.Workload.QueriesPerTrial} queries`, 'query')}<p class="footnote">One-time index build: ${e(number(ranking.MedianIndexBuildMs))} ms. Queries plus build: ${e(number(ranking.MedianIndexedWithBuildMs))} ms. ${e(ranking.Caveat)}</p></div>` : ''}
    <div class="notice section-space">${icon('performance')}<div><strong>Workload limits</strong><p>${e(p.Caveat)}</p></div></div><section class="card section-space">${cardHead('Method and reproducibility', 'Raw measurements travel with the snapshot.')}<p class="padded">${e(p.Method)}</p><div class="table-wrap"><table><thead><tr><th>Input</th><th>Content SHA-256</th></tr></thead><tbody>${snapshot.inputs.map(i => `<tr><td>${e(i.file)}</td><td class="hash">${e(i.sha256)}</td></tr>`).join('')}</tbody></table></div></section>`;
}

function deploymentView(): string {
  return `<div class="page-title"><div><span class="eyebrow">PORTABLE DASHBOARD · PERSISTENT ENGINE</span><h1>Deployment</h1><p>Run the web workspace locally or on Vercel. Keep the engine near its data source.</p></div>${tag('Read-only dashboard', 'green')}</div><div class="two-grid"><section class="card">${cardHead('Web dashboard', 'Static assets · Vercel or local')}<div class="deployment-summary">${icon('overview')}<h3>Fast to load. Simple to host.</h3><p>The bundled report uses synthetic data. A downloaded snapshot can also be opened privately in this browser.</p><dl><dt>Current report</dt><dd>${e(reportSource)}</dd><dt>Data</dt><dd>${e(snapshot.dataKind)}</dd><dt>Generated</dt><dd>${e(date(snapshot.generatedAt))} · ${e(time(snapshot.generatedAt))} IST</dd><dt>Broker</dt><dd>Disconnected</dd></dl><div class="button-row"><a class="button secondary" href="https://tradetest-dashboard.vercel.app" target="_blank" rel="noopener noreferrer">Open hosted dashboard</a><label class="button">Open snapshot<input id="snapshot-file" type="file" accept=".json,application/json" hidden /></label><button class="button secondary" id="reset-report">Restore example</button></div></div></section>
    <section class="card">${cardHead('Persistent .NET service', 'Optional read-only connection')}<form id="api-form" class="api-form"><p>Use the local service for development, or an HTTPS service for remote access. The token stays in memory and is cleared on refresh.</p><label>Service URL<input type="url" name="url" required value="${e(apiDraft || apiBase)}" placeholder="http://127.0.0.1:5080" autocomplete="off" /></label><label>Access token<input type="password" name="token" placeholder="Required for a remote service" autocomplete="off" /></label><button class="button" type="submit" ${isLoading ? 'disabled' : ''}>Load service snapshot ${icon('arrow')}</button><p class="footnote">Remote connections need HTTPS and an allowed dashboard origin. Browser local-network permissions may be required for localhost.</p></form></section></div>
    <section class="card section-space">${cardHead('Activation gates', 'Code completion and trading evidence are tracked separately.')}<div class="gate-list">${snapshot.gates.map(g => `<article class="gate"><span class="gate-id">${e(g.id.toUpperCase())}</span><div><h3>${e(g.title)}</h3><p>${e(g.detail)}</p></div>${tag(g.state, g.state === 'Implemented' ? 'green' : g.state === 'Partial' ? 'amber' : '')}</article>`).join('')}</div></section><p class="footnote">Public deployments should contain only intentionally public data. API keys and broker credentials do not belong in the web build.</p>`;
}

function render(): void {
  const active = view();
  app.innerHTML = `<aside class="sidebar"><a class="brand" href="#overview"><span class="brand-mark">T</span><span>TradeTest<small>Research workspace</small></span></a><div class="workspace-label">WORKSPACE <span>IN</span></div><nav aria-label="Main navigation">${views.map(v => `<a href="#${v}" class="nav-item ${active === v ? 'active' : ''}" ${active === v ? 'aria-current="page"' : ''}>${icon(v)}<span>${labels[v]}</span></a>`).join('')}</nav><div class="sidebar-footer"><div class="offline-indicator"><i></i>PAPER / OFFLINE</div><p>India focused.<br/>Evidence comes first.</p><a href="https://github.com/dvajjala-ui/tradetest" target="_blank" rel="noopener noreferrer">Project repository ↗</a></div></aside>
    <div class="workspace"><header class="topbar"><div class="breadcrumb">Workspace <span>/</span> <strong>${labels[active]}</strong></div><div class="topbar-actions">${tag(active === 'research' && companyReport ? 'User-supplied research' : snapshot.dataKind + ' data', 'amber')}<button class="icon-button" id="refresh-report" title="Refresh report" aria-label="Refresh report" ${isLoading ? 'disabled' : ''}>${icon('refresh')}</button><button class="button small secondary" id="download-report">${icon('download')}<span>Export snapshot</span></button></div></header><main id="main" tabindex="-1">${flash ? `<div class="flash ${flashError ? 'error' : ''}" role="${flashError ? 'alert' : 'status'}">${e(flash)}<button id="dismiss-flash" aria-label="Dismiss message">×</button></div>` : ''}${({ overview, research: researchView, replay: replayView, studies: studiesView, performance: performanceView, deployment: deploymentView }[active])()}<footer class="main-footer"><span>TradeTest · ${e(active === 'research' && companyReport ? 'RESEARCH / READ ONLY · ' + companySource : snapshot.mode + ' · ' + reportSource)}</span><span>${e(active === 'research' && companyReport ? 'Company reports contain user-supplied evidence. Source metadata and verification labels require independent review.' : snapshot.evidenceNote)}</span></footer></main></div>`;
  bind();
}

function bind(): void {
  document.querySelector('#refresh-report')?.addEventListener('click', () => { if (view() === 'research' && companyReport) { if (companySource.startsWith('Local file:')) message('Open an updated company report to refresh this local file.'); else void loadCompany(); } else void loadCurrent(); });
  document.querySelector('#download-report')?.addEventListener('click', () => {
    const url = URL.createObjectURL(new Blob([JSON.stringify(snapshot, null, 2)], { type: 'application/json' }));
    const anchor = document.createElement('a'); anchor.href = url; anchor.download = 'tradetest-snapshot.json'; anchor.click(); URL.revokeObjectURL(url);
  });
  document.querySelector('#download-ai')?.addEventListener('click', () => {
    const url = URL.createObjectURL(new Blob([JSON.stringify(snapshot.aiEvaluation, null, 2)], { type: 'application/json' }));
    const anchor = document.createElement('a'); anchor.href = url; anchor.download = 'tradetest-recorded-ai-evaluation.json'; anchor.click(); URL.revokeObjectURL(url);
  });
  document.querySelector('#dismiss-flash')?.addEventListener('click', () => { flash = ''; render(); });
  document.querySelector('#company-select')?.addEventListener('change', event => { chosenSecurity = (event.target as HTMLSelectElement).value; query = ''; factsPage = 0; render(); });
  document.querySelector('#fact-search')?.addEventListener('input', event => { query = (event.target as HTMLInputElement).value; factsPage = 0; document.querySelector('#fact-results')!.innerHTML = factResults(); });
  document.querySelector('#fact-results')?.addEventListener('click', event => {
    const button = (event.target as Element).closest<HTMLButtonElement>('[data-fact-page]'); if (!button || button.disabled) return;
    factsPage = Math.max(0, factsPage + Number(button.dataset.factPage));
    document.querySelector('#fact-results')!.innerHTML = factResults();
  });
  document.querySelectorAll<HTMLAnchorElement>('[data-fact]').forEach(anchor => anchor.addEventListener('click', event => {
    event.preventDefault(); query = ''; const input = document.querySelector<HTMLInputElement>('#fact-search'); if (input) input.value = '';
    factsPage = Math.floor(Math.max(0, matchingFacts().findIndex(f => f.factId === anchor.dataset.fact)) / factsPageSize);
    document.querySelector('#fact-results')!.innerHTML = factResults(); const row = document.getElementById(`fact-${anchor.dataset.fact}`); row?.scrollIntoView({ block: 'center', behavior: 'smooth' }); row?.classList.add('highlight');
  }));
  document.querySelector('#company-form')?.addEventListener('submit', event => {
    event.preventDefault(); const form = new FormData(event.target as HTMLFormElement);
    researchRequest = { securityId: String(form.get('securityId') ?? ''), asOf: String(form.get('asOf') ?? ''), query: String(form.get('query') ?? '') };
    void loadCompany();
  });
  document.querySelector('#company-file')?.addEventListener('change', async event => {
    const file = (event.target as HTMLInputElement).files?.[0]; if (!file) return;
    const version = ++loadVersion; isLoading = false;
    try {
      if (file.size > 2 * 1024 * 1024) throw new Error('Company reports must be no larger than 2 MB.');
      const next = parseCompanyResearch(JSON.parse(await file.text())); if (version !== loadVersion) return;
      companyReport = next; companySource = 'Local file: ' + file.name; chosenSecurity = next.securityId; query = ''; factsPage = 0;
      message('Company report opened locally. The file was not uploaded.');
    } catch (error) { if (version === loadVersion) message(errorMessage(error), true); }
  });
  document.querySelector('#restore-company')?.addEventListener('click', () => { ++loadVersion; isLoading = false; companyReport = null; companySource = ''; chosenSecurity = ''; query = ''; factsPage = 0; message('Example evidence restored.'); });
  document.querySelector('#export-company')?.addEventListener('click', () => {
    if (!companyReport) return;
    const url = URL.createObjectURL(new Blob([JSON.stringify(companyReport, null, 2)], { type: 'application/json' }));
    const anchor = document.createElement('a'); anchor.href = url; anchor.download = 'tradetest-company-' + companyReport.securityId.replace(/[^a-zA-Z0-9_-]/g, '_') + '.json'; anchor.click(); URL.revokeObjectURL(url);
  });
  document.querySelector('#snapshot-file')?.addEventListener('change', async event => {
    const file = (event.target as HTMLInputElement).files?.[0]; if (!file) return;
    const version = ++loadVersion; isLoading = false;
    try {
      if (file.size > 2 * 1024 * 1024) throw new Error('Snapshot files must be no larger than 2 MB.');
      const next = parseSnapshot(JSON.parse(await file.text())); if (version !== loadVersion) return;
      snapshot = next; reportSource = 'Local file: ' + file.name; apiBase = ''; apiDraft = ''; apiToken = ''; companyReport = null; companySource = ''; chosenSecurity = ''; query = ''; factsPage = 0;
      message('Snapshot opened locally. The file was not uploaded to a server.');
    } catch (error) { if (version === loadVersion) message(errorMessage(error), true); }
  });
  document.querySelector('#reset-report')?.addEventListener('click', () => { apiBase = ''; apiDraft = ''; apiToken = ''; reportSource = 'Bundled example'; void loadCurrent(); });
  document.querySelector('#api-form')?.addEventListener('submit', async event => {
    event.preventDefault(); const form = new FormData(event.target as HTMLFormElement);
    apiDraft = String(form.get('url') ?? '');
    try { const base = validateApiUrl(apiDraft); const token = String(form.get('token') ?? ''); if (await fetchReport(base + '/api/snapshot', token, base)) { apiBase = base; apiDraft = base; apiToken = token; researchRequest = { securityId: '', asOf: '', query: '' }; render(); } }
    catch (error) { message(errorMessage(error), true); }
  });
}
function errorMessage(error: unknown): string { return error instanceof Error ? error.message : 'Unable to load this report.'; }
function message(value: string, error = false): void { flash = value; flashError = error; render(); }
async function fetchReport(url: string, token = '', source = 'Bundled example'): Promise<boolean> {
  const version = ++loadVersion; isLoading = true; if (snapshot) render();
  try {
    const next = parseSnapshot(await fetchJson(url, token)); if (version !== loadVersion) return false;
    snapshot = next; reportSource = source; flash = ''; chosenSecurity = ''; query = ''; factsPage = 0; companyReport = null; companySource = '';
    return true;
  } catch (error) { if (version !== loadVersion) return false; throw error; }
  finally { if (version === loadVersion) { isLoading = false; if (snapshot) render(); } }
}
async function loadCompany(): Promise<void> {
  if (!apiBase) { message('Connect a read-only service on the Deployment screen first.', true); return; }
  const request = { ...researchRequest }, version = ++loadVersion;
  isLoading = true; render();
  try {
    if (!request.securityId.trim() || request.securityId.length > 128 || request.query.length > 500 || !Number.isFinite(Date.parse(request.asOf)) || !/(Z|[+-]\d{2}:\d{2})$/i.test(request.asOf)) throw new Error('Supply a security ID and an ISO timestamp with an explicit time zone.');
    const params = new URLSearchParams({ asOf: request.asOf, query: request.query });
    const next = parseCompanyResearch(await fetchJson(apiBase + '/api/research/' + encodeURIComponent(request.securityId) + '?' + params, apiToken));
    if (version !== loadVersion) return;
    if (next.securityId !== request.securityId || Date.parse(next.asOf) !== Date.parse(request.asOf)) throw new Error('The service returned evidence for a different company or date.');
    companyReport = next; companySource = apiBase; chosenSecurity = next.securityId; query = ''; factsPage = 0; flash = 'Dated company evidence loaded.'; flashError = false;
  } catch (error) { if (version === loadVersion) { flash = errorMessage(error); flashError = true; } }
  finally { if (version === loadVersion) { isLoading = false; render(); } }
}
async function loadCurrent(): Promise<void> {
  if (!apiBase && reportSource.startsWith('Local file:')) { message('This is a local file. Open an updated snapshot to refresh it.'); return; }
  try { await fetchReport(apiBase ? apiBase + '/api/snapshot' : '/snapshot.json', apiToken, apiBase || 'Bundled example'); }
  catch (error) { if (snapshot) message(errorMessage(error), true); else app.innerHTML = `<div class="loading-screen"><h1>Unable to load the workspace</h1><p role="alert">${e(errorMessage(error))}</p><button class="button" id="retry-load">Try again</button></div>`; document.querySelector('#retry-load')?.addEventListener('click', () => { void loadCurrent(); }); }
}
window.addEventListener('hashchange', () => { if (snapshot) { flash = ''; render(); document.querySelector<HTMLElement>('#main')?.focus({ preventScroll: true }); window.scrollTo(0, 0); } });
void loadCurrent();
