import 'package:flutter/material.dart';

/// Render compacto do `DetailsMd` da estratégia (issue #22, problema 3).
/// Markdown-lite próprio (sem nova dependência): entende `## título`,
/// `-`/`*` bullets, `**negrito**` e `` `code` ``, com fallback para texto
/// puro quando vazio. Estilo compacto com scroll interno.
class StrategyDetailsMd extends StatelessWidget {
  final String markdown;
  const StrategyDetailsMd({super.key, required this.markdown});

  @override
  Widget build(BuildContext context) {
    final md = markdown.trim();
    if (md.isEmpty) {
      return const Text('Sem detalhes para esta estratégia.',
          style: TextStyle(fontSize: 11, color: Colors.grey));
    }
    return Container(
      width: double.infinity,
      constraints: const BoxConstraints(maxHeight: 160),
      padding: const EdgeInsets.all(8),
      decoration: BoxDecoration(
        color: const Color(0xFF1e1e1e),
        borderRadius: BorderRadius.circular(4),
        border: Border.all(color: const Color(0xFF3d3d3d)),
      ),
      child: SingleChildScrollView(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: _blocks(md),
        ),
      ),
    );
  }

  List<Widget> _blocks(String md) {
    final out = <Widget>[];
    for (final raw in md.split('\n')) {
      final line = raw.trimRight();
      final t = line.trim();
      if (t.isEmpty) {
        out.add(const SizedBox(height: 4));
      } else if (t.startsWith('## ')) {
        out.add(Padding(
          padding: const EdgeInsets.only(bottom: 2),
          child: Text(t.substring(3).trim(),
              style: const TextStyle(
                  fontSize: 12, fontWeight: FontWeight.bold)),
        ));
      } else if (t.startsWith('# ')) {
        out.add(Text(t.substring(2).trim(),
            style:
                const TextStyle(fontSize: 12, fontWeight: FontWeight.bold)));
      } else if (t.startsWith('- ') || t.startsWith('* ')) {
        out.add(Padding(
          padding: const EdgeInsets.only(left: 8, bottom: 2),
          child: _rich('• ${t.substring(2)}', 11, Colors.grey.shade300),
        ));
      } else {
        out.add(Padding(
          padding: const EdgeInsets.only(bottom: 2),
          child: _rich(t, 11, Colors.grey.shade300),
        ));
      }
    }
    return out;
  }

  Widget _rich(String text, double size, Color color) {
    // Suporte mínimo a **bold** e `code` por linha.
    final spans = <TextSpan>[];
    final exp = RegExp(r'(\*\*.+?\*\*|`.+?`)');
    var pos = 0;
    for (final m in exp.allMatches(text)) {
      if (m.start > pos) {
        spans.add(TextSpan(
            text: text.substring(pos, m.start),
            style: TextStyle(fontSize: size, color: color)));
      }
      final tok = m.group(0)!;
      if (tok.startsWith('**')) {
        spans.add(TextSpan(
            text: tok.substring(2, tok.length - 2),
            style: TextStyle(
                fontSize: size, color: color, fontWeight: FontWeight.bold)));
      } else {
        spans.add(TextSpan(
            text: tok.substring(1, tok.length - 1),
            style: TextStyle(
                fontSize: size,
                color: Colors.white,
                backgroundColor: const Color(0xFF3d3d3d))));
      }
      pos = m.end;
    }
    if (pos < text.length) {
      spans.add(TextSpan(
          text: text.substring(pos),
          style: TextStyle(fontSize: size, color: color)));
    }
    return Text.rich(TextSpan(children: spans));
  }
}
