# Maddy eldervibe.dev — MVP Maddy + Webmail

Bộ Docker để triển khai mail cá nhân trên VPS Linux. Hai tài khoản được tạo khi chạy bootstrap: **shuneo@TEN-MIEN** và **admin@TEN-MIEN**. Đây là email trên tên miền của bạn, không phải tài khoản @gmail.com.

Quy trình production đã kiểm chứng cho Piggy Farm, bao gồm yêu cầu mở port với nhà cung cấp VPS và xử lý firewall Docker, nằm trong [`MANUAL_PIGGY_FARM.md`](MANUAL_PIGGY_FARM.md).

## Đã có

- Đăng nhập bằng tài khoản Maddy; phiên 1 giờ; đăng xuất.
- Hộp thư đến, phân trang 20 thư, nút làm mới, đọc nội dung.
- Gửi thư văn bản tới một người nhận, lưu bản sao vào `Sent`.
- Hiển thị tiếng Việt. HTML trong thư được chuyển thành văn bản; không chạy script/tải ảnh theo dõi.
- CSRF, cookie Secure/HttpOnly/SameSite, giới hạn đăng nhập và gửi cơ bản.
- SMTP/IMAP dùng TLS được kiểm tra chứng chỉ; không tắt xác minh trong bản triển khai.
- Mật khẩu sinh ngẫu nhiên riêng cho hai tài khoản. Cookie không chứa mật khẩu; thông tin IMAP chỉ giữ trong RAM của web server.
- Alias `postmaster@` và `abuse@` nhận vào `admin@`.
- Script cấu hình, tạo tài khoản, cấp/gia hạn TLS, backup và đóng gói.

## Phạm vi MVP

Chưa có tệp đính kèm trên web, trả lời nhanh, bản nháp, xóa thư, tìm kiếm, đổi mật khẩu trên web, đăng ký công khai, chống spam nội dung/Rspamd hay antivirus. Thư trên 10 MB mở bằng ứng dụng IMAP. Web không thay đổi cờ đã đọc. `admin` là hộp thư thường, không có quyền đọc hộp thư `shuneo`. Đổi mật khẩu dùng CLI Maddy.

Maddy ghi IMAP storage là beta. Đây là MVP cá nhân, cần backup và kiểm tra trước khi sử dụng cho thư quan trọng. Một worker Gunicorn là chủ đích vì phiên được giữ trong RAM; restart web sẽ đăng xuất tất cả người dùng. Không tăng workers/replicas trước khi chuyển session sang một kho dùng chung an toàn.

## Cấu trúc

```text
Internet -- SMTP 25 --> Maddy -- IMAP 993 --> Webapp
Internet <-- SMTP 25 -- Maddy <-- STARTTLS 587 -- Webapp
Trình duyệt -- HTTPS 443 --> Caddy trên host --> Webapp nội bộ :8000
```

`compose.yaml`: hai dịch vụ chạy thường xuyên (`maddy`, `web`) và `certbot` chỉ chạy khi cấp/gia hạn chứng chỉ. Caddy trên host cung cấp HTTPS cho webmail; Maddy vẫn dùng cùng chứng chỉ cho SMTP/IMAP TLS. Maddy dùng image upstream 0.9.5; web được build từ `web/Dockerfile`. Database/thư nằm trong `runtime/mail`, được giữ qua việc tạo lại container. Không mount Docker socket vào ứng dụng.

## 1. Chuẩn bị VPS

Hướng dẫn dành cho VPS Linux mới, khuyến nghị Ubuntu 24.04/Debian, Docker Engine và Compose v2, Python 3. Gợi ý khởi đầu 2 vCPU, 2 GB RAM, SSD ít nhất 20 GB cho hai hộp thư; đây là ước lượng, dung lượng thực tùy lượng mail.

1. Sở hữu tên miền và quyền quản lý DNS.
2. Có IPv4 public cố định, nhà cung cấp cho đặt PTR và mở **TCP 25 inbound + outbound**.
3. Mở inbound TCP **25, 587, 993, 80, 443** ở firewall nhà cung cấp; giữ SSH chỉ từ IP của bạn nếu có thể.
4. Đảm bảo không có dịch vụ khác chiếm các cổng này. Lệnh kiểm tra: `sudo ss -lntp`.
5. Cài Docker theo tài liệu chính thức: https://docs.docker.com/engine/install/ubuntu/ ; kiểm tra `docker version` và `docker compose version`.
6. Cài tiện ích: `sudo apt update && sudo apt install -y python3 unzip dnsutils netcat-openbsd`.

