import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import 'theme/fms_theme.dart';
import 'services/fms_api_service.dart';
import 'services/live_cabin_comms_service.dart';
import 'screens/splash_screen.dart';

void main() {
  WidgetsFlutterBinding.ensureInitialized();

  // Support both portrait and landscape modes dynamically
  SystemChrome.setPreferredOrientations([
    DeviceOrientation.portraitUp,
    DeviceOrientation.portraitDown,
    DeviceOrientation.landscapeLeft,
    DeviceOrientation.landscapeRight,
  ]);

  // Keep system UI clean and sticky immersive
  SystemChrome.setEnabledSystemUIMode(SystemUiMode.immersiveSticky);

  FmsApiService().init();

  runApp(const VirexaCabinApp());
}

class VirexaCabinApp extends StatelessWidget {
  const VirexaCabinApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Virexa One FMS',
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
                  top: 24,
                  left: 16,
                  right: 16,
                  child: Center(
                    child: Material(
                      color: alert.urgent
                          ? const Color(0xF03B151A)
                          : const Color(0xF0092233),
                      elevation: 16,
                      borderRadius: BorderRadius.circular(12),
                      child: ConstrainedBox(
                        constraints: const BoxConstraints(maxWidth: 580),
                        child: Container(
                          padding: const EdgeInsets.fromLTRB(16, 12, 8, 12),
                          decoration: BoxDecoration(
                            border: Border.all(
                              color: alert.urgent
                                  ? FmsTheme.redHazard
                                  : FmsTheme.emeraldGreen,
                              width: 2,
                            ),
                            borderRadius: BorderRadius.circular(12),
                            boxShadow: FmsTheme.neonGlowShadow(
                              alert.urgent
                                  ? FmsTheme.redHazard
                                  : FmsTheme.emeraldGreen,
                              opacity: 0.4,
                            ),
                          ),
                          child: Row(
                            children: [
                              Icon(
                                alert.voice
                                    ? Icons.record_voice_over_rounded
                                    : Icons.mark_chat_unread_rounded,
                                color: alert.urgent
                                    ? FmsTheme.redHazard
                                    : FmsTheme.emeraldGreen,
                                size: 28,
                              ),
                              const SizedBox(width: 14),
                              Expanded(
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  mainAxisSize: MainAxisSize.min,
                                  children: [
                                    Text(
                                      alert.title,
                                      style: FmsTheme.titleMedium.copyWith(
                                        fontSize: 13.5,
                                        fontWeight: FontWeight.w800,
                                        color: alert.urgent
                                            ? FmsTheme.redHazard
                                            : FmsTheme.emeraldGreen,
                                      ),
                                    ),
                                    const SizedBox(height: 3),
                                    Text(
                                      alert.body,
                                      style: FmsTheme.bodyNormal.copyWith(
                                        fontSize: 12.5,
                                        color: Colors.white,
                                        fontWeight: FontWeight.w600,
                                      ),
                                      maxLines: 3,
                                      overflow: TextOverflow.ellipsis,
                                    ),
                                  ],
                                ),
                              ),
                              IconButton(
                                tooltip: 'Tutup pemberitahuan',
                                onPressed: live.dismissAlert,
                                icon: const Icon(Icons.close_rounded, size: 20),
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
      home: const SplashScreen(),
    );
  }
}
