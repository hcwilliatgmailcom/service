window.Service = window.Service || {};

(function(AR) {
  const Icon = AR.Icon;

  function ListView({ active, filtered, selectedRow, onSelectRow, onRefresh, pickPrimary, pickSecondary, avatarColor }) {
    return (
      <section className="w-[420px] shrink-0 border-r border-stone-200 bg-white flex flex-col min-h-0">
        <div className="flex items-center justify-between px-4 h-11 border-b border-stone-200 text-xs text-stone-600 shrink-0">
          <div className="flex items-center gap-3"><Icon name="filter" size={14}/><span>{filtered.length} von {active.rows.length}</span></div>
          <button className="p-1 rounded hover:bg-stone-100" onClick={onRefresh}><Icon name="refresh-cw" size={14}/></button>
        </div>
        <ul className="flex-1 overflow-y-auto">
          {filtered.length === 0 && <li className="text-center text-xs text-stone-500 py-10">Keine Treffer.</li>}
          {filtered.map(row => {
            const isSel = selectedRow && row[active.pk] === selectedRow[active.pk];
            return <li key={String(row[active.pk])} onClick={() => onSelectRow(row[active.pk])} className={`cursor-pointer flex items-start gap-3 px-4 py-3 border-b border-stone-100 transition ${isSel ? 'bg-amber-50/80 border-l-4 border-l-amber-500 pl-3' : 'hover:bg-stone-50 border-l-4 border-l-transparent pl-3'}`}>
              <div className="w-9 h-9 rounded-full grid place-items-center text-xs font-semibold shrink-0 text-white" style={{ background: avatarColor(row[active.pk]) }}>{String(pickPrimary(row)).slice(0, 2).toUpperCase()}</div>
              <div className="flex-1 min-w-0"><div className="flex items-center justify-between gap-2"><span className="font-medium text-sm text-stone-900 truncate">{pickPrimary(row)}</span><span className="text-[10px] font-mono text-stone-400 shrink-0">#{row[active.pk]}</span></div><div className="text-xs text-stone-500 truncate">{pickSecondary(row)}</div></div>
            </li>;
          })}
        </ul>
      </section>
    );
  }

  AR.ListView = ListView;
})(window.Service);
