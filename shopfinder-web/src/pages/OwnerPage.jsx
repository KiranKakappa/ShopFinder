import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api, getToken, setToken } from '../api.js';
import { DAY_NAMES, timeAgo, toLocalInput, fromLocalInput } from '../format.js';
import LocationPicker from '../components/LocationPicker.jsx';
import SwipeSwitch from '../components/SwipeSwitch.jsx';
import { StatusPill, ProductRow, Empty } from '../components/Bits.jsx';

export default function OwnerPage() {
  const [shop, setShop] = useState(null);
  const [checking, setChecking] = useState(!!getToken());

  useEffect(() => {
    if (!getToken()) return;
    api.me().then(setShop).catch(() => setToken(null)).finally(() => setChecking(false));
  }, []);

  const onAuth = ({ token, shop }) => { setToken(token); setShop(shop); };
  const logout = () => { setToken(null); setShop(null); };

  return (
    <div className="app owner">
      <header className="topbar">
        <Link to="/" className="brand"><span className="brand-mark" />ShopFinder <span className="brand-sub">for owners</span></Link>
        <div className="topbar-right">
          <Link to="/" className="btn btn-ghost btn-sm">Customer view</Link>
          {shop && <button className="btn btn-ghost btn-sm" onClick={logout}>Log out</button>}
        </div>
      </header>
      <main className="owner-main">
        {checking ? <p className="muted">Loading…</p>
          : shop ? <Dashboard shop={shop} setShop={setShop} />
          : <AuthView onAuth={onAuth} />}
      </main>
    </div>
  );
}

/* ---------------- Login / sign-up ---------------- */

const emptyShopForm = {
  name: '', category: '', description: '', address: '',
  openTime: '09:00', closeTime: '21:00', openDays: [1, 2, 3, 4, 5, 6],
  location: null
};