Các lệnh Docker cần quyền phù hợp. Có thể dùng shell quản trị `sudo -i` rồi làm các bước dưới. Thư mục `/opt/shuneo-mail-mvp` và tác vụ cron phải dùng cùng quyền. Docker publish port có thể đi ngoài một số quy tắc UFW: dùng firewall nhà cung cấp hoặc DOCKER-USER để kiểm soát truy cập; web :8000 không được publish.

Nếu outbound 25 bị chặn, bản này CHƯA cấu hình SMTP relay: cần nhà cung cấp mở hoặc chỉnh Maddy theo relay của bạn trước khi gửi ra Internet.

## 2. Upload và giải nén

Upload `dist/shuneo-mail-mvp.zip` tới VPS bằng SCP/SFTP. Ví dụ từ máy cá nhân:

```bash
scp shuneo-mail-mvp.zip root@IP_VPS:/opt/
```

Trên VPS:

```bash
cd /opt
unzip shuneo-mail-mvp.zip
cd /opt/shuneo-mail-mvp
```

Nếu có file image `shuneo-mail-images-linux-amd64.tar`, có thể upload thêm và chạy `docker load -i /duong-dan/shuneo-mail-images-linux-amd64.tar` để dùng image đã build. File này dành cho VPS x86_64/amd64. Với ARM64, build trên VPS và kiểm tra hỗ trợ kiến trúc của các image upstream.

**Không upload `.env`, `secrets/`, `runtime/` của bản chạy thử trên máy cá nhân.** Gói ZIP tự loại chúng để VPS sinh tài khoản và khóa riêng.

## 3. Đặt tên miền và tạo mật khẩu

Thay `example.com` bằng domain thực của bạn:

```bash
python3 scripts/manage.py init --domain example.com --hostname mail.example.com
```

Script tạo `.env`, thư mục dữ liệu và `secrets/accounts.json` với hai mật khẩu ngẫu nhiên. Không chạy lại `init` trên dữ liệu cũ hoặc đổi domain trực tiếp sau khi đã có thư. Có thể xem tài khoản khi cần bằng `cat secrets/accounts.json`; không chia sẻ file này. Chưa cần sử dụng mật khẩu ở bước DNS.

## 4. DNS trước khi khởi động

Giả sử IP VPS là `203.0.113.10` (IP minh họa, PHẢI thay):

| Loại | Tên | Giá trị |
|---|---|---|
| A | `mail` | IPv4 VPS |
| MX | `@` | ưu tiên 10, `mail.example.com` |
| TXT | `@` | `v=spf1 ip4:203.0.113.10 -all` |
| TXT | `_dmarc` | `v=DMARC1; p=none; rua=mailto:admin@example.com` |

PTR ở nhà cung cấp VPS: `203.0.113.10 -> mail.example.com`. Nếu đang dùng domain có mail thật, đừng thay MX trước khi lên kế hoạch chuyển đổi. Chỉ có **một SPF record**; ví dụ SPF ở trên dành cho domain mới chỉ gửi qua VPS này. MX không làm thay đổi nơi chạy website.

Nếu dùng Cloudflare, bản ghi `mail` phải là **DNS only** (mây xám). Không thêm AAAA cho đến khi IPv6, PTR, firewall và đường gửi IPv6 đã được cấu hình/kiểm tra. Bản này chỉ nêu SPF IPv4.

Kiểm tra:

```bash
dig +short A mail.example.com
dig +short MX example.com
dig +short -x IP_VPS
```

DNS cần trỏ đúng IP trước khi xin chứng chỉ. DKIM sẽ thêm ở bước 7.

## 5. Cấp chứng chỉ HTTPS/TLS

Trong thư mục dự án, dùng một địa chỉ email hiện có để nhận thông báo (không dùng hộp thư mới chưa hoạt động):

```bash
python3 scripts/manage.py certificate --email EMAIL_DANG_DUNG
```

Lệnh này dùng Let's Encrypt, đồng ý điều khoản của Let's Encrypt, cần Internet và port 80 đang trống/truy cập được. Caddy được dừng tạm trong lúc Certbot standalone sử dụng port 80. Nếu lỗi, kiểm tra A/AAAA, cổng 80, firewall và DNS proxy; tránh thử liên tục gây rate limit.

Chứng chỉ dùng chung cho HTTPS, SMTP STARTTLS và IMAP. Khóa ở `runtime/letsencrypt`, không sửa quyền thành công khai. Image Maddy hiện chạy với quyền mặc định upstream để đọc khóa bind mount; chỉ nên cho người quản trị tin cậy truy cập host/Docker.

## 6. Khởi động và tạo hai hộp thư

```bash
docker compose up -d --build
python3 scripts/manage.py accounts
docker compose ps
docker compose logs --tail=100 maddy web
```

