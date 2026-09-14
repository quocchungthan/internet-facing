# Manual triển khai mail server cho Piggy Farm

Tài liệu này mô tả quy trình production đã được kiểm chứng với Maddy chạy trong
Docker, webmail bind tại `127.0.0.1:8080`, Nginx và Let's Encrypt chạy trực tiếp
trên VPS. Thay toàn bộ giá trị ví dụ trước khi thực hiện.

## 1. Thông tin cần chuẩn bị

Đặt các giá trị sau theo hệ thống Piggy Farm:

```text
DOMAIN=piggyfarm.example
MAIL_HOSTNAME=mail.piggyfarm.example
VPS_IP=203.0.113.10
ADMIN_EMAIL=your-existing-email@gmail.com
DEPLOY_DIR=/opt/piggyfarm-mail
```

Không dùng nguyên `piggyfarm.example` hoặc `203.0.113.10`; đây chỉ là ví dụ.
Email quản trị phải là hộp thư đang hoạt động để nhận cảnh báo Let's Encrypt.

VPS nên có:

- IPv4 public cố định;
- Ubuntu 24.04 hoặc Debian tương đương;
- tối thiểu 2 vCPU, 2 GB RAM và 20 GB SSD cho hệ thống nhỏ;
- quyền root hoặc sudo;
- Docker Engine, Docker Compose v2, Python 3, Nginx, Certbot và `dnsutils`;
- khả năng đặt PTR/rDNS và mở SMTP port 25 hai chiều.

Không triển khai lên IP động, mạng NAT không kiểm soát được port-forward, hoặc VPS
không cho đặt PTR.

## 2. Yêu cầu nhà cung cấp VPS trước khi cài

Nhiều nhà cung cấp chặn SMTP ở firewall bên ngoài VPS. Rule UFW trong máy không
thể gỡ giới hạn này. Tạo ticket trước khi đổi MX:

```text
Subject: Request to allow mail server ports and configure PTR

Hello,

Please allow inbound TCP ports 25, 587 and 993 for server <VPS_IP>,
from source 0.0.0.0/0.

Please confirm that outbound TCP port 25 is also not filtered.

Please configure the PTR/rDNS record:
<VPS_IP> -> <MAIL_HOSTNAME>

The server will be used as a legitimate low-volume mail server for Piggy Farm.
Please confirm when the network firewall and SMTP restrictions have been removed.

Thank you.
```

Trong control panel của VPS, nếu có Network Firewall/Security Group, mở inbound:

| Port | Giao thức | Nguồn | Mục đích |
|---|---|---|---|
| 25 | TCP | `0.0.0.0/0` | Nhận SMTP từ máy chủ mail khác |
| 587 | TCP | `0.0.0.0/0` | Gửi thư có xác thực/STARTTLS |
| 993 | TCP | `0.0.0.0/0` | IMAP TLS |
| 80 | TCP | `0.0.0.0/0` | Let's Encrypt và redirect HTTP |
| 443 | TCP | `0.0.0.0/0` | Webmail HTTPS |

Chỉ mở SSH 22 từ IP quản trị nếu điều kiện vận hành cho phép. Không mở database,
Docker socket hoặc cổng web nội bộ 8080 ra Internet.

## 3. Cấu hình DNS

Tạo các record sau tại DNS provider:

| Loại | Tên | Giá trị |
|---|---|---|
| A | `mail` | `<VPS_IP>` |
| MX | `@` | priority `10`, `<MAIL_HOSTNAME>` |
| TXT | `@` | `v=spf1 ip4:<VPS_IP> -all` |
| TXT | `_dmarc` | `v=DMARC1; p=none; rua=mailto:admin@<DOMAIN>` |

Nếu dùng Cloudflare, record `mail` phải là **DNS only** (mây xám). Không thêm AAAA
khi chưa cấu hình đầy đủ IPv6, firewall IPv6 và PTR IPv6.

Kiểm tra DNS:

```bash
dig +short A <MAIL_HOSTNAME>
dig +short MX <DOMAIN>
dig +short -x <VPS_IP>
```

Kết quả cần thỏa mãn:

