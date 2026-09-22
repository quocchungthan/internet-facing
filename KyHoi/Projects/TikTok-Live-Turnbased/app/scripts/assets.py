"""Original vector board/pieces. No external piece-art license dependency."""
from pathlib import Path
p=Path(__file__).resolve().parents[1]/'web/public'
(p/'pieces').mkdir(parents=True,exist_ok=True)
for color,names in [('r',dict(K='帥',A='仕',B='相',N='馬',R='車',C='炮',P='兵')),('b',dict(K='將',A='士',B='象',N='馬',R='車',C='砲',P='卒'))]:
    ink='#a1362c' if color=='r' else '#243d30'
    for kind,label in names.items():
        svg=f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100"><circle cx="51" cy="54" r="39" fill="#694321" opacity=".23"/><circle cx="50" cy="49" r="40" fill="#f5dfb6" stroke="#b28d55" stroke-width="2"/><circle cx="50" cy="49" r="33" fill="none" stroke="{ink}" stroke-width="1.6"/><text x="50" y="66" text-anchor="middle" font-family="Noto Serif CJK SC,SimSun,serif" font-weight="bold" font-size="48" fill="{ink}">{label}</text></svg>'''
        (p/f'pieces/{color}{kind}.svg').write_text(svg,encoding='utf-8')
parts=['<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 900 1000"><rect width="900" height="1000" fill="#e5c895"/><g stroke="#87663e" stroke-width="1.6" fill="none">']
for row in range(10): parts.append(f'<path d="M50 {50+100*row}H850"/>')
for col in range(9):
    x=50+col*100
    parts.append(f'<path d="M{x} 50V450 M{x} 550V950"/>' if col not in (0,8) else f'<path d="M{x} 50V950"/>')
parts += ['<path d="M350 50L550 250M550 50L350 250M350 750L550 950M550 750L350 950"/>','</g><g fill="#967448" font-family="serif" font-size="32"><text x="180" y="512">楚 河</text><text x="595" y="512">漢 界</text></g></svg>']
(p/'board.svg').write_text(''.join(parts),encoding='utf-8')
