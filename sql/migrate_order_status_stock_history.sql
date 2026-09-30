-- ============================================================
-- 이미 만들어 둔 shopping DB 에 주문 상태 관리 / 재고 변경 이력 기능을 추가한다. (한 번만 실행)
-- 새로 설치할 때는 schema.sql 에 이미 들어 있으므로 실행할 필요가 없다.
--
-- 실행: mysql -u root -p shopping < sql/migrate_order_status_stock_history.sql
-- ============================================================

-- 상품 수정으로 생긴 새 행이 최초 상품을 가리키도록 한다. (최초 상품은 NULL)
ALTER TABLE product
    ADD COLUMN origin_product_id INT NULL AFTER is_active,
    ADD CONSTRAINT fk_product_origin FOREIGN KEY (origin_product_id) REFERENCES product (product_id),
    ADD INDEX idx_product_origin (origin_product_id);

CREATE TABLE IF NOT EXISTS stock_history (
    history_id        INT AUTO_INCREMENT PRIMARY KEY,
    product_id        INT         NOT NULL,
    origin_product_id INT         NOT NULL,
    change_qty        INT         NOT NULL,
    stock_before      INT         NOT NULL,
    stock_after       INT         NOT NULL,
    reason            VARCHAR(20) NOT NULL,
    order_id          INT         NULL,
    member_id         INT         NULL,
    created_at        DATETIME    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (product_id) REFERENCES product (product_id),
    FOREIGN KEY (order_id)   REFERENCES orders (order_id),
    FOREIGN KEY (member_id)  REFERENCES member (member_id),
    INDEX idx_stock_history_origin (origin_product_id, history_id)
);
