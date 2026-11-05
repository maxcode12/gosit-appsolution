"use client";

import { useEffect } from "react";

/**
 * Opens the document card a link points at.
 *
 * A closed `<details>` does not open for `#id`, so a link such as "Add insurance" from the
 * setup checklist would land on a collapsed card. This runs on load and when the hash
 * changes, and opens the card the hash names.
 */
export function OpenCardFromLink() {
  useEffect(() => {
    function open() {
      const id = window.location.hash.slice(1);
      if (!id) return;

      const card = document.getElementById(id);

      if (card instanceof HTMLDetailsElement) {
        card.open = true;
        card.scrollIntoView({ block: "start" });
      }
    }

    open();
    window.addEventListener("hashchange", open);

    return () => window.removeEventListener("hashchange", open);
  }, []);

  return null;
}