function AuthView({ onAuth }) {
  const [mode, setMode] = useState('login');
  const [phone, setPhone] = useState('');
  const [password, setPassword] = useState('');
  const [form, setForm] = useState(emptyShopForm);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  const submit = async (e) => {
    e.preventDefault();
    setError('');
    if (mode === 'register' && !form.location) { setError('Pin your shop on the map.'); return; }
    setBusy(true);
    try {
      const res = mode === 'login'
        ? await api.login(phone, password)
        : await api.register({ ...toShopPayload(form), phone, password });
      onAuth(res);
    } catch (err) {
      setError(err.message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="auth">
      <div className="auth-intro">
        <h1>{mode === 'login' ? 'Welcome back' : 'List your shop'}</h1>
        <p className="muted">Tell customers when you’re open and post discounts in seconds.</p>
      </div>
      <div className="tabs">
        <button className={mode === 'login' ? 'tab tab-on' : 'tab'} onClick={() => { setMode('login'); setError(''); }}>Log in</button>
        <button className={mode === 'register' ? 'tab tab-on' : 'tab'} onClick={() => { setMode('register'); setError(''); }}>Sign up</button>
      </div>
      <form className="panel form" onSubmit={submit}>
        <label>Phone number
          <input type="tel" autoComplete="tel" value={phone} onChange={(e) => setPhone(e.target.value)} required />
        </label>
        <label>Password
          <input type="password" autoComplete={mode === 'login' ? 'current-password' : 'new-password'}
            minLength={6} value={password} onChange={(e) => setPassword(e.target.value)} required />
        </label>
        {mode === 'register' && <ShopFields form={form} setForm={setForm} />}
        {error && <p className="error">{error}</p>}
        <button className="btn btn-primary btn-block" disabled={busy}>
          {busy ? 'Please wait…' : mode === 'login' ? 'Log in' : 'Create my shop'}
        </button>
        {mode === 'login' && (
          <p className="small muted">Demo: phone <b>5550101</b>, password <b>demo1234</b></p>
        )}
      </form>
    </div>
  );
}

function toShopPayload(f) {
  return {
    name: f.name, category: f.category, description: f.description, address: f.address,
    openTime: f.openTime, closeTime: f.closeTime, openDays: f.openDays,
    latitude: f.location?.lat, longitude: f.location?.lng
  };
}

function fromShop(s) {
  return {
    name: s.name, category: s.category, description: s.description, address: s.address,
    openTime: s.openTime, closeTime: s.closeTime, openDays: s.openDays,
    location: { lat: s.latitude, lng: s.longitude }
  };
}

function ShopFields({ form, setForm }) {
  const [categories, setCategories] = useState([]);
  const [cityCenter, setCityCenter] = useState(null);

  useEffect(() => {
    api.categories().then(setCategories).catch(() => {});
    // Start the map where existing shops are, so new owners land in their own city.
    api.shops({}).then((list) => {
      if (list.length) {
        const lat = list.reduce((a, s) => a + s.latitude, 0) / list.length;
        const lng = list.reduce((a, s) => a + s.longitude, 0) / list.length;
        setCityCenter([lat, lng]);
      }
    }).catch(() => {});
  }, []);

  const set = (k) => (e) => setForm((f) => ({ ...f, [k]: e.target.value }));
  const toggleDay = (d) => setForm((f) => ({
    ...f,
    openDays: f.openDays.includes(d) ? f.openDays.filter((x) => x !== d) : [...f.openDays, d].sort()
  }));

  return (
    <>
      <label>Shop name
        <input value={form.name} onChange={set('name')} maxLength={120} required />
      </label>
      <label>Category
        <select value={form.category} onChange={set('category')} required>
          <option value="" disabled>Choose…</option>
          {categories.map((c) => <option key={c}>{c}</option>)}
        </select>
      </label>
      <label>Short description <span className="muted small">(optional)</span>
        <textarea rows={2} value={form.description} onChange={set('description')} maxLength={1000} />
      </label>
      <label>Address
        <input value={form.address} onChange={set('address')} maxLength={300} required />
      </label>
      <div className="row2">
        <label>Opens at
          <input type="time" value={form.openTime} onChange={set('openTime')} required />
        </label>
        <label>Closes at
          <input type="time" value={form.closeTime} onChange={set('closeTime')} required />
        </label>
      </div>
      <p className="small muted hint">Closing earlier than opening means overnight (e.g. 6 PM → 2 AM). Same time = open 24 hours.</p>
      <fieldset className="days">
        <legend>Open on</legend>
        {DAY_NAMES.map((name, d) => (
          <label key={d} className={`day ${form.openDays.includes(d) ? 'day-on' : ''}`}>
            <input type="checkbox" checked={form.openDays.includes(d)} onChange={() => toggleDay(d)} />
            {name}
          </label>
        ))}
      </fieldset>
      <div>
        <div className="label-text">Location</div>
        <LocationPicker value={form.location} fallbackCenter={cityCenter}
          onChange={(location) => setForm((f) => ({ ...f, location }))} />
      </div>
    </>
  );
}

/* ---------------- Dashboard ---------------- */

function Dashboard({ shop, setShop }) {
  const [section, setSection] = useState('today');
  return (
    <div className="dash">
      <div className="dash-head">
        <div>
          <span className="category">{shop.category}</span>
          <h1>{shop.name}</h1>
        </div>
        <div className="tabs">
          <button className={section === 'today' ? 'tab tab-on' : 'tab'} onClick={() => setSection('today')}>Status</button>
          <button className={section === 'products' ? 'tab tab-on' : 'tab'} onClick={() => setSection('products')}>
            Products & deals <span className="count">{shop.products.length}</span>
          </button>
          <button className={section === 'settings' ? 'tab tab-on' : 'tab'} onClick={() => setSection('settings')}>Shop details</button>
        </div>
      </div>
      {section === 'today' && <StatusCard shop={shop} setShop={setShop} />}
      {section === 'products' && <Products shop={shop} setShop={setShop} />}
      {section === 'settings' && <Settings shop={shop} setShop={setShop} />}
    </div>
  );
}

function StatusCard({ shop, setShop }) {
  const [busy, setBusy] = useState('');
  const [error, setError] = useState('');

  const setMode = async (mode) => {
    setBusy(mode); setError('');
    try { setShop(await api.setStatus(mode)); }
    catch (e) { setError(e.message); }
    finally { setBusy(''); }
  };

  return (
    <section className="panel status-card">
      <div className={`big-status ${shop.isOpen ? 'big-open' : 'big-closed'}`}>
        <div className="big-label">Customers see your shop as</div>
        <div className="big-value">{shop.isOpen ? 'OPEN' : 'CLOSED'}</div>
        <div className="small">{shop.statusMessage} · updated {timeAgo(shop.lastStatusChangeUtc)}</div>
      </div>

      {shop.closedReportsToday > 0 && (
        <div className="warn">
          ⚠ {shop.closedReportsToday} {shop.closedReportsToday === 1 ? 'customer' : 'customers'} reported your shop closed today.
          Tap the correct status below to clear it.
        </div>
      )}

      <SwipeSwitch
        isOpen={shop.isOpen}
        busy={!!busy}
        onChange={(open) => setMode(open ? 'open' : 'closed')}
      />
      <p className="small muted swipe-caption">Swipe right to open · swipe left to close</p>
      <button className="btn btn-ghost btn-block" disabled={!!busy || shop.statusSource === 'hours'} onClick={() => setMode('auto')}>
        {shop.statusSource === 'hours' ? '✓ Following regular hours' : 'Go back to regular hours'}
      </button>
      {error && <p className="error">{error}</p>}
      <p className="small muted">
        Regular hours: {shop.hours}. A swipe only lasts until midnight — tomorrow your
        regular hours apply again, so customers are never misled if you forget to switch.
      </p>
    </section>
  );
}

const emptyProduct = { name: '', description: '', price: '', discountPercent: 0, dealEnds: '', inStock: true };

function Products({ shop, setShop }) {
  const [editing, setEditing] = useState(null); // null | 'new' | product
  const [error, setError] = useState('');

  const reload = async () => setShop(await api.me());

  const toPayload = (p, overrides = {}) => ({
    name: p.name, description: p.description, price: p.price,
    discountPercent: p.discountPercent, dealEndsUtc: p.dealEndsUtc ?? null, inStock: p.inStock,
    ...overrides
  });

  const toggleStock = async (p) => {
    setError('');
    try { await api.updateProduct(p.id, toPayload(p, {
      inStock: !p.inStock,
      // keep a running deal as-is; drop an expired one so it doesn't come back
      discountPercent: p.dealActive ? p.discountPercent : 0,
      dealEndsUtc: p.dealActive ? p.dealEndsUtc : null
    })); await reload(); }
    catch (e) { setError(e.message); }
  };

  const remove = async (p) => {
    if (!window.confirm(`Delete “${p.name}”?`)) return;
    setError('');
    try { await api.deleteProduct(p.id); await reload(); }
    catch (e) { setError(e.message); }
  };

  return (
    <section className="panel">
      <div className="panel-head">
        <h2>Products & deals</h2>
        <button className="btn btn-primary btn-sm" onClick={() => setEditing('new')}>+ Add product</button>
      </div>
      {error && <p className="error">{error}</p>}
      {shop.products.length === 0
        ? <Empty title="No products yet">Add what you sell. Give a discount to show up under “Deals”.</Empty>
        : (
          <ul className="products">
            {shop.products.map((p) => (
              <ProductRow key={p.id} product={p} actions={
                <div className="row-actions">
                  <button className="link" onClick={() => setEditing(p)}>Edit</button>
                  <button className="link" onClick={() => toggleStock(p)}>{p.inStock ? 'Mark out of stock' : 'Back in stock'}</button>
                  <button className="link link-danger" onClick={() => remove(p)}>Delete</button>
                </div>
              } />
            ))}
          </ul>
        )}
      {editing && (
        <ProductForm
          initial={editing === 'new' ? emptyProduct : {
            name: editing.name, description: editing.description, price: editing.price,
            discountPercent: editing.dealActive ? editing.discountPercent : 0,
            dealEnds: editing.dealActive ? toLocalInput(editing.dealEndsUtc) : '',
            inStock: editing.inStock
          }}
          onCancel={() => setEditing(null)}
          onSave={async (data) => {
            if (editing === 'new') await api.addProduct(data);
            else await api.updateProduct(editing.id, data);
            await reload();
            setEditing(null);
          }}
        />
      )}
    </section>
  );
}

function ProductForm({ initial, onCancel, onSave }) {
  const [f, setF] = useState(initial);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const set = (k) => (e) => setF((x) => ({ ...x, [k]: e.target.type === 'checkbox' ? e.target.checked : e.target.value }));

  const price = Number(f.price) || 0;
  const pct = Number(f.discountPercent) || 0;
  const final = Math.round(price * (100 - pct)) / 100;

  const submit = async (e) => {
    e.preventDefault();
    setBusy(true); setError('');
    try {
      await onSave({
        name: f.name, description: f.description,
        price: Number(f.price), discountPercent: pct,
        dealEndsUtc: pct > 0 ? fromLocalInput(f.dealEnds) : null,
        inStock: f.inStock
      });
    } catch (err) {
      setError(err.message);
      setBusy(false);
    }
  };

  return (
    <div className="modal-wrap" onClick={onCancel}>
      <form className="modal form" onClick={(e) => e.stopPropagation()} onSubmit={submit}>
        <h2>{initial.name ? 'Edit product' : 'Add product'}</h2>
        <label>Name
          <input value={f.name} onChange={set('name')} maxLength={120} required autoFocus />
        </label>
        <label>Description <span className="muted small">(optional)</span>
          <input value={f.description} onChange={set('description')} maxLength={1000} />
        </label>
        <div className="row2">
          <label>Price (USD)
            <input type="number" min="0.01" step="0.01" value={f.price} onChange={set('price')} required />
          </label>
          <label>Discount %
            <input type="number" min="0" max="90" step="1" value={f.discountPercent} onChange={set('discountPercent')} />
          </label>
        </div>
        {pct > 0 && (
          <>
            <label>Deal ends <span className="muted small">(optional — leave empty to keep it running)</span>
              <input type="datetime-local" value={f.dealEnds} onChange={set('dealEnds')} />
            </label>
            {price > 0 && (
              <p className="small">Customers pay <b>${final.toFixed(2)}</b> instead of ${price.toFixed(2)}.</p>
            )}
          </>
        )}
        <label className="check">
          <input type="checkbox" checked={f.inStock} onChange={set('inStock')} /> In stock
        </label>
        {error && <p className="error">{error}</p>}
        <div className="modal-actions">
          <button type="button" className="btn btn-ghost" onClick={onCancel}>Cancel</button>
          <button className="btn btn-primary" disabled={busy}>{busy ? 'Saving…' : 'Save'}</button>
        </div>
      </form>
    </div>
  );
}

function Settings({ shop, setShop }) {
  const [form, setForm] = useState(() => fromShop(shop));
  const [msg, setMsg] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  const submit = async (e) => {
    e.preventDefault();
    setBusy(true); setMsg(''); setError('');
    try {
      setShop(await api.updateShop(toShopPayload(form)));
      setMsg('Saved.');
    } catch (err) {
      setError(err.message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <form className="panel form" onSubmit={submit}>
      <h2>Shop details</h2>
      <p className="small muted">Login phone: {shop.phone}</p>
      <ShopFields form={form} setForm={setForm} />
      {error && <p className="error">{error}</p>}
      {msg && <p className="ok">{msg}</p>}
      <button className="btn btn-primary btn-block" disabled={busy}>{busy ? 'Saving…' : 'Save changes'}</button>
    </form>
  );
}
