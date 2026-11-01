// Instance maintenance keeps preview tokens and the selected target separate from global navigation.
(() => {
  const bridge = window.chrome?.webview;
  const post = (message) => bridge?.postMessage(message);
  const view = {
    version: '',
    name: '',
    saves: false,
    servers: false,
    uninstall: null,
    checked: null,
    preview: null,
  };
  let pendingExport = null;

  function status(text) {
    const node = document.querySelector('[data-instance-status]');
    if (node) node.textContent = text;
  }

  function mount() {
    const panel = document.querySelector('[data-instance-tools]');
    if (!panel || panel.dataset.bound) return;
    panel.dataset.bound = '1';
    const instances = (window.state?.instances || []).filter(
      (item) => item.loader !== '请安装版本'
    );
    if (!instances.length) {
      panel.textContent = '请先安装或导入实例。';
      return;
    }
    if (!instances.some((item) => item.name === view.version))
      view.version = instances.some((item) => item.name === Q.v)
        ? Q.v
        : window.state?.selectedInstance || instances[0].name;
    panel.innerHTML = `<label class="field-label">目标实例</label><select class="select" data-maintenance-version></select>
      <div class="install-fields" style="margin-top:16px"><label>新实例名称<input class="input" data-clone-name placeholder="克隆时必填"></label>
      <label><span>包含内容</span><span><input type="checkbox" data-clone-saves> 存档</span><span><input type="checkbox" data-clone-servers> 服务器列表</span></label></div>
      <div class="settings-actions"><button class="btn btn-primary" data-clone-instance>克隆为独立实例</button><button class="btn btn-ghost" data-export-instance>导出整合包 ZIP</button><button class="btn btn-ghost" data-uninstall-preview>预览卸载范围</button></div>
      <p data-instance-status role="status"></p><div data-uninstall-review></div><hr>
      <h3>内容更新</h3><p>只检查通过启动器管理的内容；更新前会显示文件变化，执行时再次核对本地指纹。</p>
      <button class="btn btn-ghost" data-check-updates>检查内容更新</button><div data-update-list></div><div data-update-preview></div>`;
    const select = panel.querySelector('[data-maintenance-version]');
    select.replaceChildren(
      ...instances.map((item) => new Option(item.displayName || item.name, item.name))
    );
    select.value = view.version;
    select.onchange = () => {
      view.version = select.value;
      view.uninstall = view.checked = view.preview = null;
      reviews();
    };
    const name = panel.querySelector('[data-clone-name]');
    name.value = view.name;
    name.oninput = () => (view.name = name.value);
    for (const [field, selector] of [
      ['saves', '[data-clone-saves]'],
      ['servers', '[data-clone-servers]'],
    ]) {
      const input = panel.querySelector(selector);
      input.checked = view[field];
      input.onchange = () => (view[field] = input.checked);
    }
    panel.querySelector('[data-clone-instance]').onclick = () => {
      if (!view.name.trim()) {
        status('请输入新实例名称。');
        return;
      }
      post({
        type: 'cloneInstance',
        version: view.version,
        name: view.name.trim(),
        saves: view.saves,
        servers: view.servers,
      });
      status('克隆请求已提交，请查看右上角任务状态。');
    };
    panel.querySelector('[data-export-instance]').onclick = () => {
      pendingExport = { version: view.version, saves: view.saves, servers: view.servers };
      post({ type: 'pickSaveFile', target: 'exportInstance' });
    };
    panel.querySelector('[data-uninstall-preview]').onclick = () =>
      post({ type: 'previewUninstall', version: view.version });
    panel.querySelector('[data-check-updates]').onclick = (event) => {
      event.target.disabled = true;
      view.checked = view.preview = null;
      reviews();
      status('正在检查更新…');
      post({ type: 'checkContentUpdates', version: view.version });
    };
    reviews();
  }

  function button(text, action) {
    const node = document.createElement('button');
    node.className = 'btn btn-ghost';
    node.textContent = text;
    node.onclick = action;
    return node;
  }

  function reviews() {
    const uninstall = document.querySelector('[data-uninstall-review]');
    const updates = document.querySelector('[data-update-list]');
    const preview = document.querySelector('[data-update-preview]');
    if (!uninstall || !updates || !preview) return;
    uninstall.replaceChildren();
    updates.replaceChildren();
    preview.replaceChildren();
    if (view.uninstall) {
      const heading = document.createElement('h3');
      heading.textContent = `卸载 ${view.uninstall.name}`;
      const notice = document.createElement('p');
      notice.textContent = view.uninstall.notice;
      const paths = document.createElement('ul');
      for (const path of view.uninstall.paths) {
        const item = document.createElement('li');
        item.textContent = path;
        paths.append(item);
      }
      const label = document.createElement('label');
      const confirm = document.createElement('input');
      confirm.type = 'checkbox';
      label.append(confirm, document.createTextNode('我已检查删除范围并确认备份'));
      const apply = button('确认卸载', () => {
        post({ type: 'applyUninstall', token: view.uninstall.token });
        view.uninstall = null;
        reviews();
        status('请在系统确认框核对卸载请求。');
      });
      apply.disabled = true;
      confirm.onchange = () => (apply.disabled = !confirm.checked);
      uninstall.append(heading, notice, paths, label, apply);
    }
    if (view.checked) {
      const selected = new Set();
      for (const item of view.checked.items) {
        const row = document.createElement('label');
        row.className = 'update-row';
        const check = document.createElement('input');
        check.type = 'checkbox';
        check.disabled = !item.available;
        check.onchange = () => (check.checked ? selected.add(item.id) : selected.delete(item.id));
        const text = document.createElement('span');
        text.textContent = `${item.name} · ${item.before} → ${item.after || item.error}`;
        row.append(check, text);
        updates.append(row);
      }
      if (!view.checked.items.length) updates.textContent = '当前实例没有启动器管理的内容。';
      updates.append(
        button('生成所选更新预览', () => {
          if (!selected.size) {
            status('请先选择更新项目。');
            return;
          }
          post({
            type: 'previewContentUpdates',
            token: view.checked.token,
            projects: [...selected],
          });
          status('正在解析更新及必需依赖…');
        })
      );
    }
    if (view.preview) {
      const title = document.createElement('h3');
      title.textContent = `更新预览 · ${(view.preview.bytes / 1048576).toFixed(2)} MiB`;
      preview.append(title);
      for (const change of view.preview.changes) {
        const row = document.createElement('p');
        row.textContent = `${change.project}: ${change.before} → ${change.after}
${change.path}`;
        preview.append(row);
      }
      preview.append(
        button('按预览加入更新队列', () => {
          post({ type: 'applyContentUpdates', token: view.preview.token });
          view.preview = null;
          reviews();
          status('更新请求已提交，请查看任务状态。');
        })
      );
    }
  }

  bridge?.addEventListener('message', (event) => {
    const message = event.data;
    if (!message) return;
    if (message.type === 'pickedPath' && message.target === 'exportInstance' && pendingExport) {
      post({ type: 'exportInstance', ...pendingExport, path: message.path });
      pendingExport = null;
      status('导出请求已提交，请查看任务状态。');
      return;
    }
    if (message.type === 'uninstallPreview') {
      view.version = message.version;
      view.uninstall = message;
      if (!document.querySelector('[data-instance-tools]'))
        window.mcugGo?.('?screen=settings&panel=instances');
      reviews();
      return;
    }
    if (!document.querySelector('[data-instance-tools]')) return;
    if (message.type === 'error') {
      status(message.message);
      document.querySelector('[data-check-updates]').disabled = false;
      return;
    }
    if (message.version !== view.version) return;
    if (message.type === 'contentUpdates') {
      view.checked = message;
      view.preview = null;
      status('检查完成，请选择项目生成预览。');
      document.querySelector('[data-check-updates]').disabled = false;
      reviews();
    }
    if (message.type === 'contentUpdatePreview') {
      view.preview = message;
      status('请核对文件变化后再执行。');
      reviews();
    }
  });
  window.McugInstances = { mount };
  mount();
})();
