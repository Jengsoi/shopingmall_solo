using System.Data.Common;
using System.Text.Json.Nodes;
using MySqlConnector;

namespace ShoppingMall.Server.Data;

/// <summary>
/// DB 접속 정보. 환경변수로 덮어쓸 수 있고, 없으면 아래 기본값을 쓴다. (기본 비밀번호는 실제로 쓸 때 꼭 바꿀 것)
/// SHOP_DB_HOST / SHOP_DB_PORT / SHOP_DB_USER / SHOP_DB_PASSWORD / SHOP_DB_NAME
/// </summary>
public static class DbConfig
{
    private static string Env(string name, string fallback)
    {
        var v = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrEmpty(v) ? fallback : v;
    }

    /// <summary>MySqlConnector 가 이해하는 접속 문자열 ("Server=...;Port=...;User ID=...;...")</summary>
    public static string ConnectionString
    {
        get
        {
            var b = new MySqlConnectionStringBuilder
            {
                Server = Env("SHOP_DB_HOST", "localhost"),
                Port = uint.Parse(Env("SHOP_DB_PORT", "3306")),
                UserID = Env("SHOP_DB_USER", "root"),
                Password = Env("SHOP_DB_PASSWORD", "1234"),
                Database = Env("SHOP_DB_NAME", "shopping"),
            };
            return b.ConnectionString;
        }
    }
}

/// <summary>
/// "재고가 부족합니다" 처럼 사용자에게 그대로 보여줘도 되는 업무 규칙 위반.
/// 핸들러에서 던지면 트랜잭션이 롤백되고, 메시지가 실패 응답으로 클라이언트에 전달된다.
/// (반대로 SQL 오류 같은 일반 예외는 내부 정보가 담겨 있으므로 클라이언트에 보여주지 않는다)
/// </summary>
public sealed class BusinessException : Exception
{
    public BusinessException(string message) : base(message) { }
}

/// <summary>
/// 트랜잭션 하나에 묶인 SQL 실행 도구. 핸들러는 이 객체로만 DB 에 접근한다.
/// 같은 SqlSession 으로 실행한 SQL 은 모두 한 트랜잭션이라, 중간에 실패하면 전부 없던 일이 된다.
/// 결과 행은 JsonObject 로 돌려주므로 그대로 응답에 실을 수 있다.
/// </summary>
public sealed class SqlSession
{
    private readonly MySqlConnection _conn;
    private readonly DbTransaction _tx;

    internal SqlSession(MySqlConnection conn, DbTransaction tx)
    {
        _conn = conn;
        _tx = tx;
    }

    /// <summary>마지막 INSERT 로 생성된 AUTO_INCREMENT 값 (새로 만든 행의 ID).</summary>
    public long LastInsertId { get; private set; }

    /// <summary>true 이면 핸들러가 정상 반환해도 커밋하지 않고 롤백한다.</summary>
    public bool RollbackRequested { get; private set; }

    /// <summary>예외를 던지지 않고 실패 응답을 돌려주면서도, 지금까지 한 변경은 되돌리고 싶을 때 호출한다.</summary>
    public void RequestRollback() => RollbackRequested = true;

    /// <summary>
    /// SQL 명령을 만든다. 값은 문자열에 이어 붙이지 않고 항상 @이름 파라미터로 넘긴다.
    /// 그래야 사용자 입력에 따옴표 같은 문자가 있어도 SQL 로 해석되지 않는다. (SQL 인젝션 방지)
    /// </summary>
    private MySqlCommand Create(string sql, (string Name, object? Value)[] args)
    {
        var cmd = new MySqlCommand(sql, _conn, (MySqlTransaction)_tx);
        foreach (var (name, value) in args)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = "@" + name;
            p.Value = value ?? DBNull.Value; // C# 의 null 은 SQL 의 NULL 로
            cmd.Parameters.Add(p);
        }
        return cmd;
    }

    /// <summary>SELECT 결과 전체. 각 행은 {"컬럼명": 값, ...} 형태의 JsonObject.</summary>
    public async Task<List<JsonObject>> RowsAsync(string sql, params (string Name, object? Value)[] args)
    {
        await using var cmd = Create(sql, args);
        await using DbDataReader reader = await cmd.ExecuteReaderAsync();

        var rows = new List<JsonObject>();
        while (await reader.ReadAsync())
        {
            var row = new JsonObject();
            for (int i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = ToNode(reader.GetValue(i));
            rows.Add(row);
        }
        return rows;
    }

    /// <summary>SELECT 결과의 첫 행 (없으면 null).</summary>
    public async Task<JsonObject?> OneAsync(string sql, params (string Name, object? Value)[] args)
    {
        var rows = await RowsAsync(sql, args);
        return rows.Count > 0 ? rows[0] : null;
    }

    /// <summary>INSERT/UPDATE/DELETE. 영향받은 행 수를 반환한다. (UPDATE ... WHERE 조건이 안 맞으면 0)</summary>
    public async Task<int> ExecAsync(string sql, params (string Name, object? Value)[] args)
    {
        await using var cmd = Create(sql, args);
        int affected = await cmd.ExecuteNonQueryAsync();
        LastInsertId = cmd.LastInsertedId;
        return affected;
    }

    /// <summary>DB 값을 JSON 값으로 변환한다. 정수는 long, 날짜는 "yyyy-MM-dd HH:mm:ss" 문자열.</summary>
    private static JsonNode? ToNode(object? v) => v switch
    {
        null => null,
        DBNull => null,                     // SQL NULL → JSON null
        bool b => JsonValue.Create(b),
        sbyte or byte or short or ushort or int or uint or long => JsonValue.Create(Convert.ToInt64(v)),
        ulong ul => JsonValue.Create((long)ul),
        decimal d => JsonValue.Create(d),   // SUM() 결과 등
        float or double => JsonValue.Create(Convert.ToDouble(v)),
        DateTime dt => JsonValue.Create(dt.ToString("yyyy-MM-dd HH:mm:ss")),
        string s => JsonValue.Create(s),
        _ => JsonValue.Create(v.ToString())
    };
}

public static class Db
{
    /// <summary>
    /// 연결 하나 + 트랜잭션 하나로 작업을 실행한다.
    /// 정상 종료하면 커밋, 예외가 나거나 RequestRollback() 이 호출됐으면 롤백한다.
    ///
    /// 사용 예:
    ///   var rows = await Db.RunAsync(db => db.RowsAsync("SELECT * FROM category"));
    /// 요청마다 새 연결을 여는 것처럼 보이지만, MySqlConnector 가 내부에서 연결을 재사용(풀링)한다.
    /// </summary>
    public static async Task<T> RunAsync<T>(Func<SqlSession, Task<T>> work)
    {
        await using var conn = new MySqlConnection(DbConfig.ConnectionString);
        await conn.OpenAsync();
        await using DbTransaction tx = await conn.BeginTransactionAsync();

        var session = new SqlSession(conn, tx);
        try
        {
            T result = await work(session);

            if (session.RollbackRequested)
                await tx.RollbackAsync();
            else
                await tx.CommitAsync();

            return result;
        }
        catch
        {
            // 작업 중 예외 → 지금까지의 변경을 모두 되돌리고 예외는 호출한 쪽으로 다시 던진다.
            try { await tx.RollbackAsync(); } catch { /* 이미 끊긴 연결이면 무시 */ }
            throw;
        }
    }
}
