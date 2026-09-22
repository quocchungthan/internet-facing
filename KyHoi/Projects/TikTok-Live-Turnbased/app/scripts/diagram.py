from pathlib import Path
from html import escape
p=Path(__file__).resolve().parents[2]/'verification/architecture.svg'
p.parent.mkdir(exist_ok=True)
parts=['''<svg xmlns="http://www.w3.org/2000/svg" width="1440" height="960" viewBox="0 0 1440 960"><defs><marker id="arrow" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="8" markerHeight="8" orient="auto-start-reverse"><path d="M0 0L10 5L0 10Z" fill="#d3b580"/></marker></defs><rect width="1440" height="960" rx="24" fill="#11221e"/><g font-family="Segoe UI,Arial,sans-serif"><text x="65" y="75" font-size="35" fill="#f5e7ce">KỲ HỘI / ENGINE &amp; LUỒNG HOẠT ĐỘNG</text><text x="65" y="116" font-size="19" fill="#b8c5b2">Hai đường độc lập: comment đi vào backend; hình ảnh đi ra livestream.</text>''']
def node(x,y,w,title,lines,color='#263f33'):
    parts.append(f'<rect x="{x}" y="{y}" width="{w}" height="115" rx="14" fill="{color}" stroke="#65745c"/>')
    parts.append(f'<text x="{x+20}" y="{y+35}" font-size="23" font-weight="600" fill="#f5e7ce">{escape(title)}</text>')
    for i,line in enumerate(lines):parts.append(f'<text x="{x+20}" y="{y+65+i*25}" font-size="17" fill="#bbc8b5">{escape(line)}</text>')
def arrow(path):parts.append(f'<path d="{path}" fill="none" stroke="#d3b580" stroke-width="3" marker-end="url(#arrow)"/>')
node(65,180,270,'01 · KHÁN GIẢ',['!team do / !team den','!vote W12 a3a4'])
node(405,180,350,'02 · NGUỒN COMMENT',['TikTok connector + Euler key','Hoặc mock local, không cần key'])
node(825,180,545,'03 · PARSER / TEAM / VOTE',['ID ổn định → chống trùng → kiểm tra cửa sổ','Đúng đội + đúng luật → 1 phiếu/người'])
arrow('M335 237H395');arrow('M755 237H815')
node(825,380,545,'04 · TURN ENGINE',['30s → chốt tally → áp dụng đúng một lần','Trống: W mới, giữ bàn. Mất mạng: tạm khóa.'],'#563b2e')
arrow('M1095 295V370')
node(65,380,320,'GAME ADAPTER',['Cờ tướng: xiangqi.js','Caro 3×3: adapter thứ hai'])
node(455,380,300,'SQLITE',['State + đội + phiếu','Move log cùng transaction'])
arrow('M825 413H395');arrow('M825 468H765')
node(65,600,360,'05 · BROWSER UI',['Snapshot read-only qua WebSocket','Board + countdown + top phiếu'])
node(505,600,330,'06 · BROADCASTING',['OBS Browser Source 1080×1920','Quyền phát TikTok riêng'])
node(915,600,455,'07 · TIKTOK LIVE',['Người xem thấy nước đi và bình chọn','Vòng lặp tiếp tục, không có AI'])
arrow('M1095 495V550H245V590');arrow('M425 657H495');arrow('M835 657H905')
parts.append('<text x="65" y="815" font-size="23" fill="#dfc492">Engine tự xây điều phối trò chơi; thư viện cờ chỉ xử lý luật.</text><text x="65" y="854" font-size="19" fill="#b8c5b2">Chuyển đội đầu vòng sau · Không vote thì chờ · Hết ván tự tạo ván mới sau 15 giây</text><text x="65" y="899" font-size="17" fill="#93a68a">MVP local đã triển khai. Comment LIVE / quyền phát / OBS thực tế còn cần tài khoản để nghiệm thu.</text></g></svg>')
p.write_text(''.join(parts),encoding='utf-8')