```text
<MAIL_HOSTNAME> -> <VPS_IP>
<DOMAIN> MX -> <MAIL_HOSTNAME>
<VPS_IP> PTR -> <MAIL_HOSTNAME>
```

Không đổi MX của domain đang sử dụng cho tới khi server, TLS và port 25 đã sẵn
sàng. Việc đổi MX sẽ chuyển thư mới sang máy chủ này.

## 4. Cài phần mềm trên VPS

```bash
sudo apt update
sudo apt install -y docker.io docker-compose-v2 nginx certbot python3 dnsutils \
  netcat-openbsd ufw
sudo systemctl enable --now docker nginx
```

Upload source vào thư mục triển khai, ví dụ:

```bash
sudo mkdir -p /opt/piggyfarm-mail
sudo chown "$USER":"$USER" /opt/piggyfarm-mail
cd /opt/piggyfarm-mail
# Giải nén/copy source của dự án vào đây.
```

Không copy `.env`, `secrets/` hoặc `runtime/` từ một mail server khác. Mỗi server
phải tự sinh secret và dữ liệu riêng.

## 5. Khởi tạo cấu hình

Trong thư mục dự án:

```bash
cd /opt/piggyfarm-mail
python3 scripts/manage.py init \
  --domain <DOMAIN> \
  --hostname <MAIL_HOSTNAME>
```

Lệnh tạo `.env`, thư mục dữ liệu và hai tài khoản bootstrap:

```text
shuneo@<DOMAIN>
admin@<DOMAIN>
```

Mật khẩu nằm trong `secrets/accounts.json`. File này phải có quyền `0600`, không
gửi qua chat/email và không commit vào Git.

## 6. Cấp chứng chỉ TLS bằng Certbot trên host

Đảm bảo DNS A đã trỏ đúng và port 80 được mở. Ở lần cấp đầu, tạm dừng Nginx để
Certbot dùng port 80 ở chế độ standalone:

```bash
sudo systemctl stop nginx
sudo certbot certonly --standalone \
  -d <MAIL_HOSTNAME> \
  --email <ADMIN_EMAIL> \
  --agree-tos \
  --non-interactive
sudo systemctl start nginx
```

Nếu Certbot thất bại, vẫn chạy `sudo systemctl start nginx` để khôi phục web trên
VPS. Tránh yêu cầu chứng chỉ lặp lại liên tục vì Let's Encrypt có rate limit.

Chứng chỉ phải xuất hiện tại:

```text
/etc/letsencrypt/live/<MAIL_HOSTNAME>/fullchain.pem
/etc/letsencrypt/live/<MAIL_HOSTNAME>/privkey.pem
```

Maddy trong cấu hình Piggy Farm mount `/etc/letsencrypt` ở chế độ read-only và
dùng cùng chứng chỉ cho SMTP STARTTLS và IMAP TLS.

## 7. Khởi động Maddy và webmail

Vì Nginx chạy trực tiếp trên host, chỉ bật `maddy` và `web`; không bật service
`proxy` trong Compose để tránh tranh port 80/443:

```bash
cd /opt/piggyfarm-mail
docker compose up -d --build maddy web
python3 scripts/manage.py accounts
docker compose ps
docker compose logs --tail=100 maddy web
```

Cần thấy Maddy lắng nghe:

```text
smtp: listening on tcp://0.0.0.0:25
submission: listening on tcp://0.0.0.0:587
imap: listening on tls://0.0.0.0:993
```

Webmail chỉ được publish tại `127.0.0.1:8080`.

## 8. Cấu hình Nginx host

Tạo `/etc/nginx/sites-available/<MAIL_HOSTNAME>`:

```nginx
server {
    listen 80;
    server_name <MAIL_HOSTNAME>;

    location / {
        return 301 https://$host$request_uri;
    }
}

server {
    listen 443 ssl http2;
    server_name <MAIL_HOSTNAME>;

    ssl_certificate /etc/letsencrypt/live/<MAIL_HOSTNAME>/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/<MAIL_HOSTNAME>/privkey.pem;
    ssl_protocols TLSv1.2 TLSv1.3;
    client_max_body_size 10m;

    add_header Strict-Transport-Security "max-age=31536000" always;

    location / {
        proxy_pass http://127.0.0.1:8080;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto https;
        proxy_read_timeout 90s;
    }
}
```

