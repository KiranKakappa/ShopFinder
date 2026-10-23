import { useEffect, useState } from 'react';
import { api } from '../api.js';
import { directionsUrl, distance, timeAgo } from '../format.js';
import { StatusPill, ProductRow, Empty } from './Bits.jsx';

/** Slide-over panel with a shop's status, hours and products. */
export default function ShopDetail({ shopId, distanceKm, refreshKey, onClose }) {
  const [shop, setShop] = useState(null);
  const [error, setError] = useState('');
  const [reported, setReported] = useState(false);
  const [reportMsg, setReportMsg] = useState('');

  useEffect(() => {
    let alive = true;
    setError('');
    api.shop(shopId)
      .then((s) => alive && setShop(s))
      .catch((e) => alive && setError(e.message));
    return () => { alive = false; };
  }, [shopId, refreshKey]);

  useEffect(() => { setReported(false); setReportMsg(''); setShop(null); }, [shopId]);

  useEffect(() => {
    const onKey = (e) => e.key === 'Escape' && onClose();
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [onClose]);

  const report = async () => {
    try {
      const r = await api.reportClosed(shopId);
      setReported(true);
      setReportMsg('Thanks — other shoppers will see your report.');
      setShop((s) => s && { ...s, closedReportsToday: r.closedReportsToday });
    } catch (e) {
      setReportMsg(e.message);
    }
  };

  return (
    <aside className="detail" role="dialog" aria-label="Shop details">
      <button className="detail-close" onClick={onClose} aria-label="Close">×</button>
      {error && <p className="error">{error}</p>}
      {!shop && !error && <p className="muted">Loading…</p>}
      {shop && (
        <>
          <div className="detail-head">
            <span className="category">{shop.category}</span>
            <h2>{shop.name}</h2>
            <div className="status-line">
              <StatusPill isOpen={shop.isOpen} source={shop.statusSource} />
              <span className="muted small">{shop.statusMessage}</span>
            </div>
            {shop.closedReportsToday > 0 && (
              <div className="warn small">
                ⚠ {shop.closedReportsToday} {shop.closedReportsToday === 1 ? 'shopper' : 'shoppers'} reported it closed today
              </div>
            )}
          </div>

          <dl className="facts">
            <dt>Hours</dt><dd>{shop.hours}</dd>
            <dt>Address</dt><dd>{shop.address}{distanceKm != null && <span className="muted"> · {distance(distanceKm)}</span>}</dd>
            <dt>Phone</dt><dd><a href={`tel:${shop.phone}`}>{shop.phone}</a></dd>
            <dt>Status</dt><dd className="muted small">Updated {timeAgo(shop.lastStatusChangeUtc)}</dd>
          </dl>
          {shop.description && <p className="desc">{shop.description}</p>}

          <div className="detail-actions">
            <a className="btn btn-primary" href={directionsUrl(shop.latitude, shop.longitude)} target="_blank" rel="noreferrer">
              Get directions
            </a>
            <a className="btn btn-ghost" href={`tel:${shop.phone}`}>Call</a>
          </div>

          <h3 className="section-title">Products <span className="muted small">({shop.products.length})</span></h3>
          {shop.products.length === 0
            ? <Empty title="No products listed yet" />
            : <ul className="products">{shop.products.map((p) => <ProductRow key={p.id} product={p} />)}</ul>}

          {shop.isOpen && (
            <div className="report">
              <span className="small muted">Went there and found it closed?</span>
              <button className="btn btn-ghost btn-sm" onClick={report} disabled={reported}>
                {reported ? 'Reported' : 'Report closed'}
              </button>
              {reportMsg && <div className="small muted">{reportMsg}</div>}
            </div>
          )}
        </>
      )}
    </aside>
  );
}
