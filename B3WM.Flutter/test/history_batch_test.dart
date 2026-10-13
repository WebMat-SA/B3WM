import 'package:flutter_test/flutter_test.dart';
import 'package:b3wm_flutter/services/state_service.dart';

void main() {
  group('computePreviousBatch (issue #18)', () {
    test('dia comum busca dia anterior', () {
      // Terça 07/10/2025 mostrando só 07/10 → busca 06/10.
      final oldest = DateTime(2025, 10, 7);
      final batch = StateService.computePreviousBatch({oldest}, oldest);
      expect(batch.$1, DateTime(2025, 10, 6));
      expect(batch.$2, DateTime(2025, 10, 6));
    });

    test('segunda com semana completa busca seg–sex anterior', () {
      // Semana 13–17/10/2025 (seg–sex) carregada, mais antigo = seg 13/10.
      final loaded = {
        DateTime(2025, 10, 13),
        DateTime(2025, 10, 14),
        DateTime(2025, 10, 15),
        DateTime(2025, 10, 16),
        DateTime(2025, 10, 17),
      };
      final batch = StateService.computePreviousBatch(
          loaded, DateTime(2025, 10, 13));
      expect(batch.$1, DateTime(2025, 10, 6));
      expect(batch.$2, DateTime(2025, 10, 10));
    });

    test('segunda sem semana completa busca só dia anterior', () {
      final loaded = {DateTime(2025, 10, 13)};
      final batch = StateService.computePreviousBatch(
          loaded, DateTime(2025, 10, 13));
      expect(batch.$1, DateTime(2025, 10, 12));
      expect(batch.$2, DateTime(2025, 10, 12));
    });

    test('mês cheio busca mês anterior inteiro', () {
      // Outubro/2025: mais antigo = 01/10 (quarta), carregado cobre todos
      // os dias úteis de 01/10 até 31/10 → batch = setembro inteiro.
      final loaded = <DateTime>{};
      for (var d = 1; d <= 31; d++) {
        final dt = DateTime(2025, 10, d);
        if (dt.weekday != DateTime.saturday &&
            dt.weekday != DateTime.sunday) {
          loaded.add(DateTime(dt.year, dt.month, dt.day));
        }
      }
      final batch = StateService.computePreviousBatch(
          loaded, DateTime(2025, 10, 1));
      expect(batch.$1, DateTime(2025, 9, 1));
      expect(batch.$2, DateTime(2025, 9, 30));
    });

    test('meio do mês não dispara batch mensal', () {
      final loaded = {
        DateTime(2025, 10, 15),
        DateTime(2025, 10, 16),
        DateTime(2025, 10, 17),
      };
      final batch = StateService.computePreviousBatch(
          loaded, DateTime(2025, 10, 15));
      expect(batch.$1, DateTime(2025, 10, 14));
      expect(batch.$2, DateTime(2025, 10, 14));
    });
  });
}
