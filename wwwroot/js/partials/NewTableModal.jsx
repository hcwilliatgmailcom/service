window.Service = window.Service || {};

(function(AR) {
  const { useState } = React;
  const Icon = AR.Icon;
  const Modal = AR.Modal;
  const OBtn = AR.OBtn;
  const api = AR.api;
  const COL_TYPES = AR.COL_TYPES;

  function NewTableModal({ open, onClose, onDone }) {
    const [name, setName] = useState('');
    const [busy, setBusy] = useState(false);
    const [err, setErr] = useState('');
    const submit = async () => {
      setErr(''); if (!name.trim()) { setErr('Tabellenname erforderlich'); return; }
      setBusy(true);
      try {
        await api('/_schema/tables', { method: 'POST', body: JSON.stringify({
          name: name.trim(),
          columns: [
            { name: 'id', type: 'int', length: 0, pk: true, not_null: false },
            { name: 'name', type: 'varchar', length: 100, pk: false, not_null: true }
          ]
        }) });
        setName('');
        onDone(name.trim());
      } catch (e) { setErr(e.message); } finally { setBusy(false); }
    };
    return <Modal open={open} onClose={onClose} title="Neue Tabelle anlegen">
      <div className="space-y-4">
        <div>
          <label className="block text-xs font-semibold text-stone-600 mb-1">Tabellenname</label>
          <input value={name} onChange={e => setName(e.target.value)} onKeyDown={e => e.key === 'Enter' && submit()} placeholder="z.B. invoices" className="w-full px-3 py-2 border border-stone-300 rounded-lg text-sm focus:border-orange-500 focus:ring-2 focus:ring-orange-200 outline-none font-mono" autoFocus/>
          <p className="text-xs text-stone-400 mt-1.5">Spalten <span className="font-mono">id</span> (PK, auto) und <span className="font-mono">name</span> (varchar) werden automatisch angelegt.</p>
        </div>
        {err && <div className="text-red-600 text-sm">{err}</div>}
        <div className="flex justify-end gap-2 pt-2 border-t border-stone-200">
          <button onClick={onClose} className="px-4 py-2 text-sm rounded-lg border border-stone-300 hover:bg-stone-50">Abbrechen</button>
          <OBtn onClick={submit} disabled={busy}><Icon name="plus" size={15}/>{busy ? 'Anlegen...' : 'Tabelle anlegen'}</OBtn>
        </div>
      </div>
    </Modal>;
  }

  AR.NewTableModal = NewTableModal;
})(window.Service);
