/// Reads API JSON fields supporting camelCase and PascalCase keys.
dynamic jsonField(Map<String, dynamic> json, String camelKey) {
  final direct = json[camelKey];
  if (direct != null) return direct;
  if (camelKey.isEmpty) return null;
  final pascal = camelKey[0].toUpperCase() + camelKey.substring(1);
  return json[pascal];
}

String? jsonStringOrNull(Map<String, dynamic> json, String key) {
  final value = jsonField(json, key);
  if (value == null) return null;
  return value.toString();
}

String jsonString(Map<String, dynamic> json, String key) {
  final value = jsonStringOrNull(json, key);
  if (value == null) {
    throw FormatException('Missing JSON field: $key');
  }
  return value;
}

int jsonInt(Map<String, dynamic> json, String key, {int defaultValue = 0}) {
  final value = jsonField(json, key);
  if (value == null) return defaultValue;
  if (value is int) return value;
  if (value is num) return value.round();
  return int.tryParse(value.toString()) ?? defaultValue;
}

double jsonDouble(Map<String, dynamic> json, String key) {
  final value = jsonField(json, key);
  if (value == null) throw FormatException('Missing JSON field: $key');
  if (value is num) return value.toDouble();
  return double.parse(value.toString());
}

DateTime jsonDateTime(Map<String, dynamic> json, String key) {
  final value = jsonStringOrNull(json, key);
  if (value == null) throw FormatException('Missing JSON field: $key');
  return DateTime.parse(value);
}

List<String> jsonStringList(Map<String, dynamic> json, String key) {
  final value = jsonField(json, key);
  if (value is! List) return const [];
  return value.map((e) => e.toString()).toList();
}
