import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../api.js';
import { distance, money, dealEnds } from '../format.js';
import ShopMap from '../components/ShopMap.jsx';
import ShopDetail from '../components/ShopDetail.jsx';
import { StatusPill, Empty } from '../components/Bits.jsx';

const REFRESH_MS = 60_000; // statuses change during the day, so refresh every minute

function useDebounced(value, ms) {
  const [v, setV] = useState(value);
  useEffect(() => {
    const t = setTimeout(() => setV(value), ms);
    return () => clearTimeout(t);
  }, [value, ms]);
  return v;
}

export default function CustomerPage() {
  const [tab, setTab] = useState('shops');
  const [search, setSearch] = useState('');
  const q = useDebounced(search.trim(), 300);
  const [category, setCategory] = useState('');
  const [openNow, setOpenNow] = useState(false);
  const [userLocation, setUserLocation] = useState(null);
  const [locMsg, setLocMsg] = useState('');

  const [categories, setCategories] = useState([]);
  const [shops, setShops] = useState([]);
  const [deals, setDeals] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [selectedId, setSelectedId] = useState(null);
  const [tick, setTick] = useState(0);

  useEffect(() => { api.categories().then(setCategories).catch(() => {}); }, []);

  useEffect(() => {
    const t = setInterval(() => setTick((n) => n + 1), REFRESH_MS);
    return () => clearInterval(t);
  }, []);

  const params = useMemo(() => ({
    q, category, openNow,
    lat: userLocation?.lat, lng: userLocation?.lng
  }), [q, category, openNow, userLocation]);

  useEffect(() => {
    let alive = true;
    setError('');
    Promise.all([api.shops(params), api.deals(params)])
      .then(([s, d]) => { if (alive) { setShops(s); setDeals(d); } })
      .catch((e) => alive && setError(e.message))
      .finally(() => alive && setLoading(false));
    return () => { alive = false; };
  }, [params, tick]);

  const locate = () => {
    if (!navigator.geolocation) { setLocMsg('Location is not available in this browser.'); return; }
    setLocMsg('Finding you…');
    navigator.geolocation.getCurrentPosition(
      (pos) => { setUserLocation({ lat: pos.coords.latitude, lng: pos.coords.longitude }); setLocMsg(''); },
      () => setLocMsg('Location permission denied — showing all shops.'),
      { enableHighAccuracy: true, timeout: 10000 }
    );
  };

  const close = useCallback(() => setSelectedId(null), []);
  const openCount = shops.filter((s) => s.isOpen).length;
  const mapShops = tab === 'shops'
    ? shops
    : shops.filter((s) => deals.some((d) => d.shopId === s.id));
  const selectedDistance = shops.find((s) => s.id === selectedId)?.distanceKm;

  return (
    <div className="app">
      <header className="topbar">
        <Link to="/" className="brand"><span className="brand-mark" />ShopFinder</Link>
        <Link to="/owner" className="btn btn-ghost btn-sm">For shop owners</Link>
      </header>

      <section className="filters">
        <div className="search">
          <input
            type="search"
            placeholder="Search shops or products — e.g. milk, charger, bread"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            aria-label="Search"
          />
          <button className={`btn btn-sm ${userLocation ? 'btn-active' : 'btn-ghost'}`} onClick={locate}>
            {userLocation ? 'Near me ✓' : 'Near me'}
          </button>
        </div>
        <div className="chips">
          <label className={`chip chip-toggle ${openNow ? 'chip-on-green' : ''}`}>
            <input type="checkbox" checked={openNow} onChange={(e) => setOpenNow(e.target.checked)} />
            <span className="dot" /> Open now
          </label>
          <button className={`chip ${category === '' ? 'chip-on' : ''}`} onClick={() => setCategory('')}>All</button>
          {categories.map((c) => (
            <button key={c} className={`chip ${category === c ? 'chip-on' : ''}`}
              onClick={() => setCategory(category === c ? '' : c)}>{c}</button>
          ))}
        </div>
        {locMsg && <div className="small muted">{locMsg}</div>}
      </section>

      <main className="layout">
        <section className="list-pane">
          <div className="tabs" role="tablist">
            <button role="tab" aria-selected={tab === 'shops'} className={tab === 'shops' ? 'tab tab-on' : 'tab'} onClick={() => setTab('shops')}>
              Shops <span className="count">{shops.length}</span>
            </button>
            <button role="tab" aria-selected={tab === 'deals'} className={tab === 'deals' ? 'tab tab-on' : 'tab'} onClick={() => setTab('deals')}>
              Deals <span className="count count-deal">{deals.length}</span>
            </button>
            {tab === 'shops' && !loading && <span className="muted small tabs-note">{openCount} open right now</span>}
          </div>

          {error && <p className="error">{error}</p>}
          {loading && <p className="muted pad">Loading shops…</p>}

          {!loading && tab === 'shops' && (
            shops.length === 0
              ? <Empty title="No shops match">Try another search, or turn off “Open now”.</Empty>
              : <ul className="cards">
                  {shops.map((s) => (
                    <li key={s.id}>
                      <button className={`card ${selectedId === s.id ? 'card-on' : ''} ${s.isOpen ? '' : 'card-closed'}`}
                        onClick={() => setSelectedId(s.id)}>
                        <div className="card-top">
                          <span className="card-name">{s.name}</span>
                          <StatusPill isOpen={s.isOpen} source={s.statusSource} />
                        </div>
                        <div className="small muted">
                          {s.category} · {s.statusMessage}{s.distanceKm != null && ` · ${distance(s.distanceKm)}`}
                        </div>
                        <div className="card-bottom small">
                          {s.activeDeals > 0 && <span className="tag tag-deal">{s.activeDeals} {s.activeDeals === 1 ? 'deal' : 'deals'}</span>}
                          {s.closedReportsToday > 0 && <span className="tag tag-warn">⚠ reported closed</span>}
                          <span className="muted">{s.hours}</span>
                        </div>
                      </button>
                    </li>
                  ))}
                </ul>
          )}

          {!loading && tab === 'deals' && (
            deals.length === 0
              ? <Empty title="No deals right now">Check back later — shops add discounts during the day.</Empty>
              : <ul className="cards">
                  {deals.map((d) => (
                    <li key={d.product.id}>
                      <button className={`card deal ${d.shopIsOpen ? '' : 'card-closed'}`} onClick={() => setSelectedId(d.shopId)}>
                        <span className="deal-pct">-{d.product.discountPercent}%</span>
                        <div className="deal-body">
                          <div className="card-name">{d.product.name}</div>
                          <div className="small muted">
                            {d.shopName} · {d.shopIsOpen ? 'Open' : 'Closed'}{d.distanceKm != null && ` · ${distance(d.distanceKm)}`}
                          </div>
                          {d.product.dealEndsUtc && <div className="small deal-ends">{dealEnds(d.product.dealEndsUtc)}</div>}
                        </div>
                        <div className="price">
                          <span className="price-final">{money(d.product.finalPrice)}</span>
                          <span className="price-old">{money(d.product.price)}</span>
                        </div>
                      </button>
                    </li>
                  ))}
                </ul>
          )}
        </section>

        <section className="map-pane">
          <ShopMap shops={mapShops} selectedId={selectedId} onSelect={setSelectedId} userLocation={userLocation} />
          <div className="legend small">
            <span><span className="dot dot-open" /> Open</span>
            <span><span className="dot dot-closed" /> Closed</span>
          </div>
        </section>
      </main>

      {selectedId && (
        <>
          <div className="scrim" onClick={close} />
          <ShopDetail shopId={selectedId} distanceKm={selectedDistance} refreshKey={tick} onClose={close} />
        </>
      )}
    </div>
  );
}
