-- ============================================================
-- shopping 데이터베이스 스키마 (추정본)
--
-- 원본 프로젝트에는 "쇼핑몰_스키마_최종_v2.sql" 이 포함되어 있지 않아서,
-- 소스코드의 SQL 문에서 쓰이는 테이블/컬럼을 보고 만든 파일입니다.
-- 이미 팀에서 쓰던 스키마 파일이 있다면 그것을 우선 사용하세요.
--
-- 실행: mysql -u root -p < schema.sql
-- ============================================================
CREATE DATABASE IF NOT EXISTS shopping DEFAULT CHARACTER SET utf8mb4;
USE shopping;

CREATE TABLE IF NOT EXISTS member (
    member_id  INT AUTO_INCREMENT PRIMARY KEY,
    login_id   VARCHAR(50)  NOT NULL UNIQUE,
    password   CHAR(64)     NOT NULL,              -- SHA-256 hex
    name       VARCHAR(50)  NOT NULL,
    address    VARCHAR(200) NULL,
    email      VARCHAR(100) NULL,
    phone      VARCHAR(30)  NULL,
    gender     VARCHAR(1)   NULL,                  -- 'M' / 'F' / ''
    role       VARCHAR(10)  NOT NULL DEFAULT 'USER',   -- 'USER' / 'ADMIN'
    is_active  BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS category (
    category_id INT AUTO_INCREMENT PRIMARY KEY,
    name        VARCHAR(50) NOT NULL UNIQUE,
    is_active   BOOLEAN     NOT NULL DEFAULT TRUE
);

CREATE TABLE IF NOT EXISTS product (
    product_id  INT AUTO_INCREMENT PRIMARY KEY,
    category_id INT          NOT NULL,
    name        VARCHAR(100) NOT NULL,
    description TEXT         NULL,
    color       VARCHAR(30)  NULL,
    size        VARCHAR(30)  NULL,
    price       INT          NOT NULL,
    stock       INT          NOT NULL DEFAULT 0,
    is_active   BOOLEAN      NOT NULL DEFAULT TRUE,   -- 수정 시 기존 행은 FALSE 로 남긴다 (이력 보존)
    created_at  DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (category_id) REFERENCES category (category_id)
);

CREATE TABLE IF NOT EXISTS cart (
    cart_id    INT AUTO_INCREMENT PRIMARY KEY,
    member_id  INT NOT NULL,
    product_id INT NOT NULL,
    quantity   INT NOT NULL,
    UNIQUE (member_id, product_id),
    FOREIGN KEY (member_id)  REFERENCES member (member_id),
    FOREIGN KEY (product_id) REFERENCES product (product_id)
);

CREATE TABLE IF NOT EXISTS orders (
    order_id   INT AUTO_INCREMENT PRIMARY KEY,
    member_id  INT         NOT NULL,
    status     VARCHAR(20) NOT NULL DEFAULT 'PAID',
    ordered_at DATETIME    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (member_id) REFERENCES member (member_id)
);

CREATE TABLE IF NOT EXISTS order_item (
    order_item_id INT AUTO_INCREMENT PRIMARY KEY,
    order_id      INT          NOT NULL,
    product_id    INT          NOT NULL,
    product_name  VARCHAR(100) NOT NULL,             -- 주문 시점의 상품명/가격을 그대로 보관
    price         INT          NOT NULL,
    quantity      INT          NOT NULL,
    FOREIGN KEY (order_id)   REFERENCES orders (order_id),
    FOREIGN KEY (product_id) REFERENCES product (product_id)
);

CREATE TABLE IF NOT EXISTS board_post (
    post_id    INT AUTO_INCREMENT PRIMARY KEY,
    member_id  INT          NOT NULL,
    title      VARCHAR(200) NOT NULL,
    content    TEXT         NULL,
    created_at DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (member_id) REFERENCES member (member_id)
);

CREATE TABLE IF NOT EXISTS comment (
    comment_id INT AUTO_INCREMENT PRIMARY KEY,
    post_id    INT      NOT NULL,
    member_id  INT      NOT NULL,
    content    TEXT     NOT NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (post_id)   REFERENCES board_post (post_id),
    FOREIGN KEY (member_id) REFERENCES member (member_id)
);
