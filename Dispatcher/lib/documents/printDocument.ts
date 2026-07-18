// Generic "open a document in a new tab and print it" helper. Knows nothing
// about work orders — callers pass a title and a self-contained HTML body
// (which brings its own @page / print CSS). The new tab shows a Print / Save-as-PDF
// button and auto-opens the browser print dialog so the user can save a PDF.

function escapeTitle(s: string): string {
  return s.replace(/[&<>]/g, (c) => (c === "&" ? "&amp;" : c === "<" ? "&lt;" : "&gt;"));
}

export function openPrintDocument(title: string, bodyHtml: string): void {
  const win = window.open("", "_blank");
  if (!win) {
    alert("Please allow pop-ups for this site to open the printable document.");
    return;
  }

  const doc = `<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8" />
<meta name="viewport" content="width=device-width, initial-scale=1" />
<title>${escapeTitle(title)}</title>
<style>
  * { box-sizing: border-box; }
  html, body { margin: 0; }
  body { background: #e9edf2; }
  .nl-toolbar {
    position: sticky; top: 0; z-index: 10;
    display: flex; gap: 10px; justify-content: flex-end; align-items: center;
    padding: 10px 18px; background: #102A43;
    font-family: system-ui, -apple-system, Segoe UI, Roboto, sans-serif;
  }
  .nl-toolbar span { color: #cdd9e6; font-size: 12px; margin-right: auto; }
  .nl-toolbar button {
    font: 600 13px system-ui; padding: 8px 16px; border: 0; border-radius: 6px;
    background: #1F6FB2; color: #fff; cursor: pointer;
  }
  @media print {
    .nl-toolbar { display: none !important; }
    body { background: #fff; }
  }
</style>
</head>
<body>
  <div class="nl-toolbar">
    <span>Use your browser's print dialog → &ldquo;Save as PDF&rdquo; to keep a copy.</span>
    <button onclick="window.print()">Print / Save as PDF</button>
  </div>
  ${bodyHtml}
  <script>window.addEventListener("load", function () { setTimeout(function () { window.print(); }, 350); });</script>
</body>
</html>`;

  win.document.open();
  win.document.write(doc);
  win.document.close();
}
