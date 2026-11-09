const { Plugin, Notice, Menu, setIcon, MarkdownRenderer } = require('obsidian');

// Canvas's runtime API is internal; keep access to it in this small adapter.
module.exports = class CanvasAuthoring extends Plugin {
  async onload() {
    this.patched = new Map();
    this.controls = new Map();
    this.creationMenus = new Map();
    this.connectionUpdates = new Map();
    this.addCommand({id: 'copy-node-link', name: '复制跳转链接', checkCallback: checking => {
      const canvas = this.canvas();
      const node = canvas?.selection.size === 1 ? [...canvas.selection][0] : null;
      if (!node || !canvas.nodes.has(node.id)) return false;
      if (!checking) this.copyNodeLink(canvas, node);
      return true;
    }});
    this.registerDomEvent(document, 'click', event => {
      if (event.button !== 0 || event.shiftKey || event.altKey) return;
      const link = event.target.closest?.('a.internal-link');
      const href = link?.getAttribute('data-href') || link?.getAttribute('href');
      if (!href || !/(?:^|\.canvas)#node=[^#]+$/.test(href)) return;
      event.preventDefault(); event.stopImmediatePropagation();
      let source = this.app.workspace.activeLeaf?.view?.file?.path;
      this.app.workspace.iterateAllLeaves(leaf => {
        if (leaf.view?.containerEl?.contains(link)) source = leaf.view.file?.path;
      });
      this.jumpToNode(href, source || '');
    }, true);
    this.addCommand({id: 'copy-content', name: '复制', checkCallback: checking => {
      const canvas = this.canvas();
      if (!this.canCopyContent(canvas)) return false;
      if (!checking) this.copyContent(canvas);
      return true;
    }});
    this.addCommand({id: 'copy-selection', name: '复制节点数据', checkCallback: checking => {
      const canvas = this.canvas();
      if (!this.canCopy(canvas)) return false;
      if (!checking) this.copySelection(canvas);
      return true;
    }});
    this.registerDomEvent(document, 'copy', event => {
      const canvas = this.canvas();
      if (!this.canCopyContent(canvas) || !event.clipboardData || document.activeElement !== canvas.wrapperEl) return;
      const data = this.selectionData(canvas);
      event.preventDefault(); event.stopImmediatePropagation();
      // Native Canvas data stays available for internal paste. External apps get content.
      if (!data.nodes.length) {
        event.clipboardData.setData('text/plain', this.selectedEdgeText(canvas));
        return;
      }
      const payload = this.nativeClipboardData(data);
      event.clipboardData.setData('obsidian/canvas', JSON.stringify(payload));
      const plain = this.simpleClipboardText(data);
      event.clipboardData.setData('text/plain', plain);
      // File bodies and image pixels require asynchronous vault reads.
      event.clipboardData.setData('text/html', this.clipboardHtml(payload, `<pre>${this.escapeHtml(plain)}</pre>`));
      this.copyContent(canvas, data);
    }, true);
    this.registerDomEvent(document, 'paste', event => {
      const canvas = this.canvas();
      if (!canvas || canvas.readonly || document.activeElement !== canvas.wrapperEl || event.defaultPrevented) return;
      if (event.clipboardData?.getData('obsidian/canvas')) return; // Let native paste handle it.
      const html = event.clipboardData?.getData('text/html') || '';
      const match = html.match(/<!--canvas-authoring:([A-Za-z0-9+/=]+)-->/);
      if (!match) return;
      event.preventDefault(); event.stopImmediatePropagation();
      try {
        const data = this.parseGroup(new TextDecoder().decode(Uint8Array.from(atob(match[1]), c => c.charCodeAt(0))));
        // Same behavior as native paste: preserve the selected fragment's layout at the viewport center.
        const box = canvas.getViewportBBox();
        const minX = Math.min(...data.nodes.map(n => n.x)), minY = Math.min(...data.nodes.map(n => n.y));
        const maxX = Math.max(...data.nodes.map(n => n.x + n.width)), maxY = Math.max(...data.nodes.map(n => n.y + n.height));
        const added = canvas.importData(canvas.cloneData(data, {
          x: (box.minX + box.maxX - minX - maxX) / 2,
          y: (box.minY + box.maxY - minY - maxY) / 2
        }), false);
        canvas.deselectAll(); for (const node of added) canvas.select(node);
        canvas.requestSave(); this.attach();
      } catch (error) { new Notice(`粘贴节点失败：${error.message}`); }
    }, true);
    this.addCommand({id: 'copy-group', name: '复制分组数据', checkCallback: checking => {
      const canvas = this.canvas(), group = this.selectedGroup(canvas);
      if (!group) return false;
      if (!checking) this.copyGroup(canvas, group);
      return true;
    }});
    this.addCommand({id: 'paste-group', name: '从剪贴板粘贴内容', checkCallback: checking => {
      const canvas = this.canvas();
      if (!canvas || canvas.readonly) return false;
      if (!checking) this.pasteGroup(canvas);
      return true;
    }});
    this.addCommand({id: 'duplicate-selection', name: '生成副本',
      hotkeys: [{modifiers: ['Mod'], key: 'd'}],
      checkCallback: checking => {
        const canvas = this.canvas();
        if (!this.canDuplicate(canvas)) return false;
        if (!checking) this.duplicate(canvas);
        return true;
      }
    });
    this.addCommand({id: 'add-annotation', name: '添加备注', checkCallback: checking => {
      const canvas = this.canvas();
      if (!canvas || canvas.readonly) return false;
      if (!checking) this.addAnnotation(canvas);
      return true;
    }});
    for (const [id, name, kind] of [
      ['add-child', '添加子节点', 'child'],
      ['add-sibling', '添加同级节点', 'sibling'],
      ['add-next', '添加后续节点', 'next']
    ]) this.addCommand({ id, name, checkCallback: checking => {
      const canvas = this.canvas();
      const node = this.selected(canvas);
      if (!node) return false;
      if (!checking) this.add(canvas, node, kind);
      return true;
    }});
    this.registerDomEvent(document, 'keydown', event => {
      if (event.isComposing) return;
      if (event.target.closest?.('input, textarea, [contenteditable="true"], .cm-editor')) return;
      // Canvas has its own keyboard scope. Handle duplication before that scope
      // consumes the event; keep the command as a discoverable menu entry.
      if ((event.metaKey || event.ctrlKey) && !event.altKey && !event.shiftKey && event.key.toLowerCase() === 'd') {
        const canvas = this.canvas();
        if (!this.canDuplicate(canvas)) return;
        event.preventDefault(); event.stopImmediatePropagation();
        this.duplicate(canvas);
        return;
      }
      if (event.defaultPrevented || event.ctrlKey || event.metaKey || event.altKey || event.shiftKey) return;
      const canvas = this.canvas(), node = this.selected(canvas);
      if (!node || !['Tab', 'Enter'].includes(event.key)) return;
      if (event.key === 'Enter' && !this.parent(canvas, node)) return;
      event.preventDefault(); event.stopImmediatePropagation();
      this.add(canvas, node, event.key === 'Tab' ? 'child' : 'sibling');
    }, true);
    this.registerEvent(this.app.workspace.on('canvas:selection-menu', (menu, canvas) => {
      if (this.canCopyContent(canvas)) menu.addItem(item => item.setTitle('复制').setIcon('copy').setSection('clipboard')
        .onClick(() => this.copyContent(canvas))); 
      if (this.canCopy(canvas)) menu.addItem(item => item.setTitle('复制节点数据').setIcon('copy').setSection('clipboard')
        .onClick(() => this.copySelection(canvas)));
      if (this.canDuplicate(canvas)) menu.addItem(item => item.setTitle('生成副本').setIcon('copy-plus').setSection('action')
        .onClick(() => this.duplicate(canvas)));
    }));
    this.registerEvent(this.app.workspace.on('canvas:node-menu', (menu, node) => {
      if (node.getData().type === 'text') {
        menu.addItem(item => item.setTitle('文字颜色').setIcon('type').onClick(event => this.fontColorMenu(node.canvas || this.canvas(), node, event)));
        menu.addItem(item => item.setTitle('作为备注／评论').setChecked(node.getData().authoringStyle === 'annotation').onClick(() => {
          const data = {...node.getData()};
          if (data.authoringStyle === 'annotation') delete data.authoringStyle;
          else data.authoringStyle = 'annotation';
          node.setData(data); (node.canvas || this.canvas()).requestSave();
        }));
      }
      const canvas = node.canvas || this.canvas();
      menu.addItem(item => item.setTitle('复制').setIcon('copy').setSection('clipboard')
        .onClick(() => this.copyContent(canvas, this.selectionData(canvas, canvas.selection.has(node) ? canvas.selection : [node]))));
      menu.addItem(item => item.setTitle('复制跳转链接').setIcon('link')
        .onClick(() => this.copyNodeLink(canvas, node)));
      if (node.getData().type !== 'group') menu.addItem(item => item.setTitle('复制节点数据').setIcon('copy')
        .onClick(() => this.copySelection(canvas)));
      if (node.getData().type === 'group') menu.addItem(item => item.setTitle('复制分组数据').setIcon('copy')
        .onClick(() => this.copyGroup(canvas, node)));
      if (this.canDuplicate(canvas)) menu.addItem(item => item.setTitle('生成副本').setIcon('copy')
        .onClick(() => this.duplicate(canvas)));
      for (const [title, kind] of [['添加子节点', 'child'], ['添加同级节点', 'sibling'], ['添加后续节点', 'next']])
        menu.addItem(item => item.setTitle(title).onClick(() => this.add(node.canvas || this.canvas(), node, kind)));
    }));
    this.registerInterval(window.setInterval(() => this.attach(), 250));
    this.attach();
  }
  nodeLink(canvas, node) {
    const file = canvas.view?.file;
    if (!file?.path) throw new Error('请先保存画板');
    const data = node.getData();
    const title = (data.label || data.text?.split('\n').find(line => line.trim()) || data.file || data.url || '查看节点')
      .replace(/[#*|\[\]\r\n]/g, '').trim().slice(0, 60) || '查看节点';
    return `[[${file.path}#node=${encodeURIComponent(node.id)}|${title}]]`;
  }
  async copyNodeLink(canvas, node) {
    try {
      await navigator.clipboard.writeText(this.nodeLink(canvas, node));
      new Notice('已复制跳转链接，可粘贴到文字中');
    } catch (error) { new Notice(`复制跳转链接失败：${error.message}`); }
  }
  async jumpToNode(href, sourcePath) {
    try {
      const split = href.lastIndexOf('#node=');
      const path = href.slice(0, split), id = decodeURIComponent(href.slice(split + 6));
      const file = path ? this.app.metadataCache.getFirstLinkpathDest(path, sourcePath) : this.app.vault.getAbstractFileByPath(sourcePath);
      if (!file || file.extension !== 'canvas') throw new Error('目标 Canvas 不存在');
      // Check before navigating so a deleted target does not move the user's view.
      let liveData;
      this.app.workspace.iterateAllLeaves(leaf => {
        if (leaf.view?.file?.path === file.path && leaf.view.canvas) liveData = leaf.view.canvas.getData();
      });
      const data = liveData || JSON.parse(await this.app.vault.cachedRead(file));
      if (!data.nodes?.some(n => n.id === id)) throw new Error('目标节点已删除，引用失效');
      const leaf = this.app.workspace.getLeaf(false);
      if (leaf.view?.file?.path !== file.path) await leaf.openFile(file);
      for (let attempt = 0; attempt < 40; attempt++) {
        if (leaf.view?.file?.path !== file.path) throw new Error('已切换画板，取消跳转');
        const canvas = leaf.view?.canvas, node = canvas?.nodes.get(id);
        if (node) {
          canvas.deselectAll(); canvas.select(node);
          canvas.zoomToBbox(node.getBBox());
          canvas.wrapperEl?.focus();
          return;
        }
        await new Promise(resolve => window.setTimeout(resolve, 50));
      }
      throw new Error('目标画板未完成加载，请重试');
    } catch (error) { new Notice(`跳转失败：${error.message}`); }
  }
  canvas() { return this.app.workspace.activeLeaf?.view?.canvas; }
  selected(canvas) {
    if (!canvas || canvas.selection.size !== 1) return null;
    const node = [...canvas.selection][0];
    return node.getData?.().type === 'text' || node.getData?.().type === 'file' ? node : null;
  }
  selectedGroup(canvas) {
    if (!canvas || canvas.selection.size !== 1) return null;
    const node = [...canvas.selection][0];
    return node.getData?.().type === 'group' ? node : null;
  }
  canCopy(canvas) {
    if (!canvas || ![...canvas.selection].some(n => canvas.nodes.has(n.id))) return false;
    return !document.activeElement?.closest?.('input, textarea, [contenteditable="true"], .cm-editor');
  }
  selectedEdgeText(canvas) {
    return [...canvas.selection].filter(item => canvas.edges.has(item.id))
      .map(edge => edge.getData().label || '').filter(Boolean).join('\n\n');
  }
  canCopyContent(canvas) {
    if (this.canCopy(canvas)) return true;
    if (!canvas || document.activeElement?.closest?.('input, textarea, [contenteditable="true"], .cm-editor')) return false;
    return Boolean(this.selectedEdgeText(canvas));
  }
  selectionData(canvas, selected = canvas.selection) {
    const data = structuredClone(canvas.getData());
    const ids = new Set([...selected].filter(n => canvas.nodes.has(n.id)).map(n => n.id));
    const groups = data.nodes.filter(n => ids.has(n.id) && n.type === 'group');
    const nodes = data.nodes.filter(n => ids.has(n.id) || groups.some(g => n.x >= g.x && n.y >= g.y &&
      n.x + n.width <= g.x + g.width && n.y + n.height <= g.y + g.height));
    const included = new Set(nodes.map(n => n.id));
    return {nodes, edges: data.edges.filter(e => included.has(e.fromNode) && included.has(e.toNode))};
  }
  groupData(canvas, group) { return this.selectionData(canvas, [group]); }
  escapeHtml(text) {
    return String(text).replace(/[&<>"']/g, c => ({'&':'&amp;', '<':'&lt;', '>':'&gt;', '"':'&quot;', "'":'&#39;'}[c]));
  }
  nativeClipboardData(data) {
    if (!data.nodes.length) throw new Error('请先选中节点');
    return {...data, center: {
      x: (Math.min(...data.nodes.map(n => n.x)) + Math.max(...data.nodes.map(n => n.x + n.width))) / 2,
      y: (Math.min(...data.nodes.map(n => n.y)) + Math.max(...data.nodes.map(n => n.y + n.height))) / 2
    }};
  }
  clipboardHtml(data, content) {
    // HTML carries the native fragment invisibly when the system image clipboard cannot carry
    // Obsidian's private MIME type. External plain text never contains JSON.
    const bytes = new TextEncoder().encode(JSON.stringify(data));
    let binary = ''; for (const byte of bytes) binary += String.fromCharCode(byte);
    return `<!--canvas-authoring:${btoa(binary)}--><div>${content}</div>`;
  }
  clipboardOrder(data) {
    const groups = data.nodes.filter(n => n.type === 'group');
    const parents = new Map();
    for (const node of data.nodes) {
      const parent = groups.filter(g => g.id !== node.id && g.width * g.height > node.width * node.height &&
        node.x >= g.x && node.y >= g.y && node.x + node.width <= g.x + g.width && node.y + node.height <= g.y + g.height)
        .sort((a,b) => a.width*a.height - b.width*b.height || a.id.localeCompare(b.id))[0];
      parents.set(node.id, parent?.id);
    }
    const result = [];
    const walk = (parent, depth) => {
      const children = data.nodes.filter(n => parents.get(n.id) === parent).sort((a,b) => a.y-b.y || a.x-b.x || a.id.localeCompare(b.id));
      for (const node of children) { result.push({node, depth}); if (node.type === 'group') walk(node.id, depth+1); }
    };
    walk(undefined, 0); return result;
  }
  fileLink(path, subpath = '') {
    return `obsidian://open?vault=${encodeURIComponent(this.app.vault.getName())}&file=${encodeURIComponent(path + subpath)}`;
  }
  simpleClipboardText(data) {
    return this.clipboardOrder(data).map(({node:n, depth}) => {
      if (n.type === 'group') return `${'#'.repeat(Math.min(depth+2,6))} ${n.label || '分组'}`;
      if (n.type === 'text') return n.text;
      if (n.type === 'link') return n.url;
      return `[${n.file.split('/').pop()}](${this.fileLink(n.file, n.subpath)})`;
    }).filter(Boolean).join('\n\n');
  }
  async noteText(file, subpath) {
    const text = await this.app.vault.cachedRead(file);
    if (!subpath || subpath === '#') return text;
    const ref = decodeURIComponent(subpath.replace(/^#/, ''));
    const cache = this.app.metadataCache.getFileCache(file);
    if (ref.startsWith('^')) {
      const block = cache?.blocks?.[ref.slice(1)];
      if (!block) throw new Error(`笔记块不存在：${subpath}`);
      return text.slice(block.position.start.offset, block.position.end.offset);
    }
    const headings = cache?.headings || [];
    const index = headings.findIndex(h => h.heading === ref);
    if (index < 0) throw new Error(`笔记章节不存在：${subpath}`);
    const start = headings[index];
    const end = headings.slice(index+1).find(h => h.level <= start.level);
    return text.slice(start.position.start.offset, end?.position.start.offset ?? text.length).trimEnd();
  }
  async imageData(file) {
    const mime = {png:'image/png',jpg:'image/jpeg',jpeg:'image/jpeg',gif:'image/gif',webp:'image/webp',svg:'image/svg+xml',bmp:'image/bmp',avif:'image/avif'}[file.extension.toLowerCase()];
    if (!mime) throw new Error(`不支持的图片格式：${file.extension}`);
    const bytes = await this.app.vault.readBinary(file);
    const blob = new Blob([bytes], {type:mime});
    const url = await new Promise((resolve,reject) => {
      const reader = new FileReader(); reader.onload = () => resolve(reader.result); reader.onerror = () => reject(reader.error); reader.readAsDataURL(blob);
    });
    return {blob, url};
  }
  async pngBlob(blob) {
    if (blob.type === 'image/png') return blob;
    const url = URL.createObjectURL(blob);
    try {
      const image = new Image();
      await new Promise((resolve,reject) => { image.onload=resolve; image.onerror=()=>reject(new Error('图片无法解码')); image.src=url; });
      const surface = document.createElement('canvas'); surface.width=image.naturalWidth; surface.height=image.naturalHeight;
      surface.getContext('2d').drawImage(image,0,0);
      return await new Promise((resolve,reject) => surface.toBlob(b => b ? resolve(b) : reject(new Error('图片编码失败')), 'image/png'));
    } finally { URL.revokeObjectURL(url); }
  }
  async renderedMarkdown(text, source) {
    const element = document.createElement('div');
    await MarkdownRenderer.render(this.app, text, element, source, this);
    // Resource URLs are app-local; embed vault images for external rich-text paste.
    for (const image of element.querySelectorAll('img')) {
      const embed = image.closest('.internal-embed');
      const link = embed?.getAttribute('src') || image.getAttribute('data-href');
      if (link) {
        const file = this.app.metadataCache.getFirstLinkpathDest(link.split('#')[0], source);
        if (file && /^(png|jpe?g|gif|webp|svg|bmp|avif)$/i.test(file.extension)) image.src=(await this.imageData(file)).url;
      }
    }
    for (const a of element.querySelectorAll('a.internal-link')) {
      const link = a.getAttribute('data-href') || a.getAttribute('href');
      if (!link) continue;
      const [path,...fragment] = link.split('#');
      const file = path ? this.app.metadataCache.getFirstLinkpathDest(path,source) : this.app.vault.getAbstractFileByPath(source);
      if (file) a.href=this.fileLink(file.path,fragment.length ? '#'+fragment.join('#') : '');
    }
    return element.innerHTML;
  }
  async contentClipboard(data, source) {
    const plain=[],html=[];let image;
    for (const {node:n,depth} of this.clipboardOrder(data)) {
      if (n.type === 'group') {
        const level=Math.min(depth+2,6), title=n.label || '分组';
        plain.push(`${'#'.repeat(level)} ${title}`);html.push(`<h${level}>${this.escapeHtml(title)}</h${level}>`);
      } else if (n.type === 'text') {
        plain.push(n.text);html.push(await this.renderedMarkdown(n.text,source));
      } else if (n.type === 'link') {
        plain.push(n.url);html.push(`<p><a href="${this.escapeHtml(n.url)}">${this.escapeHtml(n.url)}</a></p>`);
      } else if (n.type === 'file') {
        const file=this.app.vault.getAbstractFileByPath(n.file);
        if (!file) throw new Error(`文件不存在：${n.file}`);
        if (/^(png|jpe?g|gif|webp|svg|bmp|avif)$/i.test(file.extension)) {
          const picture=await this.imageData(file);
          plain.push(`![${file.name}](${this.fileLink(file.path)})`);
          html.push(`<p><img src="${picture.url}" alt="${this.escapeHtml(file.name)}"></p>`);
          if (data.nodes.length===1) image=await this.pngBlob(picture.blob);
        } else if (file.extension.toLowerCase()==='md') {
          const text=await this.noteText(file,n.subpath);plain.push(text);html.push(await this.renderedMarkdown(text,file.path));
        } else {
          const url=this.fileLink(file.path,n.subpath);plain.push(`[${file.name}](${url})`);
          html.push(`<p><a href="${this.escapeHtml(url)}">${this.escapeHtml(file.name)}</a></p>`);
        }
      }
    }
    return {plain:plain.filter(Boolean).join('\n\n'),html:this.clipboardHtml(this.nativeClipboardData(data),html.join('\n')),image};
  }
  async copyContent(canvas, data = this.selectionData(canvas)) {
    try {
      if (!data.nodes.length) {
        const text=this.selectedEdgeText(canvas);
        if (text) await navigator.clipboard.writeText(text);
        return;
      }
      // Resolve the item lazily so clipboard permission is requested in the user gesture.
      const content=this.contentClipboard(data,canvas.view?.file?.path || '');
      const singleImage=data.nodes.length===1 && data.nodes[0].type==='file' && /\.(png|jpe?g|gif|webp|svg|bmp|avif)$/i.test(data.nodes[0].file);
      const formats={'text/html':content.then(c => new Blob([c.html],{type:'text/html'}))};
      if (singleImage) formats['image/png']=content.then(c => c.image);
      else formats['text/plain']=content.then(c => new Blob([c.plain],{type:'text/plain'}));
      await navigator.clipboard.write([new ClipboardItem(formats)]);
    } catch (error) { new Notice(`复制内容失败：${error.message}`); }
  }
  async copySelection(canvas) {
    try {
      const data = this.selectionData(canvas);
      if (!data.nodes.length) throw new Error('请先选中节点');
      await navigator.clipboard.writeText(JSON.stringify(data, null, 2));
      new Notice(`已复制 ${data.nodes.length} 个节点到剪贴板`);
    } catch (error) { new Notice(`复制失败：${error.message}`); }
  }
  async copyGroup(canvas, group) {
    try {
      const data = this.groupData(canvas, group);
      await navigator.clipboard.writeText(JSON.stringify(data, null, 2));
      const references = data.nodes.filter(n => n.type === 'file').length;
      new Notice(`已复制分组及 ${data.nodes.length - 1} 个节点${references ? '（图片和笔记仅包含文件引用）' : ''}`);
    } catch (error) {
      new Notice(`复制分组失败：${error.message}`);
    }
  }
  parseGroup(text) {
    const trimmed = text.trim();
    const fenced = trimmed.match(/^```(?:json)?\s*\n([\s\S]*?)\n```$/i);
    const data = JSON.parse(fenced ? fenced[1] : trimmed);
    const fail = message => { throw new Error(message); };
    if (!data || !Array.isArray(data.nodes) || !Array.isArray(data.edges)) fail('需要包含 nodes 和 edges 的 Canvas JSON');
    const ids = new Set();
    for (const n of data.nodes) {
      if (!n || typeof n.id !== 'string' || !n.id || ids.has(n.id)) fail('节点 ID 缺失或重复');
      ids.add(n.id);
      if (!['text', 'file', 'link', 'group'].includes(n.type)) fail('不支持的节点类型');
      if (![n.x, n.y, n.width, n.height].every(Number.isFinite) || n.width <= 0 || n.height <= 0) fail('节点位置或尺寸无效');
      const field = {text:'text', file:'file', link:'url', group:'label'}[n.type];
      if (n.type !== 'group' && typeof n[field] !== 'string') fail(`节点缺少 ${field}`);
    }
    if (!data.nodes.length) fail('剪贴板内容没有节点');
    const edgeIds = new Set();
    for (const e of data.edges) {
      if (!e || typeof e.id !== 'string' || edgeIds.has(e.id)) fail('连线 ID 缺失或重复');
      edgeIds.add(e.id);
      if (!ids.has(e.fromNode) || !ids.has(e.toNode)) fail('连线引用了分组之外或不存在的节点');
      for (const key of ['fromSide', 'toSide']) if (e[key] !== undefined && !['top','right','bottom','left'].includes(e[key])) fail('连线方向无效');
      for (const key of ['fromEnd', 'toEnd']) if (e[key] !== undefined && !['none','arrow'].includes(e[key])) fail('箭头类型无效');
    }
    return data;
  }
  insertGroup(canvas, data, position) {
    if (canvas.readonly) throw new Error('画板只读');
    const current = canvas.getData();
    const minX = Math.min(...data.nodes.map(n => n.x)), minY = Math.min(...data.nodes.map(n => n.y));
    // With no mouse position (command palette), append beneath the board.
    const target = position || {x: minX, y: current.nodes.length ? Math.max(...current.nodes.map(n => n.y + n.height)) + 100 : 0};
    const dx = target.x - minX;
    let dy = target.y - minY;
    const width = Math.max(...data.nodes.map(n => n.x + n.width)) - minX;
    const height = Math.max(...data.nodes.map(n => n.y + n.height)) - minY;
    // Reserve the entire incoming frame, including whitespace, against existing groups.
    let hits;
    do {
      hits = current.nodes.filter(n => target.x < n.x + n.width + 24 && target.x + width + 24 > n.x &&
        minY + dy < n.y + n.height + 24 && minY + dy + height + 24 > n.y);
      if (hits.length) dy = Math.max(...hits.map(n => n.y + n.height + 24)) - minY;
    } while (hits.length);
    const freshId = () => crypto.randomUUID().replaceAll('-', '').slice(0, 16);
    const remap = new Map(data.nodes.map(n => [n.id, freshId()]));
    const nodes = data.nodes.map(n => ({...n, id: remap.get(n.id), x: n.x + dx, y: n.y + dy}));
    const edges = data.edges.map(e => ({...e, id: freshId(), fromNode: remap.get(e.fromNode), toNode: remap.get(e.toNode)}));
    const added = canvas.importData({nodes, edges}, false);
    canvas.deselectAll();
    for (const node of added) canvas.select(node);
    canvas.requestSave();
    this.attach();
    canvas.zoomToSelection?.();
  }
  async pasteGroup(canvas, position) {
    try {
      const data = this.parseGroup(await navigator.clipboard.readText());
      this.insertGroup(canvas, data, position);
      new Notice('已粘贴为新内容，原稿保留');
    } catch (error) {
      new Notice(`粘贴分组失败：${error.message}`);
    }
  }
  canDuplicate(canvas) {
    if (!canvas || canvas.readonly || !canvas.selection.size) return false;
    const active = document.activeElement;
    if (active?.closest?.('input, textarea, [contenteditable="true"], .cm-editor')) return false;
    return [...canvas.selection].some(item => canvas.nodes.has(item.id));
  }
  duplicate(canvas) {
    if (!this.canDuplicate(canvas)) return;
    const data = structuredClone(canvas.getData());
    const ids = new Set([...canvas.selection].filter(item => canvas.nodes.has(item.id)).map(item => item.id));
    // Groups are spatial frames in JSON Canvas. Include only fully enclosed nodes.
    const groups = data.nodes.filter(n => ids.has(n.id) && n.type === 'group');
    for (const n of data.nodes) {
      if (groups.some(g => n.x >= g.x && n.y >= g.y &&
          n.x + n.width <= g.x + g.width && n.y + n.height <= g.y + g.height)) ids.add(n.id);
    }
    const originals = data.nodes.filter(n => ids.has(n.id));
    const dx = Math.max(...originals.map(n => n.x + n.width)) - Math.min(...originals.map(n => n.x)) + 60;
    let dy = 0;
    // Move the copy as one unit; leave the original board layout untouched.
    const obstacles = data.nodes.filter(n => n.type !== 'group');
    const collides = () => originals.filter(n => n.type !== 'group').flatMap(n =>
      obstacles.filter(o => n.x + dx < o.x + o.width + 24 && n.x + dx + n.width + 24 > o.x &&
        n.y + dy < o.y + o.height + 24 && n.y + dy + n.height + 24 > o.y)
        .map(o => o.y + o.height + 24 - n.y));
    for (let hits = collides(); hits.length; hits = collides()) dy = Math.max(...hits);
    const freshId = () => crypto.randomUUID().replaceAll('-', '').slice(0, 16);
    const remap = new Map(originals.map(n => [n.id, freshId()]));
    const copies = originals.map(n => ({...n, id: remap.get(n.id), x: n.x + dx, y: n.y + dy}));
    // Copy internal edges only, regardless of arrow type. Never graft onto an external flow.
    const edges = data.edges.filter(e => ids.has(e.fromNode) && ids.has(e.toNode))
      .map(e => ({...e, id: freshId(), fromNode: remap.get(e.fromNode), toNode: remap.get(e.toNode)}));
    data.nodes.push(...copies); data.edges.push(...edges);
    canvas.importData(data);
    canvas.deselectAll();
    for (const n of copies) {
      const node = canvas.nodes.get(n.id);
      if (node) canvas.select(node);
    }
    canvas.requestSave();
    this.attach();
  }
  isConnection(data) {
    // JSON Canvas defaults: fromEnd=none, toEnd=arrow.
    return (data.fromEnd ?? 'none') === 'none' && (data.toEnd ?? 'arrow') === 'none';
  }
  parent(canvas, node) {
    const incoming = [...canvas.edges.values()].filter(e => {
      const d = e.getData();
      return d.toNode === node.id && this.isConnection(d);
    });
    return incoming.length === 1 ? canvas.nodes.get(incoming[0].getData().fromNode) : null;
  }
  add(canvas, source, kind, direction = 'right') {
    if (!canvas) return;
    if (kind === 'sibling') {
      const sibling = source;
      source = this.parent(canvas, sibling);
      if (source) {
        const edge = [...canvas.edges.values()].find(e => {
          const d = e.getData();
          return d.fromNode === source.id && d.toNode === sibling.id && this.isConnection(d);
        });
        direction = edge.getData().fromSide;
      }
      if (!source) { new Notice('当前节点没有唯一的来源连接，无法确定从哪里添加同级节点'); return; }
      kind = 'child';
    }
    const data = canvas.getData();
    const original = source.getData();
    const child = kind === 'child';
    const width = child ? Math.max(200, Math.min(260, original.width * .85)) : original.width;
    const height = child ? 80 : Math.max(100, original.height);
    const gap = child ? 80 : 100;
    const vertical = direction === 'top' || direction === 'bottom';
    let x = vertical ? original.x + (original.width - width) / 2 : direction === 'left' ? original.x - width - gap : original.x + original.width + gap;
    let y = !vertical ? original.y : direction === 'top' ? original.y - height - gap : original.y + original.height + gap;
    while (data.nodes.some(n => n.type !== 'group' && x < n.x + n.width + 24 && x + width + 24 > n.x && y < n.y + n.height + 24 && y + height + 24 > n.y)) {
      const obstacles = data.nodes.filter(n => n.type !== 'group' && x < n.x + n.width + 24 && x + width + 24 > n.x && y < n.y + n.height + 24 && y + height + 24 > n.y);
      if (vertical) x = Math.max(...obstacles.map(n => n.x + n.width + 24));
      else y = Math.max(...obstacles.map(n => n.y + n.height + 24));
    }
    const id = crypto.randomUUID().replaceAll('-', '').slice(0, 16);
    data.nodes.push({ id, type: 'text', text: '', x, y, width, height, ...(original.color ? {color: original.color} : {}) });
    data.edges.push({ id: crypto.randomUUID().replaceAll('-', '').slice(0,16), fromNode: source.id, fromSide: direction, toNode: id, toSide: {right:'left', left:'right', top:'bottom', bottom:'top'}[direction], fromEnd: 'none', toEnd: child ? 'none' : 'arrow' });
    // importData updates the live canvas, including its native undo/save pipeline.
    canvas.importData(data);
    canvas.requestSave();
    window.setTimeout(() => {
      const node = canvas.nodes.get(id);
      if (!node) return;
      canvas.deselectAll(); canvas.select(node);
      node.startEditing?.();
      this.attach();
    }, 0);
  }
  attach() {
    this.releaseConnectionUpdates();
    this.app.workspace.iterateAllLeaves(leaf => {
      const canvas = leaf.view?.canvas;
      if (!canvas) return;
      for (const node of canvas.nodes.values()) this.paintNode(node);
      this.syncTransparentChoice(canvas, leaf.view);
      this.patchCreationMenu(canvas);
      this.syncStyleButton(canvas, leaf.view);
      this.syncControls(canvas);
      for (const edge of canvas.edges.values()) {
        if (this.patched.has(edge)) {
          this.draw(edge);
          continue;
        }
        const original = edge.updatePath;
        if (typeof original !== 'function') continue;
        const plugin = this;
        const wrapped = function(...args) {
          const result = original.apply(this, args);
          plugin.draw(this);
          return result;
        };
        edge.updatePath = wrapped;
        this.patched.set(edge, {original, wrapped});
        edge.updatePath();
      }
      for (const [edge, record] of this.patched) if (edge.canvas === canvas && !canvas.edges.has(edge.id)) {
        if (edge.updatePath === record.wrapped) edge.updatePath = record.original;
        this.patched.delete(edge);
      }
    });
  }
  patchCreationMenu(canvas) {
    if (this.creationMenus.has(canvas)) return;
    const original = canvas.showCreationMenu;
    if (typeof original !== 'function') return;
    const plugin = this;
    const wrapped = function(menu, position, size) {
      const result = original.call(this, menu, position, size);
      if (!this.readonly) menu.addItem(item => item.setTitle('从剪贴板粘贴内容').setIcon('clipboard-paste')
        .onClick(() => plugin.pasteGroup(this, position)));
      if (!this.readonly) menu.addItem(item => item.setTitle('添加备注').setSection('create').setIcon('sticky-note')
        .onClick(() => plugin.addAnnotation(this, position)));
      return result;
    };
    canvas.showCreationMenu = wrapped;
    this.creationMenus.set(canvas, {original, wrapped});
  }
  addAnnotation(canvas, position) {
    const box = canvas.getViewportBBox();
    const node = canvas.createTextNode({
      pos: position || {x: (box.minX + box.maxX) / 2 - 150, y: (box.minY + box.maxY) / 2 - 60},
      size: {width: 300, height: 120}, text: '输入备注', focus: true
    });
    node.setData({...node.getData(), authoringStyle: 'annotation', color: 'transparent', fontColor: '1'});
    canvas.requestSave();
    this.attach();
  }
  paintNode(node) {
    let data = node.getData();
    const el = node.nodeEl;
    if (!el) return;
    el.classList.toggle('canvas-authoring-transparent', data.color === 'transparent');
    const fontColor = data.fontColor;
    el.classList.toggle('canvas-authoring-font-color', Boolean(fontColor));
    if (fontColor) el.style.setProperty('--authoring-font-color', /^[1-6]$/.test(fontColor) ? `var(--canvas-color-${fontColor})` : fontColor);
    else el.style.removeProperty('--authoring-font-color');
  }
  setFontColor(canvas, node, color) {
    const data = {...node.getData()};
    if (color) data.fontColor = color;
    else delete data.fontColor;
    node.setData(data);
    canvas.requestSave();
    this.paintNode(node);
  }
  fontColorMenu(canvas, node, event) {
    const menu = new Menu();
    const colors = [['默认', undefined], ['红色', '1'], ['橙色', '2'], ['黄色', '3'], ['绿色', '4'], ['青色', '5'], ['紫色', '6']];
    for (const [name, color] of colors) menu.addItem(item => item.setTitle(name)
      .setChecked(node.getData().fontColor === color)
      .onClick(() => this.setFontColor(canvas, node, color)));
    menu.showAtMouseEvent(event);
  }
  syncTransparentChoice(canvas, view) {
    const palettes = new Set([...view.containerEl.querySelectorAll('.canvas-color-picker-item')].map(el => el.parentElement));
    for (const palette of palettes) {
      const selected = [...canvas.selection];
      const transparent = selected.length > 0 && selected.every(node => node.getData().color === 'transparent');
      const existing = palette.querySelector('.canvas-authoring-transparent-choice');
      if (existing) {
        existing.classList.toggle('is-active', transparent);
        if (transparent) palette.querySelectorAll('.canvas-color-picker-item:not(.canvas-authoring-transparent-choice)').forEach(el => el.classList.remove('is-active'));
        continue;
      }
      // Native picker treats the unsupported transparent value as a custom black.
      // The extension owns its swatch and selection state instead.
      if (transparent) palette.querySelectorAll('.canvas-color-picker-item').forEach(el => el.classList.remove('is-active'));
      const option = document.createElement('div');
      option.className = 'canvas-color-picker-item canvas-authoring-transparent-choice';
      option.classList.toggle('is-active', transparent);
      option.setAttribute('role', 'button'); option.tabIndex = 0;
      option.title = '透明（边框和底色）'; option.setAttribute('aria-label', '透明（边框和底色）');
      const apply = () => {
        if (canvas.readonly) return;
        for (const node of canvas.selection) {
          if (!canvas.nodes.has(node.id)) continue;
          node.setData({...node.getData(), color: 'transparent'});
          this.paintNode(node);
        }
        canvas.requestSave();
        palette.querySelectorAll('.canvas-color-picker-item').forEach(el => el.classList.remove('is-active'));
        option.classList.add('is-active');
      };
      option.addEventListener('click', event => { event.stopPropagation(); apply(); });
      option.addEventListener('keydown', event => {
        if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); apply(); }
      });
      palette.appendChild(option);
    }
  }
  syncStyleButton(canvas, view) {
    const controls = canvas.cardMenuEl || view.containerEl.querySelector('.canvas-card-menu');
    if (controls && !canvas.readonly && !controls.querySelector('.canvas-authoring-new-note')) {
      const add = document.createElement('div');
      add.setAttribute('role', 'button');
      add.tabIndex = 0;
      add.className = 'canvas-card-menu-button canvas-authoring-new-note';
      add.title = '添加备注'; add.setAttribute('aria-label', '添加备注');
      setIcon(add, 'type');
      add.addEventListener('click', event => { event.stopPropagation(); this.addAnnotation(canvas); });
      add.addEventListener('keydown', event => {
        if (event.key === 'Enter' || event.key === ' ') {
          event.preventDefault(); this.addAnnotation(canvas);
        }
      });
      controls.appendChild(add);
    }
    const toolbar = view.containerEl.querySelector('.canvas-menu');
    if (!toolbar) return;
    const node = this.selected(canvas);
    let button = toolbar.querySelector('.canvas-authoring-style');
    if (!node || node.getData().type !== 'text') { button?.remove(); return; }
    if (!button) {
      button = document.createElement('button');
      button.className = 'clickable-icon canvas-authoring-style';
      button.type = 'button';
      button.title = '文字颜色';
      button.setAttribute('aria-label', '文字颜色');
      setIcon(button, 'type');
      button.addEventListener('pointerdown', e => e.stopPropagation());
      button.addEventListener('click', event => {
        event.stopPropagation();
        const current = this.selected(canvas);
        if (current) this.fontColorMenu(canvas, current, event);
      });
      toolbar.appendChild(button);
    }
  }
  // Reuse native hit testing, pan, cancellation and the empty-space creation menu.
  // Only this gesture's edge ends differ from the native flow connector.
  startConnection(event, canvas, node, direction, button) {
    if (canvas.readonly || !event.isPrimary || event.button !== 0) return;
    if (typeof node.onConnectionPointerdown !== 'function') {
      new Notice('当前 Obsidian 版本不支持此连线操作'); return;
    }
    this.cancelConnectionStart?.();
    button.connectionDragged = false;
    const original = canvas.addEdge, plugin = this, win = button.ownerDocument.defaultView;
    const cleanup = () => {
      if (canvas.addEdge === wrapped) canvas.addEdge = original;
      win.removeEventListener('pointerup', release);
      win.removeEventListener('pointercancel', release);
      win.removeEventListener('blur', cleanup);
      win.removeEventListener('keydown', escape);
      if (plugin.cancelConnectionStart === cleanup) plugin.cancelConnectionStart = null;
    };
    const release = e => { if (e.pointerId === event.pointerId) cleanup(); };
    const escape = e => { if (e.key === 'Escape') cleanup(); };
    const wrapped = function(edge, ...args) {
      // Ignore unrelated additions while waiting for the native drag threshold.
      if (edge.from?.node !== node || edge.from?.side !== direction) return original.call(this, edge, ...args);
      cleanup();
      button.connectionDragged = true;
      const update = edge.update;
      const association = function(from, to) {
        const result = update.call(this, {...from, end: 'none'}, {...to, end: 'none'});
        plugin.draw(this);
        return result;
      };
      edge.update = association;
      plugin.connectionUpdates.set(edge, {original: update, wrapped: association});
      const result = original.call(this, edge, ...args);
      association.call(edge, edge.from, edge.to);
      plugin.draw(edge);
      return result;
    };
    canvas.addEdge = wrapped;
    this.cancelConnectionStart = cleanup;
    win.addEventListener('pointerup', release);
    win.addEventListener('pointercancel', release);
    win.addEventListener('blur', cleanup);
    win.addEventListener('keydown', escape);
    try { node.onConnectionPointerdown(event, direction); }
    catch (error) { cleanup(); throw error; }
  }
  releaseConnectionUpdates() {
    for (const [edge, record] of this.connectionUpdates) {
      const canvas = edge.canvas;
      if (canvas.edges.has(edge.id) && (canvas.isDragging || !canvas.nodes.has(edge.to.node.id))) continue;
      if (edge.update === record.wrapped) edge.update = record.original;
      this.connectionUpdates.delete(edge);
    }
  }
  syncControls(canvas) {
    const selected = this.selected(canvas);
    for (const [node, element] of this.controls) {
      if (!element.isConnected || (node.canvas === canvas && node !== selected)) {
        element.remove(); this.controls.delete(node);
      }
    }
    if (!selected || canvas.readonly || this.controls.has(selected) || !selected.nodeEl) return;
    const container = document.createElement('div');
    container.className = 'canvas-authoring-controls';
    const labels = {top:'上', right:'右', bottom:'下', left:'左'};
    for (const direction of Object.keys(labels)) {
      const button = document.createElement('button');
      button.className = `canvas-authoring-add canvas-authoring-${direction}`;
      button.textContent = '+';
      button.type = 'button';
      button.setAttribute('aria-label', `向${labels[direction]}添加子节点`);
      button.title = `点击向${labels[direction]}添加子节点；拖拽连接节点或在空白处新建`;
      button.addEventListener('pointerdown', event => {
        event.stopPropagation();
        this.startConnection(event, canvas, selected, direction, button);
      });
      // Canvas starts resize/drag on press; isolate both pointer and legacy mouse events.
      for (const type of ['mousedown', 'touchstart']) {
        button.addEventListener(type, event => {
          event.preventDefault();
          event.stopPropagation();
        }, {passive: false});
      }
      button.addEventListener('dblclick', event => event.stopPropagation());
      button.addEventListener('click', event => {
        event.preventDefault(); event.stopPropagation();
        if (button.connectionDragged) { button.connectionDragged = false; return; }
        this.add(canvas, selected, 'child', direction);
      });
      container.appendChild(button);
    }
    selected.nodeEl.appendChild(container);
    this.controls.set(selected, container);
  }
  draw(edge) {
    const data = edge.getData();
    if (!this.isConnection(data)) return;
    if (!edge.path || !edge.from?.node || !edge.to?.node) return;
    // Use actual card edges, not the native Bezier's offset control endpoints.
    const endpoint = end => {
      const box = end.node.getBBox();
      const x = (box.minX + box.maxX) / 2, y = (box.minY + box.maxY) / 2;
      return {left:{x:box.minX,y}, right:{x:box.maxX,y}, top:{x,y:box.minY}, bottom:{x,y:box.maxY}}[end.side];
    };
    const a = endpoint(edge.from), b = endpoint(edge.to);
    if (!a || !b) return;
    const mid = (a.x + b.x) / 2;
    const vertical = data.fromSide === 'top' || data.fromSide === 'bottom';
    const midY = (a.y + b.y) / 2;
    const elbow = vertical ? `M ${a.x} ${a.y} L ${a.x} ${midY} L ${b.x} ${midY} L ${b.x} ${b.y}` : `M ${a.x} ${a.y} L ${mid} ${a.y} L ${mid} ${b.y} L ${b.x} ${b.y}`;
    const path = elbow;
    for (const element of [edge.path.display, edge.path.interaction]) {
      if (element.getAttribute('d') !== path) element.setAttribute('d', path);
    }
  }
  onunload() {
    this.cancelConnectionStart?.();
    for (const [edge, record] of this.connectionUpdates) {
      if (edge.update === record.wrapped) edge.update = record.original;
    }
    this.connectionUpdates.clear();
    for (const [canvas, {original, wrapped}] of this.creationMenus) {
      if (canvas.showCreationMenu === wrapped) canvas.showCreationMenu = original;
    }
    this.creationMenus.clear();
    document.querySelectorAll('.canvas-authoring-style, .canvas-authoring-new-note').forEach(el => el.remove());
    document.querySelectorAll('.canvas-authoring-transparent-choice').forEach(el => el.remove());
    document.querySelectorAll('.canvas-authoring-transparent, .canvas-authoring-font-color').forEach(el => {
      el.classList.remove('canvas-authoring-transparent', 'canvas-authoring-font-color');
      el.style.removeProperty('--authoring-font-color');
    });
    for (const element of this.controls.values()) element.remove();
    this.controls.clear();
    for (const [edge, {original, wrapped}] of this.patched) {
      if (edge.updatePath === wrapped) edge.updatePath = original;
      edge.updatePath();
    }
    this.patched.clear();
  }
};
