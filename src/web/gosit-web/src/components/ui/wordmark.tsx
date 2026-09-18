import Image from "next/image";
import gositIcon from "../../../public/brand/gosit-icon.png";

/**
 * The GoSit wordmark.
 *
 * The house/family/pet mark from the real brand logo (see public/brand/gosit-logo-full.png
 * for the full lockup with wordmark and tagline baked in) plus "GoSit" set in the site's
 * own type, rather than the image's baked-in wordmark text — the image reads well at
 * marketing-page size but its lettering blurs below about 40px, which is smaller than
 * this renders in a header or a tab bar.
 */
export function Wordmark({ className = "" }: { className?: string }) {
  return (
    <span className={`inline-flex items-center gap-2.5 ${className}`}>
      <Image src={gositIcon} alt="" aria-hidden className="size-9" priority />

      <span className="font-heading text-lg font-extrabold tracking-tight text-text-primary">
        GoSit
      </span>
    </span>
  );
}
