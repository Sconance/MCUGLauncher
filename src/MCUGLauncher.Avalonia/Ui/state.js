// Shared collections start empty in native mode.
const servers = [];
const announcements = [];
const recs = [];
const quotes = [];
const versionGroups = [];
const instances = [];
const mods = [];
const DS = [
  {
    key: 'announcements',
    label: '公告',
    hint: '主页公告栏与全部公告',
    url: 'https://raw.githubusercontent.com/waibibabu1239/MCUGL-storage/main/announcements.json',
  },
  {
    key: 'servers',
    label: '服务器',
    hint: '主页正在开设的服务器',
    url: 'https://raw.githubusercontent.com/waibibabu1239/MCUGL-storage/main/servers.json',
  },
  {
    key: 'recommendations',
    label: '推荐',
    hint: '主页摘要与下载页群友推荐',
    url: 'https://raw.githubusercontent.com/waibibabu1239/MCUGL-storage/main/recommendations.json',
  },
  {
    key: 'quotes',
    label: '语录',
    hint: '主页群友语录（文字或图片）',
    url: 'https://raw.githubusercontent.com/waibibabu1239/MCUGL-storage/main/quotes.json',
  },
];

window.mcugData = { announcements, recs, servers, quotes, mods };
window.state = {
  servers,
  announcements,
  recs,
  quotes,
  mods,
  root: '',
  java: '',
  ram: 4096,
  width: 1280,
  height: 720,
  dataSources: { announcements: '', servers: '', recommendations: '', quotes: '' },
  javaList: [],
  logText: '',
  diagText: '',
  catalog: {},
  catalogLoading: {},
  vanilla: [],
};
