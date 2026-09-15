import type { MetadataRoute } from "next";

// The web app manifest, as a ROUTE rather than a static public/manifest.webmanifest.
//
// Why it has to be a route: start_url and scope must equal the app's mount point, which is "/"
// locally and "/driver" in the DigitalOcean image (BASE_PATH, baked at build time — see
// next.config.ts). A static file cannot vary per build without a substitution step; a route
// can read process.env at build time and Next applies basePath to the manifest URL itself.
//
// GOTCHA, and it only ever surfaces on the prefixed build: Next does NOT auto-prefix the icon
// `src` strings inside the manifest body. They must carry the prefix explicitly. Get it wrong
// and everything looks fine locally, while the deployed app silently offers no install prompt —
// visible only in DevTools → Application → Manifest.
const basePath = process.env.BASE_PATH ?? "";

export default function manifest(): MetadataRoute.Manifest {
  return {
    name: "Northern Link — Driver",
    short_name: "NL Driver",
    description: "Trips, manifests, inspections and hours of service for Northern Link drivers.",
    start_url: `${basePath}/`,
    scope: `${basePath}/`,

    // fullscreen, not standalone: the tablet is mounted in a vehicle and dedicated to this app.
    // Every pixel of chrome is a pixel not showing the manifest.
    display: "fullscreen",

    // Applies to the INSTALLED app only. In a browser tab on an unlocked tablet this does
    // nothing, which is why components/OrientationNotice.tsx exists — that is the real
    // mechanism, not a belt-and-braces extra.
    orientation: "landscape",

    background_color: "#EEF2F6",
    theme_color: "#102A43",
    categories: ["business", "productivity"],

    icons: [
      { src: `${basePath}/icons/icon-192.png`, sizes: "192x192", type: "image/png" },
      { src: `${basePath}/icons/icon-512.png`, sizes: "512x512", type: "image/png" },
      {
        src: `${basePath}/icons/icon-maskable-512.png`,
        sizes: "512x512",
        type: "image/png",
        purpose: "maskable",
      },
    ],
  };
}