Kích hoạt và kiểm tra:

```bash
sudo ln -s /etc/nginx/sites-available/<MAIL_HOSTNAME> \
  /etc/nginx/sites-enabled/<MAIL_HOSTNAME>
sudo nginx -t
sudo systemctl reload nginx
```

Mở `https://<MAIL_HOSTNAME>` và đăng nhập bằng tài khoản trong
`secrets/accounts.json`.

## 9. Firewall UFW và Docker

Mở firewall host:

```bash
sudo ufw allow 25/tcp
sudo ufw allow 587/tcp
sudo ufw allow 993/tcp
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw status verbose
```

### Bẫy DOCKER-USER đã gặp thực tế

Port có thể hiện `LISTEN` và UFW có `ALLOW`, nhưng traffic Docker vẫn bị drop bởi
rule kiểu sau:

```text
-A DOCKER-USER -i ens1 -j DROP
```

Kiểm tra:

```bash
sudo iptables -L DOCKER-USER -n -v --line-numbers
```

Nếu có rule DROP toàn bộ interface public, thêm ngoại lệ **trước** nó:

```bash
sudo iptables -I DOCKER-USER 3 -i ens1 -p tcp \
  -m multiport --dports 25,587,993 -j ACCEPT
```

Thay `ens1` bằng interface public tìm được từ `ip route get 1.1.1.1`. Để rule tồn
tại sau reboot, thêm vào phần `DOCKER-USER` của `/etc/ufw/after.rules`, trước rule
DROP:

```text
-A DOCKER-USER -i ens1 -p tcp -m multiport --dports 25,587,993 -j ACCEPT
-A DOCKER-USER -i ens1 -j DROP
```

Nếu block có nhãn `ANSIBLE MANAGED`, phải cập nhật cả template/playbook Ansible;
nếu không, lần chạy Ansible tiếp theo sẽ xóa ngoại lệ.

Sau khi sửa, xác nhận counter ACCEPT tăng khi kiểm tra từ Internet:

```bash
sudo iptables -L DOCKER-USER -n -v --line-numbers
```

## 10. Thêm DKIM vào DNS

Sau khi Maddy chạy:

```bash
docker compose exec -T maddy sh -c 'cat /data/dkim_keys/*.dns'
```

Copy public TXT record được in ra vào DNS, thường có tên:

```text
default._domainkey.<DOMAIN>
```

Không công khai file private key. Kiểm tra:

```bash
dig +short TXT default._domainkey.<DOMAIN>
dig +short TXT <DOMAIN>
dig +short TXT _dmarc.<DOMAIN>
```

Nếu đây là **cài lại cùng domain**, ưu tiên phục hồi cả file `.key` và `.dns` từ
backup `runtime/mail/dkim_keys/` trước khi khởi động Maddy. Nếu để Maddy sinh key
mới, phải cập nhật TXT DNS cho khớp trước khi gửi thư; nếu không DKIM sẽ fail.
Không copy private key qua terminal, chat hoặc một kênh không mã hóa.

## 11. Kiểm tra từ Internet

Không chỉ kiểm tra bằng `ss` trên VPS. Phải thử từ mạng bên ngoài:

```bash
sudo ss -lntp | grep -E ':(25|587|993) '
```

Dùng một port checker nhiều khu vực cho TCP 25, 587 và 993. Nếu bên ngoài timeout,
bắt gói trong lúc kiểm tra:

```bash
sudo tcpdump -ni any 'tcp port 25'
```

Diễn giải:

- Không thấy SYN: vướng DNS, firewall/security group hoặc nhà cung cấp VPS.
- Có SYN nhưng không có SYN-ACK: vướng firewall/NAT/`DOCKER-USER` trên VPS.
- Bắt tay TCP được nhưng Maddy từ chối: xem log SMTP và recipient/domain.

Theo dõi Maddy rồi gửi thư từ Gmail:

```bash
cd /opt/piggyfarm-mail
docker compose logs -f maddy
```

Gửi Gmail tới `admin@<DOMAIN>`. Thành công phải có dạng:

```text
smtp: incoming message
smtp: RCPT ok
smtp: accepted
```

