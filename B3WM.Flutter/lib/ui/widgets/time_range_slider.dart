import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:intl/intl.dart';
import '../../services/state_service.dart';

class TimeRangeSlider extends StatelessWidget {
  const TimeRangeSlider({super.key});

  @override
  Widget build(BuildContext context) {
    return Consumer<StateService>(builder: (context, state, _) {
      final bars = state.barsTimeFrameFilter;
      final count = bars.length;
      if (count == 0) return const SizedBox.shrink();

      final start = state.dateRangeStart;
      final end = state.dateRangeEnd;

      String formatDate(int idx) {
        final bar = bars.elementAtOrNull(idx);
        if (bar == null) return '';
        final startDate = bars.elementAtOrNull(state.dateRangeStart);
        final fmt = startDate != null &&
                bar.date.day == startDate.date.day &&
                bar.date.month == startDate.date.month
            ? DateFormat('HH:mm')
            : DateFormat('dd/MM HH:mm');
        return fmt.format(bar.date);
      }

      return Container(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
        decoration: BoxDecoration(
          border: Border(
              top: BorderSide(color: Colors.grey.shade800.withOpacity(0.3))),
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            // Date labels
            Row(
              children: [
                Text(formatDate(start),
                    style: const TextStyle(fontSize: 10, color: Colors.grey)),
                const Spacer(),
                Text('–',
                    style: const TextStyle(fontSize: 10, color: Colors.grey)),
                const Spacer(),
                Text(formatDate(end),
                    style: const TextStyle(fontSize: 10, color: Colors.grey)),
              ],
            ),
            const SizedBox(height: 2),
            // Track
            LayoutBuilder(builder: (context, constraints) {
              final totalWidth = constraints.maxWidth;
              final thumbSize = 14.0;
              final trackHeight = 40.0;
              final leftPct = start / (count > 0 ? count : 1);
              final rightPct = end / (count > 0 ? count : 1);
              final leftPos = leftPct * totalWidth;
              final rightPos = rightPct * totalWidth;

              return GestureDetector(
                onPanUpdate: (details) {
                  // Simple tap-based range adjustment
                },
                child: SizedBox(
                  height: trackHeight,
                  child: Stack(
                    children: [
                      // Track background
                      Positioned.fill(
                        child: Container(
                          margin: const EdgeInsets.symmetric(vertical: 13),
                          decoration: BoxDecoration(
                            color: Colors.grey.shade800,
                            borderRadius: BorderRadius.circular(4),
                          ),
                        ),
                      ),
                      // Tick marks
                      ...List.generate(count, (i) {
                        if (i % 10 != 0 && i != 0 && i != count - 1) {
                          return const SizedBox.shrink();
                        }
                        final pct = i / (count > 0 ? count : 1);
                        final isMajor = i % 10 == 0;
                        return Positioned(
                          left: pct * totalWidth - (isMajor ? 1 : 0.5),
                          top: isMajor ? 4 : 10,
                          child: Container(
                            width: isMajor ? 2 : 1,
                            height: isMajor ? trackHeight - 8 : trackHeight - 20,
                            color: isMajor
                                ? Colors.grey.shade500
                                : Colors.grey.shade700,
                          ),
                        );
                      }),
                      // Selected range highlight
                      Positioned(
                        left: leftPos,
                        right: totalWidth - rightPos,
                        top: 13,
                        bottom: 13,
                        child: Container(
                          decoration: BoxDecoration(
                            gradient: LinearGradient(
                              colors: [
                                Colors.blue.shade700,
                                Colors.blue.shade900
                              ],
                            ),
                            borderRadius: BorderRadius.circular(4),
                          ),
                        ),
                      ),
                      // Thumb start
                      Positioned(
                        left: leftPos - thumbSize / 2,
                        top: 0,
                        child: GestureDetector(
                          onHorizontalDragUpdate: (details) {
                            final newStart =
                                ((details.globalPosition.dx / totalWidth) *
                                        count)
                                    .round()
                                    .clamp(0, end - 1);
                            state.applyVolumeFilter(newStart, end);
                          },
                          child: Container(
                            width: thumbSize,
                            height: thumbSize,
                            margin: const EdgeInsets.only(top: 13),
                            decoration: BoxDecoration(
                              color: Colors.white,
                              shape: BoxShape.circle,
                              border: Border.all(
                                  color: Colors.blue, width: 3),
                              boxShadow: [
                                BoxShadow(
                                    color: Colors.black.withOpacity(0.25),
                                    blurRadius: 4),
                              ],
                            ),
                          ),
                        ),
                      ),
                      // Thumb end
                      Positioned(
                        left: rightPos - thumbSize / 2,
                        top: 0,
                        child: GestureDetector(
                          onHorizontalDragUpdate: (details) {
                            final newEnd =
                                ((details.globalPosition.dx / totalWidth) *
                                        count)
                                    .round()
                                    .clamp(start + 1, count);
                            state.applyVolumeFilter(start, newEnd);
                          },
                          child: Container(
                            width: thumbSize,
                            height: thumbSize,
                            margin: const EdgeInsets.only(top: 13),
                            decoration: BoxDecoration(
                              color: Colors.white,
                              shape: BoxShape.circle,
                              border: Border.all(
                                  color: Colors.blue, width: 3),
                              boxShadow: [
                                BoxShadow(
                                    color: Colors.black.withOpacity(0.25),
                                    blurRadius: 4),
                              ],
                            ),
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
              );
            }),
            // Controls row
            const SizedBox(height: 2),
            Row(
              children: [
                IconButton(
                  icon: const Icon(Icons.restart_alt, size: 16),
                  onPressed: () => state.applyVolumeFilter(0, count),
                  tooltip: 'Reset range',
                ),
                const Spacer(),
                IconButton(
                  icon: Icon(
                    Icons.auto_mode,
                    size: 16,
                    color: state.profileAutoByPriceStructure
                        ? Colors.green
                        : Colors.red,
                  ),
                  onPressed: () => state.setProfileAutoByPriceStructure(!state.profileAutoByPriceStructure),
                  tooltip: state.profileAutoByPriceStructure ? 'Auto Mode: On' : 'Auto Mode: Off',
                ),
                Text(
                  '$start - $end',
                  style: const TextStyle(fontSize: 10, color: Colors.grey),
                ),
              ],
            ),
          ],
        ),
      );
    });
  }
}

extension _ListElementAtOrNull<T> on List<T> {
  T? elementAtOrNull(int index) {
    if (index < 0 || index >= length) return null;
    return this[index];
  }
}
