window.Service = window.Service || {};

(function(AR) {
  const { useState } = React;
  const Icon = AR.Icon;
  const Modal = AR.Modal;
  const OBtn = AR.OBtn;
  const api = AR.api;
  const COL_TYPES = AR.COL_TYPES;

  function AddColumnModal({ open, onClose, table, onDone }) {
    const [name, setName] = useState('');
    const [type, setType] = useState('varchar');
    const [length, setLength] = useState('255');
    const [notNull, setNotNull] = useState(false);
    const [def, setDef] = useState('');
    const [busy, setBusy] = useState(false);
    const [err, setErr] = useState('');
    const submit = async () => {
      setErr(''); if (!name.trim()) { setErr('Name erforderlich'); return; }
      setBusy(true);
      try {
        await api(`/_schema/${table}/columns`, { method: 'POST', body: JSON.stringify({ name: name.trim(), type, length: parseInt(length) || 0, not_null: notNull, default: def }) });
        setName(''); setType('varchar'); setLength('255'); setNotNull(false); setDef(''); onDone();
      } catch (e) { setErr(e.message); } finally { setBusy(false); }
    };
    return <Modal open={open} onClose={onClose} title={`Spalte zu "${table}" hinzufuegen`}>
      <div className="space-y-3">
        <div><label className="block text-xs font-semibold text-stone-600 mb-1">Name</label><input value={name} onChange={e => setName(e.target.value)} placeholder="z.B. phone" className="w-full px-3 py-2 border border-stone-300 rounded-lg text-sm font-mono outline-none focus:border-orange-500 focus:ring-2 focus:ring-orange-200"/></div>
        <div className="flex gap-3">
          <div className="flex-1"><label className="block text-xs font-semibold text-stone-600 mb-1">Typ</label><select value={type} onChange={e => setType(e.target.value)} className="w-full px-3 py-2 border border-stone-300 rounded-lg text-sm bg-white outline-none focus:border-orange-500">{COL_TYPES.map(t => <option key={t}>{t}</option>)}</select></div>
          {(type === 'varchar' || type === 'char') && <div className="w-24"><label className="block text-xs font-semibold text-stone-600 mb-1">Laenge</label><input value={length} onChange={e => setLength(e.target.value)} className="w-full px-3 py-2 border border-stone-300 rounded-lg text-sm font-mono text-center outline-none focus:border-orange-500"/></div>}
        </div>
        <div><label className="block text-xs font-semibold text-stone-600 mb-1">Default</label><input value={def} onChange={e => setDef(e.target.value)} placeholder="optional" className="w-full px-3 py-2 border border-stone-300 rounded-lg text-sm outline-none focus:border-orange-500 focus:ring-2 focus:ring-orange-200"/></div>
        <label className="flex items-center gap-2 text-sm cursor-pointer"><input type="checkbox" checked={notNull} onChange={e => setNotNull(e.target.checked)} className="accent-orange-500"/>NOT NULL</label>
        {err && <div className="text-red-600 text-sm">{err}</div>}
        <div className="flex justify-end gap-2 pt-2 border-t border-stone-200">
          <button onClick={onClose} className="px-4 py-2 text-sm rounded-lg border border-stone-300 hover:bg-stone-50">Abbrechen</button>
          <OBtn onClick={submit} disabled={busy}><Icon name="plus" size={15}/>{busy ? '...' : 'Hinzufuegen'}</OBtn>
        </div>
      </div>
    </Modal>;
  }

  AR.AddColumnModal = AddColumnModal;
})(window.Service);
