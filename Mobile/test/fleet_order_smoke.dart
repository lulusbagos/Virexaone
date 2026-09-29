import 'package:virexa_mobile/models/fleet_models.dart';

void main() {
  FleetUnit unit(String name, int age, {bool position = true}) =>
      FleetUnit.fromJson({
        'unit_id': age,
        'unit_name': name,
        'category': 'Hauler',
        'activity_name': 'Hauling',
        'last_heard_seconds_ago': age,
        'latitude': position ? 1.0 : null,
        'longitude': position ? 117.0 : null,
        'easting': position ? 570000.0 : 0.0,
        'northing': position ? 114000.0 : 0.0,
      });

  final units = [
    unit('DT-OLD', 90),
    unit('DT-NONE', 0, position: false),
    unit('DT-NEW', 4),
    unit('DT-MID', 15),
  ]..sort(FleetUnit.compareByGpsAge);
  assert(
    units.map((item) => item.unitName).join(',') ==
        'DT-NEW,DT-MID,DT-OLD,DT-NONE',
  );
}
