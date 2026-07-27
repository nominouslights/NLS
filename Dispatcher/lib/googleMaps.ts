// ---------------------------------------------------------------------------
// Google Maps JS API (Places library) loader — the ONE external-origin browser
// call in this app (the /api/* proxy is unaffected). The key is a NEXT_PUBLIC_*
// value read at build/runtime; it ships to the browser by design and must be
// locked down with an HTTP-referrer restriction in the Google Cloud console.
//
// We inject the script tag once and memoize the load promise on window, so
// multiple mounts (e.g. reopening the Stop form) reuse the same loaded SDK.
// Minimal hand-rolled typings — the app does not depend on @types/google.maps.
// ---------------------------------------------------------------------------

export interface GooglePlaceAddressComponent {
  long_name: string;
  short_name: string;
  types: string[];
}

export interface GooglePlaceResult {
  address_components?: GooglePlaceAddressComponent[];
  formatted_address?: string;
  name?: string;
  geometry?: {
    location?: {
      lat: () => number;
      lng: () => number;
    };
  };
}

export interface GoogleAutocomplete {
  addListener: (event: string, handler: () => void) => void;
  getPlace: () => GooglePlaceResult;
}

interface GoogleAutocompleteOptions {
  fields?: string[];
  types?: string[];
}

interface GoogleMapsApi {
  maps: {
    places: {
      Autocomplete: new (
        input: HTMLInputElement,
        opts?: GoogleAutocompleteOptions,
      ) => GoogleAutocomplete;
    };
    event: {
      clearInstanceListeners: (instance: object) => void;
    };
  };
}

declare global {
  interface Window {
    google?: GoogleMapsApi;
    __nlGoogleMapsPromise?: Promise<GoogleMapsApi>;
  }
}

/**
 * Loads (once) the Google Maps JS API with the Places library and resolves with
 * the `google` global. Rejects if the API key is missing or the script fails to
 * load — callers surface that as a "type the address manually" fallback.
 */
export function loadGoogleMaps(): Promise<GoogleMapsApi> {
  if (typeof window === "undefined") {
    return Promise.reject(new Error("Google Maps can only load in the browser."));
  }
  if (window.google?.maps?.places) {
    return Promise.resolve(window.google);
  }
  if (window.__nlGoogleMapsPromise) {
    return window.__nlGoogleMapsPromise;
  }

  const key = process.env.NEXT_PUBLIC_GOOGLE_MAPS_API_KEY;
  if (!key) {
    return Promise.reject(
      new Error("NEXT_PUBLIC_GOOGLE_MAPS_API_KEY is not set — add it to Dispatcher/.env.local."),
    );
  }

  const promise = new Promise<GoogleMapsApi>((resolve, reject) => {
    const script = document.createElement("script");
    script.src = `https://maps.googleapis.com/maps/api/js?key=${encodeURIComponent(
      key,
    )}&libraries=places&loading=async&v=weekly`;
    script.async = true;
    script.defer = true;
    script.onload = () => {
      if (window.google?.maps?.places) {
        resolve(window.google);
      } else {
        reject(new Error("Google Maps loaded but the Places library is unavailable."));
      }
    };
    script.onerror = () => {
      // Let a later attempt retry from scratch.
      window.__nlGoogleMapsPromise = undefined;
      reject(new Error("Failed to load the Google Maps script — check the API key restrictions."));
    };
    document.head.appendChild(script);
  });

  window.__nlGoogleMapsPromise = promise;
  return promise;
}

export interface ParsedPlace {
  street: string;
  city: string;
  province: string;
  postalCode: string;
  country: string;
  latitude: number | null;
  longitude: number | null;
}

function pick(
  components: GooglePlaceAddressComponent[],
  type: string,
  useShort = false,
): string {
  const match = components.find((c) => c.types.includes(type));
  if (!match) return "";
  return useShort ? match.short_name : match.long_name;
}

/**
 * Flattens a Google Places result into the structured address + coordinates the
 * Stop form persists. Returns null when the result has neither components nor
 * geometry (e.g. the user pressed Enter on a bare string).
 */
export function parsePlace(place: GooglePlaceResult): ParsedPlace | null {
  const components = place.address_components ?? [];
  const loc = place.geometry?.location;
  if (components.length === 0 && !loc) return null;

  const streetNumber = pick(components, "street_number");
  const route = pick(components, "route");
  const street = [streetNumber, route].filter(Boolean).join(" ").trim();

  const city =
    pick(components, "locality") ||
    pick(components, "postal_town") ||
    pick(components, "sublocality") ||
    pick(components, "administrative_area_level_2");

  return {
    street,
    city,
    province: pick(components, "administrative_area_level_1", true),
    postalCode: pick(components, "postal_code"),
    country: pick(components, "country"),
    latitude: loc ? loc.lat() : null,
    longitude: loc ? loc.lng() : null,
  };
}
