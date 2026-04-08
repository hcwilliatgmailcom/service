window.Service = window.Service || {};

(function(AR) {
  const { useRef, useEffect } = React;

  function Icon({ name, size = 16, className = '', strokeWidth = 2 }) {
    const ref = useRef(null);
    useEffect(() => {
      if (!ref.current || !window.lucide) return;
      ref.current.innerHTML = '';
      const s = lucide.icons[name]; if (!s) return;
      const [, attrs, inner] = s;
      const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
      Object.entries(attrs).forEach(([k, v]) => svg.setAttribute(k, v));
      svg.setAttribute('width', size); svg.setAttribute('height', size); svg.setAttribute('stroke-width', strokeWidth);
      svg.innerHTML = inner.map(([tag, a]) => {
        const el = document.createElementNS('http://www.w3.org/2000/svg', tag);
        Object.entries(a).forEach(([k, v]) => el.setAttribute(k, v));
        return el.outerHTML;
      }).join('');
      ref.current.appendChild(svg);
    }, [name, size, strokeWidth]);
    return <span ref={ref} className={`inline-flex items-center justify-center ${className}`}/>;
  }

  AR.Icon = Icon;
})(window.Service);
