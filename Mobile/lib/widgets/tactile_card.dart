import 'package:flutter/material.dart';
import '../theme/fms_theme.dart';

class TactileCard extends StatefulWidget {
  final Widget child;
  final VoidCallback? onTap;
  final Color? borderColor;
  final Color? ledColor;
  final Color? bgColor;
  final double borderRadius;
  final EdgeInsetsGeometry padding;
  final bool isSelected;

  const TactileCard({
    super.key,
    required this.child,
    this.onTap,
    this.borderColor,
    this.ledColor,
    this.bgColor,
    this.borderRadius = 10.0,
    this.padding = const EdgeInsets.all(10.0),
    this.isSelected = false,
  });

  @override
  State<TactileCard> createState() => _TactileCardState();
}

class _TactileCardState extends State<TactileCard> {
  bool _isPressed = false;

  @override
  Widget build(BuildContext context) {
    final effectiveBorderColor = widget.borderColor ??
        (widget.isSelected ? FmsTheme.emeraldGreen : FmsTheme.cardBorder);

    final effectiveLedColor = widget.ledColor ??
        (widget.isSelected ? FmsTheme.emeraldGreen : FmsTheme.cyanAccent);

    final effectiveBgColor = widget.bgColor ??
        (widget.isSelected ? const Color(0xE808281A) : FmsTheme.cardBg);

    return AnimatedScale(
      scale: _isPressed ? 0.97 : 1.0,
      duration: const Duration(milliseconds: 100),
      child: Material(
        color: Colors.transparent,
        child: InkWell(
          onTap: widget.onTap,
          onTapDown: (_) => setState(() => _isPressed = true),
          onTapUp: (_) => setState(() => _isPressed = false),
          onTapCancel: () => setState(() => _isPressed = false),
          borderRadius: BorderRadius.circular(widget.borderRadius),
          child: Container(
            padding: widget.padding,
            decoration: BoxDecoration(
              color: effectiveBgColor,
              borderRadius: BorderRadius.circular(widget.borderRadius),
              border: Border.all(
                color: effectiveBorderColor,
                width: widget.isSelected ? 1.8 : 1.2,
              ),
              boxShadow: widget.isSelected
                  ? FmsTheme.neonGlowShadow(effectiveBorderColor, opacity: 0.35)
                  : [
                      BoxShadow(
                        color: Colors.black.withValues(alpha: 0.35),
                        blurRadius: 8,
                        offset: const Offset(0, 3),
                      ),
                    ],
            ),
            child: Row(
              children: [
                // Glowing Left LED Strip Pill
                Container(
                  width: 4.5,
                  height: double.infinity,
                  decoration: BoxDecoration(
                    color: effectiveLedColor,
                    borderRadius: BorderRadius.circular(3),
                    boxShadow: [
                      BoxShadow(
                        color: effectiveLedColor.withValues(alpha: 0.6),
                        blurRadius: 6,
                        spreadRadius: 0.5,
                      ),
                    ],
                  ),
                ),
                const SizedBox(width: 10),
                Expanded(child: widget.child),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
