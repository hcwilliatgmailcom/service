window.Service = window.Service || {};

(function(AR) {
  const Icon = AR.Icon;
  const OBtn = AR.OBtn;

  function SchemaView({ active, getFk, onShowAddCol, onShowAddFk, onShowImport, onDropTable, onEditCol, onDropColumn }) {
    return (
      <section className="flex-1 min-w-0 bg-stone-50 flex flex-col">
        <div className="px-8 pt-6 pb-4 border-b border-stone-200 bg-white shrink-0">
          <div className="flex items-center justify-between">
            <div>
              <div className="text-[11px] uppercase tracking-wider text-orange-600 font-semibold flex items-center gap-1"><Icon name="wrench" size={12}/>Schema-Editor</div>
              <h1 className="text-2xl font-semibold text-stone-900 mt-1">{active.label}</h1>
              <div className="text-xs text-stone-500 mt-1">{active.columns.length} Spalten &middot; {active.type} &middot; PK: {active.pk}</div>
            </div>
            <div className="flex items-center gap-2">
              <OBtn onClick={onShowAddCol}><Icon name="plus" size={15}/>Spalte</OBtn>
              {!active.isView && <OBtn onClick={onShowAddFk}><Icon name="link" size={15}/>FK</OBtn>}
              {!active.isView && <OBtn onClick={onShowImport}><Icon name="download" size={15}/>Importieren</OBtn>}
              <OBtn danger onClick={onDropTable}><Icon name="trash-2" size={15}/>Tabelle loeschen</OBtn>
            </div>
          </div>
        </div>
        <div className="flex-1 overflow-y-auto px-8 py-6">
          <div className="max-w-3xl mx-auto bg-white rounded-2xl border border-stone-200 shadow-sm overflow-hidden">
            <table className="w-full text-sm">
              <thead><tr className="bg-orange-50 border-b border-orange-200">
                <th className="text-left px-5 py-2.5 text-[11px] uppercase tracking-wider text-orange-700 font-semibold">Spalte</th>
                <th className="text-left px-5 py-2.5 text-[11px] uppercase tracking-wider text-orange-700 font-semibold">Typ</th>
                <th className="text-left px-5 py-2.5 text-[11px] uppercase tracking-wider text-orange-700 font-semibold">Flags</th>
                <th className="text-left px-5 py-2.5 text-[11px] uppercase tracking-wider text-orange-700 font-semibold">Default</th>
                <th className="px-5 py-2.5 w-24"></th>
              </tr></thead>
              <tbody>
                {active.columns.map(col => {
                  const fk = getFk(col.name);
                  return <tr key={col.name} className="border-b border-stone-100 last:border-b-0 hover:bg-orange-50/30 group">
                    <td className="px-5 py-3 font-mono text-sm font-medium">{col.name}
                      {fk && <span className="ml-1.5 inline-flex items-center px-1.5 py-0 rounded bg-blue-100 text-blue-600 text-[9px] font-semibold">FK-&gt;{fk.ref_table}</span>}
                    </td>
                    <td className="px-5 py-3 text-stone-600">{col.type}</td>
                    <td className="px-5 py-3">
                      <div className="flex gap-1 flex-wrap">
                        {col.isPk && <span className="px-1.5 py-0 rounded bg-amber-100 text-amber-700 text-[10px] font-semibold">PK</span>}
                        {col.auto && <span className="px-1.5 py-0 rounded bg-stone-100 text-stone-600 text-[10px] font-semibold">AUTO</span>}
                        {!col.nullable && !col.isPk && <span className="px-1.5 py-0 rounded bg-red-50 text-red-600 text-[10px] font-semibold">NOT NULL</span>}
                      </div>
                    </td>
                    <td className="px-5 py-3 text-stone-500 text-xs font-mono">{col.default || <span className="text-stone-300">-</span>}</td>
                    <td className="px-5 py-3">
                      {!col.isPk && !active.isView && (
                        <div className="flex items-center gap-1 opacity-0 group-hover:opacity-100 transition">
                          <button onClick={() => onEditCol(col)} className="p-1 rounded text-orange-500 hover:bg-orange-100" title="Spalte aendern"><Icon name="pencil" size={14}/></button>
                          <button onClick={() => onDropColumn(col.name)} className="p-1 rounded text-red-400 hover:bg-red-50" title="Spalte loeschen"><Icon name="trash-2" size={14}/></button>
                        </div>
                      )}
                    </td>
                  </tr>;
                })}
              </tbody>
            </table>
          </div>
        </div>
      </section>
    );
  }

  AR.SchemaView = SchemaView;
})(window.Service);
