# Privacy

Light Pilot is local-first.

## What Stays Local

- Settings
- Monitor preferences
- App override rules
- Local diagnostics
- Optional content brightness aggregates

No data is uploaded. No cloud account is required. The app works offline.

## Content Brightness Analysis

Content brightness analysis is off by default. If enabled, Light Pilot captures a tiny in-memory screen sample and immediately reduces it to aggregate luminance values:

- average luminance
- white pixel ratio
- bright pixel ratio
- dark pixel ratio
- brightness classification

Screenshots are not stored. Raw pixels are not logged. Samples are not sent anywhere.

## Logs

Normal logs must not contain document text, window titles, screenshots, pixels, or private screen content. Diagnostics may include sanitized timings, monitor identifiers, selected brightness targets, and error codes.

## Disable / Override

Users can:

- disable Auto
- pause for 30 minutes
- pause until tomorrow
- turn off content brightness analysis
- disable DDC/CI
- reset defaults
- quit the app
