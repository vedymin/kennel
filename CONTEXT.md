# Kennel

A reservation management system for a dog boarding hotel ("hotel dla psów"). Owners book stays for their dogs, and the system tracks kennel occupancy to ensure every reservation has a guaranteed spot.

## Language

**Owner**:
A person or institution that owns one or more dogs. Identified by name. Created inline during reservation — Ola picks from existing or creates new.
_Avoid_: Client, customer

**Dog**:
A dog registered in the system, identified by the natural key (dog name + **Owner**). Created automatically on first reservation with that name/owner pair.
_Avoid_: Pet, animal

**Reservation**:
A boarding stay for a single **Dog**, defined by start date, end date, and optionally arrival/departure times. One dog = one reservation. Two dogs from the same owner arriving together = two reservations. Times are optional — when absent the system assumes arrival at 12:00 and departure at 11:00, providing a buffer for kennel turnover.
_Avoid_: Booking, appointment

**Kennel**:
A physical enclosure in the hotel where dogs stay. Has a name (e.g. "Boks 1", "Mała klatka"). Capacity and weight limits are planned but not yet modeled.
_Avoid_: Box, cage, crate

**Occupation**:
A planned period during which a **Dog** occupies a specific **Kennel**. A **Reservation** consists of one or more consecutive **Occupations** that together must fully cover the reservation's date range with no gaps. When a dog is moved between kennels, its single occupation is split into two. Each dog has its own occupation — two dogs sharing a kennel have separate occupation records pointing to the same kennel. Times follow the same rules as **Reservation** (optional, same defaults).
_Avoid_: Assignment, placement, stay

**Incompatibility**:
An explicit record that two **Dogs** of the same **Owner** cannot share a **Kennel**. By default, dogs of the same owner are assumed compatible. Incompatibility is the exception, recorded when the owner reports the dogs cannot coexist.
_Avoid_: Conflict, restriction

**Source**:
Where a reservation originates — either `local` (created in-app) or `google` (synced from Google Calendar).
_Avoid_: Origin, provider

**GoogleConnection**:
A stored OAuth2 refresh token that links the kennel's backend to a Google account for calendar syncing.
_Avoid_: Account link, integration

**SourceStatus**:
Per-source health indicator reported alongside reservation data. One of: `ok`, `not_configured`, `not_connected`, `unauthorized`, `error`.
_Avoid_: Health check, connection state

**ReservationAggregation**:
The process of merging reservations from all sources into a single sorted list with per-source status.
_Avoid_: Merge, sync

## Relationships

- A **Dog** belongs to exactly one **Owner**
- A **Reservation** is for exactly one **Dog** and belongs to exactly one **Source**
- A **Reservation** consists of one or more **Occupations** that fully cover its date range (no gaps allowed)
- An **Occupation** assigns one **Dog** to one **Kennel** for a date range
- A **Kennel** may hold multiple **Dogs** simultaneously, but only if all dogs belong to the same **Owner** and no **Incompatibility** exists between any pair
- An **Incompatibility** links two **Dogs** of the same **Owner** — dogs of different owners can never share a kennel regardless
- A **GoogleConnection** enables the `google` **Source** — without it, **SourceStatus** is `not_connected`
- **ReservationAggregation** combines reservations from all **Sources** and reports each one's **SourceStatus**
- Only `local` **Reservations** can be deleted; `google` **Reservations** are read-only
- Deleting a **Reservation** cascades to its **Occupations**; the **Dog** and **Owner** remain in the system

## Kennel sharing rules

1. **Different owners → never together.** No exceptions, no override.
2. **Same owner, no Incompatibility → allowed.** This is the default for dogs of the same owner.
3. **Same owner, Incompatibility exists → not allowed.** Ola records this when the owner reports the dogs cannot coexist.

Capacity and weight-based soft limits are planned for a future iteration.

## Example dialogue

> **Dev:** "What happens when the Google token expires?"
> **Domain expert:** "The **SourceStatus** for google becomes `unauthorized`. Local **Reservations** still show up — **ReservationAggregation** doesn't fail, it just reports the degraded **Source**."

> **Dev:** "Can I put two dogs from different owners in the same kennel if Ola says it's fine?"
> **Domain expert:** "No. Never. Even if they're best friends on the walk. The **Kennel** sharing rule is absolute — different **Owners** means separate **Kennels**."

> **Dev:** "A reservation is from May 1 to May 10, but Ola only assigned a kennel for the first 5 days. Can she save it?"
> **Domain expert:** "No. **Occupations** must fully cover the **Reservation** date range. She needs to assign a **Kennel** for May 5–10 before confirming."

## Flagged ambiguities

- "Reservation" vs "Event" — Google Calendar calls them events, but in this domain they are **Reservations**. The mapper translates calendar events into **Reservations** at the boundary.
- "Kennel" as app name vs domain term — the app is called "Kennel" but a kennel is also a physical enclosure. Context disambiguates; the domain term always refers to the physical box.
- Google **Reservations** are read-only and outside the kennel/occupation system. They appear on the reservation list but not on the kennel occupancy plan. A future importer may bridge google reservations into the local model with full **Dog**/**Owner**/**Occupation** data.
