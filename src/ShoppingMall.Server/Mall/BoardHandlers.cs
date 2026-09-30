using System.Text.Json.Nodes;
using ShoppingMall.Protocol;
using ShoppingMall.Server.Data;

namespace ShoppingMall.Server.Mall;

/// <summary>
/// 게시판 (handlers/board_handler.py)
/// 조회는 누구나 가능하고, 글/댓글 쓰기·수정·삭제는 로그인이 필요하다.
/// (원본은 로그인 세션이 없으면 1번 회원으로 처리하는 임시 코드였다.)
/// </summary>
public static class BoardHandlers
{
    public static void Register(Dictionary<string, Handler> map)
    {
        map["board_list"] = BoardListAsync;
        map["board_detail"] = BoardDetailAsync;
        map["board_create"] = BoardCreateAsync;
        map["board_update"] = BoardUpdateAsync;
        map["board_delete"] = BoardDeleteAsync;
        map["comment_create"] = CommentCreateAsync;
        map["comment_update"] = CommentUpdateAsync;
        map["comment_delete"] = CommentDeleteAsync;
    }

    private static bool TryMember(JsonObject req, out long memberId)
    {
        memberId = req.Int("member_id") ?? 0;
        return memberId != 0;
    }

    private static async Task<JsonObject> BoardListAsync(SqlSession db, JsonObject req, Session s)
    {
        long page = Math.Max(1, req.Int("page") ?? 1);
        long size = Math.Clamp(req.Int("size") ?? 20, 1, 100);
        string keyword = (req.Str("keyword") ?? "").Trim();
        long offset = (page - 1) * size;

        string where = "";
        var args = new List<(string, object?)>();
        if (keyword.Length > 0)
        {
            where = "WHERE bp.title LIKE @keyword";
            args.Add(("keyword", $"%{keyword}%"));
        }

        var totalRow = await db.OneAsync($"SELECT COUNT(*) AS total FROM board_post bp {where}", args.ToArray());
        long total = totalRow?.Int("total") ?? 0;

        args.Add(("size", size));
        args.Add(("offset", offset));
        var rows = await db.RowsAsync($@"
            SELECT bp.post_id, bp.title, bp.created_at, m.name AS author,
                   (SELECT COUNT(*) FROM comment c WHERE c.post_id = bp.post_id) AS comment_count
            FROM board_post bp
            JOIN member m ON bp.member_id = m.member_id
            {where}
            ORDER BY bp.post_id DESC
            LIMIT @size OFFSET @offset", args.ToArray());

        return Resp.Ok(new JsonObject
        {
            ["posts"] = Resp.Array(rows),
            ["total"] = total,
            ["page"] = page,
            ["size"] = size,
        });
    }

    private static async Task<JsonObject> BoardDetailAsync(SqlSession db, JsonObject req, Session s)
    {
        long? postId = req.Int("post_id");
        if (postId is null or 0)
            return Resp.Fail("post_id가 필요합니다.");

        var post = await db.OneAsync(@"
            SELECT bp.post_id, bp.member_id, bp.title, bp.content, bp.created_at, m.name AS author
            FROM board_post bp
            JOIN member m ON bp.member_id = m.member_id
            WHERE bp.post_id = @post_id",
            ("post_id", postId));
        if (post is null)
            return Resp.Fail("게시글을 찾을 수 없습니다.");

        var comments = await db.RowsAsync(@"
            SELECT c.comment_id, c.member_id, c.content, c.created_at, m.name AS author
            FROM comment c
            JOIN member m ON c.member_id = m.member_id
            WHERE c.post_id = @post_id
            ORDER BY c.comment_id",
            ("post_id", postId));

        post["comments"] = Resp.Array(comments);
        return Resp.Ok(post);
    }

    private static async Task<JsonObject> BoardCreateAsync(SqlSession db, JsonObject req, Session s)
    {
        if (!TryMember(req, out var memberId)) return Resp.Fail("로그인이 필요합니다.");

        string title = (req.Str("title") ?? "").Trim();
        string content = req.Str("content") ?? "";
        if (title.Length == 0)
            return Resp.Fail("제목을 입력하세요.");

        await db.ExecAsync(
            "INSERT INTO board_post (member_id, title, content) VALUES (@member_id, @title, @content)",
            ("member_id", memberId), ("title", title), ("content", content));
        return Resp.Ok(new JsonObject { ["post_id"] = db.LastInsertId });
    }

    private static async Task<JsonObject> BoardUpdateAsync(SqlSession db, JsonObject req, Session s)
    {
        if (!TryMember(req, out var memberId)) return Resp.Fail("로그인이 필요합니다.");

        long? postId = req.Int("post_id");
        if (postId is null or 0)
            return Resp.Fail("post_id가 필요합니다.");

        var post = await db.OneAsync("SELECT member_id FROM board_post WHERE post_id = @post_id", ("post_id", postId));
        if (post is null)
            return Resp.Fail("게시글을 찾을 수 없습니다.");
        if (post.Int("member_id") != memberId)
            return Resp.Fail("작성자만 수정할 수 있습니다.");

        var sets = new List<string>();
        var args = new List<(string, object?)>();

        string? title = req.Str("title");
        if (title is not null)
        {
            title = title.Trim();
            if (title.Length == 0)
                return Resp.Fail("제목을 입력하세요.");
            sets.Add("title = @title");
            args.Add(("title", title));
        }

        string? content = req.Str("content");
        if (content is not null)
        {
            sets.Add("content = @content");
            args.Add(("content", content));
        }

        if (sets.Count == 0)
            return Resp.Fail("수정할 내용이 없습니다.");

        args.Add(("post_id", postId));
        await db.ExecAsync($"UPDATE board_post SET {string.Join(", ", sets)} WHERE post_id = @post_id", args.ToArray());
        return Resp.Ok(message: "게시글이 수정되었습니다.");
    }

    private static async Task<JsonObject> BoardDeleteAsync(SqlSession db, JsonObject req, Session s)
    {
        if (!TryMember(req, out var memberId)) return Resp.Fail("로그인이 필요합니다.");

        long? postId = req.Int("post_id");
        if (postId is null or 0)
            return Resp.Fail("post_id가 필요합니다.");

        var post = await db.OneAsync("SELECT member_id FROM board_post WHERE post_id = @post_id", ("post_id", postId));
        if (post is null)
            return Resp.Fail("게시글을 찾을 수 없습니다.");
        if (post.Int("member_id") != memberId)
            return Resp.Fail("작성자만 삭제할 수 있습니다.");

        await db.ExecAsync("DELETE FROM comment WHERE post_id = @post_id", ("post_id", postId));
        await db.ExecAsync("DELETE FROM board_post WHERE post_id = @post_id", ("post_id", postId));
        return Resp.Ok(message: "게시글이 삭제되었습니다.");
    }

    private static async Task<JsonObject> CommentCreateAsync(SqlSession db, JsonObject req, Session s)
    {
        if (!TryMember(req, out var memberId)) return Resp.Fail("로그인이 필요합니다.");

        long? postId = req.Int("post_id");
        string content = (req.Str("content") ?? "").Trim();

        if (postId is null or 0)
            return Resp.Fail("post_id가 필요합니다.");
        if (content.Length == 0)
            return Resp.Fail("댓글 내용을 입력하세요.");

        var post = await db.OneAsync("SELECT post_id FROM board_post WHERE post_id = @post_id", ("post_id", postId));
        if (post is null)
            return Resp.Fail("게시글을 찾을 수 없습니다.");

        await db.ExecAsync(
            "INSERT INTO comment (post_id, member_id, content) VALUES (@post_id, @member_id, @content)",
            ("post_id", postId), ("member_id", memberId), ("content", content));
        return Resp.Ok(new JsonObject { ["comment_id"] = db.LastInsertId });
    }

    private static async Task<JsonObject> CommentUpdateAsync(SqlSession db, JsonObject req, Session s)
    {
        if (!TryMember(req, out var memberId)) return Resp.Fail("로그인이 필요합니다.");

        long? commentId = req.Int("comment_id");
        string content = (req.Str("content") ?? "").Trim();

        if (commentId is null or 0)
            return Resp.Fail("comment_id가 필요합니다.");
        if (content.Length == 0)
            return Resp.Fail("댓글 내용을 입력하세요.");

        var comment = await db.OneAsync("SELECT member_id FROM comment WHERE comment_id = @comment_id", ("comment_id", commentId));
        if (comment is null)
            return Resp.Fail("댓글을 찾을 수 없습니다.");
        if (comment.Int("member_id") != memberId)
            return Resp.Fail("작성자만 수정할 수 있습니다.");

        await db.ExecAsync("UPDATE comment SET content = @content WHERE comment_id = @comment_id",
            ("content", content), ("comment_id", commentId));
        return Resp.Ok(message: "댓글이 수정되었습니다.");
    }

    private static async Task<JsonObject> CommentDeleteAsync(SqlSession db, JsonObject req, Session s)
    {
        if (!TryMember(req, out var memberId)) return Resp.Fail("로그인이 필요합니다.");

        long? commentId = req.Int("comment_id");
        if (commentId is null or 0)
            return Resp.Fail("comment_id가 필요합니다.");

        var comment = await db.OneAsync("SELECT member_id FROM comment WHERE comment_id = @comment_id", ("comment_id", commentId));
        if (comment is null)
            return Resp.Fail("댓글을 찾을 수 없습니다.");
        if (comment.Int("member_id") != memberId)
            return Resp.Fail("작성자만 삭제할 수 있습니다.");

        await db.ExecAsync("DELETE FROM comment WHERE comment_id = @comment_id", ("comment_id", commentId));
        return Resp.Ok(message: "댓글이 삭제되었습니다.");
    }
}
