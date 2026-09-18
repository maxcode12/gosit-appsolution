"use client";

import Image from "next/image";
import { useEffect, useState } from "react";

type Screen = {
  src: string;
  alt: string;
  caption: string;
};

/**
 * One phone, cycling through real screens of the GoSit app.
 *
 * A row of static phones reads as a poster; this reads as the app actually running.
 * Screens crossfade on a timer and a person can also step through them by hand — the
 * dots are real buttons, not just progress ticks, since a slow reader shouldn't fight
 * the auto-advance to finish a caption.
 */
export function AppShowcase({ screens }: { screens: Screen[] }) {
  const [index, setIndex] = useState(0);
  const [paused, setPaused] = useState(false);

  useEffect(() => {
    if (paused) return;
    const id = setInterval(() => {
      setIndex((current) => (current + 1) % screens.length);
    }, 3200);
    return () => clearInterval(id);
  }, [paused, screens.length]);

  return (
    <div
      className="mx-auto flex max-w-sm flex-col items-center"
      onMouseEnter={() => setPaused(true)}
      onMouseLeave={() => setPaused(false)}
    >
      <div className="relative w-full max-w-[260px] [perspective:1200px]">
        <div className="relative w-full animate-[phone-float_6s_ease-in-out_infinite] rounded-[2.5rem] border-[7px] border-brand-deep bg-brand-deep p-2 shadow-[0_35px_60px_-15px_rgba(0,0,0,0.45)] [transform:perspective(1200px)rotateY(-6deg)rotateX(2deg)]">
          <div className="relative aspect-[405/833] overflow-hidden rounded-[1.75rem] bg-surface">
            {screens.map((screen, screenIndex) => (
              <Image
                key={screen.src}
                src={screen.src}
                alt={screen.alt}
                fill
                sizes="260px"
                className={`object-cover object-top transition-opacity duration-700 ease-in-out ${
                  screenIndex === index ? "opacity-100" : "opacity-0"
                }`}
                priority={screenIndex === 0}
              />
            ))}
          </div>
          <div
            aria-hidden
            className="absolute left-1/2 top-3 h-1.5 w-16 -translate-x-1/2 rounded-full bg-brand-deep"
          />
        </div>
      </div>

      <div className="mt-7 flex gap-2" role="tablist" aria-label="App screens">
        {screens.map((screen, screenIndex) => (
          <button
            key={screen.src}
            type="button"
            role="tab"
            aria-selected={screenIndex === index}
            aria-label={`Show ${screen.alt}`}
            onClick={() => setIndex(screenIndex)}
            className={`h-2 rounded-full transition-all duration-300 ${
              screenIndex === index ? "w-6 bg-brand-primary" : "w-2 bg-border"
            }`}
          />
        ))}
      </div>

      <p className="mt-4 min-h-[2.5rem] max-w-xs text-center text-sm leading-relaxed text-text-secondary">
        {screens[index].caption}
      </p>
    </div>
  );
}
