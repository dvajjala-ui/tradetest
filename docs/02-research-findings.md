# Research Findings and Evidence

This document captures external findings discussed during project formation. Pricing, limits and broker policies are time-sensitive; verify all values again before production use.

## 1. Groww Trading API

The conversation established that Groww provides a Trading API intended for programmatic trading workflows.

Capabilities discussed included:

- market data,
- historical data,
- order placement,
- order modification/cancellation,
- positions/portfolio data,
- intraday/MIS use,
- smart order patterns,
- live market feeds.

The preferred production integration is therefore:

~~~text
TradeTest -> Groww API
~~~

not browser-click automation.

### API pricing snapshot discussed

At the time of discussion, Groww API pricing was referenced as:

- ₹499 + taxes/month, or
- ₹4,999/year before taxes.

The annual plan was estimated at approximately ₹492/month when averaged including 18% GST.

Re-check pricing before subscribing.

## 2. Static IP requirement

The conversation identified Groww's static-IP requirement for API order placement.

This directly affects deployment choices.

Two viable approaches:

### A. User PC

- run .NET Worker locally,
- obtain static IP from ISP,
- whitelist it with Groww.

Pros:
- no VPS bill,
- easy local debugging.

Cons:
- depends on home power/internet/PC uptime,
- ISP static-IP availability/cost varies.

### B. VPS/cloud server

- run .NET Worker on a small VPS,
- use server's stable/reserved IP,
- whitelist that address.

Pros:
- predictable uptime,
- cleaner production environment,
- easier monitoring.

Cons:
- additional monthly cost.

## 3. Browser automation

Technically possible through computer-use/browser automation systems, but not recommended as the primary live execution mechanism.

Failure modes discussed:

- UI changes,
- wrong element click,
- instrument confusion,
- login expiry,
- OTP/CAPTCHA,
- slow rendering,
- unexpected modal,
- latency,
- duplicated action,
- weak idempotency.

Conclusion:

> Browser automation can be useful for experiments/manual workflows, but broker API execution is safer and more auditable for real-money automation.

## 4. Public algo-trading experiences

### Groww-linked Reddit example

A user reportedly shared a Groww verified-P&L link and described:

- years of attempts,
- approximately ₹4–₹6 lakh spent/lost across failed experimentation, subscriptions and AI costs,
- later starting a run with about ₹1 lakh,
- roughly ₹1.17 lakh profit after charges over 37 live days.

Important limitations:

- strategy involved stock options,
- holding period was around 1–5 days,
- short observation window,
- not comparable to ₹5,000 cash-equity intraday V1.

Takeaway:

> The visible profitable run should not hide the long experimentation/failure history.

### Dhan API bot example

Another developer described a Python + Dhan setup using:

- direct REST,
- WebSockets,
- automated strategy logic.

A reported day included:

- 30 trades,
- 25 wins,
- 5 losses,
- about ₹8,691 net P&L under their cost model.

More important: the developer found a software/accounting bug causing fake P&L due to strike-switching and mismatched contract tracking.

Takeaway:

> Independent broker reconciliation is mandatory. Internal P&L alone is not trustworthy.

## 5. Backtest-to-live degradation

A public trader experience discussed live option entries being materially worse than backtests.

Observed causes included:

- API acknowledgement latency,
- fast market movement,
- spread,
- slippage,
- delayed fills.

This leads to the correct performance equation:

~~~text
gross strategy edge
- brokerage
- taxes/fees
- bid/ask spread
- slippage
- latency impact
- rejected/partial-order impact
= live net expectancy
~~~

Any backtest that omits these costs is insufficient.

## 6. Retail profitability base rates

SEBI research referenced in the discussion found that a majority of individual intraday traders lose money.

A separate derivatives/F&O study showed even worse retail outcomes.

Project implication:

- do not start with options/F&O,
- do not treat AI as a shortcut around statistical edge,
- evaluate the project using rigorous evidence.

## 7. Transaction-cost issue with tiny accounts

Groww equity intraday brokerage was discussed as approximately:

> ₹20 or 0.1% per executed order, whichever is lower, with a minimum around ₹5.

For small orders, this can mean about ₹5 brokerage on entry + ₹5 on exit before additional charges.

This is why the original ₹500 live experiment was rejected as a useful profitability test.

For ₹500:

- ₹5 net/day = 1% daily,
- ₹10 net/day = 2% daily,
- required gross return is larger after fees.

The user later changed the live capital to ₹5,000, while paying infrastructure separately.

## 8. Groq vs OpenAI model access

An important correction from the conversation:

> Groq does not provide GPT-6 Sol or GPT-6 Astra.

Groq can provide supported hosted models such as GPT-OSS 120B.

OpenAI models must be called via OpenAI's own API.

Therefore, TradeTest should treat model providers independently:

~~~text
Groq provider
  -> GPT-OSS 120B

OpenAI provider
  -> GPT-6 Sol
  -> GPT-6 Astra
~~~

## 9. Why Groq is useful

Groq-hosted GPT-OSS was considered a good candidate for:

- cheap screening,
- fast classification,
- structured candidate review,
- rejecting obvious low-quality setups.

Pricing discussed at the time was very low relative to premium models, making model cost a minor component of the overall system.

## 10. Why Sol is useful

GPT-6 Sol was chosen as the likely default main reasoning model for:

- normal candidate review,
- interpreting verified daily context,
- contradictory-signal detection,
- explaining TAKE/REJECT/WAIT decisions.

It should not calculate trivial indicators that C# can calculate exactly.

## 11. Why Astra is useful

Astra should be reserved for:

- ambiguous candidates,
- high-context situations,
- disagreement between model/rules/context,
- weekly post-market analysis,
- strategy hypothesis generation,
- deep review of losing/winning clusters.

Astra is not expected to magically make a strategy profitable.

## 12. Codex / Claude / Gemini / other research models

The user proposed using available/free AI accounts to increase research diversity.

The agreed role is:

- pre-market research,
- cross-checking,
- code review,
- post-market analysis,
- strategy review,
- backtest inspection.

They should **not** be required for live safety-critical order execution.

Consumer chat/browser sessions can be unreliable because of:

- usage limits,
- login/session expiry,
- UI changes,
- rate limiting.

Where automation is critical, use documented APIs.

## 13. Multiple models do not prove a fact

Critical reasoning rule:

~~~text
Claude: BUY
Groq: BUY
Sol: BUY
Astra: BUY
~~~

does not prove the trade is valid.

Models can share:

- the same bad source,
- the same market narrative,
- correlated reasoning errors.

Instead:

1. verify factual claims,
2. record source and timestamp,
3. give verified facts to the models,
4. use model disagreement as a signal for uncertainty,
5. use backtests/live evidence as the final judge.

## 14. Source hierarchy

Preferred source priority:

1. NSE/BSE/company filings.
2. SEBI/RBI/government/regulatory sources.
3. High-quality financial news.
4. Other web sources.
5. Reddit/social/community reports.

Community reports are valuable for operational failure modes and lived experience, but should not be treated as proof of expected returns.

## 15. Research conclusion

The strongest project direction is not "AI autonomous trader."

It is:

> deterministic quantitative execution + carefully scoped AI research/reasoning + strict risk controls + complete evidence/logging.

That architecture directly addresses the failures repeatedly observed in public algo-trading experiences.
