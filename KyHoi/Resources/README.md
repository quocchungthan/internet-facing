# Resources

Các tài liệu public đã được tải thực về thư mục này. `source-index.md` là bảng tìm nhanh; `source-index.json` chứa URL, timestamp UTC, SHA-256, số byte và trạng thái tải.

Chạy `python Resources/download.py` từ workspace để cập nhật snapshot. Nguồn rules/render runtime pin commit; docs live được lưu theo ngày tải. HTML không phải một website offline hoàn chỉnh (có thể thiếu script/ảnh phụ); Markdown và `.d.ts` dùng được trực tiếp.

Ưu tiên `connector-2.5.0-*` cho bản đang cài, vì README của default branch hoặc ví dụ Euler có thể dùng API cũ. Bản npm package-lock là nguồn thật cho version runtime.

Chưa có: dashboard/quota riêng của Euler key, quyền LIVE/RTMP tài khoản TikTok, stream key, thông tin billing/SSH/DNS. Đây là tài nguyên cần đăng nhập và người dùng cung cấp; không có tài liệu giả thay thế. Không tải Laya weights.
