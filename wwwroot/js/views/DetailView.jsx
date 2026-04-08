window.Service = window.Service || {};

(function(AR) {
  const Icon = AR.Icon;
  const FkAutocomplete = AR.FkAutocomplete;

  function DetailView({ active, activeKey, selectedRow, creating, editing, draft, saving, onEdit, onNew, onDelete, onCancel, onSave, setDraft, getFk, getFkLabel, navigateToFk, renderFkCell, generateCurl, pickPrimary }) {
    if (!selectedRow && !creating) {
      return (
        <section className="flex-1 min-w-0 bg-stone-50 flex flex-col">
          <div className="flex-1 grid place-items-center text-center text-stone-400">
            <div><Icon name="inbox" size={48} className="mx-auto mb-3" strokeWidth={1.2}/><div className="text-sm">Waehle einen Datensatz</div></div>
          </div>
        </section>
      );
    }

    return (
      <section className="flex-1 min-w-0 bg-stone-50 flex flex-col">
        <div className="px-8 pt-6 pb-4 border-b border-stone-200 bg-white shrink-0">
          <div className="flex items-center justify-between gap-4">
            <div>
              <div className="text-[11px] uppercase tracking-wider text-stone-500 font-semibold">{active.type} &middot; {active.label}</div>
              <h1 className="text-2xl font-semibold text-stone-900 mt-1">{creating ? 'Neuer Datensatz' : pickPrimary(selectedRow)}</h1>
              <div className="mt-2 flex items-center gap-2 text-xs font-mono text-stone-500">
                <span className={`inline-flex items-center px-2 py-0.5 rounded font-semibold ${creating ? 'bg-green-100 text-green-700' : editing ? 'bg-blue-100 text-blue-700' : 'bg-emerald-100 text-emerald-700'}`}>{creating ? 'POST' : editing ? 'PATCH' : 'GET'}</span>
                <span>{AR.API_BASE}/{activeKey}/{!creating && selectedRow ? selectedRow[active.pk] : ''}</span>
              </div>
            </div>
            <div className="flex items-center gap-2">
              {!editing && !active.isView && <><button onClick={onEdit} className="px-4 h-9 rounded-full bg-stone-900 text-white text-sm font-medium hover:bg-stone-700">Bearbeiten</button><button onClick={onDelete} className="w-9 h-9 grid place-items-center rounded-full hover:bg-red-50 text-red-600" title="DELETE"><Icon name="trash-2" size={16}/></button></>}
              {editing && <><button onClick={onCancel} className="px-4 h-9 rounded-full border border-stone-300 text-sm hover:bg-stone-100">Abbrechen</button><button onClick={onSave} disabled={saving} className="px-4 h-9 rounded-full bg-amber-500 text-white text-sm font-semibold hover:bg-amber-600 flex items-center gap-1.5 shadow disabled:opacity-60"><Icon name="save" size={16}/>{saving ? '...' : 'Speichern'}</button></>}
              {active.isView && <span className="text-xs text-stone-500 italic">nur lesbar (View)</span>}
            </div>
          </div>
        </div>
        <div className="flex-1 overflow-y-auto px-8 py-6">
          <div className="max-w-2xl mx-auto bg-white rounded-2xl border border-stone-200 shadow-sm overflow-hidden">
            <table className="w-full text-sm"><tbody>
              {active.columns.map(col => {
                const value = editing ? (draft?.[col.name] ?? '') : (selectedRow ? selectedRow[col.name] : '');
                const fk = getFk(col.name);
                return <tr key={col.name} className="border-b border-stone-100 last:border-b-0">
                  <td className="w-1/3 align-top px-5 py-3 bg-stone-50/50">
                    <div className="font-mono text-[11px] text-stone-500 uppercase tracking-wide flex items-center gap-1.5">{col.name}
                      {fk && <span className="inline-flex items-center px-1.5 py-0 rounded bg-blue-100 text-blue-600 text-[9px] font-semibold">FK</span>}
                    </div>
                    <div className="text-[10px] text-stone-400 mt-0.5">{col.type}{col.readonly ? ' · auto' : ''}{col.isPk ? ' · PK' : ''}{fk ? ` · -> ${fk.ref_table}` : ''}</div>
                  </td>
                  <td className="px-5 py-3">
                    {fk ? renderFkCell(col, value, editing && !col.readonly) : editing && !col.readonly ? <input value={value ?? ''} onChange={e => setDraft({ ...draft, [col.name]: e.target.value })} className="w-full px-3 py-1.5 border border-stone-300 rounded-md focus:outline-none focus:border-amber-500 focus:ring-2 focus:ring-amber-200 text-sm"/>
                    : <span className="text-stone-800">{value === '' || value == null ? <span className="text-stone-400 italic">null</span> : String(value)}</span>}
                  </td>
                </tr>;
              })}
            </tbody></table>
          </div>
          <div className="max-w-2xl mx-auto mt-6"><div className="text-[11px] uppercase tracking-wider text-stone-500 font-semibold mb-2">cURL</div><pre className="bg-stone-900 text-stone-100 text-xs font-mono rounded-xl p-4 overflow-x-auto whitespace-pre-wrap">{generateCurl()}</pre></div>
        </div>
      </section>
    );
  }

  AR.DetailView = DetailView;
})(window.Service);
