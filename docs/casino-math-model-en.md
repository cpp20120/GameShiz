# CasinoShiz: casino bot mathematical model

Snapshot date: 2026-06-22.

This document describes the current code model, not the desired balance. Main sources:
`games/Games.*/Application/Services/*`, `games/Games.*/Domain/Configuration/*`,
`games/Games.Meta/*`, `framework/BotFramework.Host/Economics/*`.

## 1. Notation

- `B_t` - player balance before the operation.
- `s` - player stake.
- `p` - player payout after the outcome.
- `m` - payout multiplier, so `p = s * m`.
- `EV[p]` - expected payout value.
- `RTP = EV[p] / s` - return-to-player without external bonuses.
- `edge = 1 - RTP` - expected share of the stake retained by the system.
- `net = p - s` - player's net result, because the stake is debited before the outcome.

Most games use the same economy:

```text
B_after = B_before - s + p
EV[net] = EV[p] - s = s * (RTP - 1)
```

Debit and credit operations are idempotent through operation ids, so repeated event delivery must not duplicate the result.

## 2. Base coin economy

### 2.1. Stakes and payouts

For games with an upfront stake:

```text
debit(s, reason = "<game>.bet")
roll outcome
credit(p, reason = "<game>.payout") if p > 0
```

The stake is limited by `MaxBet` in the specific game's settings. If the player does not have enough funds, the stake is rejected.

### 2.2. Transfers

Transfer sends `n` coins to the recipient and debits the sender:

```text
fee_raw = n * FeePercent
fee_half = round_away_from_zero(fee_raw * 2) / 2
fee = max(MinFeeCoins, round_away_from_zero(fee_half))
total_debit = n + fee
```

Default:

```text
FeePercent = 0.03
MinFeeCoins = 1
```

The transfer fee is a coin sink.

### 2.3. Daily bonus

Daily bonus:

```text
bonus = min(MaxBonus, floor(balance * PercentOfBalance / 100))
```

If `balance <= 0`, `PercentOfBalance <= 0`, `MaxBonus <= 0`, or `floor(...) < 1`, the bonus is `0`.

Default:

```text
PercentOfBalance = 0.35
MaxBonus = 8
MaxCatchUpDays = 14
```

This is a coin faucet capped at `8` coins per local day.

### 2.4. Gas tax and bank tax

Gas tax:

```text
GasDefault = 0.0285
GasModifier = sqrt(2)

GasFunction(x) = max(1, ((x + 1) ^ log10(x + 1) - 1) / 39.15)

if x < 10:
  gas(x) = round(GasFunction(x) * GasModifier)
else:
  gas(x) = round(x * GasDefault * GasModifier)
```

Current important behavior: `gas(9) = 1`, but `gas(10) = 0` because of rounding in the second branch.

Bank tax:

```text
if bank < 70:       bank_tax = -2
else if bank < 120: bank_tax = 0
else:               bank_tax = round(max(4, min(gas(floor(bank)), 25)) / 2)
```

## 3. Per-game model

### 3.1. Telegram dice games

Telegram dice outcomes are treated as uniformly distributed integer values:

- `dicecube`, `darts`, `bowling`: `face in {1..6}`;
- `football`, `basketball`: `face in {1..5}`;
- `slots`: 64 encoded reel combinations.

#### Dice cube

Default multipliers:

| face | 1 | 2 | 3 | 4 | 5 | 6 |
|---:|---:|---:|---:|---:|---:|---:|
| `m` | 0 | 0 | 0 | 1 | 2 | 2 |

```text
EV[p] = s * (0 + 0 + 0 + 1 + 2 + 2) / 6 = 5s/6
RTP = 83.3333%
edge = 16.6667%
```

`Mult4`, `Mult5`, and `Mult6` are configurable and are snapshotted into the pending bet row. General formula:

```text
RTP = (Mult4 + Mult5 + Mult6) / 6
```

#### Darts

Multipliers:

| face | 1 | 2 | 3 | 4 | 5 | 6 |
|---:|---:|---:|---:|---:|---:|---:|
| `m` | 0 | 0 | 0 | 1 | 2 | 2 |

```text
RTP = 5/6 = 83.3333%
edge = 16.6667%
```

#### Bowling

Multipliers:

| face | 1 | 2 | 3 | 4 | 5 | 6 |
|---:|---:|---:|---:|---:|---:|---:|
| `m` | 0 | 0 | 0 | 1 | 2 | 2 |

