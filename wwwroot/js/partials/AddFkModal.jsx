window.Service = window.Service || {};

(function(AR) {
  const { useState } = React;
  const Icon = AR.Icon;
  const Modal = AR.Modal;
  const OBtn = AR.OBtn;
  const api = AR.api;

  function AddFkModal({ open, onClose, table, columns, allTables, onDone }) {
    const [col, setCol] = useState('');
    const [refTable, setRefTable] = useState('');
    const [refCol, setRefCol] = useState('');
    const [busy, setBusy] = useState(false);
    const [err, setErr] = useState('');
    const refTableObj = allTables && allTables[refTable];
    const refCols = refTableObj ? refTableObj.columns : [];
    const submit = async () => {
      setErr(''); if (!col || !refTable || !refCol) { setErr('Alle Felder erforderlich'); return; }
      setBusy(true);
      try {
        await api(`/_schema/${table}/fk`, { method: 'POST', body: JSON.stringify({ column: col, ref_table: refTable, ref_column: refCol }) });
        setCol(''); setRefTable(''); setRefCol(''); onDone();
      } catch (e) { setErr(e.message); } finally { setBusy(false); }
    };
    const tableOptions = allTables ? Object.keys(allTables).filter(k => allTables[k].type === 'TABLE') : [];
    return <Modal open={open} onClose={onClose} title={`Foreign Key auf "${table}"`}>
      <div className="space-y-3">
        <div><label className="block text-xs font-semibold text-stone-600 mb-1">Spalte (in {table})</label>
          <select value={col} onChange={e => setCol(e.target.value)} className="w-full px-3 py-2 border border-stone-300 rounded-lg text-sm bg-white outline-none focus:border-orange-500">
            <option value="">-- Spalte waehlen --</option>
            {(columns || []).filter(c => !c.isPk).map(c => <option key={c.name} value={c.name}>{c.name} ({c.type})</option>)}
          </select>
        </div>
        <div><label className="block text-xs font-semibold text-stone-600 mb-1">Referenz-Tabelle</label>
          <select value={refTable} onChange={e => { setRefTable(e.target.value); setRefCol(''); }} className="w-full px-3 py-2 border border-stone-300 rounded-lg text-sm bg-white outline-none focus:border-orange-500">
            <option value="">-- Tabelle waehlen --</option>
            {tableOptions.map(t => <option key={t} value={t}>{t}</option>)}
          </select>
        </div>
        {refTable && <div><label className="block text-xs font-semibold text-stone-600 mb-1">Referenz-Spalte (in {refTable})</label>
          <select value={refCol} onChange={e => setRefCol(e.target.value)} className="w-full px-3 py-2 border border-stone-300 rounded-lg text-sm bg-white outline-none focus:border-orange-500">
            <option value="">-- Spalte waehlen --</option>
            {refCols.map(c => <option key={c.name} value={c.name}>{c.name} ({c.type}){c.isPk ? ' PK' : ''}</option>)}
          </select>
        </div>}
        {err && <div className="text-red-600 text-sm">{err}</div>}
        <div className="flex justify-end gap-2 pt-2 border-t border-stone-200">
          <button onClick={onClose} className="px-4 py-2 text-sm rounded-lg border border-stone-300 hover:bg-stone-50">Abbrechen</button>
          <OBtn onClick={submit} disabled={busy}><Icon name="link" size={15}/>{busy ? '...' : 'FK anlegen'}</OBtn>
        </div>
      </div>
    </Modal>;
  }

  AR.AddFkModal = AddFkModal;
})(window.Service);
