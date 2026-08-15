import 'package:flutter/material.dart';

/// "Leaf Rapids → Thompson" with the arrow as an icon, not a glyph —
/// Barlow lacks U+2192 and the CanvasKit fallback font is a network fetch,
/// so a text arrow renders as tofu offline.
class RouteText extends StatelessWidget {
  final String from;
  final String to;
  final TextStyle style;

  const RouteText({super.key, required this.from, required this.to, required this.style});

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Flexible(child: Text(from, style: style, overflow: TextOverflow.ellipsis)),
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: 6),
          child: Icon(Icons.arrow_forward, size: (style.fontSize ?? 14) + 1, color: style.color),
        ),
        Flexible(child: Text(to, style: style, overflow: TextOverflow.ellipsis)),
      ],
    );
  }
}
