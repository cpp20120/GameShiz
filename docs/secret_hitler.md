# Optimal Behavior of a Hidden Coalition in *Secret Hitler*

**A probabilistic analysis of policy-oriented Fascist strategies**

## Contents

1. [Notation](#notation)
2. [Mathematical model](#mathematical-model)
3. [Information and strategy](#information-and-strategy)
4. [Critical strategies](#critical-strategies)
5. [One-round policy probability](#one-round-policy-probability)
6. [Markov model](#markov-model)
7. [Numerical results](#numerical-results)
8. [Limitations](#limitations)
9. [Conclusion](#conclusion)

## Notation

| Symbol | Meaning |
|---|---|
| $C$ | Set of players |
| $N=\lvert C\rvert$ | Number of players, $N\in\{5,6,7,8,9,10\}$ |
| $L,F,H$ | Liberal, Fascist, and Hitler roles |
| $A_t$ | Policy-card pool available at round $t$ |
| $X_t$ | Number of Liberal cards among the President's three cards |
| $Y_t$ | Indicator that a Fascist policy is enacted in round $t$ |
| $I_t(p)$ | Information available to player $p$ after round $t$ |
| $P(d_F),P(d_H)$ | Probability that a Fascist or Hitler discards a Liberal card when able |
| $\beta$ | Probability that the current President belongs to the Fascist coalition |
| $(n_L,n_F)$ | Number of enacted Liberal and Fascist policies |

## Mathematical Model

### Game structure

Treat *Secret Hitler* as a finite stochastic game with incomplete information and hidden roles. Let

$$
C=\{p_1,\ldots,p_N\}, \qquad N\in\{5,6,7,8,9,10\},
$$

and assign each player a fixed hidden role:

$$
\operatorname{role}(p)\in\{L,F,H\}.
$$

The role distribution is fixed by the official setup for $N$ players. Play proceeds in discrete rounds $t=1,2,\ldots,T$, where the stopping time $T$ depends on the realized game path.

### Policy deck

Initially, the deck contains 17 policies:

$$
\lvert A_1^L\rvert=6, \qquad \lvert A_1^F\rvert=11, \qquad
A_1=A_1^L\cup A_1^F.
$$

In each successful legislative session, the President draws three cards without replacement, discards one, and passes two to the Chancellor. The Chancellor discards one of those cards and enacts the other.

Let $X_t$ be the number of Liberal policies in the President's draw. Conditional on the current pool, $X_t$ has a hypergeometric distribution:

$$
\Pr(X_t=k)=
\frac{\binom{\lvert A_t^L\rvert}{k}
      \binom{\lvert A_t^F\rvert}{3-k}}
     {\binom{\lvert A_t\rvert}{3}},
\qquad k\in\{0,1,2,3\}.
$$

## Information and Strategy

### Information states

The information state $I_t(p)$ contains everything player $p$ has publicly observed by round $t$:

- enacted policies;
- Presidential and Chancellorial claims;
- election results;
- publicly known executive actions.

It excludes hidden roles and cards privately seen by other players. An observer therefore maintains epistemic beliefs such as

$$
\Pr\!\left(\operatorname{role}(q)=r\mid I_t(p)\right),
\qquad q\in C,\quad r\in\{L,F,H\}.
$$

Given an observable round outcome $O_t$, these beliefs can be updated by Bayes' rule:

$$
\Pr(r\mid I_t)=
\frac{\Pr(O_t\mid r,I_{t-1})\Pr(r\mid I_{t-1})}
{\sum_{r'\in\{L,F,H\}}
 \Pr(O_t\mid r',I_{t-1})\Pr(r'\mid I_{t-1})}.
$$

### Coalition strategy

The model focuses on the President's discard decision when the draw contains both policy types. Define:

$$
P(d_F),P(d_H)\in[0,1],
$$

where $P(d_F)$ and $P(d_H)$ are the probabilities that a Fascist President or Hitler, respectively, discards a Liberal policy and later claims that no Liberal-preserving choice was available.

The parameters are separated because the roles are strategically asymmetric. Exposure of Hitler is substantially more costly than exposure of an ordinary Fascist, especially once three Fascist policies have been enacted.

Liberal Presidents are assumed to preserve a Liberal policy whenever possible and to report their draw truthfully. This is a modeling assumption, not a claim about optimal play in the full game.

### Objective

The Fascist coalition has two victory conditions:

1. **Election victory** $W_E$: Hitler is elected Chancellor after at least three Fascist policies have been enacted.
2. **Policy victory** $W_A$: six Fascist policies are enacted.

The full objective is

$$
V(P(d_F),P(d_H))=
\Pr\!\left(W_A\cup W_E\mid P(d_F),P(d_H)\right).
$$

The reduced model below evaluates only $W_A$. It therefore measures the strength of policy acceleration, not total Fascist win probability.

## Critical Strategies

Two boundary strategies are considered:

| Strategy | $P(d_F)$ | $P(d_H)$ | Behavior when a Liberal discard can force a Fascist policy |
|---|---:|---:|---|
| Honest | 0 | 0 | Preserve the Liberal option and report truthfully |
| Always lie | 1 | 1 | Discard the Liberal card and deny having a choice |

To isolate the President's contribution, the reduced model makes two strong assumptions:

- all public claims are trusted, so reputational and Bayesian penalties are suppressed;
- the Chancellor always enacts a Liberal policy when the received pair contains one.

These assumptions intentionally favor a clean calculation over a complete simulation of social play.

## One-Round Policy Probability

Define

$$
Y_t=
\begin{cases}
1,&\text{if a Fascist policy is enacted in round }t,\\
0,&\text{otherwise.}
\end{cases}
$$

The four possible Presidential draws behave as follows under the Chancellor assumption:

| $X_t$ | Draw | Honest President | Coalition President using “always lie” |
|---:|---|---|---|
| 0 | $\{F,F,F\}$ | $F$ is forced | $F$ is forced |
| 1 | $\{L,F,F\}$ | $L$ is enacted | Discard $L$; $F$ is forced |
| 2 | $\{L,L,F\}$ | $L$ is enacted | $L$ remains available and is enacted |
| 3 | $\{L,L,L\}$ | $L$ is forced | $L$ is forced |

Thus an honest President produces a Fascist policy only when $X_t=0$. The always-lie strategy can additionally convert the $X_t=1$ case, but only when the President belongs to the Fascist coalition. Therefore,

$$
\Pr(Y_t=1\mid S_{\text{honest}})=\Pr(X_t=0),
$$

$$
\Pr(Y_t=1\mid S_{\text{lie}})=
\Pr(X_t=0)+\beta\Pr(X_t=1).
$$

This is the central structural result: Presidential deception can affect only draws containing exactly one Liberal and two Fascist policies under the reduced assumptions.

### Coalition Presidency Rate

With an uninterrupted cyclic Presidency, $\beta$ equals the Fascist coalition's share of the table:

$$
\beta=\frac{\lvert C_F\rvert+\lvert C_H\rvert}{\lvert C\rvert}.
$$

| Players | Liberals | Fascists | Hitler | $\beta$ |
|---:|---:|---:|---:|---:|
| 5 | 3 | 1 | 1 | 0.400 |
| 6 | 4 | 1 | 1 | 0.333 |
| 7 | 4 | 2 | 1 | 0.429 |
| 8 | 5 | 2 | 1 | 0.375 |
| 9 | 5 | 3 | 1 | 0.444 |
| 10 | 6 | 3 | 1 | 0.400 |

Within this model, nine-player games give the coalition the highest Presidential share, followed by seven-player games.

## Markov Model

Let the state be

$$
S=(n_L,n_F),qquad n_L\in\{0,\ldots,5\},\quad
n_F\in\{0,\ldots,6\}.
$$

To make the state sufficient, assume that before each round all non-enacted policies are uniformly mixed into one pool. The remaining counts are

$$
R_L=6-n_L,\qquad R_F=11-n_F,\qquad
R=17-n_L-n_F.
$$

For the always-lie strategy, the probability of moving from $(n_L,n_F)$ to $(n_L,n_F+1)$ is

$$
p_{n_L,n_F}=
\frac{\binom{R_L}{0}\binom{R_F}{3}}
     {\binom{R}{3}}
+\beta
\frac{\binom{R_L}{1}\binom{R_F}{2}}
     {\binom{R}{3}}.
$$

The first term is a forced all-Fascist draw. The second is the coalition's opportunity to convert an $\{L,F,F\}$ draw.

Let $W_A(n_L,n_F)$ be the probability of an eventual Fascist policy victory. Backward induction gives

$$
W_A(n_L,n_F)=
p_{n_L,n_F}W_A(n_L,n_F+1)
+(1-p_{n_L,n_F})W_A(n_L+1,n_F),
$$

with absorbing boundaries

$$
W_A(n_L,6)=1,qquad W_A(5,n_F)=0.
$$

## Numerical Results

Solving the recurrence from the initial state $(0,0)$ gives the following policy-only victory probabilities:

| Players | $\beta$ | $W_A(0,0)$ under always lie |
|---:|---:|---:|
| 5 | 0.400 | 39.52% |
| 6 | 0.333 | 31.74% |
| 7 | 0.429 | 43.03% |
| 8 | 0.375 | 36.53% |
| 9 | 0.444 | **45.01%** |
| 10 | 0.400 | 39.52% |

For comparison, setting $\beta=0$ removes every strategic conversion opportunity and leaves only forced $\{F,F,F\}$ draws. The resulting policy-victory probability is approximately **6.17%**.

The calculation shows that aggressive policy manipulation has a large effect in the reduced model, raising the probability by roughly 25.6 to 38.8 percentage points depending on player count. Nevertheless, even under assumptions that remove reputational costs and Chancellor resistance, the best listed configuration remains below 50%.

This does **not** imply that the Fascist coalition wins fewer than half of real games. The model excludes Hitler's election, executive powers, failed-election top-decks, player-driven nominations and votes, investigations, executions, and strategic Chancellors.

## Limitations

The model is analytically useful but deliberately narrow:

- **Deck state is approximated.** Real discarded cards are not immediately reshuffled after every round. Treating all non-enacted cards as one uniformly mixed pool loses information about the draw pile and discard pile.
- **Presidential rotation is simplified.** Special elections, failed governments, eligibility restrictions, and executions alter the probability that a coalition member becomes President.
- **Chancellor behavior is fixed.** A Fascist Chancellor can manipulate mixed pairs, while a Liberal Chancellor may make errors or strategic choices.
- **Social inference is suppressed.** Repeated suspicious claims can change nominations and votes. In real play, deception has an endogenous reputational cost.
- **Only policy victory is valued.** A policy-maximizing move may reduce the probability of electing Hitler, even though election is a primary coalition victory condition.
- **Role asymmetry is collapsed numerically.** The recurrence uses the aggregate $\beta$ and does not distinguish the strategic cost of exposing Hitler from exposing another Fascist.

Consequently, the recurrence should be read as a benchmark for one mechanism, not as a complete optimal strategy for the game.

## Conclusion

Under the reduced assumptions, always discarding a Liberal policy from an $\{L,F,F\}$ hand is the strongest policy-acceleration strategy available to a Fascist President. Its influence is precisely bounded: it changes the enacted policy only for that one draw composition and only during a coalition Presidency.

The strategy substantially increases the chance of winning through six Fascist policies, from about **6.17%** without strategic conversion to at most **45.01%** in the nine-player configuration. However, it still does not produce a majority policy-victory probability, even in a model that ignores distrust and assumes no penalty for lying. This supports the central conclusion that a pure “win through policies as quickly as possible” strategy is insufficient on its own.

Optimal coalition play must therefore balance three competing objectives: accelerating Fascist policies, preserving Hitler's credibility for a later election victory, and controlling the information revealed by legislative claims. A fuller solution requires a partially observable stochastic-game model in which nomination, voting, executive actions, deck state, and posterior role beliefs are part of the state. The Markov model developed here remains useful as a transparent lower-dimensional benchmark against which those richer strategies can be compared.