Nếu đã `docker load` image, có thể dùng `docker compose up -d --no-build` thay vì build.

Mở **https://mail.example.com**, đăng nhập bằng email đầy đủ và mật khẩu trong `secrets/accounts.json`. Chạy `accounts` lần nữa không chủ đích reset mật khẩu/tài khoản đã có. Tạo thông tin đăng nhập và kho IMAP là hai thao tác riêng; script xử lý cả hai.

Gửi thử từ `shuneo@example.com` tới `admin@example.com`; đăng xuất, vào admin, nhấn Làm mới và đọc thư. Gửi ngược lại. Kiểm tra mục Đã gửi.

## 7. Thêm DKIM

```bash
docker compose exec maddy sh -c 'cat /data/dkim_keys/*.dns'
```

Sao chép **public DNS record** mà Maddy sinh ra vào DNS provider. Với selector mặc định thường là `default._domainkey`, giá trị TXT là toàn bộ `v=DKIM1; ...; p=...`. Nếu giao diện tách name/type/value thì điền từng trường, không dán cả dòng zone vào value. Không đưa file private key lên DNS hoặc chia sẻ nó.

```bash
dig +short TXT default._domainkey.example.com
dig +short TXT example.com
dig +short TXT _dmarc.example.com
```

Nếu thư mục DKIM chưa có file, kiểm tra log và cấu hình Maddy trước khi thử gửi Internet. Không tự bịa public key. DMARC `p=none` là chế độ theo dõi; chỉ chuyển sang quarantine/reject sau khi mọi nguồn gửi hợp lệ xác thực đúng.

## 8. Kiểm tra gửi/nhận Internet

1. Từ Gmail của bạn, gửi tới cả hai tài khoản trên VPS và kiểm tra nhận thư.
2. Từ webapp, gửi một thư tới Gmail bạn sở hữu.
3. Trong Gmail, mở “Hiển thị thư gốc / Show original”, kiểm tra SPF, DKIM và DMARC PASS.
4. Kiểm tra cả Spam. SMTP được chấp nhận không bảo đảm Inbox.
5. Kiểm tra bounce trong hộp thư người gửi và log `docker compose logs --tail=200 maddy`.

Không gửi hàng loạt để thử. Maddy cấu hình outbound yêu cầu kết nối mã hóa; máy chủ đích không hỗ trợ TLS có thể không nhận được thư từ bộ này. Việc giao thư thật ra Internet chưa được chứng minh chỉ bằng bài test local.

## 9. Tự động gia hạn chứng chỉ

Kiểm tra một lần:

```bash
python3 scripts/manage.py renew --dry-run
```

Thêm vào **root crontab** bằng `sudo crontab -e` (thay đường dẫn nếu khác):

```cron
17 3 * * * cd /opt/shuneo-mail-mvp && /usr/bin/python3 scripts/manage.py renew >> /var/log/shuneo-mail-renew.log 2>&1
```

Port 80 cần tiếp tục mở cho HTTP challenge. Script dừng/khởi động lại Caddy trong quá trình renewal và reload Caddy cùng restart Maddy sau renewal thành công. Có thể có gián đoạn ngắn; kiểm tra log và ngày hết hạn định kỳ.

## 10. Backup và khôi phục

Trên VPS, dùng `scripts/backup.sh` (bọc `manage.py backup`, thêm checksum, mã hóa GPG tùy chọn và dọn archive cũ):

```bash
cd /opt/shuneo-mail-mvp
BACKUP_KEEP=7 BACKUP_PASSPHRASE='<passphrase>' bash scripts/backup.sh
```

`BACKUP_KEEP` (mặc định 7) là số archive giữ lại trong `backups/`. Nếu đặt `BACKUP_PASSPHRASE`, archive được mã hóa `gpg --symmetric --cipher-algo AES256` thành `.tar.gz.gpg` và bản thường bị xóa; thiếu `gpg` thì script báo lỗi chứ không để lại file chưa mã hóa. Chạy định kỳ bằng root crontab (xem comment đầu script). Vẫn có thể gọi trực tiếp `python3 scripts/manage.py backup` nếu chỉ cần archive thô.

Chạy từ xa: workflow **Backup MailBox** (`.github/workflows/backup-mailbox.yml`, chỉ `workflow_dispatch`) với input `keep`, `download`, `dry_run`. Workflow kiểm tra dung lượng trống, chạy `scripts/backup.sh` qua SSH, xác nhận `maddy` và `web` chạy lại, và chỉ tải archive về làm artifact khi `download=true` **và** có secret `MAILBOX_BACKUP_PASSPHRASE` (dùng chung các secret/vars `MAILBOX_SSH_*`, `MAILBOX_DEPLOY_DIR` như workflow deploy). Không bao giờ upload archive chưa mã hóa. Mất passphrase là mất toàn bộ archive `.gpg`.

