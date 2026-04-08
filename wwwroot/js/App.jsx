window.Service = window.Service || {};

(function(AR) {
  const { useState, useEffect, useCallback } = React;
  const Icon = AR.Icon;
  const OBtnOutline = AR.OBtnOutline;
  const FkAutocomplete = AR.FkAutocomplete;
  const Sidebar = AR.Sidebar;
  const ListView = AR.ListView;
  const DetailView = AR.DetailView;
  const SchemaView = AR.SchemaView;
  const NewTableModal = AR.NewTableModal;
  const AddColumnModal = AR.AddColumnModal;
  const EditColumnModal = AR.EditColumnModal;
  const AddFkModal = AR.AddFkModal;
  const ImportModal = AR.ImportModal;
  const api = AR.api;
  const loadResources = AR.loadResources;

  function App() {
    const [resources, setResources] = useState(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);
    const [activeKey, setActiveKey] = useState(null);
    const [selectedRowId, setSelectedRowId] = useState(null);
    const [search, setSearch] = useState('');
    const [editing, setEditing] = useState(false);
    const [draft, setDraft] = useState(null);
    const [creating, setCreating] = useState(false);
    const [toast, setToast] = useState(null);
    const [saving, setSaving] = useState(false);
    const [showNewTable, setShowNewTable] = useState(false);
    const [showAddCol, setShowAddCol] = useState(false);
    const [showEditCol, setShowEditCol] = useState(null);
    const [showAddFk, setShowAddFk] = useState(false);
    const [showImport, setShowImport] = useState(false);
    const [schemaMode, setSchemaMode] = useState(false);

    const reload = useCallback(async () => {
      setLoading(true); setError(null);
      try {
        const res = await loadResources(); setResources(res);
        const keys = Object.keys(res);
        if (!activeKey || !res[activeKey]) { setActiveKey(keys[0] || null); const f = res[keys[0]]; if (f && f.rows.length > 0) setSelectedRowId(f.rows[0][f.pk]); }
      } catch (e) { setError(e.message); } finally { setLoading(false); }
    }, []);
    useEffect(() => { reload(); }, [reload]);

    const flash = (msg, kind = 'ok') => { setToast({ msg, kind }); setTimeout(() => setToast(null), 3000); };

    if (loading && !resources) return <div className="h-full flex items-center justify-center bg-stone-50"><div className="text-center"><div className="w-10 h-10 border-4 border-amber-500 border-t-transparent rounded-full spinner mx-auto mb-4"/><div className="text-stone-600">Lade Schema...</div></div></div>;
    if (error && !resources) return <div className="h-full flex items-center justify-center bg-stone-50"><div className="text-center max-w-md"><div className="text-red-500 text-lg font-semibold mb-2">Fehler</div><div className="text-stone-600 mb-4">{error}</div><button onClick={reload} className="px-4 py-2 bg-amber-500 text-white rounded-lg">Erneut</button></div></div>;
    if (!resources || !activeKey) return null;

    const active = resources[activeKey];
    const filtered = search ? active.rows.filter(r => Object.values(r).some(v => String(v).toLowerCase().includes(search.toLowerCase()))) : active.rows;
    const selectedRow = active.rows.find(r => r[active.pk] === selectedRowId) || filtered[0] || null;
    const getFk = cn => active.foreignKeys.find(f => f.column === cn);
    const getFkLabel = (cn, v) => { const o = active.fkOptions[cn]; if (!o) return null; const m = o.find(x => String(x.value) === String(v)); return m ? m.label : null; };
    const navigateToFk = (fk, v) => { const rt = fk.ref_table; if (!resources[rt]) return; setActiveKey(rt); setSearch(''); setEditing(false); setCreating(false); setDraft(null); setSchemaMode(false); const rr = resources[rt]; const row = rr.rows.find(r => String(r[rr.pk]) === String(v)); setSelectedRowId(row ? row[rr.pk] : (rr.rows[0]?.[rr.pk] ?? null)); };

    const handleSelectResource = k => { setActiveKey(k); setSearch(''); setEditing(false); setCreating(false); setDraft(null); setSelectedRowId(resources[k].rows[0]?.[resources[k].pk] ?? null); };
    const handleEdit = () => { if (active.isView) return; setEditing(true); setCreating(false); setDraft({ ...selectedRow }); };
    const handleNew = () => { if (active.isView) return; const e = {}; active.columns.forEach(c => { e[c.name] = ''; }); setDraft(e); setCreating(true); setEditing(true); };
    const handleSave = async () => {
      setSaving(true);
      try {
        if (creating) { const b = {}; active.columns.forEach(c => { if (!c.readonly && draft[c.name] !== '') b[c.name] = draft[c.name]; }); await api(`/${activeKey}/`, { method: 'POST', body: JSON.stringify(b) }); flash(`POST /${activeKey}/ -> 201`); }
        else { const b = {}; active.columns.forEach(c => { if (!c.readonly && !c.isPk) b[c.name] = draft[c.name]; }); await api(`/${activeKey}/${selectedRow[active.pk]}`, { method: 'PATCH', body: JSON.stringify(b) }); flash(`PATCH -> 200`); }
        setEditing(false); setCreating(false); setDraft(null); await reload();
      } catch (e) { flash(`Fehler: ${e.message}`, 'error'); } finally { setSaving(false); }
    };
    const handleDelete = async () => { if (active.isView || !selectedRow) return; if (!confirm(`Datensatz #${selectedRow[active.pk]} loeschen?`)) return; try { await api(`/${activeKey}/${selectedRow[active.pk]}`, { method: 'DELETE' }); flash('Geloescht'); await reload(); } catch (e) { flash(e.message, 'error'); } };
    const handleCancel = () => { setEditing(false); setCreating(false); setDraft(null); };

    const handleDropTable = async (key) => {
      const k = key || activeKey;
      if (!confirm(`Tabelle "${k}" wirklich LOESCHEN? Alle Daten gehen verloren!`)) return;
      try { await api(`/_schema/tables/${k}`, { method: 'DELETE' }); flash(`Tabelle ${k} geloescht`); setActiveKey(null); await reload(); } catch (e) { flash(e.message, 'error'); }
    };
    const handleDropColumn = async (colName) => {
      if (!confirm(`Spalte "${colName}" wirklich entfernen?`)) return;
      try { await api(`/_schema/${activeKey}/columns/${colName}`, { method: 'DELETE' }); flash(`Spalte ${colName} entfernt`); await reload(); } catch (e) { flash(e.message, 'error'); }
    };
    const handleDropFk = async (col) => {
      if (!confirm(`FK auf "${col}" entfernen?`)) return;
      try { await api(`/_schema/${activeKey}/fk/${col}`, { method: 'DELETE' }); flash('FK entfernt'); await reload(); } catch (e) { flash(e.message, 'error'); }
    };

    const totalRows = Object.values(resources).reduce((s, r) => s + r.rows.length, 0);
    const pickPrimary = (row) => { const c = active.columns.map(c => c.name.toLowerCase()); const get = k => row[Object.keys(row).find(r => r.toLowerCase() === k)]; if (c.includes('first_name') && c.includes('last_name')) return `${get('first_name')} ${get('last_name')}`; if (c.includes('name')) return get('name'); if (c.includes('customer')) return get('customer'); return `#${row[active.pk]}`; };
    const pickSecondary = (row) => { const c = active.columns.map(c => c.name.toLowerCase()); const get = k => row[Object.keys(row).find(r => r.toLowerCase() === k)]; if (c.includes('email')) return get('email'); if (c.includes('status') && c.includes('amount')) { const l = getFkLabel('customer_id', get('customer_id')); return `${l ? l + ' · ' : ''}${get('status')} · EUR ${get('amount')}`; } if (c.includes('price')) return `EUR ${get('price')} · Stock: ${get('stock')}`; if (c.includes('total')) return `${get('orders')} Bestellungen · EUR ${get('total')}`; return ''; };
    const avatarColor = s => { const p = ['#d97706','#dc2626','#0f766e','#1d4ed8','#7c3aed','#db2777','#65a30d','#0891b2']; const n = typeof s === 'number' ? s : String(s).charCodeAt(0); return p[n % p.length]; };
    const generateCurl = () => { const b = location.origin + AR.API_BASE; const u = `${b}/${activeKey}/${creating ? '' : (selectedRow ? selectedRow[active.pk] : '')}`; if (creating && draft) { const body = {}; active.columns.filter(c => !c.readonly).forEach(c => { body[c.name] = draft[c.name] || ''; }); return `curl -X POST "${u}" \\\n  -H "Content-Type: application/json" \\\n  -u ${AR.API_USER}:*** \\\n  -d '${JSON.stringify(body, null, 2)}'`; } if (editing && draft) { const body = {}; active.columns.filter(c => !c.readonly && !c.isPk).forEach(c => { body[c.name] = draft[c.name] || ''; }); return `curl -X PATCH "${u}" \\\n  -H "Content-Type: application/json" \\\n  -u ${AR.API_USER}:*** \\\n  -d '${JSON.stringify(body, null, 2)}'`; } return `curl -X GET "${u}" \\\n  -H "Accept: application/json" \\\n  -u ${AR.API_USER}:***`; };

    const renderFkCell = (col, value, isEditing) => {
      const fk = getFk(col.name); if (!fk) return null;
      if (isEditing) return <FkAutocomplete options={active.fkOptions[col.name] || []} value={draft?.[col.name] ?? ''} onChange={v => setDraft({ ...draft, [col.name]: v })} placeholder={`${fk.ref_table} suchen...`}/>;
      const label = getFkLabel(col.name, value);
      return <div className="flex items-center gap-2"><span className="text-stone-800">{value == null ? '' : String(value)}</span>
        {label && <button onClick={() => navigateToFk(fk, value)} className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full bg-blue-50 hover:bg-blue-100 text-blue-700 text-xs font-medium transition" title={`-> ${fk.ref_table} #${value}`}><Icon name="external-link" size={11}/>{label}</button>}
        {!label && value != null && <button onClick={() => navigateToFk(fk, value)} className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full bg-stone-100 hover:bg-stone-200 text-stone-600 text-xs"><Icon name="external-link" size={11}/>{fk.ref_table}</button>}
      </div>;
    };

    return (
      <div className="h-screen w-full flex flex-col bg-stone-50 text-stone-800">
        {/* -- Topbar -- */}
        <header className="flex items-center gap-4 px-5 h-14 border-b border-stone-200 bg-white shrink-0">
          <div className="flex items-center gap-2">
            <div className="w-9 h-9 rounded-full bg-gradient-to-br from-red-500 via-orange-500 to-amber-400 grid place-items-center shadow-sm"><Icon name="database" size={20} className="text-white" strokeWidth={2.4}/></div>
            <div className="leading-tight"><div className="font-semibold text-stone-900">Service Explorer</div><div className="text-[11px] text-stone-500 font-mono">Oracle &middot; .NET Core MVC</div></div>
          </div>
          <div className="flex-1 max-w-2xl mx-auto">
            <div className="flex items-center gap-2 bg-stone-100 hover:bg-stone-200/70 focus-within:bg-white focus-within:ring-2 focus-within:ring-amber-400/50 rounded-full px-4 h-10 border border-transparent focus-within:border-stone-200">
              <Icon name="search" size={16} className="text-stone-500"/>
              <input value={search} onChange={e => setSearch(e.target.value)} placeholder={`In ${active.label} suchen...`} className="flex-1 bg-transparent outline-none text-sm"/>
              {search && <button onClick={() => setSearch('')}><Icon name="x" size={16} className="text-stone-500 hover:text-stone-800"/></button>}
            </div>
          </div>
          <div className="flex items-center gap-3">
            <OBtnOutline small onClick={() => setSchemaMode(m => !m)} title="Datenmodell bearbeiten">
              <Icon name="wrench" size={13}/>{schemaMode ? 'Daten' : 'Schema'}
            </OBtnOutline>
            <span className="text-xs text-stone-500">{totalRows} Datensaetze</span>
            <button onClick={reload} className="p-1.5 rounded-full hover:bg-stone-100"><Icon name="refresh-cw" size={16} className={`text-stone-500 ${loading ? 'spinner' : ''}`}/></button>
          </div>
        </header>

        <div className="flex flex-1 min-h-0">
          <Sidebar
            resources={resources} activeKey={activeKey} schemaMode={schemaMode} active={active}
            onSelectResource={handleSelectResource} onNewRecord={handleNew}
            onNewTable={() => setShowNewTable(true)}
            onDropTable={handleDropTable} onDropFk={handleDropFk}
          />

          <ListView
            active={active} filtered={filtered} selectedRow={selectedRow}
            onSelectRow={id => { setSelectedRowId(id); setEditing(false); setCreating(false); }}
            onRefresh={() => { reload(); flash(`GET /${activeKey}/ -> 200`); }}
            pickPrimary={pickPrimary} pickSecondary={pickSecondary} avatarColor={avatarColor}
          />

          {schemaMode ? (
            <SchemaView
              active={active} getFk={getFk}
              onShowAddCol={() => setShowAddCol(true)}
              onShowAddFk={() => setShowAddFk(true)}
              onShowImport={() => setShowImport(true)}
              onDropTable={() => handleDropTable()}
              onEditCol={col => setShowEditCol(col)}
              onDropColumn={handleDropColumn}
            />
          ) : (
            <DetailView
              active={active} activeKey={activeKey} selectedRow={selectedRow}
              creating={creating} editing={editing} draft={draft} saving={saving}
              onEdit={handleEdit} onNew={handleNew} onDelete={handleDelete}
              onCancel={handleCancel} onSave={handleSave} setDraft={setDraft}
              getFk={getFk} getFkLabel={getFkLabel} navigateToFk={navigateToFk}
              renderFkCell={renderFkCell} generateCurl={generateCurl} pickPrimary={pickPrimary}
            />
          )}
        </div>

        {/* -- Toast -- */}
        {toast && <div className={`fixed bottom-6 left-1/2 -translate-x-1/2 px-5 py-3 rounded-full text-white text-sm shadow-2xl font-mono flex items-center gap-2 z-50 ${toast.kind === 'error' ? 'bg-red-600' : 'bg-stone-900'}`}><span className={`w-2 h-2 rounded-full animate-pulse ${toast.kind === 'error' ? 'bg-red-300' : 'bg-emerald-400'}`}/>{toast.msg}</div>}

        {/* ====== MODALS ====== */}
        <NewTableModal open={showNewTable} onClose={() => setShowNewTable(false)} onDone={async (name) => { flash(`Tabelle "${name}" angelegt`); setShowNewTable(false); await reload(); setActiveKey(name); setSchemaMode(true); }}/>
        <AddColumnModal open={showAddCol} onClose={() => setShowAddCol(false)} table={activeKey} onDone={async () => { flash('Spalte hinzugefuegt'); setShowAddCol(false); await reload(); }}/>
        <EditColumnModal open={!!showEditCol} col={showEditCol} onClose={() => setShowEditCol(null)} table={activeKey} onDone={async () => { flash('Spalte geaendert'); setShowEditCol(null); await reload(); }}/>
        <AddFkModal open={showAddFk} onClose={() => setShowAddFk(false)} table={activeKey} columns={active.columns} allTables={resources} onDone={async () => { flash('FK angelegt'); setShowAddFk(false); await reload(); }}/>
        <ImportModal open={showImport} onClose={() => setShowImport(false)} localTables={resources} onDone={async () => { flash('Import abgeschlossen'); await reload(); }}/>
      </div>
    );
  }

  AR.App = App;
})(window.Service);