```text
RTP = 5/6 = 83.3333%
edge = 16.6667%
```

#### Football

Multipliers:

| face | 1 | 2 | 3 | 4 | 5 |
|---:|---:|---:|---:|---:|---:|
| `m` | 0 | 0 | 0 | 2 | 2 |

```text
EV[p] = s * 4/5
RTP = 80%
edge = 20%
```

#### Basketball

Multipliers:

| face | 1 | 2 | 3 | 4 | 5 |
|---:|---:|---:|---:|---:|---:|
| `m` | 0 | 0 | 0 | 2 | 2 |

```text
RTP = 80%
edge = 20%
```

### 3.2. Slots (`/dice`, Telegram slot machine)

Current `DiceOptions.Cost = 9`. Gas is added on top:

```text
loss = Cost + gas(Cost)
```

For the default:

```text
gas(9) = 1
loss = 10
```

The Telegram slot value is decoded into 3 reels with 4 symbols:

```text
roll_0 = ((value - 1) >> 0) & 3
roll_1 = ((value - 1) >> 2) & 3
roll_2 = ((value - 1) >> 4) & 3
```

Symbols and sum weights:

| index | symbol | stake price |
|---:|---|---:|
| 0 | bar | 1 |
| 1 | cherry | 1 |
| 2 | lemon | 2 |
| 3 | seven | 3 |

Payout:

```text
rolls_sum = sum(stake_price[roll_i])

three seven  -> 77
three lemon  -> 30
three cherry -> 23
three bar    -> 21
two seven    -> 10 + rolls_sum
two lemon    -> 6 + rolls_sum
any other pair -> 4 + rolls_sum
no pair      -> rolls_sum - 3
```

Across all 64 equally likely outcomes:

```text
sum(payouts) = 610
EV[p] = 610 / 64 = 9.53125
```

For the default `loss = 10`:

```text
RTP = 9.53125 / 10 = 95.3125%
edge = 4.6875%
EV[net] = -0.46875 coins per spin
```

If `Cost` changes, RTP relative to the debit changes:

```text
RTP(Cost) = 9.53125 / (Cost + gas(Cost))
```

### 3.3. Blackjack

Model:

- the deck is shuffled randomly;
- start: player gets 2 cards, dealer gets 2 cards;
- player's natural blackjack at the start settles immediately;
- dealer draws while total is `< 17`;
- double is allowed only on the player's first two cards;
- double debits one more stake and doubles `Bet`;
- ace counts as 11 when it does not bust, otherwise as 1.

Settlement formula:

```text
if player_total > 21: payout = 0
else if player_blackjack and not dealer_blackjack: payout = bet + floor(3 * bet / 2)
else if player_blackjack and dealer_blackjack: payout = bet
else if dealer_total > 21: payout = 2 * bet
else if player_total > dealer_total: payout = 2 * bet
else if player_total < dealer_total: payout = 0
else: payout = bet
```

Natural blackjack pays `2.5x` the stake with integer `floor(1.5 * bet)` in the bonus part.

There is no closed-form RTP in the code because it depends on player strategy: hit/stand/double are user choices. Auditing it requires a separate Monte Carlo simulation or exact enumeration by strategy.

### 3.4. Pick

The user defines `N` variants and chooses `k` backed variants. Win:

```text
P(win) = k / N
gross = s * N / k
base_payout = floor(gross * (1 - HouseEdge))
```

Default:

```text
HouseEdge = 0.05
MinVariants = 2
MaxVariants = 10
MaxBet = 5000
```

Without streak bonus:

```text
EV[p] ~= s * (1 - HouseEdge)
RTP ~= 95%
edge ~= 5%
```

Because of `floor`, actual RTP is slightly below 95% for small stakes.

Streak bonus applies only to top-level Pick, not to chain:

```text
factor = min(max(0, streak_after - 1), StreakCap)
bonus = floor(s * factor * StreakBonusPerWin)
total_credit = base_payout + bonus
```

Default:

```text
StreakBonusPerWin = 0.5
StreakCap = 4
ChainMaxDepth = 5
```

Consequence: on a long winning streak, the streak bonus can exceed the house edge and become a coin faucet. This is not an implementation bug; it is the current balance behavior.

Chain:

```text
stake_next = total_credit
```

Chain does not apply streak math, but lets winnings be reinvested up to `ChainMaxDepth`.