Script dừng ba dịch vụ để backup SQLite/thư nhất quán rồi bật lại. Có gián đoạn ngắn. Archive ở `backups/` chứa dữ liệu mail, cấu hình, mật khẩu và khóa: phải bảo vệ, mã hóa khi lưu nơi khác. Copy backup ra ngoài VPS, đừng chỉ giữ cùng ổ đĩa. Không chạy cùng lúc với renewal hoặc cập nhật.

Khôi phục vào một thư mục triển khai **mới/trống**, dùng đúng phiên bản gói và domain cũ:

```bash
cd /opt/shuneo-mail-mvp-restored
# Thư mục này phải có mã nguồn đã giải nén từ gói ZIP và chưa chạy init/up.
gpg --batch --decrypt --output backup.tar.gz /duong-dan/backup.tar.gz.gpg   # chỉ khi archive .gpg
tar -xzf /duong-dan/backup.tar.gz
docker compose up -d --build
```

Chỉ giải nén backup do chính bạn tạo/tin cậy. Nếu chuyển IP, cập nhật A, PTR, SPF. Kiểm tra chứng chỉ còn hạn, đăng nhập, đọc thư cũ và gửi/nhận trước khi coi khôi phục hoàn tất. Không ghi đè lên thư mục production đang chạy. Dữ liệu `runtime/` không được xóa khi cập nhật.

## 11. Đổi mật khẩu và vận hành

Xem cú pháp upstream trước khi đổi:

```bash
docker compose exec maddy maddy -config /data/maddy.conf creds password --help
docker compose exec maddy maddy -config /data/maddy.conf creds password shuneo@example.com
docker compose restart web
```

Nhập mật khẩu qua prompt, không đặt trong command line. Cập nhật kho mật khẩu riêng/`secrets/accounts.json` để tránh lưu thông tin cũ. Restart web hủy các phiên hiện tại. File accounts chỉ dùng bootstrap; chỉnh file không tự đổi mật khẩu Maddy.

Lệnh thường dùng:

```bash
docker compose ps
docker compose logs --tail=100 maddy
docker compose logs --tail=100 web
df -h
docker compose restart web
docker compose down
docker compose up -d
```

`down` không xóa bind mount `runtime/`. Trước khi nâng image, backup, xem release notes và kiểm tra khôi phục. Không dùng auto-update không kiểm soát cho kho mail.

## Chạy thử trên máy có Docker (không cần domain)

Trong bản giải nén mới:

```bash
python scripts/manage.py init --domain example.test --local
docker compose up -d --build
python scripts/manage.py accounts
python tests/integration.py
```

Mở `https://localhost:8443`. Chứng chỉ thử tự ký nên trình duyệt cảnh báo; chỉ bản local này mới dùng chứng chỉ thử. Các cổng chỉ bind loopback: SMTP 2525, submission 1587, IMAP 1993, HTTP 8080, HTTPS 8443. Truy cập trực tiếp HTTPS; redirect HTTP dùng hostname mail nên không dành cho local preview. Domain `.test` không gửi/nhận Internet. Chứng chỉ thử hết hạn sau 14 ngày.

Các bài test chỉ gửi giữa hai tài khoản local, kiểm tra Sent, tiếng Việt, escaping HTML, CSRF, injection, SMTP authentication, sender impersonation và open relay. Không dùng test này trên production.

## Đóng gói lại

```bash
python scripts/manage.py package
docker save -o dist/shuneo-mail-images-linux-amd64.tar shuneo-webmail:1.0.0 foxcpp/maddy:0.9.5 certbot/certbot:v4.2.0
```

ZIP chứa source/config/hướng dẫn, không chứa bí mật/dữ liệu. Image tar là tùy chọn để không phải build/pull trên VPS cùng kiến trúc; vẫn cần Internet để cấp TLS, DNS và gửi/nhận mail.

## Tài liệu tham khảo

- https://maddy.email/tutorials/setting-up/
- https://maddy.email/faq/
- https://github.com/foxcpp/maddy
- https://support.google.com/mail/answer/81126
- https://eff-certbot.readthedocs.io/en/stable/using.html
- https://docs.docker.com/engine/install/ubuntu/

Maddy upstream được phân phối theo GPL-3.0; source/release tương ứng: https://github.com/foxcpp/maddy/tree/v0.9.5 . Gói ứng dụng sử dụng image upstream nguyên bản, không sửa binary Maddy.
