import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import 'theme/fms_theme.dart';
import 'services/fms_api_service.dart';
import 'services/live_cabin_comms_service.dart';
import 'screens/login_screen.dart';
import 'screens/cabin_dashboard_screen.dart';

void main() {
  WidgetsFlutterBinding.ensureInitialized();
  // Lock landscape orientation for authentic in-cabin tablet cockpit experience
  SystemChrome.setPreferredOrientations([
    DeviceOrientation.landscapeLeft,
    DeviceOrientation.landscapeRight,
  ]);

  // Hide system status bar for immersive full-screen FMS cockpit
  SystemChrome.setEnabledSystemUIMode(SystemUiMode.immersiveSticky);

  FmsApiService().init();

  runApp(const VirexaCabinApp());
}

class VirexaCabinApp extends StatelessWidget {
  const VirexaCabinApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Virexa FMS In-Cabin Mobile',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(
        brightness: Brightness.dark,
        scaffoldBackgroundColor: FmsTheme.bgDark,
        colorScheme: const ColorScheme.dark(
          primary: FmsTheme.emeraldGreen,
          secondary: FmsTheme.cyanAccent,
          surface: FmsTheme.surfaceDark,
        ),
      ),
      builder: (context, child) => AnimatedBuilder(
        animation: LiveCabinCommsService(),
        child: child,
        builder: (context, navigator) {
          final live = LiveCabinCommsService();
          final alert = live.alert;
          return Stack(
            children: [
              navigator ?? const SizedBox.shrink(),
              if (alert != null)
                Positioned(
                  top: 20,
                  left: 20,
                  right: 20,
                  child: Center(
                    child: Material(
                      color: alert.urgent
                          ? const Color(0xFF3A2023)
                          : const Color(0xFF10282A),
                      elevation: 16,
                      child: ConstrainedBox(
                        constraints: const BoxConstraints(maxWidth: 560),
                        child: Container(
                          padding: const EdgeInsets.fromLTRB(16, 11, 6, 11),
                          decoration: BoxDecoration(
                            border: Border.all(
                              color: alert.urgent
                                  ? FmsTheme.redHazard
                                  : FmsTheme.emeraldGreen,
                              width: 2,
                            ),
                          ),
                          child: Row(
                            children: [
                              Icon(
                                alert.voice
                                    ? Icons.record_voice_over
                                    : Icons.mark_chat_unread,
                                color: alert.urgent
                                    ? FmsTheme.redHazard
                                    : FmsTheme.emeraldGreen,
                              ),
                              const SizedBox(width: 12),
                              Expanded(
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  mainAxisSize: MainAxisSize.min,
                                  children: [
                                    Text(
                                      alert.title,
                                      style: FmsTheme.titleMedium.copyWith(
                                        fontSize: 14,
                                      ),
                                    ),
                                    const SizedBox(height: 3),
                                    Text(
                                      alert.body,
                                      style: FmsTheme.bodyNormal,
                                      maxLines: 3,
                                      overflow: TextOverflow.ellipsis,
                                    ),
                                  ],
                                ),
                              ),
                              IconButton(
                                tooltip: 'Tutup pemberitahuan',
                                onPressed: live.dismissAlert,
                                icon: const Icon(Icons.close),
                              ),
                            ],
                          ),
                        ),
                      ),
                    ),
                  ),
                ),
            ],
          );
        },
      ),
      home: const MainCabinNavigator(),
    );
  }
}

class MainCabinNavigator extends StatefulWidget {
  const MainCabinNavigator({super.key});

  @override
  State<MainCabinNavigator> createState() => _MainCabinNavigatorState();
}

class _MainCabinNavigatorState extends State<MainCabinNavigator> {
  final FmsApiService _api = FmsApiService();

  @override
  void initState() {
    super.initState();
    _api.addListener(_onStateChanged);
  }

  void _onStateChanged() {
    if (mounted) setState(() {});
  }

  @override
  void dispose() {
    _api.removeListener(_onStateChanged);
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    if (!_api.isLoggedIn) {
      return LoginScreen(
        onLoginSuccess: () {
          setState(() {});
        },
      );
    }

    return const CabinDashboardScreen();
  }
}