### 3.5. Pick lottery

Pool lottery:

```text
pot = sum(stake_i)
if entrants < MinEntrantsToSettle:
  refund all stakes
else:
  winner ~ Uniform(entries)
  fee = floor(pot * HouseFeePercent)
  payout = pot - fee
```

Default:

```text
MinEntrantsToSettle = 2
HouseFeePercent = 0.05
```

For an entrant with one equal stake `s` among `n` entrants:

```text
P(win) = 1/n
EV[p] = (pot - fee) / n
```

If all stakes are equal and `fee ~= 5%`, RTP is around `95%`.

Daily lottery uses the same idea with tickets:

```text
pot = ticket_count * TicketPrice
winner probability = user_tickets / total_tickets
payout = pot - floor(pot * HouseFeePercent)
```

### 3.6. Horse race

Horse race is a pari-mutuel pool with odds that depend on stake distribution.

For horse `h`:

```text
S_h = total stake on horse h
P = total pot

if S_h = 0:
  k_h = 1.0
else:
  k_h = floor(((P - S_h) / (1.1 * S_h)) * 1000) / 1000 + 1
```

If `h` wins, each stake on it pays:

```text
payout_i = floor(stake_i * k_h)
```

Total for the winning horse:

```text
total_payout_h ~= S_h + (P - S_h) / 1.1
```

Expected hold before rounding:

```text
house_hold_h = P - total_payout_h
             = P - S_h - (P - S_h) / 1.1
             = (P - S_h) / 11
```

Hold depends on which horse wins and how much was staked on it. If a favorite with large `S_h` wins, hold is lower; if an underdog wins, hold is higher.

The winner is generated in `SpeedGenerator.GenPlaces(HorseCount)`. Full horse EV requires separately checking the winner distribution in that generator.

## 4. Meta progression

### 4.1. XP per game

The formula is read from `meta_seasons.config.xp`. If JSON is missing or invalid, defaults are used:

```text
play = 5
win = 25
loss = 2
stakeMultiplier = 0.01
minXpPerGame = 1
maxXpPerGame = 500
```

Calculation:

```text
base_xp = is_win ? config.xp.win : config.xp.loss
stake_xp = floor(max(0, stake) * config.xp.stakeMultiplier)
xp_delta = clamp(config.xp.play + base_xp + stake_xp, minXpPerGame, maxXpPerGame)
```

With defaults:

```text
win:  xp = clamp(30 + floor(0.01 * stake), 1, 500)
loss: xp = clamp(7  + floor(0.01 * stake), 1, 500)
```

### 4.2. Rating

Rating is read from `meta_seasons.config.rating`:

```text
enabled = true
start = 1000
winDelta = 16
lossDelta = -12
```

Calculation:

```text
rating_delta = enabled ? (is_win ? winDelta : lossDelta) : 0
rating_after = max(0, rating_before + rating_delta)
```

Initial rating on the first record:

```text
rating_initial = max(0, start + rating_delta)
```

### 4.3. Level curve

Level curve is read from `meta_seasons.config.levels`:

```text
xpPerLevelSquaredBase = 100
```

```text
level(xp) = max(1, floor(sqrt(xp / xpPerLevelSquaredBase)) + 1)
xp_floor(level) = xpPerLevelSquaredBase * (level - 1)^2
```

Examples:

| level | minimum XP |
|---:|---:|
| 1 | 0 |
| 2 | 100 |
| 3 | 400 |
| 4 | 900 |
| 5 | 1600 |
| 10 | 8100 |

### 4.4. Seasons

Current season model:

```text
DefaultDurationDays = 14
DefaultPreparedSeasonCount = 30
```

Runtime behavior:

1. expired active season is moved to `finished`;
2. planned season that covers `now` is moved to `active`;
3. if there is no matching planned season, a new 14-day active season is created;
4. the planned seasons queue is filled up to 30 future seasons.

`SeasonPlanFactory` generates different JSON config themes:

```text
balanced
quest_rush
high_roller
clan_wars
tournament_arc
jackpot_hunt
streak_sprint
risk_watch
```

XP, rating start, rating delta, and level curve are applied from the season JSON config.

Season rewards are also read from the season JSON config:

```text
rewards.playerTop = [5000, 2500, 1000]
rewards.clanTop   = [10000, 5000, 2500]
```

Actual payout by place:

