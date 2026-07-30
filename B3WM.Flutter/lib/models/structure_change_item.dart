class StructureChangeItem {
  final DateTime date;
  final bool isUp;
  final double oldValue;
  final double newValue;

  StructureChangeItem({
    required this.date,
    required this.isUp,
    required this.oldValue,
    required this.newValue,
  });
}
