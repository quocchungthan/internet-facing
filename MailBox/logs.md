# Nhật ký kiểm thử manual Piggy Farm

Ngày thực hiện: 2026-09-12 (Europe/Amsterdam)

Mục tiêu: gỡ deployment Maddy/webmail hiện tại, triển khai mới theo
`MANUAL_PIGGY_FARM.md`, rồi kiểm tra gửi/nhận. Nhật ký không chứa mật khẩu,
secret hoặc private key.

## 1. Kiểm kê trước khi gỡ — 23:32 CEST

- Domain hiện tại: `eldervibe.dev`.
- Mail hostname: `mail.eldervibe.dev`.
- Hai container thuộc deployment: `shuneo-mail-maddy-1` và
  `shuneo-mail-web-1`; cả hai ở trạng thái `Up 12 hours`.
- Port publish: TCP 25, 587, 993 trên mọi IPv4; webmail chỉ tại
  `127.0.0.1:8080`.
- Dữ liệu mail hiện tại khoảng 392 KiB và có các SQLite WAL đang hoạt động.
- Nginx host vượt qua `nginx -t`.
- Chứng chỉ host tồn tại tại `/etc/letsencrypt/live/mail.eldervibe.dev`.
- `DOCKER-USER` có ngoại lệ TCP 25/587/993 trước rule DROP của `ens1`.
- Container Stalwart cũ tên `stalwart` đã ở trạng thái `Exited`; không liên quan
  đến hai container production đang chạy.

Quyết định an toàn: dừng dịch vụ trước khi backup SQLite, tạo bản backup có thể
khôi phục, và chỉ gỡ đúng container `maddy`/`web`. Không xóa image, chứng chỉ,
Nginx hoặc container của ứng dụng khác.

## 2. Backup và gỡ deployment cũ — 23:34 CEST

- Dừng `web`, sau đó dừng `maddy` để các database SQLite được đóng sạch.
- Tạo archive `backups/pre-reinstall-20260912T233212CEST.tar.gz`.
- Gỡ đúng hai container `shuneo-mail-web-1` và `shuneo-mail-maddy-1`.
- Chuyển cây dữ liệu gốc `.env`, `runtime/`, `secrets/` vào
  `backups/pre-reinstall-tree-20260912T233212CEST/`.
- Không xóa dữ liệu; có thể rollback từ archive hoặc cây backup.
- Không gỡ Docker image để tránh một lần tải mạng không cần thiết.

## 3. Khởi tạo deployment mới — 23:35 CEST

- Chạy `scripts/manage.py init` với domain `eldervibe.dev` và hostname
  `mail.eldervibe.dev` trong thư mục repository hiện tại.
- Tạo mới `.env`, `runtime/` và `secrets/`.
- Tạo thông tin bootstrap mới cho `admin@eldervibe.dev` và
  `shuneo@eldervibe.dev`; không in mật khẩu ra terminal/log.
- Kiểm tra DNS: A trỏ `mail.eldervibe.dev` tới `82.38.64.129`; MX priority 10
  trỏ về `mail.eldervibe.dev`; PTR của IP trả về `mail.eldervibe.dev`.
- Tái sử dụng chứng chỉ Let's Encrypt production đang hợp lệ cho đúng SAN
  `mail.eldervibe.dev`, hết hạn `2026-12-10 05:46:46 UTC` (còn 88 ngày tại lúc
  kiểm tra). Không xin lại chứng chỉ trùng lặp để tránh rate limit.
- `nginx -t` thành công.

## 4. Build và khởi động service mới — 23:36 CEST

- Chạy `docker compose up -d --build maddy web`; không bật Compose service
  `proxy`, vì Nginx host đang giữ port 80/443.
- Image web build thành công; hai container mới được tạo và chuyển sang `Up`.
- Webmail chỉ publish tại `127.0.0.1:8080`.
- Maddy publish TCP 25, 587 và 993 trên IPv4.
- Chạy `scripts/manage.py accounts`; tạo credential và IMAP mailbox cho hai tài
  khoản bootstrap. CLI có cảnh báo không thể tắt echo trong non-TTY, nhưng password
  được truyền qua stdin bởi script và không xuất hiện trong log; cả hai account báo
  `Account ready`.
- Log Maddy xác nhận SMTP, submission và IMAP đều lắng nghe; server version 0.9.5
  khởi động thành công.
- Maddy sinh DKIM keypair mới. Cần đối chiếu với TXT DNS trước khi kiểm tra gửi ra.

## 5. Kiểm tra tính liên tục DKIM — 23:38 CEST

