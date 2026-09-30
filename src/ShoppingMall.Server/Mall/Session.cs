using System.Text.Json.Nodes;
using ShoppingMall.Server.Data;

namespace ShoppingMall.Server.Mall;

/// <summary>소켓 연결 하나 = 로그인 세션 하나.</summary>
public sealed class Session
{
    public long? MemberId { get; set; }
    public string? Role { get; set; }

    public bool IsAdmin => Role == "ADMIN";

    public void Clear()
    {
        MemberId = null;
        Role = null;
    }
}

/// <summary>모든 핸들러 공통 시그니처. Python 의 handler(cursor, request) 에 세션이 추가됐다.</summary>
public delegate Task<JsonObject> Handler(SqlSession db, JsonObject request, Session session);
