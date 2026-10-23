import { useEffect, useState } from 'react';
import { MapContainer, TileLayer, CircleMarker, useMap, useMapEvents } from 'react-leaflet';

function ClickToPick({ onPick }) {
  useMapEvents({ click: (e) => onPick({ lat: e.latlng.lat, lng: e.latlng.lng }) });
  return null;
}

function Recenter({ value }) {
  const map = useMap();
  useEffect(() => {
    if (value) map.setView([value.lat, value.lng], Math.max(map.getZoom(), 16));
  }, [value?.lat, value?.lng]); // eslint-disable-line react-hooks/exhaustive-deps
  return null;
}

/** Owner taps the map (or uses GPS) to set where the shop is. */
export default function LocationPicker({ value, onChange, fallbackCenter }) {
  const [locating, setLocating] = useState(false);
  const [error, setError] = useState('');
  const [recenterTo, setRecenterTo] = useState(null);
  const center = value ? [value.lat, value.lng] : fallbackCenter || [40.7306, -73.9866];

  const useGps = () => {
    if (!navigator.geolocation) { setError('Location is not available in this browser.'); return; }
    setLocating(true);
    setError('');
    navigator.geolocation.getCurrentPosition(
      (pos) => {
        const v = { lat: pos.coords.latitude, lng: pos.coords.longitude };
        onChange(v);
        setRecenterTo(v);
        setLocating(false);
      },
      () => { setError('Could not get your location. Tap the map instead.'); setLocating(false); },
      { enableHighAccuracy: true, timeout: 10000 }
    );
  };

  return (
    <div className="picker">
      <div className="picker-bar">
        <span className="muted small">
          {value ? `Pinned at ${value.lat.toFixed(5)}, ${value.lng.toFixed(5)}` : 'Tap the map where your shop is'}
        </span>
        <button type="button" className="btn btn-ghost btn-sm" onClick={useGps} disabled={locating}>
          {locating ? 'Locating…' : 'Use my location'}
        </button>
      </div>
      {error && <p className="error small">{error}</p>}
      <MapContainer center={center} zoom={value ? 16 : 13} className="picker-map">
        <TileLayer
          attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
          url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
        />
        <ClickToPick onPick={onChange} />
        <Recenter value={recenterTo} />
        {value && (
          <CircleMarker center={[value.lat, value.lng]} radius={11}
            pathOptions={{ color: '#fff', weight: 3, fillColor: '#ea580c', fillOpacity: 1 }} />
        )}
      </MapContainer>
    </div>
  );
}
