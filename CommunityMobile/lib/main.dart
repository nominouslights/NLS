import 'package:flutter/material.dart';

import 'theme/nl_theme.dart';
import 'widgets/app_shell.dart';

void main() {
  runApp(const CommunityMobileApp());
}

class CommunityMobileApp extends StatelessWidget {
  const CommunityMobileApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Northern Link',
      debugShowCheckedModeBanner: false,
      theme: buildNlTheme(),
      home: const AppShell(),
    );
  }
}
