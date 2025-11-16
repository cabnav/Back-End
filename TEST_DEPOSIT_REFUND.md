# Hướng Dẫn Test Chức Năng Hoàn Cọc Khi Final Cost < Deposit Amount

## 📋 Mục đích
Test chức năng tự động hoàn tiền cọc dư khi `deposit_amount > cost_after_discount`.

## 🎯 Tình huống test

### Case 1: Deposit lớn hơn chi phí
- **Deposit**: 100,000 VND
- **Cost Before Discount**: 80,000 VND
- **Applied Discount**: 30,000 VND
- **Cost After Discount**: 50,000 VND (80,000 - 30,000)
- **Final Cost**: 0 VND (max(0, 50,000 - 100,000))
- **Kỳ vọng hoàn cọc**: 50,000 VND (100,000 - 50,000)

### Case 2: Deposit bằng chi phí
- **Deposit**: 50,000 VND
- **Cost After Discount**: 50,000 VND
- **Final Cost**: 0 VND
- **Kỳ vọng hoàn cọc**: 0 VND (không hoàn)

### Case 3: Deposit nhỏ hơn chi phí
- **Deposit**: 30,000 VND
- **Cost After Discount**: 50,000 VND
- **Final Cost**: 20,000 VND (50,000 - 30,000)
- **Kỳ vọng hoàn cọc**: 0 VND (không hoàn, phải trả thêm 20,000)

## 🚀 Các bước test

### Bước 1: Chuẩn bị dữ liệu

1. **Chạy SQL script** để tạo hoặc cập nhật session test:
   ```sql
   -- File: Migrations/Test_DepositRefund.sql
   ```

2. **Hoặc tạo session test thủ công**:
   - Tạo reservation với deposit payment = 100,000 VND
   - Tạo charging session và hoàn thành với:
     - `cost_before_discount` = 80,000
     - `applied_discount` = 30,000
     - `deposit_amount` = 100,000 (sẽ được set tự động từ reservation)
     - `final_cost` = 0 (tự động tính)

### Bước 2: Ghi lại dữ liệu ban đầu

```sql
-- Ghi lại wallet balance trước khi test
SELECT user_id, wallet_balance 
FROM [User] 
WHERE user_id = <your_user_id>;

-- Ghi lại session info
SELECT 
    session_id,
    cost_before_discount,
    applied_discount,
    (cost_before_discount - ISNULL(applied_discount, 0)) as cost_after_discount,
    deposit_amount,
    final_cost
FROM ChargingSession
WHERE session_id = <your_session_id>;
```

### Bước 3: Test qua API

**API Endpoint:**
```
POST /api/payments/pay-by-session
Authorization: Bearer <token>
Content-Type: application/json

{
  "sessionId": <session_id>
}
```

**Ví dụ request:**
```http
POST https://localhost:7035/api/payments/pay-by-session
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
Content-Type: application/json

{
  "sessionId": 123
}
```

### Bước 4: Kiểm tra kết quả

#### 4.1. Kiểm tra Response
Response phải chứa:
- `success: true`
- `message`: Có thông báo về việc hoàn cọc
- `walletInfo.balance`: Số dư ví đã tăng

#### 4.2. Kiểm tra Wallet Transaction
```sql
SELECT 
    transaction_id,
    transaction_type,
    amount,
    description,
    reference_id,
    created_at
FROM WalletTransaction
WHERE user_id = <your_user_id>
    AND transaction_type = 'credit'
    AND description LIKE '%Hoàn tiền cọc dư%'
    AND reference_id = <session_id>
ORDER BY created_at DESC;
```

**Kỳ vọng:**
- Có 1 transaction type = 'credit'
- Amount = 50,000 VND (deposit - cost_after_discount)
- Description = "Hoàn tiền cọc dư cho phiên sạc #<session_id>"

#### 4.3. Kiểm tra Wallet Balance
```sql
SELECT wallet_balance 
FROM [User] 
WHERE user_id = <your_user_id>;
```

**Kỳ vọng:**
- Wallet balance tăng thêm đúng số tiền hoàn cọc

#### 4.4. Kiểm tra Payment Record
```sql
SELECT 
    payment_id,
    session_id,
    amount,
    payment_status,
    payment_type,
    created_at
FROM Payment
WHERE session_id = <session_id>
    AND payment_type = 'session_payment';
```

**Kỳ vọng:**
- Payment record được tạo
- Amount = 0 (vì final_cost = 0, không cần trả thêm)
- Payment status = 'success'

### Bước 5: Verify Log

Kiểm tra console log có message:
```
[PaymentService] Hoàn tiền cọc dư: Deposit=100000, CostAfterDiscount=50000, Refund=50000
```

## 📝 Test Cases Checklist

- [ ] **TC1**: Deposit > Cost After Discount → Hoàn tiền dư
  - [ ] Wallet balance tăng đúng số tiền hoàn
  - [ ] Wallet transaction credit được tạo
  - [ ] Payment record amount = 0
  - [ ] Console log hiển thị thông tin hoàn cọc

- [ ] **TC2**: Deposit = Cost After Discount → Không hoàn
  - [ ] Không có wallet transaction credit
  - [ ] Payment record amount = 0
  - [ ] Wallet balance không đổi

- [ ] **TC3**: Deposit < Cost After Discount → Trả thêm tiền
  - [ ] Wallet transaction debit được tạo
  - [ ] Payment record amount > 0
  - [ ] Wallet balance giảm đúng số tiền cần trả

## 🐛 Debug

### Nếu không hoàn cọc:
1. Kiểm tra `depositAmount > costAfterDiscount` trong code
2. Kiểm tra `depositAmount > 0`
3. Kiểm tra session có `reservation_id` không
4. Kiểm tra deposit payment có status = 'success' không

### Nếu số tiền hoàn sai:
1. Kiểm tra `costAfterDiscount` = `cost_before_discount - applied_discount`
2. Kiểm tra `refundAmount` = `depositAmount - costAfterDiscount`
3. Kiểm tra `final_cost` = `max(0, costAfterDiscount - depositAmount)`

## 📊 SQL Queries hữu ích

```sql
-- Tìm sessions có deposit > cost_after_discount
SELECT 
    s.session_id,
    s.cost_before_discount,
    s.applied_discount,
    (s.cost_before_discount - ISNULL(s.applied_discount, 0)) as cost_after_discount,
    s.deposit_amount,
    s.final_cost,
    (s.deposit_amount - (s.cost_before_discount - ISNULL(s.applied_discount, 0))) as should_refund
FROM ChargingSession s
WHERE s.status = 'completed'
    AND s.reservation_id IS NOT NULL
    AND s.deposit_amount > 0
    AND s.deposit_amount > (s.cost_before_discount - ISNULL(s.applied_discount, 0));

-- Kiểm tra wallet transactions hoàn cọc
SELECT 
    wt.*,
    u.email
FROM WalletTransaction wt
INNER JOIN [User] u ON wt.user_id = u.user_id
WHERE wt.transaction_type = 'credit'
    AND wt.description LIKE '%Hoàn tiền cọc dư%'
ORDER BY wt.created_at DESC;
```

## ✅ Kết luận

Sau khi test thành công:
- ✅ Hoàn cọc tự động khi deposit > cost_after_discount
- ✅ Wallet transaction được tạo đúng
- ✅ Wallet balance cập nhật đúng
- ✅ Payment record được tạo đúng

