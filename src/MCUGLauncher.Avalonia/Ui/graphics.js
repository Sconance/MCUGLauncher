// Page rendering only; native operations are handled by feature modules.
// Same eight-pixel Steve face as the native fallback; no network is needed.
const DefaultAvatar = () => {
  let pixels = '';
  for (let y = 0; y < 8; y++) {
    for (let x = 0; x < 8; x++) {
      let color = y < 2 || (y === 2 && (x === 0 || x === 7)) ? '#472f22' : '#bb8761';
      if (y === 4 && [1, 2, 5, 6].includes(x)) color = [2, 5].includes(x) ? '#385b9f' : '#ffffff';
      if (y === 6 && x >= 2 && x <= 5) color = '#67402e';
      pixels += `<rect x="${x}" y="${y}" width="1" height="1" fill="${color}"/>`;
    }
  }
  return `<svg class="avatar-svg" viewBox="0 0 8 8" shape-rendering="crispEdges" role="img" aria-label="史蒂夫默认头像">${pixels}</svg>`;
};

const SkinFigure = () => `
<svg class="avatar-svg" viewBox="0 0 16 24" shape-rendering="crispEdges" aria-hidden="true">
  <rect width="16" height="24" fill="#DCEFE3"/>
  <rect x="3" y="17" width="4" height="6" fill="#1C4B31"/>
  <rect x="9" y="17" width="4" height="6" fill="#1C4B31"/>
  <rect x="2" y="9" width="2" height="8" fill="#CE966F"/>
  <rect x="12" y="9" width="2" height="8" fill="#CE966F"/>
  <rect x="4" y="9" width="8" height="8" fill="#2C6B47"/>
  <rect x="4" y="2" width="8" height="7" fill="#DDAA84"/>
  <rect x="3" y="1" width="10" height="3" fill="#4B3126"/>
  <rect x="2" y="3" width="2" height="6" fill="#4B3126"/>
  <rect x="12" y="3" width="2" height="6" fill="#4B3126"/>
  <rect x="5" y="5" width="2" height="2" fill="#2D2421"/>
  <rect x="9" y="5" width="2" height="2" fill="#2D2421"/>
  <rect x="7" y="7" width="2" height="1" fill="#B97C63"/>
</svg>`;

const QuotePhoto = () => `
<svg viewBox="0 0 208 132" preserveAspectRatio="xMidYMid slice" shape-rendering="crispEdges" aria-hidden="true">
  <defs>
    <linearGradient id="sky" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#8BC7C9"/><stop offset=".55" stop-color="#C9E3D0"/><stop offset="1" stop-color="#F7D8A8"/>
    </linearGradient>
  </defs>
  <rect width="208" height="132" fill="url(#sky)"/>
  <rect x="146" y="24" width="22" height="22" fill="#FFF0C4"/>
  <rect x="150" y="20" width="14" height="30" fill="#FFF0C4" opacity=".35"/>
  <path d="M0 78 28 58 48 68 70 45 98 70 126 54 158 74 184 58 208 76V132H0Z" fill="#78A88B"/>
  <path d="M0 92 30 80 56 88 82 72 112 90 140 80 170 92 208 82V132H0Z" fill="#4E8063"/>
  <rect y="105" width="208" height="27" fill="#4E8C8F"/>
  <path d="M0 105h208v4H0z" fill="#B8DDC7" opacity=".55"/>
  <path d="M20 63v34M22 74h-8M22 84h-9M184 66v33M186 76h8M186 88h-8" stroke="#315B43" stroke-width="4"/>
  <path d="M8 55h24l-4 9H12ZM176 56h26l-4 9h-18Z" fill="#2E6647"/>
  <path d="M34 93v13M172 96v12" stroke="#2D5A40" stroke-width="3"/>
  <rect x="18" y="118" width="6" height="6" fill="#E8D39B"/>
  <rect x="182" y="116" width="6" height="6" fill="#E8D39B"/>
</svg>`;

const Avatar = () =>
  /^data:image\/png;base64,/.test(window.state?.avatar || '')
    ? `<img class="avatar-svg" style="image-rendering:pixelated" src="${window.state.avatar}" alt="当前账号头像">`
    : DefaultAvatar();
