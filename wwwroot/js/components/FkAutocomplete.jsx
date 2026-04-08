window.Service = window.Service || {};

(function(AR) {
  const { useState, useEffect, useMemo, useRef } = React;
  const Icon = AR.Icon;

  function FkAutocomplete({ options, value, onChange, placeholder }) {
    const [query, setQuery] = useState('');
    const [open, setOpen] = useState(false);
    const [hi, setHi] = useState(0);
    const wr = useRef(null), inp = useRef(null), lr = useRef(null);
    const sel = options.find(o => String(o.value) === String(value));
    const filtered = useMemo(() => {
      if (!query) return options;
      const q = query.toLowerCase();
      return options.filter(o => String(o.value).toLowerCase().includes(q) || o.label.toLowerCase().includes(q));
    }, [options, query]);

    const hl = (text, q) => {
      if (!q) return text;
      const i = text.toLowerCase().indexOf(q.toLowerCase());
      if (i === -1) return text;
      return <>{text.slice(0, i)}<mark className="bg-amber-200 text-amber-900 rounded-sm px-0">{text.slice(i, i + q.length)}</mark>{text.slice(i + q.length)}</>;
    };

    useEffect(() => {
      const h = e => { if (wr.current && !wr.current.contains(e.target)) { setOpen(false); setQuery(''); } };
      document.addEventListener('mousedown', h);
      return () => document.removeEventListener('mousedown', h);
    }, []);
    useEffect(() => { setHi(0); }, [filtered.length]);
    useEffect(() => { if (open && lr.current) { const el = lr.current.children[hi]; if (el) el.scrollIntoView({ block: 'nearest' }); } }, [hi, open]);

    const pick = item => { onChange(String(item.value)); setQuery(''); setOpen(false); };
    const kd = e => {
      if (!open && (e.key === 'ArrowDown' || e.key === 'Enter')) { setOpen(true); e.preventDefault(); return; }
      if (!open) return;
      if (e.key === 'ArrowDown') { e.preventDefault(); setHi(i => Math.min(i + 1, filtered.length - 1)); }
      else if (e.key === 'ArrowUp') { e.preventDefault(); setHi(i => Math.max(i - 1, 0)); }
      else if (e.key === 'Enter') { e.preventDefault(); if (filtered[hi]) pick(filtered[hi]); }
      else if (e.key === 'Escape') { setOpen(false); setQuery(''); }
    };
    const palette = ['#d97706','#dc2626','#0f766e','#1d4ed8','#7c3aed','#db2777','#65a30d','#0891b2'];

    return (
      <div ref={wr} className="relative">
        <div className="flex items-center border border-stone-300 rounded-md focus-within:border-amber-500 focus-within:ring-2 focus-within:ring-amber-200 bg-white">
          <input ref={inp} value={open ? query : (sel ? `${sel.value} -- ${sel.label}` : (value || ''))} onChange={e => { setQuery(e.target.value); setOpen(true); }} onFocus={() => { setOpen(true); setQuery(''); }} onKeyDown={kd} placeholder={placeholder || 'Suchen...'} className="flex-1 px-3 py-1.5 text-sm bg-transparent outline-none rounded-md"/>
          {value && <button onClick={e => { e.stopPropagation(); onChange(''); setQuery(''); setOpen(false); }} className="px-2 text-stone-400 hover:text-stone-700" type="button"><Icon name="x" size={14}/></button>}
          <span className="px-2 text-stone-400"><Icon name="chevron-down" size={14}/></span>
        </div>
        {open && <ul ref={lr} className="absolute z-50 left-0 right-0 mt-1 max-h-56 overflow-y-auto bg-white border border-stone-200 rounded-lg shadow-xl">
          {filtered.length === 0 && <li className="px-3 py-2 text-xs text-stone-400 italic">Keine Treffer</li>}
          {filtered.map((item, idx) => <li key={item.value} onMouseDown={e => { e.preventDefault(); pick(item); }} onMouseEnter={() => setHi(idx)} className={`flex items-center gap-2 px-3 py-2 cursor-pointer text-sm ${idx === hi ? 'bg-amber-50 text-amber-900' : 'hover:bg-stone-50'} ${String(item.value) === String(value) ? 'font-semibold' : ''}`}>
            <span className="w-7 h-7 rounded-full text-[10px] font-bold text-white shrink-0 grid place-items-center" style={{ background: palette[item.value % 8] }}>{String(item.label).slice(0, 2).toUpperCase()}</span>
            <div className="flex-1 min-w-0 truncate">{hl(item.label, query)}</div>
            <span className="text-[10px] font-mono text-stone-400 shrink-0">#{item.value}</span>
            {String(item.value) === String(value) && <Icon name="check" size={14} className="text-amber-600 shrink-0"/>}
          </li>)}
        </ul>}
      </div>
    );
  }

  AR.FkAutocomplete = FkAutocomplete;
})(window.Service);
