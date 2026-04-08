window.Service = window.Service || {};

(function(AR) {
  const { useState, useEffect } = React;
  const Icon = AR.Icon;
  const Modal = AR.Modal;
  const OBtn = AR.OBtn;
  const OBtnOutline = AR.OBtnOutline;
  const api = AR.api;

  function ImportModal({ open, onClose, localTables, onDone }) {
    const [step, setStep] = useState(1);
    const [drivers, setDrivers] = useState([]);
    const [dbType, setDbType] = useState(''); const [host, setHost] = useState(''); const [port, setPort] = useState('');
    const [database, setDatabase] = useState(''); const [dbUser, setDbUser] = useState(''); const [dbPass, setDbPass] = useState('');
    const [testRes, setTestRes] = useState(null); const [testing, setTesting] = useState(false);
    const [srcTables, setSrcTables] = useState([]); const [srcTable, setSrcTable] = useState('');
    const [srcCols, setSrcCols] = useState([]); const [loadingCols, setLoadingCols] = useState(false);
    const [dstTable, setDstTable] = useState(''); const [mapping, setMapping] = useState([]);
    const [syncMode, setSyncMode] = useState('merge');
    const [preview, setPreview] = useState(null); const [loadingPrev, setLoadingPrev] = useState(false);
    const [syncing, setSyncing] = useState(false); const [syncRes, setSyncRes] = useState(null);

    useEffect(() => {
      if (!open) return;
      api('/_import/drivers').then(d => { setDrivers(d.drivers || []); if (d.drivers.length > 0 && !dbType) setDbType(d.drivers[0].driver); }).catch(() => {});
      setStep(1); setTestRes(null); setSrcTables([]); setSrcTable(''); setSrcCols([]);
      setDstTable(''); setMapping([]); setPreview(null); setSyncRes(null);
    }, [open]);

    const conn = () => ({ type: dbType, host, port: parseInt(port) || 0, database, user: dbUser, password: dbPass });

    const testConn = async () => {
      setTesting(true); setTestRes(null);
      try {
        const r = await api('/_import/test', { method: 'POST', body: JSON.stringify(conn()) });
        setTestRes({ ok: true, msg: 'Verbunden! Server: ' + r.version });
        const t = await api('/_import/tables', { method: 'POST', body: JSON.stringify(conn()) });
        setSrcTables(t.tables || []); setStep(2);
      } catch (e) { setTestRes({ ok: false, msg: e.message }); } finally { setTesting(false); }
    };

    const pickSrc = async (tbl) => {
      setSrcTable(tbl); setSrcCols([]); setMapping([]); setPreview(null);
      if (!tbl) return;
      setLoadingCols(true);
      try { const r = await api('/_import/columns', { method: 'POST', body: JSON.stringify({ ...conn(), table: tbl }) }); setSrcCols(r.columns || []); } catch (e) {} finally { setLoadingCols(false); }
    };

    const buildMapping = (sCols, tbl) => {
      if (!tbl || !localTables[tbl]) return [];
      const lCols = localTables[tbl].columns.map(c => c.name);
      return sCols.map(sc => {
        const m = lCols.find(lc => lc.toLowerCase() === sc.name.toLowerCase());
        return { src: sc.name, srcType: sc.type, dst: m || '' };
      });
    };

    const pickDst = (tbl) => { setDstTable(tbl); setMapping(buildMapping(srcCols, tbl)); };
    useEffect(() => { if (srcCols.length > 0 && dstTable) setMapping(buildMapping(srcCols, dstTable)); }, [srcCols]);

    const setMap = (i, dst) => { const m = [...mapping]; m[i] = { ...m[i], dst }; setMapping(m); };

    const doPreview = async () => {
      setLoadingPrev(true); setPreview(null);
      try { const r = await api('/_import/preview', { method: 'POST', body: JSON.stringify({ ...conn(), table: srcTable, limit: 10 }) }); setPreview(r); } catch (e) { setPreview({ error: e.message }); } finally { setLoadingPrev(false); }
    };

    const doSync = async () => {
      setSyncing(true); setSyncRes(null);
      try {
        const activeMap = mapping.filter(m => m.src && m.dst);
        const r = await api('/_import/sync', { method: 'POST', body: JSON.stringify({ ...conn(), source_table: srcTable, target_table: dstTable, mapping: activeMap, mode: syncMode }) });
        setSyncRes(r); if (!r.error && onDone) onDone();
      } catch (e) { setSyncRes({ error: e.message }); } finally { setSyncing(false); }
    };

    const dstCols = dstTable && localTables[dstTable] ? localTables[dstTable].columns.map(c => c.name) : [];
    const driverInfo = drivers.find(d => d.driver === dbType);

    return <Modal open={open} onClose={onClose} title="Daten importieren / synchronisieren" wide>
      <div className="space-y-5">
        <div className="flex items-center gap-2 text-xs">
          {['Verbindung','Quelle','Mapping & Sync'].map((l, i) => <React.Fragment key={i}>
            {i > 0 && <div className="w-6 h-px bg-stone-300"/>}
            <span className={`px-2 py-1 rounded-full font-semibold ${step > i + 1 ? 'bg-orange-100 text-orange-700' : step === i + 1 ? 'bg-orange-500 text-white' : 'bg-stone-100 text-stone-400'}`}>{i + 1}. {l}</span>
          </React.Fragment>)}
        </div>

        <div className={`p-4 rounded-xl border ${step === 1 ? 'border-orange-300 bg-orange-50/30' : 'border-stone-200 bg-stone-50'}`}>
          <div className="flex items-center justify-between mb-3">
            <div className="font-semibold text-sm flex items-center gap-2"><Icon name="plug" size={15} className="text-orange-500"/>Verbindung</div>
            {testRes && <span className={`text-xs px-2 py-0.5 rounded-full font-semibold ${testRes.ok ? 'bg-green-100 text-green-700' : 'bg-red-100 text-red-700'}`}>{testRes.ok ? 'Verbunden' : 'Fehler'}</span>}
          </div>
          {step === 1 && <>
          <div className="grid grid-cols-2 gap-3">
            <div className="col-span-2"><label className="block text-xs font-semibold text-stone-600 mb-1">Datenbank-Typ</label>
              <select value={dbType} onChange={e => { setDbType(e.target.value); const di = drivers.find(d => d.driver === e.target.value); if (di) setPort(String(di.defaultPort)); }} className="w-full px-3 py-2 border border-stone-300 rounded-lg text-sm bg-white outline-none focus:border-orange-500">
                {drivers.map(d => <option key={d.driver} value={d.driver}>{d.label}</option>)}
                {drivers.length === 0 && <option value="">Keine Treiber verfuegbar</option>}
              </select>
            </div>
            <div><label className="block text-xs font-semibold text-stone-600 mb-1">Host</label><input value={host} onChange={e => setHost(e.target.value)} placeholder="z.B. db.example.com" className="w-full px-3 py-2 border border-stone-300 rounded-lg text-sm outline-none focus:border-orange-500 font-mono"/></div>
            <div><label className="block text-xs font-semibold text-stone-600 mb-1">Port</label><input value={port} onChange={e => setPort(e.target.value)} placeholder={driverInfo ? String(driverInfo.defaultPort) : ''} className="w-full px-3 py-2 border border-stone-300 rounded-lg text-sm outline-none focus:border-orange-500 font-mono"/></div>
            <div className="col-span-2"><label className="block text-xs font-semibold text-stone-600 mb-1">Service Name / SID</label><input value={database} onChange={e => setDatabase(e.target.value)} placeholder="z.B. FREEPDB1" className="w-full px-3 py-2 border border-stone-300 rounded-lg text-sm outline-none focus:border-orange-500 font-mono"/></div>
            <div><label className="block text-xs font-semibold text-stone-600 mb-1">Benutzer</label><input value={dbUser} onChange={e => setDbUser(e.target.value)} className="w-full px-3 py-2 border border-stone-300 rounded-lg text-sm outline-none focus:border-orange-500 font-mono"/></div>
            <div><label className="block text-xs font-semibold text-stone-600 mb-1">Passwort</label><input type="password" value={dbPass} onChange={e => setDbPass(e.target.value)} className="w-full px-3 py-2 border border-stone-300 rounded-lg text-sm outline-none focus:border-orange-500 font-mono"/></div>
          </div>
          {testRes && !testRes.ok && <div className="mt-2 text-red-600 text-xs bg-red-50 rounded-lg p-2">{testRes.msg}</div>}
          <div className="mt-3 flex justify-end"><OBtn onClick={testConn} disabled={testing || !host || !database}><Icon name="zap" size={14}/>{testing ? 'Teste...' : 'Verbindung testen'}</OBtn></div>
          </>}
          {step > 1 && <button onClick={() => setStep(1)} className="text-xs text-orange-600 hover:underline mt-1">Verbindung aendern</button>}
        </div>

        {step >= 2 && <div className={`p-4 rounded-xl border ${step === 2 ? 'border-orange-300 bg-orange-50/30' : 'border-stone-200 bg-stone-50'}`}>
          <div className="font-semibold text-sm mb-3 flex items-center gap-2"><Icon name="table-2" size={15} className="text-orange-500"/>Quelltabelle ({srcTables.length} Tabellen)</div>
          <select value={srcTable} onChange={e => { pickSrc(e.target.value); if (e.target.value) setStep(3); }} className="w-full px-3 py-2 border border-stone-300 rounded-lg text-sm bg-white outline-none focus:border-orange-500">
            <option value="">-- Tabelle waehlen --</option>
            {srcTables.map(t => <option key={t.name} value={t.name}>{t.name}</option>)}
          </select>
          {loadingCols && <div className="mt-2 text-xs text-stone-500 flex items-center gap-2"><div className="w-3 h-3 border-2 border-orange-400 border-t-transparent rounded-full spinner"/>Lade Spalten...</div>}
          {srcCols.length > 0 && <div className="mt-2 flex flex-wrap gap-1">{srcCols.map(c => <span key={c.name} className="px-2 py-0.5 rounded bg-stone-100 text-xs font-mono">{c.name} <span className="text-stone-400">{c.type}</span></span>)}</div>}
          {srcTable && <div className="mt-2 flex justify-end"><OBtnOutline small onClick={doPreview} disabled={loadingPrev}><Icon name="eye" size={13}/>{loadingPrev ? '...' : 'Vorschau'}</OBtnOutline></div>}
          {preview && !preview.error && <div className="mt-2 max-h-40 overflow-auto border border-stone-200 rounded-lg">
            <table className="w-full text-xs"><thead><tr className="bg-stone-50 sticky top-0">{preview.rows.length > 0 && Object.keys(preview.rows[0]).map(k => <th key={k} className="px-2 py-1 text-left font-mono text-stone-600 border-b border-stone-200 whitespace-nowrap">{k}</th>)}</tr></thead>
            <tbody>{preview.rows.map((r, i) => <tr key={i} className="border-b border-stone-100 hover:bg-stone-50">{Object.values(r).map((v, j) => <td key={j} className="px-2 py-1 truncate max-w-[120px] whitespace-nowrap">{v === null ? <span className="text-stone-300 italic">NULL</span> : String(v)}</td>)}</tr>)}</tbody></table>
          </div>}
          {preview && preview.error && <div className="mt-2 text-red-600 text-xs">{preview.error}</div>}
        </div>}

        {step >= 3 && srcCols.length > 0 && <div className="p-4 rounded-xl border border-orange-300 bg-orange-50/30">
          <div className="font-semibold text-sm mb-3 flex items-center gap-2"><Icon name="git-merge" size={15} className="text-orange-500"/>Ziel & Spalten-Mapping</div>
          <div className="grid grid-cols-2 gap-3 mb-3">
            <div><label className="block text-xs font-semibold text-stone-600 mb-1">Zieltabelle (lokal)</label>
              <select value={dstTable} onChange={e => pickDst(e.target.value)} className="w-full px-3 py-2 border border-stone-300 rounded-lg text-sm bg-white outline-none focus:border-orange-500">
                <option value="">-- Tabelle waehlen --</option>
                {localTables && Object.keys(localTables).filter(k => localTables[k].type === 'TABLE').map(k => <option key={k} value={k}>{k}</option>)}
              </select>
            </div>
            <div><label className="block text-xs font-semibold text-stone-600 mb-1">Sync-Modus</label>
              <select value={syncMode} onChange={e => setSyncMode(e.target.value)} className="w-full px-3 py-2 border border-stone-300 rounded-lg text-sm bg-white outline-none focus:border-orange-500">
                <option value="merge">Merge (UPSERT)</option>
                <option value="insert">Nur neue (INSERT)</option>
                <option value="replace">Ersetzen (DELETE + INSERT)</option>
              </select>
            </div>
          </div>
          <div className="text-[10px] text-stone-500 mb-2 px-1">
            {syncMode === 'merge' && 'Neue Zeilen einfuegen, bestehende aktualisieren (MERGE)'}
            {syncMode === 'insert' && 'Nur neue Zeilen einfuegen'}
            {syncMode === 'replace' && 'Alle Zeilen in Zieltabelle loeschen, dann neu einfuegen'}
          </div>
          {mapping.length > 0 && <div className="border border-stone-200 rounded-lg overflow-hidden mb-3">
            <table className="w-full text-xs"><thead><tr className="bg-orange-50">
              <th className="px-3 py-2 text-left font-semibold text-orange-700">Quelle</th>
              <th className="px-3 py-2 text-left text-stone-400 text-[10px]">Typ</th>
              <th className="px-3 py-2 text-center text-orange-400 w-8"></th>
              <th className="px-3 py-2 text-left font-semibold text-orange-700">Ziel</th>
            </tr></thead><tbody>
              {mapping.map((m, i) => <tr key={i} className="border-t border-stone-100 hover:bg-orange-50/50">
                <td className="px-3 py-1.5 font-mono font-medium">{m.src}</td>
                <td className="px-3 py-1.5 text-stone-400">{m.srcType}</td>
                <td className="px-3 py-1.5 text-center"><Icon name="arrow-right" size={12} className="text-orange-400 mx-auto"/></td>
                <td className="px-3 py-1.5"><select value={m.dst} onChange={e => setMap(i, e.target.value)} className="w-full px-2 py-1 border border-stone-200 rounded text-xs bg-white outline-none focus:border-orange-500">
                  <option value="">-- ignorieren --</option>
                  {dstCols.map(c => <option key={c} value={c}>{c}</option>)}
                </select></td>
              </tr>)}
            </tbody></table>
          </div>}

          {syncRes && !syncRes.error && <div className="p-3 rounded-lg bg-green-50 border border-green-200 text-sm mb-3">
            <div className="font-semibold text-green-800 mb-1 flex items-center gap-2"><Icon name="check-circle" size={16} className="text-green-600"/>Sync abgeschlossen</div>
            <div className="text-xs text-green-700 grid grid-cols-2 gap-1">
              <div>Quellzeilen: <strong>{syncRes.source_rows}</strong></div>
              <div>Eingefuegt: <strong>{syncRes.inserted}</strong></div>
              <div>Aktualisiert: <strong>{syncRes.updated}</strong></div>
              <div>Unveraendert: <strong>{syncRes.unchanged}</strong></div>
            </div>
            {syncRes.errors && syncRes.errors.length > 0 && <div className="text-red-600 text-xs mt-2 p-2 bg-red-50 rounded"><strong>{syncRes.errors.length} Fehler:</strong><br/>{syncRes.errors.slice(0, 5).join('\n')}</div>}
          </div>}
          {syncRes && syncRes.error && <div className="p-3 rounded-lg bg-red-50 border border-red-200 text-sm text-red-700 mb-3">{syncRes.error}</div>}

          <div className="flex justify-end gap-2">
            <OBtn onClick={doSync} disabled={syncing || !dstTable || mapping.filter(m => m.dst).length === 0}>
              <Icon name="download" size={15}/>{syncing ? 'Synchronisiere...' : 'Sync starten'}
            </OBtn>
          </div>
        </div>}

        <div className="flex justify-end pt-2 border-t border-stone-200">
          <button onClick={onClose} className="px-4 py-2 text-sm rounded-lg border border-stone-300 hover:bg-stone-50">Schliessen</button>
        </div>
      </div>
    </Modal>;
  }

  AR.ImportModal = ImportModal;
})(window.Service);
