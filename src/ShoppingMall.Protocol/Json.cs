using System.Text.Json.Nodes;

namespace ShoppingMall.Protocol;

/// <summary>
/// Python 의 request.get("key") 처럼 JsonObject 에서 값을 안전하게 꺼내는 헬퍼.
/// 키가 없거나 타입이 다르면 예외 대신 null 을 돌려준다.
/// </summary>
public static class Json
{
    public static string? Str(this JsonObject o, string key) =>
        o[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    /// <summary>정수만 허용한다. 5.0 같은 실수나 true/false 는 null.</summary>
    public static long? Int(this JsonObject o, string key) =>
        o[key] is JsonValue v && v.TryGetValue<long>(out var l) ? l : null;

    public static bool? Bool(this JsonObject o, string key) =>
        o[key] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : null;

    public static JsonObject? Obj(this JsonObject o, string key) => o[key] as JsonObject;

    public static JsonArray? Arr(this JsonObject o, string key) => o[key] as JsonArray;

    /// <summary>Python 의 truthiness (None, 0, "", False 는 거짓).</summary>
    public static bool IsTrue(JsonNode? n)
    {
        if (n is null) return false;
        if (n is JsonValue v)
        {
            if (v.TryGetValue<bool>(out var b)) return b;
            if (v.TryGetValue<long>(out var l)) return l != 0;
            if (v.TryGetValue<double>(out var d)) return d != 0;
            if (v.TryGetValue<string>(out var s)) return s.Length > 0;
        }
        return true;
    }

    public static bool IsOk(this JsonObject response) => response.Str("status") == "success";

    public static IEnumerable<JsonObject> Objects(this JsonArray array) =>
        array.OfType<JsonObject>();
}

/// <summary>{"status":"success"|"fail", ...} 응답 생성 헬퍼.</summary>
public static class Resp
{
    public static JsonObject Ok(JsonNode? data = null, string? message = null)
    {
        var r = new JsonObject { ["status"] = "success" };
        if (data is not null) r["data"] = data;
        if (message is not null) r["message"] = message;
        return r;
    }

    public static JsonObject Fail(string message) =>
        new() { ["status"] = "fail", ["message"] = message };

    public static JsonArray Array(IEnumerable<JsonNode?> items)
    {
        var a = new JsonArray();
        foreach (var item in items) a.Add(item);
        return a;
    }
}
