# Community Mobile — SHELVED

**This app is shelved. Do not build new features here.**

The Community Booking & Dispatch spec (2026-08) supersedes it: the passenger app ships as a
**Next.js PWA** in a future sibling folder, not as Flutter. This mockup is retained solely as the
**information-architecture reference** for that PWA — 11 screens showing the intended passenger
flow. Payments for that flow are **Square + Interac e-Transfer** (Stripe is superseded).

## What this is

A design mockup only. Every value on screen comes from `lib/data/mock_data.dart` — there is no
API wiring, no auth, and no persistence. It is not orchestrated by `aspire run` (Aspire has no
Flutter primitive, and the mockup has no API to wait for), and it is excluded from CI: the four
deployable images are the API and the three Next.js frontends, and this is not a server workload.

There are deliberately **no tests** here. Tests on a shelved mockup become dead weight the day the
PWA lands. `flutter analyze` is clean and is the only check that applies.

## If you do need to run it

```
flutter run -d chrome     # or an iOS/Android simulator
flutter analyze           # the CI-style check; warnings are the bar, as in Backend
```

Note: any `flutter` invocation triggers a `pub get` that modifies `pubspec.lock` (it is stale
relative to its constraints), so expect a dirty tree afterwards.

## Design notes worth preserving

- The palette is the supplied mobile design's (`#005493` primary, `#E8A020` gold) and is
  deliberately **not** a copy of Dispatcher's `theme.ts`.
- The four protected status hexes and the **colour + icon + label** rule apply unchanged — see
  `lib/widgets/status_chip.dart`. Status is never colour alone.
- Barlow fonts are bundled as TTF assets (`assets/fonts/`) rather than fetched at runtime, so the
  app renders identically offline. Text avoids glyphs Barlow lacks (e.g. `→` is an `Icon`, see
  `lib/widgets/route_text.dart`).

Full context: the root `CLAUDE.md` folder map, and the `northern-link-architecture` skill.
