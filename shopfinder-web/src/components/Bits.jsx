import { money, dealEnds } from '../format.js';

export function StatusPill({ isOpen, source }) {
  return (
    <span className={`pill ${isOpen ? 'pill-open' : 'pill-closed'}`}>
      <span className="dot" />
      {isOpen ? 'Open' : 'Closed'}
      {source === 'manual' && <span className="pill-note">· by owner</span>}
    </span>
  );
}

export function Price({ product }) {
  return (
    <span className="price">
      <span className="price-final">{money(product.finalPrice)}</span>
      {product.dealActive && <span className="price-old">{money(product.price)}</span>}
    </span>
  );
}

export function DiscountBadge({ product }) {
  if (!product.dealActive) return null;
  return <span className="badge-deal">-{product.discountPercent}%</span>;
}

export function ProductRow({ product, actions }) {
  return (
    <li className={`product ${product.inStock ? '' : 'product-out'}`}>
      <div className="product-main">
        <div className="product-name">
          {product.name} <DiscountBadge product={product} />
        </div>
        {product.description && <div className="muted small">{product.description}</div>}
        <div className="small product-meta">
          {!product.inStock && <span className="tag tag-out">Out of stock</span>}
          {product.dealActive && product.dealEndsUtc && <span className="tag">{dealEnds(product.dealEndsUtc)}</span>}
        </div>
      </div>
      <div className="product-side">
        <Price product={product} />
        {actions}
      </div>
    </li>
  );
}

export function Empty({ title, children }) {
  return (
    <div className="empty">
      <div className="empty-title">{title}</div>
      {children && <div className="muted small">{children}</div>}
    </div>
  );
}
