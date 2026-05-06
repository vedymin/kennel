# Kennel

A reservation management system for a dog boarding hotel ("hotel dla psów"). Owners book stays for their dogs, and the system aggregates reservations from both local entries and Google Calendar.

## Language

**Reservation**:
A boarding stay for a single dog, defined by dog name, start date, and end date.
_Avoid_: Booking, appointment

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

- A **Reservation** belongs to exactly one **Source**
- A **GoogleConnection** enables the `google` **Source** — without it, **SourceStatus** is `not_connected`
- **ReservationAggregation** combines reservations from all **Sources** and reports each one's **SourceStatus**
- Only `local` **Reservations** can be deleted; `google` **Reservations** are read-only

## Example dialogue

> **Dev:** "What happens when the Google token expires?"
> **Domain expert:** "The **SourceStatus** for google becomes `unauthorized`. Local **Reservations** still show up — **ReservationAggregation** doesn't fail, it just reports the degraded **Source**."

## Flagged ambiguities

- "Reservation" vs "Event" — Google Calendar calls them events, but in this domain they are **Reservations**. The mapper translates calendar events into **Reservations** at the boundary.
