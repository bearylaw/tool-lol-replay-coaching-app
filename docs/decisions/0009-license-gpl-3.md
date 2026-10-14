# License Rift Review under GPL-3.0-or-later instead of MIT

- Status: accepted
- Date: 2026-10-14

## Context

The repository went public on 2026-10-14 under MIT. The coach wants the most
credit possible while the tool stays open source. MIT lets anyone ship a
closed, changed copy that only carries the copyright line somewhere in it.
All code so far is the coach's own (with the agent), so the license can
change freely. The dependencies (ZstdSharp.Port, xunit) are MIT or Apache-2.0
and fit inside a GPL program.

## Considered options

1. MIT: most adoption; forks may close the source.
2. Apache-2.0: a NOTICE file forks must carry; forks may still close the source.
3. GPL-3.0-or-later: every copy and fork keeps the notices, marks its
   changes and publishes its source under the same license.

## Decision

GPL-3.0-or-later. It is the only option where a fork cannot become a closed
product, and every copy keeps the credit. "Or later" follows the FSF's
recommendation so future GPL versions can apply.

## Consequences

Companies that only ship closed software will not reuse the code. Outside
contributions come in under the same license. The private personal edition
is not distributed, so the GPL puts no duty on it; if it is ever shared,
it ships its source under GPL too. Revisit only if a contributor's code is
merged, since a later change of license would then need their consent.
