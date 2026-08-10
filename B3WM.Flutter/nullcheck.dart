class C {
  List<int> list;
  C(this.list);
}

List<int> getterDynamicNull() {
  dynamic x = null;
  return x;
}

void main() {
  try {
    getterDynamicNull();
  } catch (e) {
    print('GETTER_DYNAMIC: $e');
  }

  dynamic jsonObj = {'selectedAgents': null};
  try {
    final sel = (jsonObj['selectedAgents'] as List<dynamic>?)?.cast<int>() ?? [];
    print('JSON_NULL_SEL: $sel');
  } catch (e) {
    print('JSON_NULL_SEL_ERR: $e');
  }

  dynamic fieldHolder = null;
  try {
    final C c = (fieldHolder as dynamic);
    final Set<int> s = <int>{...c.list};
    print('SPREAD: $s');
  } catch (e) {
    print('SPREAD_ERR: $e');
  }
}
