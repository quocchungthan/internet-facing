# Kiểm chứng bản phòng

Trong app: `npm.cmd run build`, `npm.cmd test`, `npm.cmd run test:browser`, `npm.cmd run soak`.

Build thành công, 34 test qua: luật mẫu, deadline, IP chuẩn hóa/bền vững, khóa đội/rejoin, request, giới hạn chỗ, guest, kick/ban, rollback, nhiều phòng, HTTP privacy và proxy. Các test engine cũ còn kiểm tra chế độ cấu hình legacy; runtime phòng bật random khi lượt trống và tắt tự mở ván mới.

10 kịch bản browser đã qua bằng kết nối loopback từ các IP riêng thật: cùng IP chia sẻ identity; đội/guest và WebSocket privacy; duyệt request; kéo thành viên; kick/ban; random; hết giờ hòa; rời phòng; mobile và caro. Xem verification/rooms-browser-results.json và ảnh room-*.png, rooms-*.png.

Soak cũ dừng sau khoảng 5 giờ khi thay luật; không tính đã qua tám giờ. Bản ghi cũ không đưa vào PR. Runner mới mặc định tám giờ real-time, Fastify inject và SQLite riêng; 15 phút đầu mỗi giờ không vote, sau đó có vote, host tự mở lại ván kết thúc để kiểm thử. Đây là kiểm thử engine/API, không thay thế kiểm thử mạng thật. SOAK_SECONDS cho chạy smoke ngắn; đọc verification/soak-status.json để biết thời lượng thực tế.

Chưa hoàn thành soak tám giờ bản phòng, TikTok LIVE thật, OBS/điện thoại thật, public deployment và chứng nhận đầy đủ luật lặp thế/đuổi quân giải đấu.

