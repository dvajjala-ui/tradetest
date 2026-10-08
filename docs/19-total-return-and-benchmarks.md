# Total returns, delisting and investable benchmarks

As of 2026-10-08. This increment closes several arithmetic and data-contract gaps in G1/G3. It uses synthetic fixtures; it does not establish an investment edge.

## Corporate-action return builder

~~~bash
dotnet run --project src/TradeTest.Cli -- build-total-return fixtures/synthetic-corporate-actions.json
~~~

`TotalReturnBuilder` consumes raw closes, action revisions, a continuous source coverage assertion, and an explicit `AsOf`. It emits a forward return chain starting at 100, with a version/input hash, first-known timestamps and source evidence IDs. The CLI also hashes the complete input file.

- **Split / bonus:** multiply the shares represented by the preceding close; the mechanical price reduction does not become a loss.
- **Regular cash dividend:** apply the cash per share immediately before that action, then reinvest at the supplied ex-date close. A sparse dataset without that close fails; the reinvestment price is not invented.
- **Simultaneous actions:** an explicit sequence defines whether dividend terms precede or follow a share change. Ambiguous sequence numbers fail.
- **Terminal settlement / write-off:** require an explicit action. A zero write-off is permitted and kept distinct from a missing ordinary price. Tradable closes after termination fail.
- **Corrections:** only revisions known by `AsOf` can replace a predecessor. An unverified visible correction withdraws the earlier terms and causes a failure for the affected calculation.
- **Coverage:** verified source metadata must assert coverage across the complete return interval. Empty actions alone do not certify that there were no actions. Coverage cannot be first known before its end date.

The output's knowledge time includes the raw-price, action and coverage dependencies. A later source correction cannot be relabelled as knowledge available at the old close time. Use the resulting series for research outcomes, not executable price quotes.

Rights, mergers, demergers, withholding tax, dividend payment delays and settlement financing are unsupported and require another explicit model. A source licence ID is metadata; vendor access and permitted use still need independent confirmation. Validate adjusted returns against another source before using market data.

Primary context: [NSE total-return index explanation](https://www.nseindia.com/static/products-services/indices-total-returns-index), [Nifty methodology](https://www.niftyindices.com/Methodology/Method_NIFTY_Equity_Indices.pdf), and [NSE corporate actions](https://www.nseindia.com/static/investor-relations/corporate-actions). This implementation is a single-security research chain, not a reproduction of an exchange's full index methodology or its special-dividend rules.

## Long-term evaluation changes

The evaluator now indexes price timestamps once, rather than repeatedly scanning an entire security history. Security IDs are compared without case differences; duplicate observations and multiple/late terminal values fail.

An optional `ReturnDataAsOf` limits outcome data to observations known by that cutoff. Without it, the report records the latest first-known time in the supplied prices. The entry and exit benchmark closes must be distinct, and every selected ordinary security needs aligned observations. An explicitly terminal outcome can carry its settlement cash to the exit date; missing delisting data still fails.

### Fee correction

For a fractional research holding, entry costs are funded before investing:

~~~text
invested cash = starting cash / (1 + entry cost rate)
sale proceeds = invested cash × total-return price ratio
final cash = sale proceeds − proceeds-based exit costs − fixed sell charges
~~~

The earlier prototype subtracted both cost rates directly from a gross percentage. With the synthetic ₹5,000, 20% price return, 10 bps per leg and ₹20 fixed sale charge, the corrected result is approximately **19.36024%**, replacing **19.4%**. This is a correction to toy arithmetic, not measured strategy performance. Terminal cash/write-offs do not create a simulated broker sale or its fee.

`CostPercent` reports return drag relative to the fully invested gross return. `TradingCostsRupees` separately reports cash fees, since these measures are not identical after the entry cash reduction. A depleted portfolio remains at zero at later rebalances; any unfunded exit charges are reported separately instead of silently disappearing. Holdings are fractional and fully liquidated at rebalances; capacity, integer share rounding and taxes still need a production portfolio model.

## Investable fund / ETF comparison

`InvestableBenchmark` is optional in the long-term input. Supply a fund/ETF return series, matching entry/exit timestamps, net of fund expenses and including distributions. Explicit source/licence metadata, entry/exit costs and a fixed exit cost accompany it.

The evaluator uses a passive buy-and-hold comparison over the common window. It does not subtract the expense ratio again from net-expense levels. [AMFI explains that published NAV is after expenses](https://www.amfiindia.com/investor/knowledge-center-info?zoneName=expenseRatio). A total-return index and an investable fund remain separate comparisons.

The report records sample count, cash fees, final value and drawdown at supplied observations. Annualized return is omitted for periods shorter than one year. Observed fund drawdown and strategy rebalance drawdown have different sampling and should not be presented as equivalent risk estimates. A NAV-based comparison also does not prove an ETF can be traded at NAV; spreads, premium/discount and execution timing need suitable supplied data/cost assumptions.

The bundled fund is invented and has two observations. It validates the reporting path; selecting an actual broad India fund and reconciling its history are still pending.

## Verification

44 .NET tests pass after this increment, including split/bonus/dividend wealth preservation, same-day sequence, missing ex-date prices, late and unverified corrections, terminal write-off/cash settlement, no resurrection, complete portfolio depletion, unfunded sale charges, cash-only fees, aligned fund prices, case-insensitive duplicates, net-expense treatment and annualization limits. The dashboard also displays the new synthetic fund and terminal examples.
