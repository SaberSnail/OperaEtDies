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
* how much of it is being done for the player (none, the current step, or all of it);
* while it's leaving its steps, the `finally` steps it's carrying out and still to carry out, and where it's going after them;
* for the player, when they started keeping a bundle its steps made, to finish later.

A character can have several sequences. One is **current**, the others are **on hold**, and those for appointments the character hasn't set off for yet are **waiting**.

* An appointment gives each of the two a waiting sequence as soon as it's made: the worker's runs through the appointment's steps, and the client's is their part in them. A waiting sequence is never current, and none of its steps are carried out, until it's time to set off and the character decides to (see Decisions below).
* An NPC has one at a time (besides waiting ones), except that setting off for an appointment for the same need puts the current one on hold. When the current one ends, the most recent sequence on hold resumes.
* The player can start another sequence while in the middle of one, which puts the first on hold. Starting a quota need measured in time, they can choose a shorter shift than anyone else would, from those the schedules offer. Every one of the player's sequences is checked after anything the player does, so finishing a step in one (such as putting the water away) counts whichever sequence it's in. The exception is one on hold for an appointment, which waits until the appointment is done.

Sequences are saved in the person's record. Nothing else about steps is kept on the person.

## Carrying out a step

Each kind of step has a handler (`StepHandler`) that says:

* **Check:** whether the step is done, not done, or has failed (and why). It's checked every time the step is reached, resumed, or something changes. A step that's already done is passed, but no step is ever skipped.
* **Carry out:** how the game carries it out: the tasks to do, an instant change, a decision to make, or a failure.
* **Player:** what the player can do toward it themselves: nothing (they can only have it done for them), act through the world (walk, change clothes, use the inventory), press a button (the step's `player.title`), choose (a decision), or ask in conversation.

The player changing clothes themselves counts as changing into and out of outfits, as a `dress` or `undress` step would: putting on all of an outfit (other than everyday clothes) they weren't dressed in changes into it, replacing what they took off, and taking all of it off again (or for an outfit of nothing at all, putting anything on) changes out of it. So undressing by hand for a `dress` step leaves the clothes for its `undress` step to put back on, and dressing by hand does that step.

The kinds fall into four groups:

| Group | Kinds | Done when | The game does it by | The player does it by |
|---|---|---|---|---|
| Conditions | `goTo`, `take`, `put`, `dress`, `undress`, `wait`, `wander` | The condition holds (at the place, carrying or wearing the things, or the time is up) | Tasks (walking, changing), then checking again; still not done is a failure | Doing it themselves in the world, or having it done for them |
| Meeting | `meet` | The appointment starts, once one asks the other and they say yes | Walking there, waiting, and asking or answering whether to start; once the wait is over, a failure | Walking there, then asking in conversation (as the worker) or answering a prompt, or having it done for them |
| Effort | `do`, `seek`, `talk`, `ask` | Its tasks are done | Tasks | A button (`do`), conversation (`talk`, `ask`), looking around themselves (`seek` is done once someone it looks for is there), or having it done for them |
| Instant | `make`, `countRun`, `remember`, `lead`, `release`, `stop` | At once | The change | The same change, at once |
| Decisions | `choose`, `repeat` | Once decided | A rule (frequency, `first`, urgency) | A prompt (except where the data gives a rule, such as `first`) |

Others' steps also ask decisions of the people they involve, made the same way: by a rule for NPCs and by a prompt for the player. Someone asked to chat agrees unless their goal is too urgent, and someone asked a question answers by its chance.

Setting off for an appointment is a decision on the waiting sequence for it. It comes due when there's just time to change, carry out the steps before its `meet` step, and get there its `early` before its time, as estimated from where the character is. An NPC always sets off, since they agreed to it. The player's sequence becomes **due**: time stops, and they're asked whether to set off or call it off. Setting off makes it current and carries on with its steps; calling it off ends both sequences for the appointment.

Starting an appointment is a decision too, and never happens by itself. The worker asks the client once both are at its place, even before its time, and the client answers: an NPC says yes if they're ready (there, set off for it, on its `meet` step, and not walking, talking, or asleep), and the player is asked, with time stopped. An NPC worker asks once they're ready themselves; the player asks in conversation. After a no, the one who said it isn't asked again until either of them leaves and comes back, `askToStartAgain` has passed, or the appointment's time comes (if the no was before it), and the client can tell the worker they're ready now (an NPC client does once they're ready, and the player can in conversation), which the worker answers the same way. A yes starts it, and anyone who hadn't set off yet sets off first.

**Controllers** fulfil the steps. The automatic controller carries out steps as the game does, for NPCs, and for the player while they have a step done for them ("Do it for me") or all of it ("Do it all for me"). The player controller leaves each step to the player, and stops at decisions to prompt them.

## Failure

A step fails with a cause:

| Cause | When |
|---|---|
| `no` | The one asked said no, or a `meet` step's wait ended after someone said no to starting the appointment. |
| `nobody` | There's no one to talk with, ask, lead, or find. |
| `unreachable` | There's nowhere to go, or no way to get there. |
| `missing` | What the step needs isn't at hand. |
| `left` | The character left the place partway through. |
| `interrupted` | Another sequence took over. |
| `hours` | The step's hours ended. |
| `noShow` | An appointment didn't start by the end of its `meet` step's wait, and no one said no. |
| `noOption` | No option of a `choose` is valid. |
| `shy` | Someone wouldn't be covered enough for their modesty rule. |

What happens is the step's `onFail` for that cause, or else that of what it's in (the `choose` option, `repeat` or `group`, step list, or need), or else `stop`. The result is one of these:

* `next`: carry on after the step.
* `stop`: give up the sequence.
* `skipTo`: carry on from the step with that `id`. It can't be a `repeat` step the failing step is inside, since it's unclear whether that means going on with the loop or starting it again.
* `loopContinue`: end this pass of the loop (the nearest `repeat` step around the `onFail`, or the one it names), whose `repeat` step decides whether to go round again, and for the player, asks whether to keep going.
* `loopBreak`: leave the loop, carrying on after its `repeat` step.

A step in a `finally` always goes on to the next step.

The player is told, on top of whatever they're doing, when a failure gives up their steps (other than an appointment taking over, which they chose, or one they missed, which they're told of as such). A failure the steps carry on from is only logged, along with the failures of anyone where the player is, but if it ends a pass of a loop, the player is told why when they're asked whether to keep going. A failure names who it was down to, if anyone in particular: who said no, never came, or was too shy.

## Finally steps

The steps of a need, step list, `choose` option, appointment, or `repeat` or `group` step can have `finally` steps, carried out whenever they're left, however that happens, such as changing back out of clothes they changed into. They're laid out after the steps (for a `repeat` step, after the `repeat` step itself, so they come once the loop as a whole is done), so finishing the steps goes on into them.

Leaving the steps any other way, such as skipping elsewhere, leaving a loop, or giving up the sequence, leaves the steps of each list it isn't going on within. The sequence then **unwinds**: it carries out the `finally` steps of each list it's leaving, innermost first, then goes on to where it was going, or ends. Only the lists of the same steps count: an appointment's steps are on their own, so the need's `finally` steps aren't carried out after them. Most of these steps check something that's already done if there's nothing to clean up, such as an `undress` step when nothing was changed into, so they're passed.

A sequence given up from outside **finishes up** the same way, unwinding on its way to its end:

* Setting off for an appointment for another need finishes up first, on the way: the sequence finishing up stays current until it's done, then the appointment's steps carry on. The time to set off allows for it.
* The player pressing Stop is asked how to stop, when there's something left: to finish up (when a `finally` step whose condition is met isn't done), to keep a bundle they carry to finish later (see below), to just stop, or to keep going. Finishing up, they take the `finally` steps as any others, shown as "Finishing up".

A `meet` step that fails, an appointment that's called off, and heading home to cover up all end the sequence at once, without its `finally` steps.

## Keeping a bundle

When the player's sequence would be given up while they carry a bundle a `make` step made, which a later `put` step puts down (such as a bucket of water to take home), they keep it, to finish later, rather than drop it: the sequence carries on from the last `goTo` step before that `put` step (or the `put` step itself), so getting home and putting it away finishes it, counting the run. They're reminded of it once they've carried it for the schedules' `carryWarningDelay`. Anyone the game decides for drops the bundle, as before. A `make` step that replaces a bundle carries over its things, so a bundle can mark how far the steps got, such as washing that's been washed.

## Interruptions

* Talking with someone where the character is doesn't interrupt anything. The step goes on afterwards.
* Leaving the place partway (other than through the step's own walking) fails the current step as `left`. The exception is a `talk` or `ask` step with the one sought: leaving before it's done goes back to the `seek` step before it, so the character carries on looking for someone.
* The player's Wait stops when one of their appointments starts, so they can carry on with its steps.
* Setting off for an appointment puts the current sequence on hold if it's for the same need. It waits there until the appointment is done (going to it isn't leaving), then carries on from wherever the character is. Otherwise the current sequence fails as `interrupted`. If it isn't given up, it's put on hold at wherever its `onFail` leads, and if it's given up with `finally` steps, it finishes up first (see Finally steps).
* A `meet` step that fails always gives up its sequence at once, whatever its `onFail`, which calls off the appointment and ends the other's sequence for it too.
* Covering up (see Modesty Cover Up in [DataFiles.md](DataFiles.md)) interrupts whatever the character was doing with a change of clothes, after which the step goes on, as after talking. Heading home to cover up instead fails the current step as `shy` and ends its sequence, whatever its `onFail`.
* A sequence that resumes is checked again, so a step it relies on that no longer holds (being somewhere for a `do`) fails as `left`, and if the clothes a later `undress` step will put back on (those its `dress` replaced) aren't worn, carried, or at home while they're there, the step it's on fails as `missing`.

## Taking part in someone else's steps

A sequence can also be someone's part in another's steps, rather than a run through steps of their own:

* **Client:** the client of an appointment carries out its `meet` step (going there and meeting the worker), then follows them until they're let go. The game walks and waits for NPCs, and the player can have those done for them.
* **Follower:** someone a `lead` step leads follows the leader until they're let go. An NPC follows with a follow task. The player's following is a sequence of its own, so it's clearly shown, and they can stop it.

Following can meet a need of the follower's: a `do` step with `partnerMeets` meets that need for the partner following through it, as `talk` and `ask` steps do for the one talked with.

## Jobs

An `ask` step with a `job` takes on work for the one asked when they say yes, such as thatching their roof: so many hours in all, at a place worked out then (usually their home). The job is world state, outliving the sequence that took it on. Until it's done, `jobSite` is where it is and `hasJob` is met, so a profession's steps can go to the job site while they have a job and look for someone to work for when they don't. Time spent at the job site meeting the need's quota counts toward the job.

The player can be on either side. NPCs can ask the player the question of an `ask` step, or to talk for a `talk` step, and the player answers yes or no themselves, warned of any of their needs a yes would keep them from, and only able to say no to an appointment they'd be too shy for (as an NPC would, failing the step as `shy`; see [DataFiles.md](DataFiles.md)). A `lead` step's player follows for at most its `minutes`, and a `dress` or `undress` step `withPartner` changes the player's clothes along with the NPC's. They can call off what they're doing with someone in conversation, which lets the other carry on without them.
