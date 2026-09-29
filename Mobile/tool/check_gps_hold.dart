import '../lib/models/gps_hold_policy.dart';

void main() {
  final now = DateTime.utc(2026, 9, 27, 10);
  assert(canDisplayHeldGps(now.subtract(const Duration(seconds: 24)), 20, now));
  assert(
    !canDisplayHeldGps(now.subtract(const Duration(seconds: 26)), 20, now),
  );
  assert(
    !canDisplayHeldGps(now.subtract(const Duration(seconds: 20)), 55, now),
  );
  assert(!canDisplayHeldGps(now.add(const Duration(seconds: 1)), 10, now));
  print('GPS hold policy passed');
}