- Chỉ so sánh public key đã chuẩn hóa, không in key ra log.
- DKIM vừa sinh không khớp TXT `default._domainkey.eldervibe.dev`.
- DKIM từ backup cũ khớp hoàn toàn TXT DNS đang công bố.
- Kết luận: cài lại cùng domain phải phục hồi DKIM cũ hoặc cập nhật DNS. Chọn phục
  hồi key cũ để không tạo khoảng thời gian DKIM fail.
- Bổ sung lưu ý này vào mục DKIM của manual.

## 6. Phục hồi DKIM và smoke test production — 23:40 CEST

- Dừng ngắn Maddy, phục hồi đúng hai file DKIM `.key`/`.dns` từ backup, rồi khởi
  động lại. Lần khởi động sau không còn log sinh key mới.
- Bộ `tests/integration.py` chủ động từ chối production vì chỉ cho phép cấu hình
  `--local`; giữ nguyên chốt an toàn, không bypass.
- Chạy smoke test production riêng, đọc credential trực tiếp từ file bảo mật và
  không in password:
  - SMTP submission 587 + STARTTLS + authentication: PASS.
  - Gửi thư thử từ `shuneo@eldervibe.dev` tới `admin@eldervibe.dev`: PASS.
  - IMAP TLS 993 đăng nhập và tìm thấy đúng thư thử trong Inbox: PASS.
  - Xác minh certificate hostname bằng trust store hệ thống: PASS.
  - HTTPS `mail.eldervibe.dev` trả HTTP 302 về trang đăng nhập: PASS.

## 7. Kiểm tra DNS và kết nối Internet — 23:42 CEST

- SPF công khai: `v=spf1 ip4:82.38.64.129 -all`.
- DMARC công khai: `v=DMARC1; p=none; rua=mailto:admin@eldervibe.dev`.
- DKIM local sau phục hồi khớp public key trên DNS: PASS.
- TCP 25 từ 5 điểm đo: 4 kết nối thành công, 1 node timeout riêng lẻ.
- TCP 587 từ 5 điểm đo: 5 kết nối thành công.
- TCP 993 từ 5 điểm đo: 5 kết nối thành công.
- Counter ngoại lệ mail trong `DOCKER-USER` tăng; rule ACCEPT nằm trước rule DROP.

## 8. Kiểm tra gửi ra Internet — 23:44 CEST

- Gửi đúng một thư nghiệm thu từ `shuneo@eldervibe.dev` tới
  `solshuneo@gmail.com`.
- SMTP authentication và submission được Maddy chấp nhận.
- Log queue của Maddy báo `delivered` cho Gmail: PASS.
- Việc Gmail xếp Inbox/Spam và kết quả SPF/DKIM/DMARC trong **Show original** cần
  được xác nhận từ giao diện Gmail.

## 9. Gia hạn TLS — 23:49 CEST

- Phát hiện `certbot.timer` ban đầu ở trạng thái disabled/inactive.
- Cài deploy hook `/etc/letsencrypt/renewal-hooks/deploy/piggyfarm-mail.sh`; hook
  reload Nginx và restart Maddy sau lần gia hạn thật.
- Bật `certbot.timer`; trạng thái cuối enabled/active, lần chạy kế tiếp được lên
  lịch lúc `2026-09-13 10:40:17 CEST`.
- Lượt dry-run đầu có random delay 324 giây theo thiết kế của Certbot nên được dừng
  và chạy lại với `--no-random-sleep-on-renew`.
- ACME staging dry-run riêng cho `mail.eldervibe.dev`: SUCCESS.
- Cú pháp deploy hook (`sh -n`): PASS.

## 10. Trạng thái cuối

- `shuneo-mail-maddy-1`: Up; publish TCP 25/587/993.
- `shuneo-mail-web-1`: Up; bind `127.0.0.1:8080`.
- Credential và IMAP mailbox cùng tồn tại cho `admin@eldervibe.dev` và
  `shuneo@eldervibe.dev`.
- `nginx -t`: PASS.
- DNS A/MX/PTR/SPF/DKIM/DMARC: khớp deployment.
- SMTP submission, local delivery, IMAP TLS, HTTPS và outbound Gmail: PASS.
- Backup trước cài lại vẫn còn trong `backups/` và không bị xóa.
- Mật khẩu bootstrap đã thay đổi do triển khai fresh; mật khẩu mới chỉ nằm trong
  `secrets/accounts.json`.

## 11. Bước xác nhận thủ công còn lại

1. Mở Gmail và kiểm tra thư có subject `Piggy Farm mail reinstall verification`;
   xem cả Spam/All Mail.
2. Mở **Show original** và xác nhận SPF, DKIM, DMARC đều PASS.
3. Từ Gmail gửi một thư mới tới `admin@eldervibe.dev` hoặc
   `shuneo@eldervibe.dev`, rồi xác nhận xuất hiện trong webmail.
