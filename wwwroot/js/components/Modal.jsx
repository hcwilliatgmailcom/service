window.Service = window.Service || {};

(function(AR) {
  const Icon = AR.Icon;

  function Modal({ open, onClose, title, children, wide }) {
    if (!open) return null;
    return (
      <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40" onClick={onClose}>
        <div className={`bg-white rounded-2xl shadow-2xl border border-stone-200 ${wide ? 'w-[600px]' : 'w-[440px]'} max-h-[80vh] flex flex-col`} onClick={e => e.stopPropagation()}>
          <div className="flex items-center justify-between px-6 py-4 border-b border-stone-200">
            <h2 className="text-lg font-semibold text-stone-900">{title}</h2>
            <button onClick={onClose} className="p-1 rounded-full hover:bg-stone-100"><Icon name="x" size={18}/></button>
          </div>
          <div className="flex-1 overflow-y-auto px-6 py-4">{children}</div>
        </div>
      </div>
    );
  }

  function OBtn({ children, onClick, disabled, small, danger, className = '', title = '' }) {
    const base = danger ? 'bg-red-500 hover:bg-red-600 text-white' : 'bg-orange-500 hover:bg-orange-600 text-white';
    const sz = small ? 'px-2.5 py-1 text-xs gap-1' : 'px-4 py-2 text-sm gap-1.5';
    return <button onClick={onClick} disabled={disabled} title={title} className={`inline-flex items-center font-semibold rounded-lg transition disabled:opacity-40 disabled:cursor-not-allowed shadow-sm ${base} ${sz} ${className}`}>{children}</button>;
  }

  function OBtnOutline({ children, onClick, small, className = '', title = '' }) {
    const sz = small ? 'px-2.5 py-1 text-xs gap-1' : 'px-3 py-1.5 text-sm gap-1.5';
    return <button onClick={onClick} title={title} className={`inline-flex items-center font-semibold rounded-lg border-2 border-orange-400 text-orange-600 hover:bg-orange-50 transition ${sz} ${className}`}>{children}</button>;
  }

  AR.Modal = Modal;
  AR.OBtn = OBtn;
  AR.OBtnOutline = OBtnOutline;
})(window.Service);
