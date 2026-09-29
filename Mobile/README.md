# Virexa One Cabin

In-cabin Flutter view for a selected hauler. The screen uses the selected unit's FMS GPS fix and source heading, not the tablet's location sensor. Target vectors are straight-line UTM bearings, **not road routing or driving instructions**.

## Data contracts

- `/api/v1/fleet/live`: unit identity, GPS position, `heading_available`, speed, activity, GPS age, payload and recorded load availability.
- `/api/v1/mtc/live`: current source assignments without generated dispatch fallback.
- `/api/v1/locations/actual`: Hexagon locations without generated or cached fallback. Requires the updated backend.
- `/api/v1/weather/current`: optional Open-Meteo report at the selected unit's coordinates.

Live navigation requires a unit GPS fix no older than 45 seconds, a source heading, and a valid target. During a short update gap, the last validated navigation is held for at most 25 seconds and visibly labeled as an old position or target; it never predicts movement. Excavator targets also require a fresh GPS fix. Disposal targets use source location coordinates. An assignment older than 12 hours is not used for automatic guidance; an operator may select a real target manually. Distance and ETA are straight-line estimates, not route distance or dispatch authority.

The Jejak view plots up to 10 minutes of distinct source GPS points collected while the app is open, nearby fresh units, and a labeled destination marker. When the destination is outside the viewport, the marker sits at the display edge and shows straight-line remaining distance. The dashed guide is not a road route; the solid breadcrumb is where the unit has actually been. Jejak's center silhouette represents the unit, not the target direction. Panah and Jejak now use one heading-up target basis; the elevated Panah projection compensates horizontal foreshortening so its displayed target direction matches Jejak. GPS heading on a stopped unit may be its last reported orientation, not a live physical compass reading.

The unit picker is not an operator login. Cabin messaging and live PTT need the matching backend communication endpoints and site configuration; a disconnected backend cannot deliver them. `recorded_loads` is labeled as recorded loads, not completed trips.

## Run and verify

1. Deploy the matching backend build and verify `/health`, `/api/v1/fleet/live`, `/api/v1/mtc/live`, and `/api/v1/locations/actual` from the tablet's authorized network.
2. Ensure Cloudflare Access/VPN permits the tablet to call the API. Never package a shared Access service token or database password in the APK. The app requires an HTTPS API URL, saved locally as a preference.
3. Run `flutter analyze --no-pub` and `flutter build apk --debug --no-pub`. Run `dart --enable-asserts tool/check_navigation.dart` for the geometry and GPS-freshness checks.

The current default API is `https://cobacoba.idccoal.id`. If it returns 502, repair the Cloudflare Tunnel/upstream first. If it returns 403, check Access policy and the tablet's authorized session/network. This client deliberately does not bypass either control.
