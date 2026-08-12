# DUNGEON LEADERBOARD — Implementation Spec

> This is the author's design, recorded verbatim. It is the authority on what this game is.
> If the implementation and this document disagree, this document is right until it is explicitly
> superseded by a dated entry in `DECISIONS.md`.

**Game jam entry. Theme: CHARGE!**

> You're a dungeon core in a competitive league. Adventurers charge in, you charge up. Milk them for
> every drop of energy, keep them alive, and don't finish bottom 10%.

---

## 1. Concept

2D top-down dungeon management game. The player is a dungeon core that harvests energy from
adventuring parties raiding it. Energy is score, score is league position, bottom 10% is eliminated.

The central inversion: **killing the adventurers is bad play.** A dead party stops generating energy.
So does a party that reaches the boss room too quickly. The optimal outcome is a party that is alive,
engaged in combat, badly wounded, and still inside the dungeon when the timer expires.

The theme is hit twice: adventurers *charge in*, the core *charges up*.

---

## 2. Core loop

| Phase | Duration | Player activity |
|---|---|---|
| **Raid** | 60s hard cap | Real-time: stall, ambush, and wound the party to maximise energy rate |
| **Shop** | 30s countdown | Spend harvested energy on rooms, mobs, traps. "Ready" button skips ahead for a bonus |

Total cycle: 90 seconds max. The player is always on a visible clock.

A run consists of repeated cycles until the player is relegated.

---

## 3. The raid phase (the actual game)

### Timer
- Hard 60-second cap, counting down, always visible.
- Run ends early — and the remaining seconds are lost — if:
  - the party wipes (all members dead), or
  - the party reaches the boss room and exits.
- Ending early is a **loss of earning window**, not a special fail state. No separate penalty needed;
  the lost time is the punishment.

### Energy (score)
Energy accrues continuously during the raid. The rate is the whole design.

```
energyRate = baseRate
           * engagementMultiplier
           * woundMultiplier
```

- `baseRate` — small constant, near-zero on its own.
- `engagementMultiplier` — scales with the number of party members currently in combat. A party
  walking down an empty corridor should generate **almost nothing**.
- `woundMultiplier` — scales sharply as party HP drops. Full HP ≈ 1x. Around 20% HP ≈ 4x. Around 5%
  HP ≈ 8x+. Tune by feel; the curve should be steep enough that the last sliver of HP is where most
  of the money is.

Display the current rate as a large pulsing number next to the total. The player must be able to see,
at a glance, that dead time is costing them.

### Player verbs (exactly three for the demo)
1. **Toggle door** — cheap/free, spammable. Opens and closes doors to steer the party and stall them.
   This is the primary "luring" verb, and doubles as the only way to save a losing party (see below).
2. **Spawn mob** — costs energy. Player chooses which spawner fires and when. Ambushing from behind
   the party should be worth more than a frontal engagement.
3. **Fire trap** — costs energy, one-shot with a cooldown. Damages the party.

Do not add more verbs until these three are proven fun.

### Preventing kills — no direct mob control
The player must **never** be able to call mobs off. Monsters that retreat mid-swing make it obvious
the dungeon is being puppeteered, and the fiction that these are dumb creatures in a cave is worth
protecting.

Instead, the safety valve is environmental: **open an escape route and let the party save
themselves.** Mobs do not pursue past a room threshold, so opening a door behind a losing party lets
them break off and retreat to heal. This costs the player the fight (and its energy rate) but
prevents the wipe.

This is the central regret and it uses no new verb — the same door that stalls them is the same door
that rescues them. Opening it too early throws away energy; too late and someone dies. Mob pathing
must respect room boundaries for this to work, so build that in from the start.

### Reading the party
Do not show exact HP bars on adventurers. Communicate wounded state through visible signals —
limping, slowed movement, blood, the healer visibly panicking and burning cooldowns. Ambiguity
between "nearly dead" and "dead in one hit" is where the tension lives.

(If this proves unreadable in playtesting, fall back to coarse three-state indicators: healthy /
hurt / critical. Never a precise number.)

---

## 4. Adventurer parties

Party of four. Archetypes:

