bool canDisplayHeldGps(
  DateTime receivedAt,
  int sourceAgeSeconds,
  DateTime now,
) {
  final elapsed = now.difference(receivedAt).inSeconds;
  return sourceAgeSeconds >= 0 &&
      elapsed >= 0 &&
      elapsed <= 25 &&
      sourceAgeSeconds + elapsed <= 70;
}
