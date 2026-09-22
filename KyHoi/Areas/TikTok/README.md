# Area: TikTok

Trách nhiệm: tài khoản kênh, quyền LIVE/PC, nguồn comment, quota Euler và điều kiện nền tảng.

Trước live: kiểm tra tài khoản còn quyền phát, username đúng, key có signing entitlement, app hiển thị connected và nhận một lệnh thử từ tài khoản khác. Không coi kết nối TCP thành công là đủ.

Khi mất nguồn: UI phải giữ bàn cờ; kiểm tra log không chứa key, trạng thái LIVE và quota/429. Backoff hiện tối đa 60 giây. Nếu lỗi entitlement kéo dài, dừng pilot để sửa cấu hình, không mua tự động hoặc đưa cookie vào frontend.

Sau phiên: ghi số viewer/watch time từ Analytics tài khoản, số lần mất kết nối, quota dùng và độ trễ đo được. Resources chính ở `../../Resources/TikTok/`.
