# Tài nguyên / cấu hình

Phòng browser chỉ cần máy có Node 24+, không cần API key. HOST mặc định 0.0.0.0, PORT 3000, ROOMS_PATH ./data/rooms. Backup toàn bộ thư mục, gồm registry.sqlite. TRUSTED_PROXY để trống khi truy cập trực tiếp; chỉ cấu hình proxy thật khi deploy.

Tài liệu upstream đã tải tại Resources/source-index.md (gốc workspace): luật/bàn cờ, connector, Euler, OBS và stack. Euler API key + TikTok username chỉ phục vụ adapter comment cũ, chưa dùng trong runtime phòng. TikTok không cung cấp IP người comment nên cần chốt định danh trước khi tích hợp lại. Connector cũ có dependency AGPL-3.0; đánh giá license trước khi tích hợp/phân phối.

Người dùng tự chuẩn bị tài khoản có quyền LIVE/phát máy tính khi pilot; RTMP URL/key nhập trong OBS nếu tài khoản hỗ trợ. Domain/DNS/VPS chỉ cần khi public deployment. Không cần AI key/GPU, chưa mua dịch vụ nào.
