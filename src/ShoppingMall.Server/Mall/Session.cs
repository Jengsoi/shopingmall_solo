using System.Text.Json.Nodes;
using ShoppingMall.Server.Data;

namespace ShoppingMall.Server.Mall;

/// <summary>
/// 로그인 상태. 소켓 연결 하나 = 세션 하나.
/// 클라이언트는 로그인한 뒤 같은 연결을 계속 쓰므로, 서버는 연결마다 "누가 로그인했는지" 를 여기에 기억해 둔다.
/// 연결이 끊기면 세션도 함께 사라진다.
/// </summary>
public sealed class Session
{
    /// <summary>로그인한 회원 ID. 로그인 전이면 null.</summary>
    public long? MemberId { get; set; }

    /// <summary>"USER" 또는 "ADMIN"</summary>
    public string? Role { get; set; }

    public bool IsAdmin => Role == "ADMIN";

    /// <summary>로그아웃·회원탈퇴 시 호출</summary>
    public void Clear()
    {
        MemberId = null;
        Role = null;
    }
}

/// <summary>
/// 모든 쇼핑몰 요청 처리 함수(핸들러)의 공통 모양.
///   db      : 이 요청 전용 트랜잭션
///   request : 클라이언트가 보낸 JSON (member_id 는 서버가 세션 값으로 채워 넣음)
///   session : 이 연결의 로그인 상태
/// 반환값이 그대로 클라이언트에 응답으로 전송된다.
/// </summary>
public delegate Task<JsonObject> Handler(SqlSession db, JsonObject request, Session session);
