using System.Data.Common;
using System.Text.Json.Nodes;
using MySqlConnector;

namespace ShoppingMall.Server.Data;

/// <summary>
/// DB 접속 정보. 환경변수로 덮어쓸 수 있고, 없으면 기존 Python 코드(db.py)와 같은 기본값을 쓴다.
/// SHOP_DB_HOST / SHOP_DB_PORT / SHOP_DB_USER / SHOP_DB_PASSWORD / SHOP_DB_NAME
/// </summary>
public static class DbConfig
{
    private static string Env(string name, string fallback)
    {
        var v = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrEmpty(v) ? fallback : v;
    }

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

/// <summary>클라이언트에게 그대로 보여줘도 되는 업무상 오류. 트랜잭션은 롤백된다.</summary>
public sealed class BusinessException : Exception
{
    public BusinessException(string message) : base(message) { }
}

/// <summary>
/// 트랜잭션 하나에 묶인 SQL 실행 도구. Python 핸들러의 cursor 에 해당한다.
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

    /// <summary>마지막 INSERT 로 생성된 AUTO_INCREMENT 값 (cursor.lastrowid).</summary>
    public long LastInsertId { get; private set; }

    /// <summary>true 이면 핸들러가 정상 반환해도 커밋하지 않고 롤백한다.</summary>
    public bool RollbackRequested { get; private set; }

    public void RequestRollback() => RollbackRequested = true;

    private MySqlCommand Create(string sql, (string Name, object? Value)[] args)
    {
        var cmd = new MySqlCommand(sql, _conn, (MySqlTransaction)_tx);
        foreach (var (name, value) in args)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = "@" + name;
            p.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }
        return cmd;
    }

    /// <summary>SELECT 결과 전체.</summary>
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

    /// <summary>SELECT 결과의 첫 행 (없으면 null). cursor.fetchone().</summary>
    public async Task<JsonObject?> OneAsync(string sql, params (string Name, object? Value)[] args)
    {
        var rows = await RowsAsync(sql, args);
        return rows.Count > 0 ? rows[0] : null;
    }

    /// <summary>INSERT/UPDATE/DELETE. 영향받은 행 수(cursor.rowcount)를 반환한다.</summary>
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
        DBNull => null,
        bool b => JsonValue.Create(b),
        sbyte or byte or short or ushort or int or uint or long => JsonValue.Create(Convert.ToInt64(v)),
        ulong ul => JsonValue.Create((long)ul),
        decimal d => JsonValue.Create(d),
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
            try { await tx.RollbackAsync(); } catch { /* 이미 끊긴 연결이면 무시 */ }
            throw;
        }
    }
}
