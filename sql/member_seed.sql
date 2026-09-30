-- shoppingmall/sql/member_seed.sql
-- 통합 테스트용 계정 시드 데이터.
-- member 테이블은 이미 쇼핑몰_스키마_최종_v2.sql로 만들어져 있으므로,
-- 이 파일은 테스트 계정 INSERT만 수행한다.
--
-- 실행 방법: mysql -u root -p shopping < member_seed.sql

USE shopping;

-- 일반 회원 테스트 계정 (member_id = 1로 고정 - 다른 팀 handler들이
-- 과거에 CURRENT_MEMBER_ID=1 기본값으로 테스트했던 것과 호환)
-- 비밀번호: 1234 (SHA-256 해시)
INSERT IGNORE INTO member (member_id, login_id, password, name, role)
VALUES (1, 'test', '03ac674216f3e15c761ee1a5e255f067953623c8b388b4459e13f978d7c846f4', '테스트회원', 'USER');

-- 관리자 로그인 테스트 계정
-- 비밀번호: admin1234 (SHA-256 해시)
INSERT IGNORE INTO member (login_id, password, name, role)
VALUES ('admin', 'ac9689e2272427085e35b9d3e3e8bed88cb3434828b43b86fc0596cad4c6e270', '관리자', 'ADMIN');
