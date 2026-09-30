import 'package:flutter/material.dart';
import '../theme/fms_theme.dart';

class TelemetryCapsule extends StatelessWidget {
  final String icon;
  final String label;
  final String value;
  final Color? valueColor;

  const TelemetryCapsule({
    super.key,
    required this.icon,
    required this.label,
    required this.value,
    this.valueColor,
  });

  @override
  Widget build(BuildContext context) {
    final activeValueColor = valueColor ?? FmsTheme.emeraldGreen;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 4),
      decoration: BoxDecoration(
        color: FmsTheme.cardHeaderBg,
        borderRadius: BorderRadius.circular(8),
        border: Border.all(
          color: FmsTheme.cardBorder,
          width: 1.0,
        ),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(icon, style: const TextStyle(fontSize: 11)),
          const SizedBox(width: 3),
          if (label.isNotEmpty) ...[
            Text(
              label,
              style: FmsTheme.caption.copyWith(color: FmsTheme.textMuted, fontSize: 8.5),
            ),
            const SizedBox(width: 3),
          ],
          Flexible(
            child: FittedBox(
              fit: BoxFit.scaleDown,
              alignment: Alignment.centerLeft,
              child: Text(
                value,
                maxLines: 1,
                style: FmsTheme.titleMedium.copyWith(
                  color: activeValueColor,
                  fontWeight: FontWeight.w800,
                  fontSize: 10.0,
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

/// Futuristic Segmented Bar Gauge
class SegmentedLedBar extends StatelessWidget {
  final double percent; // 0.0 to 1.0
  final int segmentCount;
  final Color activeColor;
  final Color inactiveColor;
  final double height;

  const SegmentedLedBar({
    super.key,
    required this.percent,
    this.segmentCount = 10,
    required this.activeColor,
    this.inactiveColor = const Color(0xFF0C1929),
    this.height = 7,
  });

  @override
  Widget build(BuildContext context) {
    final activeCount = (percent.clamp(0.0, 1.0) * segmentCount).round();

    return SizedBox(
      height: height,
      child: Row(
        children: List.generate(segmentCount, (index) {
          final isLit = index < activeCount;
          return Expanded(
            child: Container(
              margin: EdgeInsets.symmetric(horizontal: index == 0 || index == segmentCount - 1 ? 0.5 : 1.0),
              decoration: BoxDecoration(
                color: isLit ? activeColor : inactiveColor,
                borderRadius: BorderRadius.circular(2),
                border: Border.all(
                  color: isLit ? activeColor.withValues(alpha: 0.9) : const Color(0x2200E5FF),
                  width: 0.6,
                ),
                boxShadow: isLit
                    ? [
                        BoxShadow(
                          color: activeColor.withValues(alpha: 0.45),
                          blurRadius: 3,
                          spreadRadius: 0.5,
                        ),
                      ]
                    : null,
              ),
            ),
          );
        }),
      ),
    );
  }
}

/// High-tech Fuel Level Gauge Component (Compact & Overflow-Safe)
class FuelLevelGaugeCard extends StatelessWidget {
  final double fuelPercent; // 0 to 100

  const FuelLevelGaugeCard({
    super.key,
    required this.fuelPercent,
  });

  @override
  Widget build(BuildContext context) {
    final clamped = fuelPercent.clamp(0.0, 100.0);
    final Color statusColor;

    if (clamped < 20) {
      statusColor = FmsTheme.redHazard;
    } else if (clamped < 40) {
      statusColor = FmsTheme.amberWarning;
    } else {
      statusColor = FmsTheme.emeraldGreen;
    }

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 3),
      decoration: BoxDecoration(
        color: const Color(0xFF061424),
        borderRadius: BorderRadius.circular(6),
        border: Border.all(color: statusColor.withValues(alpha: 0.45), width: 1.0),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Row(
            children: [
              Icon(Icons.local_gas_station_rounded, size: 10, color: statusColor),
              const SizedBox(width: 2),
              Text(
                "FUEL",
                style: TextStyle(color: FmsTheme.textLight, fontSize: 8.5, fontWeight: FontWeight.bold),
              ),
              const Spacer(),
              FittedBox(
                fit: BoxFit.scaleDown,
                child: Text(
                  "${clamped.toStringAsFixed(0)}%",
                  style: TextStyle(color: statusColor, fontSize: 9.0, fontWeight: FontWeight.w900),
                ),
              ),
            ],
          ),
          const SizedBox(height: 2),
          SegmentedLedBar(
            percent: clamped / 100.0,
            segmentCount: 8,
            activeColor: statusColor,
            height: 5,
          ),
        ],
      ),
    );
  }
}

/// High-tech Engine Temperature Gauge Component (Compact & Overflow-Safe)
class EngineTempGaugeCard extends StatelessWidget {
  final double tempC; // e.g. 86.0

  const EngineTempGaugeCard({
    super.key,
    required this.tempC,
  });

  @override
  Widget build(BuildContext context) {
    final clamped = tempC.clamp(40.0, 130.0);
    final percent = ((clamped - 40.0) / 80.0).clamp(0.0, 1.0);

    final Color statusColor;
    if (tempC > 102) {
      statusColor = FmsTheme.redHazard;
    } else if (tempC > 92) {
      statusColor = FmsTheme.amberWarning;
    } else {
      statusColor = FmsTheme.cyanAccent;
    }

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 3),
      decoration: BoxDecoration(
        color: const Color(0xFF061424),
        borderRadius: BorderRadius.circular(6),
        border: Border.all(color: statusColor.withValues(alpha: 0.45), width: 1.0),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Row(
            children: [
              Icon(Icons.thermostat_rounded, size: 10, color: statusColor),
              const SizedBox(width: 2),
              Text(
                "TEMP",
                style: TextStyle(color: FmsTheme.textLight, fontSize: 8.5, fontWeight: FontWeight.bold),
              ),
              const Spacer(),
              FittedBox(
                fit: BoxFit.scaleDown,
                child: Text(
                  "${tempC.toStringAsFixed(0)}°C",
                  style: TextStyle(color: statusColor, fontSize: 9.0, fontWeight: FontWeight.w900),
                ),
              ),
            ],
          ),
          const SizedBox(height: 2),
          SegmentedLedBar(
            percent: percent,
            segmentCount: 8,
            activeColor: statusColor,
            height: 5,
          ),
        ],
      ),
    );
  }
}

