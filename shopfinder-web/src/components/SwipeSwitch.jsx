import { useEffect, useRef, useState } from 'react';

/**
 * Slide-to-switch control for the shop status.
 * Drag the knob right to OPEN, left to CLOSE (past halfway to commit).
 * Also works with a tap on the knob/track (toggles) and the keyboard (← → Space Enter).
 */
export default function SwipeSwitch({ isOpen, busy, onChange }) {
  const trackRef = useRef(null);
  const drag = useRef(null);          // { startX, startPos, max, moved }
  const [pos, setPos] = useState(null); // knob offset in px while dragging; null = resting

  useEffect(() => { setPos(null); }, [isOpen]);

  const maxOffset = () => {
    const t = trackRef.current;
    if (!t) return 0;
    const knob = t.querySelector('.swipe-knob');
    return t.clientWidth - knob.offsetWidth - 8; // 4px padding each side
  };

  const commit = (open) => {
    setPos(null);
    if (!busy && open !== isOpen) onChange(open);
  };

  const onPointerDown = (e) => {
    if (busy) return;
    const max = maxOffset();
    drag.current = { startX: e.clientX, startPos: isOpen ? max : 0, max, moved: false };
    e.currentTarget.setPointerCapture?.(e.pointerId);
  };

  const onPointerMove = (e) => {
    const d = drag.current;
    if (!d) return;
    const dx = e.clientX - d.startX;
    if (Math.abs(dx) > 4) d.moved = true;
    setPos(Math.min(d.max, Math.max(0, d.startPos + dx)));
  };

  const onPointerUp = (e) => {
    const d = drag.current;
    drag.current = null;
    if (!d) return;
    if (!d.moved) { commit(!isOpen); return; } // simple tap toggles
    const final = Math.min(d.max, Math.max(0, d.startPos + (e.clientX - d.startX)));
    commit(final > d.max / 2);
  };

  const onKeyDown = (e) => {
    if (e.key === 'ArrowRight') { e.preventDefault(); commit(true); }
    else if (e.key === 'ArrowLeft') { e.preventDefault(); commit(false); }
    else if (e.key === ' ' || e.key === 'Enter') { e.preventDefault(); commit(!isOpen); }
  };

  const dragging = pos !== null;
  const max = dragging ? (drag.current?.max || maxOffset()) : 0;
  const progress = dragging ? pos / (max || 1) : isOpen ? 1 : 0; // 0 = closed side, 1 = open side
  const showOpen = progress > 0.5;

  return (
    <div
      ref={trackRef}
      className={`swipe ${showOpen ? 'swipe-open' : 'swipe-closed'} ${busy ? 'swipe-busy' : ''}`}
      role="switch"
      aria-checked={isOpen}
      aria-label="Shop status. Swipe right to open, left to close."
      tabIndex={0}
      onKeyDown={onKeyDown}
      onPointerDown={onPointerDown}
      onPointerMove={onPointerMove}
      onPointerUp={onPointerUp}
      onPointerCancel={() => { drag.current = null; setPos(null); }}
    >
      <span className="swipe-hint swipe-hint-left">« Swipe to close</span>
      <span className="swipe-hint swipe-hint-right">Swipe to open »</span>
      <span
        className="swipe-knob"
        style={dragging
          ? { transform: `translateX(${pos}px)`, transition: 'none' }
          : isOpen ? { transform: 'translateX(100%)' } : undefined}
      >
        {busy ? '…' : showOpen ? 'OPEN' : 'CLOSED'}
      </span>
    </div>
  );
}
