# Kỳ Hội — phòng chơi theo IP

Bản hiện tại có tạo/nhập mã phòng, tên, guest và quản trị host. Kéo quân hoặc chạm hai ô để vote; mũi tên xuất hiện ngay, nước trùng nhau có nét dày hơn.

## Chạy

```sh
cd KyHoi/Projects/TikTok-Live-Turnbased/app
npm ci
npm run build
npm start
```

Mở http://127.0.0.1:3000/. `/operator` chuyển về trang tạo/nhập phòng. Thiết bị khác dùng địa chỉ LAN của máy server, cổng 3000. Thêm `?view=live` vào URL phòng để ẩn bảng quản lý khi phát OBS.

- Một IP = một người, dùng chung tên/đội/phiếu/quyền host giữa các tab. Khi qua NAT, cùng IP công cộng được tính chung.
- Tạo phòng bắt đầu đồng hồ trận ngay. Host đặt số người (gồm guest), giới hạn từng đội, guest, thời gian lượt và trận.
- Đội đã chọn bị khóa cả sau rời/vào lại. Gửi request để host duyệt; host cũng có thể kéo thành viên sang đội/guest, kick, ban, bỏ ban, mở ván mới, đóng phòng.
- Host xem tất cả vote; người chơi chỉ xem đội mình; guest không xem vote. Lọc ngay tại API/WS. Một người một phiếu hợp lệ cuối cùng.
- Lượt trống tự đi ngẫu nhiên hợp lệ. Hết thời gian trận hòa. Hết ván giữ kết quả chờ host mở ván mới.
- Nút rời giải phóng chỗ; đóng tab không tự rời. Giữ một chỗ cho host quay lại.

Không cần API key cho phòng browser. Comment TikTok chưa nối vào runtime phòng vì sự kiện không có IP khán giả. OBS hiển thị nội dung theo quyền người mở trang.

Xem architecture.md, resource-checklist.md, acceptance-tests.md. Tài liệu bản cũ chỉ được giữ trong workspace phát triển, không đưa vào PR.

Kiểm thử browser lần đầu: chạy `npx playwright install chromium` (Linux CI dùng --with-deps), rồi `npm run test:browser`.