| Role | Behaviour |
|---|---|
| **Tank** | High HP, low damage, draws mob aggro. Sword and shield — *walking* tank, not a vehicle. |
| **Healer** | Heals wounded allies from a limited mana pool. **The player's best customer** — sustains the party through repeated near-death, generating enormous energy. |
| **Ranged** | Attacks mobs from a distance, fragile. |
| **Mage** | Burst damage, fragile, limited resource. |

Party AI: pathfind toward the boss room, engage mobs encountered en route, retreat/heal when
critical, exit when the boss room is reached or the timer expires.

Party composition is the primary source of run-to-run variation. A tanky party and a glass-cannon
party play completely differently in the same dungeon layout — exploit this before adding more
dungeon content.

---

## 5. Shop phase

- 30-second visible countdown. Next party enters at zero.
- A **"Ready"** button starts the next raid early and grants an energy bonus scaled to the time
  skipped. Greedy players skip planning.
- Six purchasable items at jam scope: two mob types, two trap types, a door, a chest.
- Chests placed in the dungeon give the party a reason to detour — useful stalling infrastructure.

---

## 6. The league

**The leaderboard is the title screen.** The game opens directly on the standings. No menu, no logo
screen. Press any key and the first party enters.

- 20 dungeons, procedurally named, player highlighted.
- Player starts around 14th.
- Red relegation line under the bottom two positions (bottom 10%).
- AI dungeon scores fluctuate plausibly each round so the standings move.
- Standings visible as a strip during play; positions update after each raid with a visible shift
  animation.
- Finishing in the bottom 10% after a raid ends the run: dungeon collapses, a new name takes the
  slot, show final position, one key to restart.

This is the 10-second hook. A new player reads the screen and immediately understands: *I am 14th,
16th is death, I need to climb.*

---

## 7. Build order

Ship in this order. The game is presentable from Milestone 2 onward.

### Milestone 1 — The sixty seconds (~3h)
One corridor. One party of four entering from the left, boss room on the right. 60s countdown. Energy
counter and rate display. Three buttons: door, spawn, trap. Run ends on wipe / boss room / 0:00,
showing total energy harvested.

**Gate: is stalling a party with doors for a full minute satisfying?** If no, stop and fix this
before building anything else. Nothing downstream rescues a dull core loop.

### Milestone 2 — The league (~3h)
Standings screen as title screen. 20 dungeons, live position, relegation line, position shifts after
each raid, relegation ends the run.

*Shippable from here.*

### Milestone 3 — The shop (~4h)
30s timer, six items, Ready button with early-start bonus.

### Milestone 4 — Everything else
Party comp variation, mob variety, chests, art pass, end-of-raid star reviews.

---

## 8. Nice-to-haves (only if time remains)

- **Star reviews.** Departing parties leave a rating and a one-line comment. Teaches the player what
  went wrong without a tutorial, and it's the best screenshot the game has.
  - ★★★★☆ *"Great atmosphere, boss fight was a real nail-biter. Third corridor dragged a bit."*
  - ★★☆☆☆ *"Walked in, killed six slimes, left with a copper ring. My nan's cellar is scarier."*
- **Named recurring adventurers.** A regular who has cleared your dungeon several times makes an
  accidental kill genuinely sting.
- **Tiered mobs** unlocked as core charge increases.

---

## 9. Feel and tone

Cute, readable top-down pixel or simple vector art. The joke is that an ancient eldritch horror is
anxious about its performance review — lean into the mundane bureaucracy of the league framing
against the dungeon fantasy.

Juice matters more than content. Spend polish time on: party members visibly panicking, mobs bursting
out of spawners, trap impacts, the energy rate number pulsing when it spikes, and the standings
animating when the player's position moves.

---

## 10. Open questions for the implementer

- Engine and target platform (Unity 2D assumed; WebGL build strongly recommended for jam voting
  traffic).
- Input scheme: mouse-click on dungeon elements is probably the cleanest for the three verbs.
- Dungeon layout: fixed rooms placed on a grid, or freeform? Grid is faster to build and easier to
  pathfind. Grid is recommended.

### Answered — see DECISIONS.md

| Question | Answer |
|---|---|
| Engine | Unity 6000.3.17f1, 2D URP (Renderer2D) |
| Platform | WebGL, published to itch.io. No GitHub Pages for this project. |
| Input | Mouse-click on dungeon elements |
| Layout | Fixed rooms on a grid |
