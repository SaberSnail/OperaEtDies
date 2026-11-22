# How Steps Are Carried Out

This describes how the engine carries out the steps of needs (see [DataFiles.md](DataFiles.md) for their format). The rule behind all of it: steps are carried out by characters, the same way for everyone. NPCs and the player differ only in who fulfils each step and who makes each decision.

## Sequences

A **sequence** (`StepSequence`) is one run through a need's steps by one character. It holds everything the steps keep track of:

* the need and the step it's on (its cursor);
* what's left of the time chosen for it (its shift);
* its partner, who it's seeking, the places it remembered, and its bundles;
* whether the current step has been carried out (its tasks were given);
* the appointment it's for, if any, and whether the character has set off for it;
* a decision it's waiting on, if any;
* how much of it is being done for the player (none, the current step, or all of it).

A character can have several sequences. One is **current**, the others are **on hold**, and those for appointments the character hasn't set off for yet are **waiting**.

* An appointment gives each of the two a waiting sequence as soon as it's made: the worker's runs through the appointment's steps, and the client's is their part in them. A waiting sequence is never current, and none of its steps are carried out, until it's time to set off and the character decides to (see Decisions below).
* An NPC has one at a time (besides waiting ones), except that setting off for an appointment for the same need puts the current one on hold. When the current one ends, the most recent sequence on hold resumes.
* The player can start another sequence while in the middle of one, which puts the first on hold. Every one of the player's sequences is checked after anything the player does, so finishing a step in one (such as putting the water away) counts whichever sequence it's in.

Sequences are saved in the person's record. Nothing else about steps is kept on the person.

## Carrying out a step

Each kind of step has a handler (`StepHandler`) that says:

* **Check:** whether the step is done, not done, or has failed (and why). It's checked every time the step is reached, resumed, or something changes. A step that's already done is passed, but no step is ever skipped.
* **Carry out:** how the game carries it out: the tasks to do, an instant change, a decision to make, or a failure.
* **Player:** what the player can do toward it themselves: nothing (they can only have it done for them), act through the world (walk, change clothes, use the inventory), press a button (the step's `player.title`), choose (a decision), or ask in conversation.

The kinds fall into four groups:

| Group | Kinds | Done when | The game does it by | The player does it by |
|---|---|---|---|---|
| Conditions | `goTo`, `take`, `put`, `dress`, `undress`, `wait`, `wander` | The condition holds (at the place, carrying or wearing the things, or the time is up) | Tasks (walking, changing), then checking again; still not done is a failure | Doing it themselves in the world, or having it done for them |
| Meeting | `meet` | The appointment starts, once both are there at its time | Walking there, then waiting until its time and for the other; once the wait is over, a failure | Walking there and being there, or having it done for them |
| Effort | `do`, `seek`, `talk`, `ask` | Its tasks are done | Tasks | A button (`do`), conversation (`talk`, `ask`), looking around themselves (`seek` is done once someone it looks for is there), or having it done for them |
| Instant | `make`, `countRun`, `remember`, `lead`, `release`, `stop` | At once | The change | The same change, at once |
| Decisions | `choose`, `repeat` | Once decided | A rule (frequency, `first`, urgency) | A prompt (except where the data gives a rule, such as `first`) |

Others' steps also ask decisions of the people they involve, made the same way: by a rule for NPCs and by a prompt for the player. Someone asked to chat agrees unless their goal is too urgent, and someone asked a question answers by its chance.

Setting off for an appointment is a decision on the waiting sequence for it. It comes due when there's just time to change, carry out the steps before its `meet` step, and get there its `early` before its time, as estimated from where the character is. An NPC always sets off, since they agreed to it. The player's sequence becomes **due**: time stops, and they're asked whether to set off or call it off. Setting off makes it current and carries on with its steps; calling it off ends both sequences for the appointment.

**Controllers** fulfil the steps. The automatic controller carries out steps as the game does, for NPCs, and for the player while they have a step done for them ("Do it for me") or all of it ("Do it all for me"). The player controller leaves each step to the player, and stops at decisions to prompt them.

## Failure

A step fails with a cause:

| Cause | When |
|---|---|
| `no` | The one asked said no. |
| `nobody` | There's no one to talk with, ask, lead, or find. |
| `unreachable` | There's nowhere to go, or no way to get there. |
| `missing` | What the step needs isn't at hand. |
| `left` | The character left the place partway through. |
| `interrupted` | Another sequence took over. |
| `hours` | The step's hours ended. |
| `noShow` | The other didn't come to an appointment by the end of its `meet` step's wait. |
| `noOption` | No option of a `choose` is valid. |

What happens is the step's `onFail` for that cause, or else that of what it's in (the `choose` option, `repeat`, step list, or need), or else `stop`. The result is one of these:

* `next`: carry on after the step.
* `stop`: give up the sequence.
* `skipTo`: carry on from the step with that `id`.

## Interruptions

* Talking with someone where the character is doesn't interrupt anything. The step goes on afterwards.
* Leaving the place partway (other than through the step's own walking) fails the current step as `left`.
* Setting off for an appointment puts the current sequence on hold if it's for the same need. Otherwise the current sequence fails as `interrupted`. If it isn't given up, it's put on hold at wherever its `onFail` leads.
* A `meet` step that fails always gives up its sequence, whatever its `onFail`, which calls off the appointment and ends the other's sequence for it too.
* A sequence that resumes is checked again, so a step it relies on that no longer holds (being somewhere for a `do`) fails as `left`, and if the clothes a later `undress` step will put back on (those its `dress` replaced) aren't worn, carried, or at home while they're there, the step it's on fails as `missing`.

## Taking part in someone else's steps

A sequence can also be someone's part in another's steps, rather than a run through steps of their own:

* **Client:** the client of an appointment carries out its `meet` step (going there and meeting the worker), then follows them until they're let go. The game walks and waits for NPCs, and the player can have those done for them.
* **Follower:** someone a `lead` step leads follows the leader until they're let go. An NPC follows with a follow task. The player's following is a sequence of its own, so it's clearly shown, and they can stop it.

Following can meet a need of the follower's: a `do` step with `partnerMeets` meets that need for the partner following through it, as `talk` and `ask` steps do for the one talked with.

## Jobs

An `ask` step with a `job` takes on work for the one asked when they say yes, such as thatching their roof: so many hours in all, at a place worked out then (usually their home). The job is world state, outliving the sequence that took it on. Until it's done, `jobSite` is where it is and `hasJob` is met, so a profession's steps can go to the job site while they have a job and look for someone to work for when they don't. Time spent at the job site meeting the need's quota counts toward the job.

The player can be on either side. NPCs can ask the player the question of an `ask` step, or to talk for a `talk` step, and the player answers yes or no themselves, warned of any of their needs a yes would keep them from. A `lead` step's player follows for at most its `minutes`, and a `dress` or `undress` step `withPartner` changes the player's clothes along with the NPC's. They can call off what they're doing with someone in conversation, which lets the other carry on without them.
