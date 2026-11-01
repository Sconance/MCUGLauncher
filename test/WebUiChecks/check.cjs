// Run with PLAYWRIGHT_MODULE set to an installed playwright module path, or npm-installed playwright.
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const { pathToFileURL } = require('node:url');
const path = require('node:path');
const fs = require('node:fs');
const assert = require('node:assert/strict');
(async () => {
  const browser = await chromium.launch({
    headless: true,
    channel: process.env.BROWSER_CHANNEL || 'msedge',
  });
  const page = await browser.newPage();
  const errors = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await page.addInitScript(() => {
    window.messages = [];
    const listeners = [];
    window.deliver = (message) => listeners.forEach((fn) => fn({ data: message }));
    window.chrome = {
      webview: {
        addEventListener: (type, fn) => listeners.push(fn),
        postMessage: (message) => {
          window.messages.push(message);
          if (message.type === 'ready')
            queueMicrotask(() =>
              window.deliver({
                type: 'state',
                player: 'Fixture',
                accountType: 'offline',
                root: 'fixture',
                java: '',
                ram: 4096,
                width: 1280,
                height: 720,
                selectedVersion: '1.21.1',
                selectedInstance: '1.21.1',
                versionGroups: [
                  { label: '原版游戏', count: 1, versions: [['1.21.1', '1.21.1 · vanilla']] },
                ],
                instances: [
                  { name: '1.21.1', loader: '原版', mods: 0, updates: 0 },
                  { name: '1.20.1', loader: '原版', mods: 0, updates: 0 },
                ],
                mods: [],
                servers: [{ name: 'Fixture Server', address: 'localhost:25565' }],
                dataSources: {},
              })
            );
        },
      },
    };
  });
  const base = pathToFileURL(path.resolve(__dirname, '../../src/MCUGLauncher/Ui/index.html')).href;
  const output = path.resolve(__dirname, '../../work/verify/report-ui');
  fs.mkdirSync(output, { recursive: true });
  for (const [width, height] of [
    [960, 640],
    [1200, 780],
    [1920, 1080],
  ]) {
    await page.setViewportSize({ width, height });
    for (const screen of [
      'home',
      'settings&panel=install',
      'settings&panel=tasks',
      'settings&panel=saves',
      'settings&panel=bookmarks',
      'settings&panel=games',
      'settings&panel=instances',
      'catalog&type=mod',
      'mods',
      'mod',
      'manage',
      'version',
      'options',
      'admin',
      'datasource',
    ]) {
      await page.goto(base + '?screen=' + screen);
      await page.waitForFunction(() => window.messages.some((m) => m.type === 'ready'));
      try {
        await page.locator('.app').waitFor({ timeout: 5000 });
      } catch (error) {
        console.error(screen, errors, await page.locator('#app').innerText());
        throw error;
      }
      const bounds = await page.locator('.app').boundingBox();
      assert.equal(Math.round(bounds.width), width);
      assert.equal(Math.round(bounds.height), height);
      assert.equal(
        await page.locator('.titlebar').evaluate((n) => n.scrollWidth <= n.clientWidth),
        true
      );
      await page.screenshot({
        path: path.join(output, `${width}-${screen.replaceAll('&', '-').replaceAll('=', '-')}.png`),
      });
    }
  }
  await page.goto(base + '?screen=home&modal=add-server');
  await page.locator('[data-server-name]').fill('Keep input');
  await page.evaluate(() =>
    window.deliver({
      type: 'serverPing',
      id: 'localhost:25565',
      online: true,
      players: 1,
      max: 20,
      ping: 5,
      motd: 'Fixture',
      version: '1.21.1',
    })
  );
  assert.equal(await page.locator('[data-server-name]').inputValue(), 'Keep input');
  assert.equal(
    await page.locator('[data-server-name]').evaluate((n) => n === document.activeElement),
    true
  );
  console.log('PASS server ping preserves modal input and focus');
  await page.goto(base + '?screen=settings&panel=tasks');
  await page.evaluate(() =>
    window.deliver({
      type: 'tasks',
      items: [
        {
          id: 'fixture',
          name: '<img src=x>',
          state: 'Running',
          percent: 35,
          message: 'Downloading',
        },
      ],
    })
  );
  assert.equal(await page.locator('[data-task-list] img').count(), 0);
  await page.getByRole('button', { name: '取消', exact: true }).click();
  assert.equal(
    await page.evaluate(() =>
      window.messages.some(
        (m) => m.type === 'taskAction' && m.id === 'fixture' && m.action === 'cancel'
      )
    ),
    true
  );
  console.log('PASS task rendering escapes names and emits cancel command');
  await page.goto(base + '?screen=settings&panel=install');
  await page.locator('[data-install-game]').fill('1.21.1');
  await page.getByRole('button', { name: '读取加载器版本', exact: true }).click();
  await page.evaluate(() =>
    window.deliver({ type: 'loaderList', version: '1.20.1', loader: 'fabric', versions: ['wrong'] })
  );
  assert.equal(await page.locator('[data-loader-install]').isDisabled(), true);
  await page.evaluate(() =>
    window.deliver({
      type: 'loaderList',
      version: '1.21.1',
      loader: 'fabric',
      versions: ['0.16.0'],
    })
  );
  await page.locator('[data-loader-install]').click();
  assert.equal(
    await page.evaluate(() =>
      window.messages.some(
        (m) => m.type === 'installLoader' && m.version === '1.21.1' && m.loaderVersion === '0.16.0'
      )
    ),
    true
  );
  console.log('PASS loader form rejects stale results and emits selected version');

  await page.goto(base + '?screen=settings&panel=instances');
  await page.waitForFunction(() => window.messages.some((m) => m.type === 'ready'));
  await page.locator('[data-clone-name]').fill('Copy fixture');
  await page.locator('[data-clone-instance]').click();
  assert.equal(
    await page.evaluate(() =>
      window.messages.some(
        (m) => m.type === 'cloneInstance' && m.version === '1.21.1' && m.name === 'Copy fixture'
      )
    ),
    true
  );
  await page.locator('[data-export-instance]').click();
  await page.locator('[data-maintenance-version]').selectOption('1.20.1');
  await page.evaluate(() =>
    window.deliver({ type: 'pickedPath', target: 'exportInstance', path: 'fixture-export.zip' })
  );
  assert.equal(
    await page.evaluate(() =>
      window.messages.some((m) => m.type === 'exportInstance' && m.version === '1.21.1')
    ),
    true
  );
  console.log('PASS clone and export commands retain explicitly selected target');
  await page.evaluate(() =>
    window.deliver({
      type: 'uninstallPreview',
      token: 'opaque',
      version: '1.20.1',
      name: 'Fixture',
      paths: ['<img src=x>'],
      notice: 'Review paths',
    })
  );
  assert.equal(await page.locator('[data-uninstall-review] img').count(), 0);
  const uninstall = page.getByRole('button', { name: '确认卸载', exact: true });
  assert.equal(await uninstall.isDisabled(), true);
  await page.getByLabel('我已检查删除范围并确认备份').check();
  await uninstall.click();
  assert.equal(
    await page.evaluate(() =>
      window.messages.some((m) => m.type === 'applyUninstall' && m.token === 'opaque' && !m.paths)
    ),
    true
  );
  console.log(
    'PASS uninstall review requires explicit acknowledgement and sends only opaque token'
  );
  await page.locator('[data-check-updates]').click();
  await page.evaluate(() =>
    window.deliver({
      type: 'contentUpdates',
      token: 'check',
      version: '1.20.1',
      items: [{ id: 'fixture', name: 'Fixture', before: 'old', after: 'new', available: true }],
    })
  );
  await page.locator('[data-update-list] input[type=checkbox]').check();
  await page.getByRole('button', { name: '生成所选更新预览', exact: true }).click();
  assert.equal(
    await page.evaluate(() =>
      window.messages.some((m) => m.type === 'previewContentUpdates' && m.projects[0] === 'fixture')
    ),
    true
  );
  await page.evaluate(() =>
    window.deliver({
      type: 'contentUpdatePreview',
      token: 'preview',
      version: '1.20.1',
      bytes: 1024,
      changes: [{ project: 'Fixture', before: 'old', after: 'new', path: 'mods/fixture.jar' }],
    })
  );
  await page.getByRole('button', { name: '按预览加入更新队列', exact: true }).click();
  assert.equal(
    await page.evaluate(() =>
      window.messages.some((m) => m.type === 'applyContentUpdates' && m.token === 'preview')
    ),
    true
  );
  assert.equal(
    await page.evaluate(() =>
      performance.getEntriesByType('resource').some((r) => r.name.endsWith('/preview-data.js'))
    ),
    false
  );
  console.log('PASS updates require file preview; native mode does not load preview fixtures');
  await page.evaluate(() => window.mcugGo('?screen=settings&panel=java'));
  await page.locator('[data-setting-java]').fill('fixture-java');
  await page.locator('[data-save-launch]').click();
  assert.deepEqual(
    await page.evaluate(() => window.messages.filter((m) => m.type === 'saveSettings').at(-1)),
    { type: 'saveSettings', java: 'fixture-java' }
  );
  await page.evaluate(() => {
    window.deliver({
      type: 'state',
      root: 'fixture',
      gameRoots: ['fixture', 'second'],
      mods: Array.from({ length: 12 }, (_, i) => ({
        id: 'm' + i,
        name: 'Mod ' + i,
        file: 'm' + i + '.jar',
        enabled: true,
      })),
      instances: [{ name: '1.21.1', loader: 'Fabric', mods: 12, updates: 0 }],
      versionGroups: [
        {
          label: '原版游戏',
          count: 2,
          versions: [
            ['Alpha', '1.21.1 · vanilla'],
            ['Beta', '1.20.1 · vanilla'],
          ],
        },
      ],
    });
    window.mcugGo('?screen=home&panel=versions');
  });
  assert.equal(await page.locator('[data-game-root]').inputValue(), 'fixture');
  await page.locator('[data-game-root]').selectOption('second');
  assert.equal(await page.evaluate(() => window.messages.at(-1).root), 'second');
  await page.locator('[data-add-root]').click();
  assert.equal(await page.evaluate(() => window.messages.at(-1).target), 'gameRoot');
  await page.evaluate(() =>
    window.deliver({ type: 'pickedPath', target: 'gameRoot', path: 'third' })
  );
  assert.equal(await page.evaluate(() => window.messages.at(-1).root), 'third');
  await page.locator('[data-home-import]').click();
  assert.equal(await page.evaluate(() => window.messages.at(-1).target), 'pack');
  await page.locator('[data-refresh-instances]').click();
  assert.equal(await page.evaluate(() => window.messages.at(-1).type), 'requestState');
  await page.locator('.version-group-head').click();
  assert.equal(await page.locator('.version-list').isVisible(), false);
  await page.locator('.version-group-head').click();
  await page.locator('.version-search input').fill('Beta');
  assert.equal(await page.locator('.version-row:visible').count(), 1);
  await page.evaluate(() => window.render());
  assert.equal(await page.locator('.version-search input').inputValue(), 'Beta');
  await page.screenshot({ path: path.join(output, 'directory-audit.png') });
  await page.evaluate(() => window.mcugGo('?screen=manage&inst=1.21.1'));
  assert.equal(await page.locator('.mtable-row').count(), 12);
  await page.locator('[data-mod-action=disable]').first().click();
  assert.deepEqual(await page.evaluate(() => window.messages.at(-1)), {
    type: 'toggleMod',
    file: 'm0.jar',
    enabled: false,
  });
  await page.evaluate(() => window.mcugGo('?screen=download&type=shader'));
  await page.locator('[data-community-refresh]').first().click();
  assert.equal(await page.evaluate(() => window.messages.at(-1).type), 'refreshCommunity');
  await page.evaluate(() => window.deliver({ type: 'communityRefreshDone', ok: false }));
  assert.equal(await page.locator('[data-community-refresh]').first().isEnabled(), true);
  console.log(
    'PASS scoped settings, directory actions, persistent filtering, full mod list and community refresh'
  );
  await page.evaluate(() => {
    window.deliver({
      type: 'state',
      mods: [{ id: 'pinned', name: 'Pinned', file: 'pinned.jar', enabled: true, update: '2.0' }],
    });
    window.mcugGo('?screen=manage&inst=1.21.1');
  });
  assert.equal(await page.locator('[data-mod-action=pin]').isDisabled(), true);
  await page.evaluate(() => {
    window.deliver({
      type: 'community',
      payload: {
        recommendations: JSON.stringify([
          { name: 'Example', type: 'server', address: 'example.org:25565', tags: [] },
        ]),
      },
    });
    window.mcugGo('?screen=download&type=server');
  });
  await page.locator('[data-rec-server]').click();
  assert.equal(await page.locator('.modal-body input').nth(1).inputValue(), 'example.org:25565');
  await page.evaluate(() => window.mcugGo('?screen=options'));
  assert.equal(
    await page.getByText('此处只保存和检查格式，不验证远端社区身份。', { exact: false }).count(),
    1
  );
  console.log(
    'PASS unsupported pin cannot install content; server recommendations open populated dialog; account page has no fake verification'
  );
  const standalone = await browser.newPage();
  standalone.on('pageerror', (error) => errors.push(error.message));
  await standalone.goto(base + '?screen=mods');
  await standalone.getByText('Sodium', { exact: true }).waitFor();
  console.log('PASS standalone browser preview still loads fixtures');
  assert.deepEqual(errors, []);
  await browser.close();
  console.log(
    'ALL_WEB_UI_CHECKS_PASSED (browser with simulated native messages; not live WebView/gameplay)'
  );
})().catch((error) => {
  console.error(error);
  process.exit(1);
});
