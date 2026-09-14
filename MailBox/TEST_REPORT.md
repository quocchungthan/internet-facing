# Kết quả kiểm tra — 2026-09-12

Môi trường: Docker Desktop Linux/amd64 trên Windows; Maddy 0.9.5, web image `shuneo-webmail:1.0.0`, Nginx 1.28. Domain thử `example.test`, cổng chỉ bind 127.0.0.1, chứng chỉ local tự ký được test client tin cậy rõ ràng (không tắt kiểm tra TLS).

## Đã xác minh

- Build image web và khởi động cả ba dịch vụ Docker thành công.
- Bootstrap hai tài khoản `shuneo` và `admin`; chạy bootstrap lại không làm mất/reset tài khoản.
- `python tests/integration.py`: **3/3 nhóm test PASS**, 56,9 giây.
- Đăng nhập hai tài khoản qua web HTTPS, gửi thư qua SMTP STARTTLS, nhận và đọc bằng IMAP TLS.
- Bản sao Đã gửi tồn tại; thư chỉ xuất hiện trong Inbox của người nhận, không lẫn hộp thư người gửi.
- Nội dung tiếng Việt được giữ nguyên. Chuỗi HTML/script trong nội dung được escape; CSP không cho chạy script.
- Đăng xuất làm mất quyền truy cập; trang riêng chuyển về đăng nhập.
- POST thiếu CSRF bị từ chối. Header injection và nhiều địa chỉ trong trường một người nhận bị từ chối.
- SMTP không công bố AUTH trước TLS; submission chưa xác thực bị từ chối.
- Tài khoản shuneo không được gửi với envelope sender admin.
- SMTP port 25 từ chối relay tới domain ngoài cho người chưa xác thực.
- `nginx -t`: cấu hình hợp lệ. `pip check`: không có dependency hỏng.
- Script backup dừng dịch vụ, tạo archive và khởi động lại thành công.
- Giải nén archive vào thư mục kiểm thử riêng, chạy image Maddy với `--network none`: đọc được hai tài khoản, Inbox admin và Sent shuneo đã khôi phục.
- Xem trực quan bản render tĩnh của giao diện Inbox bằng trình duyệt: bố cục hiển thị đúng. Trình duyệt tự động không mở live HTTPS local vì chứng chỉ tự ký; không bỏ qua cảnh báo. Luồng live được kiểm tra bằng HTTP client tin cậy chứng chỉ test, cùng SMTP/IMAP thật.

## Cần xác minh trên VPS

- DNS A/MX/PTR/SPF/DKIM/DMARC thực tế và khả năng kết nối port 25 hai chiều.
- Cấp chứng chỉ Let's Encrypt và `renew --dry-run` với domain thật.
- Gửi/nhận Internet, kết quả xác thực trong Gmail và khả năng vào Inbox.
- Firewall của nhà cung cấp, backup off-site và giám sát ổ đĩa/chứng chỉ.

Không có email thử được gửi ra domain bên ngoài. Không tuyên bố đã kiểm thử khả năng giao mail Internet, khả năng chịu tải lớn hoặc an toàn production đầy đủ.
