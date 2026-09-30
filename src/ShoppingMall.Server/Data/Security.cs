using System.Security.Cryptography;
using System.Text;

namespace ShoppingMall.Server.Data;

/// <summary>비밀번호 해시 관련 도우미.</summary>
public static class Security
{
    /// <summary>
    /// 비밀번호를 SHA-256 해시(소문자 16진수 64글자)로 바꾼다. DB 에는 원문이 아니라 이 값만 저장한다.
    /// member_seed.sql 의 테스트 계정 해시도 같은 방식으로 만들었다.
    /// (참고: 실제 서비스라면 솔트를 쓰는 bcrypt/Argon2 같은 전용 알고리즘이 더 안전하다)
    /// </summary>
    public static string HashPassword(string password) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password))).ToLowerInvariant();

    /// <summary>
    /// 두 해시가 같은지 비교한다. 일반 == 비교는 앞글자부터 다르면 바로 끝나서 걸린 시간으로 정보가 샐 수 있으므로,
    /// 항상 끝까지 비교하는 FixedTimeEquals 를 쓴다. (타이밍 공격 방지)
    /// </summary>
    public static bool HashEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
