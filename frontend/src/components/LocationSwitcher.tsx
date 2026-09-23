import { useEffect, useRef, useState } from 'react';
import { Check, ChevronDown, MapPin } from 'lucide-react';
import { locationsApi } from '@/api/masters';
import type { Location } from '@/models/masters';
import { useUiStore } from '@/store/uiStore';

/**
 * The workspace the member is currently looking at. The choice is shared —
 * the dashboard's banners and the booking form both read it — and persisted
 * per device, so it survives a reload the way the theme does.
 */
export function LocationSwitcher() {
  const { locationId, setLocationId } = useUiStore();
  const [locations, setLocations] = useState<Location[]>([]);
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    locationsApi
      .list({ pageSize: 100 })
      .then((page) => {
        const active = page.items.filter((l) => l.isActive);
        setLocations(active);
        // Settle on the house default the first time, so the header never
        // reads "Select location" for someone who has not chosen yet.
        if (active.length > 0 && !active.some((l) => l.locationId === locationId)) {
          setLocationId((active.find((l) => l.isDefault) ?? active[0]).locationId);
        }
      })
      .catch(() => {
        // Cosmetic context only — every screen still has its own filters.
      });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    if (!open) return;
    const onPointerDown = (event: PointerEvent) => {
      if (ref.current && !ref.current.contains(event.target as Node)) setOpen(false);
    };
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setOpen(false);
    };
    document.addEventListener('pointerdown', onPointerDown);
    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('pointerdown', onPointerDown);
      document.removeEventListener('keydown', onKeyDown);
    };
  }, [open]);

  if (locations.length === 0) {
    return null;
  }

  const current = locations.find((l) => l.locationId === locationId) ?? locations[0];

  return (
    <div className="locpick" ref={ref}>
      <button
        type="button"
        className="locpick__trigger"
        aria-haspopup="listbox"
        aria-expanded={open}
        onClick={() => setOpen((o) => !o)}
        title={current.city ? `${current.locationName} · ${current.city}` : current.locationName}
      >
        <MapPin size={16} aria-hidden />
        <span className="locpick__name">{current.locationName}</span>
        <ChevronDown size={15} className="locpick__chevron" aria-hidden />
      </button>

      {open && (
        <div className="locpick__popover" role="listbox" aria-label="Workspace location">
          <div className="locpick__section">Workspace location</div>
          {locations.map((location) => (
            <button
              key={location.locationId}
              type="button"
              role="option"
              aria-selected={location.locationId === current.locationId}
              className="locpick__item"
              onClick={() => {
                setLocationId(location.locationId);
                setOpen(false);
              }}
            >
              <span className="locpick__item-text">
                <span className="locpick__item-name">{location.locationName}</span>
                {location.city && <span className="locpick__item-meta">{location.city}</span>}
              </span>
              {location.locationId === current.locationId && <Check size={15} />}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
