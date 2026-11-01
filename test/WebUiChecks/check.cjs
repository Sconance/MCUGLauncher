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
  assert.equal(await page.locator('[data-task-summary] progress').getAttribute('value'), '35');
  assert.equal(await page.locator('[data-task-list] progress').getAttribute('value'), '35');

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
  await page.evaluate(() => {
    window.deliver({ type: 'state', accountType: 'offline', player: 'OfflineName' });
    window.mcugGo('?screen=home');
  });
  await page.locator('[data-account=microsoft]').click();
  assert.deepEqual(await page.evaluate(() => window.messages.at(-1)), {
    type: 'switchAccountMode',
    account: 'microsoft',
  });
  assert.equal(await page.locator('[data-ms-login]').count(), 0);
  await page.locator('[data-account=mua]').click();
  assert.deepEqual(await page.evaluate(() => window.messages.at(-1)), {
    type: 'switchAccountMode',
    account: 'mua',
  });
  assert.equal(await page.locator('[data-mua-submit]').count(), 0);
  await page.evaluate(() =>
    window.deliver({
      type: 'accountSwitchResult',
      account: 'mua',
      needsLogin: true,
      message: 'expired',
    })
  );
  assert.equal(await page.locator('[data-mua-submit]').isVisible(), true);
  console.log('PASS account tabs restore modes and only expired authorization opens login');
  await page.evaluate(() => window.mcugGo('?screen=home&panel=versions'));
  await page.locator('.version-search input').fill('Beta');
  await page.evaluate(() =>
    window.deliver({
      type: 'accountState',
      accountType: 'microsoft',
      player: 'FastName',
      accountLabel: '正版账号',
    })
  );
  assert.equal(await page.locator('.acc-name').textContent(), 'FastName');
  assert.equal(await page.locator('.version-search input').inputValue(), 'Beta');
  console.log('PASS account-only updates preserve the page and search input');

  await page.evaluate(() => window.McugResourcePicker.open('fixture', 'mod', 'Fixture'));
  const resourceRequest = await page.evaluate(() => window.messages.at(-1));
  await page.evaluate(
    (requestId) =>
      window.deliver({
        type: 'resourceVersions',
        requestId,
        token: 'resource-token',
        game: '1.21.1',
        loader: 'fabric',
        items: [
          { id: 'v1', name: 'One', files: ['one.jar'] },
          { id: 'v2', name: 'Two', files: ['two.jar'] },
        ],
      }),
    resourceRequest.requestId
  );
  await page.locator('[data-resource-version]').selectOption('v2');
  await page.locator('[data-resource-mode]').selectOption('local');
  await page.locator('[data-resource-path]').fill('fixture-downloads');
  await page.setViewportSize({ width: 960, height: 640 });
  await page.screenshot({ path: path.join(output, 'custom-resource-960.png') });
  await page.locator('[data-resource-confirm]').click();
  assert.deepEqual(await page.evaluate(() => window.messages.at(-1)), {
    type: 'queueContentVersion',
    token: 'resource-token',
    versionId: 'v2',
    mode: 'local',
    path: 'fixture-downloads',
  });
  await page.evaluate(() => window.deliver({ type: 'resourceQueued', token: 'resource-token' }));
  await page.evaluate(() =>
    window.deliver({
      type: 'packPreview',
      token: 'pack-token',
      name: 'Original',
      root: 'fixture',
      game: '1.21.1',
      loader: 'fabric',
    })
  );
  await page.locator('[data-pack-name]').fill('Custom Pack');
  await page.locator('[data-pack-directory]').fill('another-root');
  await page.locator('[data-pack-confirm]').click();
  assert.deepEqual(await page.evaluate(() => window.messages.at(-1)), {
    type: 'confirmPack',
    token: 'pack-token',
    name: 'Custom Pack',
    directory: 'another-root',
  });
  await page.evaluate(() => window.deliver({ type: 'packQueued', token: 'pack-token' }));
  await page.evaluate(() =>
    window.deliver({
      type: 'muaProfiles',
      token: 'roles',
      items: [
        { id: 'one', name: 'First' },
        { id: 'two', name: 'Second' },
      ],
    })
  );
  await page.locator('[data-mua-profile]').selectOption('two');
  await page.locator('[data-mua-profile-confirm]').click();
  assert.equal(await page.evaluate(() => window.messages.at(-1).profileId), 'two');
  await page.evaluate(() => window.McugLaunchOptions.open('1.21.1'));
  const optionRequest = await page.evaluate(() => window.messages.at(-1).requestId);
  await page.evaluate(
    (requestId) =>
      window.deliver({
        type: 'launchOptions',
        requestId,
        token: 'settings-token',
        version: '1.21.1',
        name: 'My instance',
        inherited: false,
        settings: {
          autoMemory: true,
          fullScreen: false,
          ramMb: 2048,
          width: 1200,
          height: 720,
          java: '',
          jvmArguments: '',
          windowTitle: '',
          server: '',
        },
      }),
    optionRequest
  );
  assert.equal(await page.locator('[data-launch-field=ramMb]').isDisabled(), true);
  await page.locator('[data-launch-auto]').uncheck();
  await page.locator('[data-launch-field=ramMb]').fill('3072');
  await page.locator('[data-launch-field=jvmArguments]').fill('-Dcustom=true');
  await page.screenshot({ path: path.join(output, 'custom-launch-960.png') });
  await page.locator('[data-launch-save]').click();
  assert.equal(await page.evaluate(() => window.messages.at(-1).settings.ramMb), 3072);
  assert.equal(
    await page.evaluate(() => window.messages.at(-1).settings.jvmArguments),
    '-Dcustom=true'
  );
  await page.evaluate(() => window.deliver({ type: 'launchOptionsSaved' }));
  await page.evaluate(() => window.mcugGo('?screen=datasource'));
  assert.equal(await page.locator('.ds-result').filter({ hasText: '尚未测试' }).count(), 4);
  const source = await page.locator('.ds-row input').first().inputValue();
  await page.evaluate(
    (url) => window.deliver({ type: 'testResult', name: 'announcements', url, ok: true, ms: 42 }),
    source
  );
  assert.equal(await page.locator('.ds-result').first().textContent(), '连接正常 · 42 ms');
  await page.locator('.ds-row input').first().fill('https://example.org/new.json');
  assert.equal(await page.locator('.ds-result').first().textContent(), '地址已修改，尚未测试');
  console.log(
    'PASS explicit resource version/destination, pack confirmation, MUA role choice, launch overrides and actual source test status'
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
