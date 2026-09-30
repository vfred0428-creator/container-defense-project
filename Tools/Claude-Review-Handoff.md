# Claude review — 2026-09-29

The user explicitly requested delegation to their installed Claude. Sent a bounded design review to the existing Claude `game` project via its desktop UI. Claude completed **Gifting audit and rank prototype**, producing `container-defense/reviews/M4_GIFTING_REVIEW.md` in its own environment (not this repository). The response was read in the app; no repository access was granted or overlapping edits made.

## Findings applied
- Deep account/inventory/receipt snapshots; no live mutation before a successful atomic write.
- One account save lock for normal saves and gift commit; synchronous authority on the Unity main thread.
- Replay comparison before affordability/capacity; receipts preserve the awarded Popularity value.
- A review/confirm sheet freezes a gift's transaction ID across retries. Underlying controls are disabled while confirming.
- Receiver-stack, gift-value multiplication and Popularity addition overflow checks; nonpositive gift values reject.
- A full 1,000-receipt ledger rejects new transfers but accepts previous receipts. No partial history reset.
- Bot matches explicitly labeled Practice Ranked; local leaderboard entries only.

## Deliberately deferred
- Claude proposed placement-based scoring and higher-tier points. This first prototype uses five stars per tier, +1 survival victory / -1 loss, tier floors and a Sovereign cap. RankPoints is stored (+20/-5) for future rules, not mixed into collection scores. These rules are shown in the game.
- Cross-profile gift abuse, received-sticker provenance, daily caps, and online account/server authority require backend decisions. No arbitrary level gates added.
- JavaScript adapters must transport Int64 sticker amounts as decimal strings/BigInt or validate their numeric range; do not silently convert to Number. Current Unity save and transactions preserve Int64 values.
- Local backup recovery can roll back an entire acknowledged transaction; cross-device receipt durability needs a server database. This prototype never recovers just one participant's balance.
- Save locking is in-process. Do not run multiple game instances against one real account file.
