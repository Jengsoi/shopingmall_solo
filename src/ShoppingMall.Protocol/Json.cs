using System.Text.Json.Nodes;

namespace ShoppingMall.Protocol;

/// <summary>
/// JsonObject 에서 값을 안전하게 꺼내는 확장 메서드 모음.
/// 네트워크로 받은 JSON 은 키가 빠졌거나 타입이 틀릴 수 있으므로, 예외를 던지는 대신 null 을 돌려준다.
/// 사용 예: request.Str("login_id"), request.Int("product_id") ?? 0
/// </summary>
public static class Json
{
    /// <summary>문자열 값. 키가 없거나 문자열이 아니면 null.</summary>
    public static string? Str(this JsonObject o, string key) =>
        o[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    /// <summary>정수만 허용한다. 5.0 같은 실수나 true/false 는 null.</summary>
    public static long? Int(this JsonObject o, string key) =>
        o[key] is JsonValue v && v.TryGetValue<long>(out var l) ? l : null;

    /// <summary>true/false 값. 1/0 이나 "true" 문자열은 인정하지 않는다(null).</summary>
    public static bool? Bool(this JsonObject o, string key) =>
        o[key] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : null;

    /// <summary>중첩 객체 {...}. 없거나 객체가 아니면 null.</summary>
    public static JsonObject? Obj(this JsonObject o, string key) => o[key] as JsonObject;

    /// <summary>배열 [...]. 없거나 배열이 아니면 null.</summary>
    public static JsonArray? Arr(this JsonObject o, string key) => o[key] as JsonArray;

    /// <summary>
    /// 값이 "참"으로 볼 수 있는지 판단한다. null, false, 0, 빈 문자열은 거짓이고 나머지는 참.
    /// DB 의 BOOLEAN 컬럼이 드라이버에 따라 true/false 또는 1/0 으로 올 수 있어서 둘 다 처리한다.
    /// </summary>
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
        return true; // 객체·배열은 참
    }

    /// <summary>쇼핑몰 서버 응답 {"status":"success", ...} 이 성공인지.</summary>
    public static bool IsOk(this JsonObject response) => response.Str("status") == "success";

    /// <summary>배열에서 객체({...})인 원소만 골라낸다. 이상한 원소가 섞여 있어도 건너뛴다.</summary>
    public static IEnumerable<JsonObject> Objects(this JsonArray array) =>
        array.OfType<JsonObject>();
}

/// <summary>
/// 쇼핑몰 서버(5000)의 응답을 만드는 도우미. 모든 응답은 아래 두 형식 중 하나다.
///   성공: {"status":"success", "data": ..., "message": ...}   (data, message 는 있을 때만)
///   실패: {"status":"fail", "message": "사용자에게 보여줄 문구"}
/// </summary>
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

    /// <summary>
    /// DB 조회 결과(행 목록)를 JSON 배열로 만든다.
    /// JsonNode 는 부모를 하나만 가질 수 있어서, 목록을 그대로 넣지 않고 새 배열에 하나씩 담는다.
    /// </summary>
    public static JsonArray Array(IEnumerable<JsonNode?> items)
    {
        var a = new JsonArray();
        foreach (var item in items) a.Add(item);
        return a;
    }
}
