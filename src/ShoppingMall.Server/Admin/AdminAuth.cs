using ShoppingMall.Protocol;
using System.Text.Json.Nodes;
using ShoppingMall.Server.Data;

namespace ShoppingMall.Server.Admin;

/// <summary>
/// 재고관리(6000)/대시보드(6001) 서버 접속 인증.
/// 원본은 접속만 하면 무조건 관리자로 취급했다(임시 코드). 여기서는 member 테이블에서
/// 아이디/비밀번호를 확인하고 role 이 ADMIN 인 활성 계정만 통과시킨다.
/// </summary>
public static class AdminAuth
{
    /// <summary>성공하면 (true, null), 실패하면 (false, 사용자에게 보여줄 메시지).</summary>
    public static async Task<(bool Ok, string? Message)> VerifyAsync(string loginId, string password)
    {
        if (loginId.Length == 0 || password.Length == 0)
            return (false, "아이디와 비밀번호를 입력하세요.");

        JsonObject? member;
        try
        {
            member = await Db.RunAsync(db => db.OneAsync(
                "SELECT password, is_active, role FROM member WHERE login_id = @login_id",
                ("login_id", loginId)));
        }
        catch (Exception e)
        {
            Console.WriteLine($"[관리자 인증] DB 오류: {e.Message}");
            return (false, "인증 처리 중 오류가 발생했습니다.");
        }

        if (member is null || !Security.HashEquals(member.Str("password") ?? "", Security.HashPassword(password)))
            return (false, "아이디 또는 비밀번호가 올바르지 않습니다.");
        if (!Json.IsTrue(member["is_active"]))
            return (false, "탈퇴한 계정입니다.");
        if (member.Str("role") != "ADMIN")
            return (false, "관리자 권한이 필요합니다.");

        return (true, null);
    }
}
