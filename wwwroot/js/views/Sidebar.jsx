window.Service = window.Service || {};

(function(AR) {
  const Icon = AR.Icon;
  const OBtn = AR.OBtn;

  function Sidebar({ resources, activeKey, schemaMode, active, onSelectResource, onNewRecord, onNewTable, onDropTable, onDropFk }) {
    const tableKeys = Object.keys(resources).filter(k => resources[k].type === 'TABLE');
    const viewKeys = Object.keys(resources).filter(k => resources[k].type === 'VIEW');

    return (
      <aside className="w-60 shrink-0 border-r border-stone-200 bg-stone-50/70 py-4 px-3 flex flex-col gap-1 overflow-y-auto">
        {schemaMode ? (
          <OBtn onClick={onNewTable} className="self-start mb-3 ml-1"><Icon name="plus" size={18}/>Neue Tabelle</OBtn>
        ) : (
          <button onClick={onNewRecord} disabled={active.isView} className="self-start mb-3 ml-1 flex items-center gap-2 pl-3 pr-5 h-12 rounded-2xl bg-white shadow-md hover:shadow-lg border border-stone-200 disabled:opacity-40 disabled:cursor-not-allowed transition">
            <Icon name="plus" size={20} className="text-amber-600"/><span className="font-medium text-sm">Neuer Datensatz</span>
          </button>
        )}

        {tableKeys.length > 0 && <div className="px-3 pt-4 pb-1 text-[10px] uppercase tracking-wider text-stone-500 font-semibold">Tabellen</div>}
        {tableKeys.map(key => (
          <div key={key} className="flex items-center group">
            <button onClick={() => onSelectResource(key)} className={`flex-1 flex items-center gap-3 pl-3 pr-2 h-9 rounded-r-full text-sm transition ${activeKey === key ? 'bg-amber-100/80 text-amber-900 font-semibold' : 'hover:bg-stone-200/60 text-stone-700'}`}>
              <Icon name="table-2" size={16} className={activeKey === key ? 'text-amber-700' : 'text-stone-500'}/>
              <span className="flex-1 text-left truncate">{resources[key].label}</span>
              {resources[key].foreignKeys.length > 0 && <Icon name="link" size={11} className="text-blue-400"/>}
              <span className={`text-[10px] font-mono ${activeKey === key ? 'text-amber-700' : 'text-stone-400'}`}>{resources[key].rows.length}</span>
            </button>
            {schemaMode && <button onClick={() => onDropTable(key)} className="opacity-0 group-hover:opacity-100 p-1 text-red-400 hover:text-red-600" title="Tabelle loeschen"><Icon name="trash-2" size={13}/></button>}
          </div>
        ))}

        {viewKeys.length > 0 && <div className="px-3 pt-4 pb-1 text-[10px] uppercase tracking-wider text-stone-500 font-semibold">Views</div>}
        {viewKeys.map(key => (
          <button key={key} onClick={() => onSelectResource(key)} className={`w-full flex items-center gap-3 pl-3 pr-2 h-9 rounded-r-full text-sm transition ${activeKey === key ? 'bg-amber-100/80 text-amber-900 font-semibold' : 'hover:bg-stone-200/60 text-stone-700'}`}>
            <Icon name="eye" size={16} className={activeKey === key ? 'text-amber-700' : 'text-stone-500'}/>
            <span className="flex-1 text-left truncate">{resources[key].label}</span>
            <span className={`text-[10px] font-mono ${activeKey === key ? 'text-amber-700' : 'text-stone-400'}`}>{resources[key].rows.length}</span>
          </button>
        ))}

        {active.foreignKeys.length > 0 && <div className="mx-3 mt-3 p-3 rounded-lg bg-blue-50 border border-blue-100">
          <div className="text-[10px] uppercase tracking-wider text-blue-600 font-semibold mb-1.5">Referenzen</div>
          {active.foreignKeys.map(fk => <div key={fk.column} className="flex items-center gap-1.5 text-[11px] text-blue-700 mb-1 last:mb-0">
            <Icon name="arrow-right" size={10} className="text-blue-400"/>
            <span className="font-mono">{fk.column}</span><span className="text-blue-400">-&gt;</span>
            <button onClick={() => onSelectResource(fk.ref_table)} className="font-mono font-semibold hover:underline">{fk.ref_table}.{fk.ref_column}</button>
            {schemaMode && <button onClick={() => onDropFk(fk.column)} className="ml-auto text-red-400 hover:text-red-600" title="FK entfernen"><Icon name="x" size={11}/></button>}
          </div>)}
        </div>}

        <div className="mt-auto px-3 py-3 text-[11px] text-stone-500 leading-relaxed border-t border-stone-200 mt-4">
          <Icon name="link-2" size={12} className="inline mr-1"/>
          <span className="font-semibold">Service aktiv</span>
        </div>
      </aside>
    );
  }

  AR.Sidebar = Sidebar;
})(window.Service);