Sau đó gửi từ webmail tới Gmail. Trong Gmail chọn **Show original/Hiển thị thư
gốc** và kiểm tra SPF, DKIM, DMARC đều PASS. Kiểm tra cả Spam và All Mail.

## 12. Tạo tài khoản mới

Ví dụ tạo `user@<DOMAIN>`:

```bash
docker compose exec maddy \
  maddy -config /data/maddy.conf creds create user@<DOMAIN>

docker compose exec maddy \
  maddy -config /data/maddy.conf imap-acct create user@<DOMAIN>
```

Phải chạy cả hai lệnh: lệnh đầu tạo mật khẩu, lệnh sau tạo mailbox. Không truyền
mật khẩu bằng `-p` vì mật khẩu sẽ nằm trong shell history. Không cần restart.

Kiểm tra:

```bash
docker compose exec -T maddy \
  maddy -config /data/maddy.conf creds list
docker compose exec -T maddy \
  maddy -config /data/maddy.conf imap-acct list
```

## 13. Gia hạn chứng chỉ

Certbot package thường cài timer tự động. Kiểm tra:

```bash
sudo systemctl status certbot.timer
sudo certbot renew --dry-run
```

Sau khi chứng chỉ được gia hạn, reload Nginx và restart Maddy để chắc chắn đọc file
mới. Tạo deploy hook:

```bash
sudo sh -c 'printf "%s\n" "#!/bin/sh" \
  "systemctl reload nginx" \
  "cd /opt/piggyfarm-mail && docker compose restart maddy" \
  > /etc/letsencrypt/renewal-hooks/deploy/piggyfarm-mail.sh'
sudo chmod 0755 /etc/letsencrypt/renewal-hooks/deploy/piggyfarm-mail.sh
```

Chạy lại `sudo certbot renew --dry-run` sau khi tạo hook.

## 14. Backup và vận hành

Với mô hình Nginx chạy trên host, không dùng `python3 scripts/manage.py backup`
trong phiên bản hiện tại: lệnh đó gọi `docker compose up -d` và có thể bật luôn
container `proxy`, gây tranh port 80/443. Backup thủ công nhất quán như sau:

```bash
cd /opt/piggyfarm-mail
docker compose stop web maddy
sudo tar -czf /root/piggyfarm-mail-backup-YYYYMMDD.tar.gz \
  runtime .env secrets maddy compose.yaml
docker compose up -d maddy web
```

Thay `YYYYMMDD` bằng ngày chạy backup. Luôn kiểm tra `docker compose ps` sau đó.

Archive chứa thư, database, mật khẩu và khóa nên phải mã hóa và copy ra nơi khác.
Không chỉ giữ backup trên cùng VPS. Định kỳ kiểm tra khả năng khôi phục.

Các lệnh vận hành:

```bash
docker compose ps
docker compose logs --tail=200 maddy
docker compose logs --tail=200 web
df -h
sudo nginx -t
sudo certbot certificates
```

Theo dõi dung lượng ổ đĩa và ngày hết hạn TLS. Không bật auto-update image mail mà
không backup và kiểm tra release notes trước.

## 15. Checklist nghiệm thu

- [ ] Provider xác nhận inbound 25/587/993 và outbound 25 không bị lọc.
- [ ] Security Group mở 25/587/993/80/443 đúng IPv4.
- [ ] A, MX và PTR khớp hostname/IP.
- [ ] Không có AAAA ngoài ý muốn; Cloudflare mail record là DNS only.
- [ ] TLS hợp lệ cho web, SMTP và IMAP.
- [ ] Maddy và web container ở trạng thái Up.
- [ ] Web chỉ bind `127.0.0.1:8080`.
- [ ] `DOCKER-USER` cho phép 25/587/993 trước rule DROP.
- [ ] Port 25 và 993 truy cập được từ nhiều mạng bên ngoài.
- [ ] Gmail gửi vào xuất hiện `smtp: accepted` và có trong Inbox.
- [ ] Gửi ra Gmail thành công; SPF, DKIM và DMARC PASS.
- [ ] Tài khoản không tồn tại bị trả `5.1.1 User does not exist`.
- [ ] `certbot renew --dry-run` thành công.
- [ ] Backup đã được copy off-site và thử khôi phục.
