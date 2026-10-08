export interface Bar { securityId: string; startsAt: string; endsAt: string; open: number; high: number; low: number; close: number; volume: number }
export interface Trade { securityId: string; quantity: number; enteredAt: string; exitedAt: string; entryPrice: number; exitPrice: number; exitReason: string; grossPnl: number; netPnl: number; costs: { total: number } }
export interface Evaluation { sessions: number; noTradeSessions: number; trades: number; grossPnl: number; tradingCosts: number; netPnl: number; fixedCapitalReturnPercent: number; winRatePercent: number | null; evidenceNote: string }
export interface Fact { factId: string; documentId: string; securityId: string; claim: string; firstKnownAt: string; verification: string }
export interface Metric { securityId: string; kind: string; value: number; firstKnownAt: string; sourceFactId: string; verification: string }
export interface Company { securityId: string; score: number; evidenceFactIds: string[]; riskFlags: string[]; modelVersion: string }
export interface Document { documentId: string; securityId: string; publisher: string; sourceUrl: string; publishedAt: string; firstKnownAt: string; licenceId: string; contentSha256: string }
export interface Benchmark { MedianBeforeMs: number; MedianAfterMs: number; ElapsedReductionPercent: number; ThroughputMultiplier: number; MedianAllocationBeforeBytes?: number; MedianAllocationAfterBytes?: number; AllocationReductionPercent?: number }
export interface Snapshot {
  schemaVersion: string; generatedAt: string; dataKind: string; mode: string; evidenceNote: string;
  inputs: { file: string; sha256: string }[]; bars: Bar[];
  replay: { strategyVersion: string; costModelVersion: string; initialCash: number; candidateCount: number; riskRejectedCount: number; submittedOrders: number; trades: Trade[]; events: { type: string; occurredAt: string; json: string }[]; netPnl: number; netReturnPercent: number; maxClosedEquityDrawdownPercent: number };
  intradayStudy: { training: Evaluation; validation: Evaluation; holdout: Evaluation; stressedHoldout: Evaluation; evidenceNote: string };
  walkForward: { folds: { number: number; trainingStartsAt: string; validationStartsAt: string; evaluationStartsAt: string; evaluationEndExclusive: string; training: Evaluation; validation: Evaluation; evaluation: Evaluation }[]; combinedOutOfSample: Evaluation; finalHoldout: Evaluation; stressedFinalHoldout: Evaluation; evidenceNote: string };
  longTerm: { strategyVersion: string; initialCapital: number; finalCapital: number; totalNetReturnPercent: number; benchmarkTriReturnPercent: number; maximumRebalanceDrawdownPercent: number; evidenceNote: string; pointInTimeUniverseSupplied: boolean; investableBenchmark?: { name: string; netReturnPercent: number; tradingCostsRupees: number; observations: number; maximumObservedDrawdownPercent: number; evidenceNote: string } | null; periods: { decisionAt: string; nextDecisionAt: string; securities: string[]; grossReturnPercent: number; costPercent: number; netReturnPercent: number; benchmarkTriReturnPercent: number }[] };
  research: { asOf: string; packetHash: string; facts: Fact[]; metrics: Metric[]; companies: Company[]; documents: Document[] };
  performance: { CheckedAtUtc: string; Method: string; Caveat: string; Comparisons: { replay: Benchmark; import: Benchmark } };
  gates: { id: string; title: string; state: string; detail: string }[];
  returnAdjustments?: { builderVersion: string; inputSha256: string; evidenceNote: string; prices: { securityId: string; closeAt: string; firstKnownAt: string; adjustedTotalReturnClose: number; isTerminal: boolean; terminalReason: string | null; sourceEvidenceIds: string[] }[] } | null;
  rankingPerformance?: { Comparison: Benchmark; Workload: { Companies: number; MetricRows: number; QueriesPerTrial: number }; MedianIndexBuildMs: number; MedianIndexedWithBuildMs: number; Caveat: string } | null;
}