```text
reward(place) =
  rewards[place - 1], if place is within the array
  0, otherwise
```

Automatic rollover performs the same lifecycle as manual activation: expired active season is closed, a matching planned season is activated, and the future season queue is refilled. Rewards for finished seasons are processed idempotently through operation ids, so a repeated job must not duplicate payouts.

## 5. Quest rotation

The quest catalog is stored in `games/Games.Meta/Infrastructure/Catalog/quest-pool.json` and materialized into a set of `QuestTemplate`.

The active rotation is selected deterministically:

```text
seed = season_id : chat_id : user_id : period_key : slot_id : index
```

Periods:

```text
daily  -> yyyy-MM-dd
weekly -> ISO yyyy-Www
```

Weighted selection:

```text
common    -> 100
uncommon  -> 60
rare      -> 25
epic      -> 10
legendary -> 4
```

`meta_seasons.config.quests.focus` narrows the candidate pool when matching quests exist in the catalog. Supported practical focuses:

```text
all-round / balanced / normal -> no narrowing
daily / weekly                -> quests for the selected period
volume                        -> volume/high_stake
payout                        -> payout/profit/multiplier
streaks                       -> streak tags/clusters
clans                         -> clan tags
tournaments                   -> tournament tags
controlled                    -> low_stake/play/loss
```

`meta_seasons.config.quests.rarityBias` changes effective weight:

```text
normal   -> no change
uncommon -> uncommon * 2
rare     -> rare/epic/legendary * 3
epic     -> epic/legendary * 4
common   -> common * 2, others / 2
```

This does not bypass the progress gate: rare or heavy quests still will not be assigned to a new player if they specify `MinLevel`, `MinGamesPlayed`, or `MinTotalStaked`.

Repeatability is reduced through:

- blocking the same quest id in one active assignment;
- blocking the same repeat key in one active assignment;
- soft block by previous periods up to `RepeatCooldownPeriods`.

Progress gate:

```text
quest unlocked iff
  player.level >= MinLevel
  and player.games_played >= MinGamesPlayed
  and player.total_staked >= MinTotalStaked
```

Quest progress delta:

```text
volume -> stake
payout -> payout
profit -> max(0, payout - stake)
other  -> 1
```

Condition filters:

```text
MinStake
MaxStake
MinPayout
MinProfit
MinMultiplier
```

## 6. Faucets, sinks, and expected drift

### 6.1. Main sinks

- negative EV dice games;
- transfer fee;
- pick/pick lottery rake;
- horse pool hold;
- gas tax on slots;
- possible admin/economy operations when used manually.

### 6.2. Main faucets

- daily bonus;
- quest rewards: XP and coins;
- season rewards;
- clan rewards;
- redeem drops;
- Pick streak bonus on long streaks.

### 6.3. Conditional drift model

For period `T`:

```text
DeltaSupply(T) =
  sum_game_payouts(T)
  + sum_daily_bonus(T)
  + sum_quest_coin_rewards(T)
  + sum_season_rewards(T)
  + sum_redeem_rewards(T)
  - sum_stakes_debited(T)
  - sum_transfer_fees(T)
  - sum_lottery_fees(T)
```

For games with RTP:

```text
EV[DeltaSupply_game] = -sum_stakes * edge
```

If the economy needs to remain stable, the faucet budget should not exceed the expected sink:

```text
daily_bonus + quest_coins + season_rewards + redeem_value
  <= expected_house_edge + transfer_fees + lottery_fees
```

The admin page `/admin/meta-economy` calculates a rough simulation for selected `days`, `players`, average stake/RTP, and seasonal reward pool, then shows the actual ledger drift from `economics_ledger` next to it. This is a quick sanity check, not Monte Carlo.


## 7. Quick Balance Summary

| Module | RTP / effect | Edge / sink |
|---|---:|---:|
| DiceCube | 83.3333% | 16.6667% |
| Darts | 83.3333% | 16.6667% |
| Bowling | 83.3333% | 16.6667% |
| Football | 80.0000% | 20.0000% |
| Basketball | 80.0000% | 20.0000% |
| Slots default | 95.3125% | 4.6875% |
| Pick base | around 95% | around 5% |
| Pick lottery | around 95% | around 5% |
| Horse | depends on pool | approximately `(P - S_winner) / 11` |
| Transfer | no RTP | 3% fee, minimum 1 |
| Daily bonus | faucet | up to 8 coins/day |
