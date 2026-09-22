import 'dart:convert';
import 'dart:io';

import 'package:file_picker/file_picker.dart';
import 'package:flutter/foundation.dart';

import '../models/config_backup.dart';

/// Resultado da leitura de um arquivo de backup.
class BackupFileRead {
  final String fileName;
  final Map<String, dynamic> json;
  BackupFileRead({required this.fileName, required this.json});
}

/// IO dos arquivos JSON de backup (export/import local).
///
/// Separado da UI para ser testável: [encode]/[decode] são puros e o
/// acesso ao disco passa por [FilePicker] (funciona no Windows).
class ConfigFileService {
  /// Abre o seletor de arquivos (*.json) e retorna o JSON decodificado.
  /// Retorna null se o usuário cancelar.
  /// Lança [FormatException] se o conteúdo não for JSON/objeto.
  Future<BackupFileRead?> pickBackupFile() async {
    final result = await FilePicker.platform.pickFiles(
      dialogTitle: 'Importar configurações B3WM',
      type: FileType.custom,
      allowedExtensions: ['json'],
      withData: true,
    );
    if (result == null || result.files.isEmpty) return null;
    final picked = result.files.first;

    String? text;
    final bytes = picked.bytes;
    if (bytes != null) {
      text = utf8.decode(bytes);
    } else if (picked.path != null) {
      text = await File(picked.path!).readAsString();
    }
    if (text == null) {
      throw const FormatException('Não foi possível ler o arquivo.');
    }
    return BackupFileRead(
      fileName: picked.name,
      json: decode(text),
    );
  }

  /// Abre o diálogo "salvar como" e grava o backup.
  /// Retorna o caminho salvo, ou null se o usuário cancelar.
  Future<String?> saveBackupFile(ConfigBackup backup) async {
    final content = backup.toPrettyJson();
    final fileName = ConfigBackup.suggestedFileName();
    final path = await FilePicker.platform.saveFile(
      dialogTitle: 'Exportar configurações B3WM',
      fileName: fileName,
      type: FileType.custom,
      allowedExtensions: ['json'],
    );
    if (path == null) return null;
    final file = File(path);
    await file.writeAsString(content, encoding: utf8);
    debugPrint('[backup] exported to $path');
    return path;
  }

  /// Decodifica o texto em `Map` (lança [FormatException] se inválido).
  static Map<String, dynamic> decode(String text) {
    dynamic decoded;
    try {
      decoded = jsonDecode(text);
    } catch (e) {
      throw FormatException('Arquivo inválido: não é um JSON válido ($e).');
    }
    if (decoded is! Map<String, dynamic>) {
      throw const FormatException('Arquivo inválido: raiz deve ser um objeto.');
    }
    return decoded;
  }

  /// Serializa o backup para texto (mesmo conteúdo de [saveBackupFile]).
  static String encode(ConfigBackup backup) => backup.toPrettyJson();
}
