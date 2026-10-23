import { useEffect, useMemo } from 'react';
import { MapContainer, TileLayer, CircleMarker, Tooltip, useMap } from 'react-leaflet';

const OPEN = '#16a34a';
const CLOSED = '#9ca3af';
const SELECTED = '#ea580c';

function FitToShops({ shops, userLocation }) {
  const map = useMap();
  // Refit only when the set of visible shops changes, not on every status refresh.
  const key = shops.map((s) => s.id).join(',') + (userLocation ? 'u' : '');
  useEffect(() => {
    const pts = shops.map((s) => [s.latitude, s.longitude]);
    if (userLocation) pts.push([userLocation.lat, userLocation.lng]);
    if (pts.length === 1) map.setView(pts[0], 15);
    else if (pts.length > 1) map.fitBounds(pts, { padding: [40, 40], maxZoom: 16 });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key, map]);
  return null;
}

function PanToSelected({ shop }) {
  const map = useMap();
  useEffect(() => {
    if (shop) map.panTo([shop.latitude, shop.longitude], { animate: true });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [shop?.id, map]);
  return null;
}

export default function ShopMap({ shops, selectedId, onSelect, userLocation }) {
  const selected = useMemo(() => shops.find((s) => s.id === selectedId), [shops, selectedId]);
  const start = shops[0] ? [shops[0].latitude, shops[0].longitude] : [40.7306, -73.9866];

  return (
    <MapContainer center={start} zoom={14} className="map" scrollWheelZoom>
      <TileLayer
        attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
        url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
      />
      <FitToShops shops={shops} userLocation={userLocation} />
      <PanToSelected shop={selected} />

      {userLocation && (
        <CircleMarker center={[userLocation.lat, userLocation.lng]} radius={8}
          pathOptions={{ color: '#fff', weight: 3, fillColor: '#2563eb', fillOpacity: 1 }}>
          <Tooltip>You are here</Tooltip>
        </CircleMarker>
      )}

      {shops.map((s) => {
        const isSel = s.id === selectedId;
        return (
          <CircleMarker
            key={s.id}
            center={[s.latitude, s.longitude]}
            radius={isSel ? 13 : 10}
            pathOptions={{
              color: '#fff', weight: 2, fillOpacity: 0.95,
              fillColor: isSel ? SELECTED : s.isOpen ? OPEN : CLOSED
            }}
            eventHandlers={{ click: () => onSelect(s.id) }}
          >
            <Tooltip direction="top" offset={[0, -8]}>
              <strong>{s.name}</strong><br />
              {s.isOpen ? 'Open' : 'Closed'} · {s.statusMessage}
            </Tooltip>
          </CircleMarker>
        );
      })}
    </MapContainer>
  );
}
