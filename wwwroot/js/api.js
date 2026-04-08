window.Service = window.Service || {};

(function(AR) {
  const API_BASE = '/api';
  const API_USER = 'admin';
  const API_PASS = 'secret';
  const AUTH = 'Basic ' + btoa(API_USER + ':' + API_PASS);
  const COL_TYPES = ['varchar','int','bigint','decimal','text','date','datetime','timestamp','boolean','json'];

  async function api(path, opts = {}) {
    const r = await fetch(API_BASE + path, {
      ...opts,
      headers: { Authorization: AUTH, 'Content-Type': 'application/json', ...(opts.headers || {}) }
    });
    if (r.status === 204) return null;
    const d = await r.json();
    if (!r.ok) throw new Error(d.error || `HTTP ${r.status}`);
    return d;
  }

  async function loadResources() {
    const root = await api('/');
    const res = {};
    for (const r of root.resources) {
      const [meta, list] = await Promise.all([api(`/_metadata/${r.name}`), api(`/${r.name}/?limit=500`)]);
      res[r.name] = {
        type: r.type, label: r.name, pk: meta.primary_key, isView: meta.is_view,
        columns: meta.columns.map(c => ({
          name: c.name, type: c.type, nullable: c.nullable, readonly: c.pk && c.auto,
          isPk: c.pk, auto: c.auto, default: c.default
        })),
        foreignKeys: meta.foreign_keys || [], rows: list.items || [], total: list.total || 0, fkOptions: {}
      };
    }
    for (const [t, r] of Object.entries(res)) {
      if (r.foreignKeys.length > 0) {
        const loads = r.foreignKeys.map(fk =>
          api(`/_fk/${t}/${fk.column}`).then(d => ({ col: fk.column, d })).catch(() => ({ col: fk.column, d: { items: [] } }))
        );
        for (const x of await Promise.all(loads)) r.fkOptions[x.col] = x.d.items;
      }
    }
    return res;
  }

  AR.API_BASE = API_BASE;
  AR.API_USER = API_USER;
  AR.API_PASS = API_PASS;
  AR.AUTH = AUTH;
  AR.COL_TYPES = COL_TYPES;
  AR.api = api;
  AR.loadResources = loadResources;
})(window.Service);
