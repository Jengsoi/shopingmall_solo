using System.Text.Json.Nodes;
using ShoppingMall.Protocol;
using ShoppingMall.Server.Data;

namespace ShoppingMall.Server.Mall;

/// <summary>회원가입 / 로그인 / 회원정보 (handlers/auth_handler.py)</summary>
public static class AuthHandlers
{
    public static void Register(Dictionary<string, Handler> map)
    {
        map["signup"] = SignupAsync;
        map["check_id"] = CheckIdAsync;
        map["login"] = LoginAsync;
        map["logout"] = LogoutAsync;
        map["member_info"] = MemberInfoAsync;
        map["member_update"] = MemberUpdateAsync;
        map["member_withdraw"] = MemberWithdrawAsync;
    }

    private static async Task<JsonObject> SignupAsync(SqlSession db, JsonObject req, Session s)
    {
        string loginId = (req.Str("login_id") ?? "").Trim();
        string password = req.Str("password") ?? "";
        string name = (req.Str("name") ?? "").Trim();
        string address = req.Str("address") ?? "";
        string email = req.Str("email") ?? "";
        string phone = req.Str("phone") ?? "";
        string gender = req.Str("gender") ?? "";

        if (loginId.Length == 0 || password.Length == 0 || name.Length == 0)
            return Resp.Fail("아이디, 비밀번호, 이름은 필수입니다.");

        var exists = await db.OneAsync("SELECT member_id FROM member WHERE login_id = @login_id", ("login_id", loginId));
        if (exists is not null)
            return Resp.Fail("이미 사용 중인 아이디입니다.");

        await db.ExecAsync(@"
            INSERT INTO member (login_id, password, name, address, email, phone, gender)
            VALUES (@login_id, @password, @name, @address, @email, @phone, @gender)",
            ("login_id", loginId), ("password", Security.HashPassword(password)), ("name", name),
            ("address", address), ("email", email), ("phone", phone), ("gender", gender));

        return Resp.Ok(new JsonObject { ["member_id"] = db.LastInsertId });
    }

    private static async Task<JsonObject> CheckIdAsync(SqlSession db, JsonObject req, Session s)
    {
        string loginId = (req.Str("login_id") ?? "").Trim();
        if (loginId.Length == 0)
            return Resp.Fail("login_id가 필요합니다.");

        var row = await db.OneAsync("SELECT member_id FROM member WHERE login_id = @login_id", ("login_id", loginId));
        return Resp.Ok(new JsonObject { ["available"] = row is null });
    }

    private static async Task<JsonObject> LoginAsync(SqlSession db, JsonObject req, Session s)
    {
        string loginId = (req.Str("login_id") ?? "").Trim();
        string password = req.Str("password") ?? "";

        if (loginId.Length == 0 || password.Length == 0)
            return Resp.Fail("아이디와 비밀번호를 입력하세요.");

        var member = await db.OneAsync(
            "SELECT member_id, login_id, name, password, is_active, role FROM member WHERE login_id = @login_id",
            ("login_id", loginId));

        if (member is null || !Security.HashEquals(member.Str("password") ?? "", Security.HashPassword(password)))
            return Resp.Fail("아이디 또는 비밀번호가 올바르지 않습니다.");
        if (!Json.IsTrue(member["is_active"]))
            return Resp.Fail("탈퇴한 계정입니다.");

        return Resp.Ok(new JsonObject
        {
            ["member_id"] = member.Int("member_id"),
            ["login_id"] = member.Str("login_id"),
            ["name"] = member.Str("name"),
            ["role"] = member.Str("role"),
        });
    }

    private static Task<JsonObject> LogoutAsync(SqlSession db, JsonObject req, Session s)
    {
        // 실제 세션 초기화는 MallServer 에서 처리한다.
        return Task.FromResult(Resp.Ok(message: "로그아웃 되었습니다."));
    }

    private static async Task<JsonObject> MemberInfoAsync(SqlSession db, JsonObject req, Session s)
    {
        if (!Json.IsTrue(req["member_id"]))
            return Resp.Fail("로그인이 필요합니다.");

        var row = await db.OneAsync(@"
            SELECT member_id, login_id, name, address, email, phone, gender, role, created_at
            FROM member
            WHERE member_id = @member_id AND is_active = TRUE",
            ("member_id", req.Int("member_id")));

        return row is null ? Resp.Fail("회원을 찾을 수 없습니다.") : Resp.Ok(row);
    }

    private static readonly string[] UpdatableColumns = { "name", "address", "email", "phone", "gender" };

    private static async Task<JsonObject> MemberUpdateAsync(SqlSession db, JsonObject req, Session s)
    {
        if (!Json.IsTrue(req["member_id"]))
            return Resp.Fail("로그인이 필요합니다.");

        var sets = new List<string>();
        var args = new List<(string, object?)>();
        foreach (var column in UpdatableColumns)
        {
            // JSON null(=Python None)이면 수정하지 않는다.
            var value = req.Str(column);
            if (value is null) continue;
            sets.Add($"{column} = @{column}");
            args.Add((column, value));
        }

        if (sets.Count == 0)
            return Resp.Fail("수정할 내용이 없습니다.");

        args.Add(("member_id", req.Int("member_id")));
        await db.ExecAsync($"UPDATE member SET {string.Join(", ", sets)} WHERE member_id = @member_id", args.ToArray());
        return Resp.Ok(message: "회원정보가 수정되었습니다.");
    }

    private static async Task<JsonObject> MemberWithdrawAsync(SqlSession db, JsonObject req, Session s)
    {
        if (!Json.IsTrue(req["member_id"]))
            return Resp.Fail("로그인이 필요합니다.");

        await db.ExecAsync("UPDATE member SET is_active = FALSE WHERE member_id = @member_id",
            ("member_id", req.Int("member_id")));
        return Resp.Ok(message: "회원 탈퇴가 완료되었습니다.");
    }
}