/// Standard High-Tech Telemetry Gauge Bar (Compact & Overflow-Safe)
class TelemetryGaugeBar extends StatelessWidget {
  final String label;
  final String valueText;
  final double fillPercent; // 0.0 to 1.0
  final Color fillColor;

  const TelemetryGaugeBar({
    super.key,
    required this.label,
    required this.valueText,
    required this.fillPercent,
    required this.fillColor,
  });

  @override
  Widget build(BuildContext context) {
    final clampedFill = fillPercent.clamp(0.0, 1.0);

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 5, vertical: 3),
      decoration: BoxDecoration(
        color: const Color(0xFF061424),
        borderRadius: BorderRadius.circular(6),
        border: Border.all(color: fillColor.withValues(alpha: 0.35), width: 1.0),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Row(
            children: [
              Text(
                label,
                style: TextStyle(color: FmsTheme.textLight, fontSize: 8.5, fontWeight: FontWeight.bold),
              ),
              const Spacer(),
              FittedBox(
                fit: BoxFit.scaleDown,
                child: Text(
                  valueText,
                  style: TextStyle(color: fillColor, fontSize: 9.0, fontWeight: FontWeight.bold),
                ),
              ),
            ],
          ),
          const SizedBox(height: 2),
          SegmentedLedBar(
            percent: clampedFill,
            segmentCount: 8,
            activeColor: fillColor,
            height: 5,
          ),
        ],
      ),
    );
  }
}
