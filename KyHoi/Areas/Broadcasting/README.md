# Area: Broadcasting

Trách nhiệm: hình ảnh browser, OBS/LIVE Studio, âm thanh (hiện chưa có), máy phát và vận hành liên tục.

1. Chạy app, mở `/` (không dùng `/operator` làm nguồn phát).
2. Trong OBS thêm Browser Source, URL `http://127.0.0.1:3000/`, 1080×1920, 30 FPS. Tắt tự unload nguồn khi không visible nếu muốn giữ kết nối UI.
3. Nếu được cấp RTMP: nhập server/stream key ở OBS. Nếu không có, kiểm tra LIVE Studio/capture browser được tài khoản hỗ trợ. Chưa thể mặc định mọi tài khoản đều có RTMP.
4. Xem bằng điện thoại khác: kiểm tra board/axes, cú pháp, countdown, vùng TikTok che và độ trễ. Ghi thời gian comment gửi → vote xuất hiện.
5. Tắt sleep trong phiên chạy dài bằng cài đặt người dùng tự chọn; không tự thay system settings. Theo dõi nhiệt, mạng, dung lượng SQLite/log và nguồn điện.

Mất viewer không dừng luồng game; mất nguồn comment thì giữ bàn cờ, hiển thị trạng thái. App không tự mở lại phiên TikTok đã bị nền tảng kết thúc và không tự điều khiển OBS ở MVP. Vận hành 24/7 ngoài app vẫn cần máy phát, quyền nền tảng và quy trình recovery.

Sau phiên: lưu metrics tổng hợp, backup DB đúng cách, ghi lỗi và phiên bản release. Không chụp/lưu stream key vào tài liệu.
