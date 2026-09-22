import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../services/config_file_service.dart';
import '../../services/state_service.dart';

/// Aba de Backup: exporta/importa todas as configs por símbolo em JSON.
///
/// O arquivo contém um [ConfigBackup] (todos os símbolos + símbolo ativo).
/// O import usa semântica "substituir tudo" e recarrega o símbolo ativo,
/// deixando drawers (agentes, thresholds, etc.) pré-selecionados.
/// Pede confirmação por ser destrutivo.
class BackupTab extends StatefulWidget {
  final bool noDrawer;
  const BackupTab({super.key, this.noDrawer = false});

  @override
  State<BackupTab> createState() => _BackupTabState();
}

class _BackupTabState extends State<BackupTab> {
  final _files = ConfigFileService();
  bool _busy = false;
  String? _lastMessage;

  Future<void> _export(BuildContext context) async {
    final state = context.read<StateService>();
    setState(() {
      _busy = true;
      _lastMessage = null;
    });
    try {
      final backup = await state.exportAllConfigs();
      if (!context.mounted) return;
      if (backup.symbols.isEmpty) {
        _snack(context, 'Nenhuma configuração para exportar.');
        return;
      }
      final path = await _files.saveBackupFile(backup);
      if (!context.mounted) return;
      if (path == null) return; // cancelado
      setState(() => _lastMessage = 'Exportado: $path');
      _snack(context, 'Backup exportado (${backup.symbols.length} símbolo(s)).');
    } catch (e) {
      if (!context.mounted) return;
      _snack(context, 'Falha ao exportar: $e');
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _import(BuildContext context) async {
    final confirmed = await _confirmImport(context);
    if (confirmed != true || !context.mounted) return;
    final state = context.read<StateService>();
    setState(() {
      _busy = true;
      _lastMessage = null;
    });
    try {
      final picked = await _files.pickBackupFile();
      if (!context.mounted) return;
      if (picked == null) return; // cancelado
      try {
        await state.importAllConfigs(picked.json);
      } catch (e) {
        // Configs já aplicadas mesmo se a recarga de dados falhar offline:
        // StateService persiste antes do setSymbol; avisa em vez de reverter.
        if (!context.mounted) return;
        _snack(context, 'Backup aplicado com ressalva: $e');
        setState(() => _lastMessage = 'Aplicado "${picked.fileName}" com ressalva.');
        return;
      }
      if (!context.mounted) return;
      setState(() => _lastMessage = 'Importado de "${picked.fileName}".');
      _snack(context, 'Backup importado. Configs aplicadas ao símbolo ativo.');
    } on FormatException catch (e) {
      if (!context.mounted) return;
      _snack(context, e.message);
    } catch (e) {
      if (!context.mounted) return;
      _snack(context, 'Falha ao importar: $e');
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<bool?> _confirmImport(BuildContext context) {
    return showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Importar backup?'),
        content: const Text(
          'Isso SUBSTITUI todas as configurações atuais pelos valores do '
          'arquivo (símbolos fora do arquivo serão removidos). Deseja continuar?',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(ctx).pop(false),
            child: const Text('Cancelar'),
          ),
          FilledButton(
            onPressed: () => Navigator.of(ctx).pop(true),
            child: const Text('Importar'),
          ),
        ],
      ),
    );
  }

  void _snack(BuildContext context, String msg) {
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(msg)));
  }

  @override
  Widget build(BuildContext context) {
    final body = Consumer<StateService>(builder: (context, state, _) {
      final symbols = state.knownConfigSymbols();
      return ListView(
        padding: const EdgeInsets.all(16),
        children: [
          _title('Backup das configurações'),
          const Text(
            'Exporta todas as configs por símbolo (agentes, thresholds, '
            'bubbles, estrutura, volume, topos/vales, VWAP, daily, trading) '
            'para um JSON. Ao importar, tudo fica pré-selecionado.',
            style: TextStyle(fontSize: 12, color: Colors.grey),
          ),
          const SizedBox(height: 16),
          _title('Símbolos neste aparelho (${symbols.length})'),
          if (symbols.isEmpty)
            const Text('Nenhum símbolo configurado ainda.',
                style: TextStyle(fontSize: 12, color: Colors.grey))
          else
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: symbols
                  .map((s) => Chip(
                        label: Text(s),
                        avatar: s == state.symbol
                            ? const Icon(Icons.check, size: 16)
                            : null,
                      ))
                  .toList(),
            ),
          const SizedBox(height: 8),
          Text(
            state.symbol.isEmpty
                ? 'Símbolo ativo: —'
                : 'Símbolo ativo: ${state.symbol}',
            style: const TextStyle(fontSize: 12, color: Colors.grey),
          ),
          const SizedBox(height: 16),
          Row(
            children: [
              Expanded(
                child: FilledButton.icon(
                  onPressed:
                      _busy ? null : () => _export(context),
                  icon: const Icon(Icons.download, size: 18),
                  label: const Text('Exportar'),
                ),
              ),
              const SizedBox(width: 8),
              Expanded(
                child: OutlinedButton.icon(
                  onPressed: _busy ? null : () => _import(context),
                  icon: const Icon(Icons.upload, size: 18),
                  label: const Text('Importar'),
                ),
              ),
            ],
          ),
          if (_busy) ...[
            const SizedBox(height: 12),
            const LinearProgressIndicator(),
          ],
          if (_lastMessage != null) ...[
            const SizedBox(height: 12),
            Text(_lastMessage!,
                style: const TextStyle(fontSize: 12, color: Colors.grey)),
          ],
          const SizedBox(height: 16),
          _syncPlaceholder(),
        ],
      );
    });

    if (widget.noDrawer) return body;
    return Scaffold(
      appBar: AppBar(title: const Text('Backup')),
      body: body,
    );
  }

  Widget _title(String t) => Padding(
        padding: const EdgeInsets.only(bottom: 8),
        child: Text(t,
            style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 14)),
      );

  /// Espaço reservado da fase 2 (Google Drive silencioso via API key).
  /// Não funcional no V1 — só documenta o próximo passo.
  Widget _syncPlaceholder() => Container(
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(
          color: Colors.blue.withValues(alpha: 0.08),
          borderRadius: BorderRadius.circular(8),
          border: Border.all(color: Colors.blue.withValues(alpha: 0.25)),
        ),
        child: const Row(
          children: [
            Icon(Icons.cloud_queue, color: Colors.blue, size: 20),
            SizedBox(width: 8),
            Expanded(
              child: Text(
                'Sincronização em nuvem (Google Drive) chega na fase 2, '
                'com drawer de Profile para a API key.',
                style: TextStyle(fontSize: 11, color: Colors.blue),
              ),
            ),
          ],
        ),
      );
}
